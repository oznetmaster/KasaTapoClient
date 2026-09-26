# Copyright (c) 2026 Neil Colvin. MIT licensed.
[CmdletBinding()]
param([string]$PlanPath=(Join-Path $PSScriptRoot '../NuGetDocumentationPatch.json'),[string]$OutputDirectory)
$ErrorActionPreference='Stop'
$plan=Get-Content -LiteralPath $PlanPath -Raw|ConvertFrom-Json
if(!$OutputDirectory){$OutputDirectory=Join-Path (Split-Path $PSScriptRoot -Parent) 'artifacts/nuget-documentation'}
if(Test-Path $OutputDirectory){throw 'Use a fresh output directory.'}
if($plan.Version -notmatch '^\d+\.\d+\.\d+$' -or [version]$plan.Version -le [version]$plan.BaseVersion){throw 'A new patch version is required.'}
$baseVersion=[version]$plan.BaseVersion;$newVersion=[version]$plan.Version
if($newVersion.Major -ne $baseVersion.Major -or $newVersion.Minor -ne $baseVersion.Minor -or $newVersion.Build -ne $baseVersion.Build+1){throw 'Documentation repair must increment only the patch version.'}
$notes=Get-Content (Join-Path (Split-Path $PlanPath -Parent) $plan.ReleaseNotesFile) -Raw
if([string]::IsNullOrWhiteSpace($notes)){throw 'Patch release notes are required.'}
New-Item -ItemType Directory -Path $OutputDirectory|Out-Null
$id=$plan.Id.ToLowerInvariant()
$base=Join-Path $OutputDirectory 'base.nupkg'
Invoke-WebRequest "https://api.nuget.org/v3-flatcontainer/$id/$($plan.BaseVersion)/$id.$($plan.BaseVersion).nupkg" -OutFile $base
if((Get-FileHash $base).Hash -ne $plan.BaseSha256){throw 'Published base package differs from the reviewed digest.'}
$output=Join-Path $OutputDirectory "$($plan.Id).$($plan.Version).nupkg"
$source=[IO.Compression.ZipFile]::OpenRead($base)
try{
 function ReadEntry($entry){$reader=[IO.StreamReader]::new($entry.Open());try{return $reader.ReadToEnd()}finally{$reader.Dispose()}}
 function EntryHash($entry){$stream=$entry.Open();try{return [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($stream))}finally{$stream.Dispose()}}
 $specs=@($source.Entries|Where-Object FullName -like '*.nuspec')
 if($specs.Count -ne 1){throw 'Expected one manifest.'}
 [xml]$spec=ReadEntry $specs[0];$metadata=$spec.package.metadata
 if($metadata.id -ne $plan.Id -or $metadata.version -ne $plan.BaseVersion){throw 'Base identity mismatch.'}
 $readmePath=[string]$metadata.readme
 $readme=ReadEntry $source.GetEntry($readmePath)
 $readme=[regex]::Replace($readme,'(?<=\]\()([^\s)]+)',[Text.RegularExpressions.MatchEvaluator]{param($m)
  if($m.Value -match '^(https?://|mailto:|#)'){return $m.Value}
  return $plan.ReadmeBaseUrl+$m.Value
 })
 $readme=$readme.TrimEnd()+"`n`n## NuGet documentation update $($plan.Version)`n`n"+$notes.Trim()+"`n"
 $metadata.version=$plan.Version
 $releaseNotes=$metadata.SelectSingleNode('*[local-name()="releaseNotes"]')
 if(!$releaseNotes){$releaseNotes=$spec.CreateElement('releaseNotes',$metadata.NamespaceURI);$null=$metadata.AppendChild($releaseNotes)}
 $releaseNotes.InnerText=$notes.Trim()
 $changes=@{$specs[0].FullName=$spec.OuterXml;$readmePath=$readme}
 foreach($entry in $source.Entries|Where-Object FullName -like '*.psmdcp'){
  [xml]$core=ReadEntry $entry
  $version=$core.SelectSingleNode('/*/*[local-name()="version"]')
  if(!$version -or $version.InnerText -ne $plan.BaseVersion){throw 'Unexpected core package version.'}
  $version.InnerText=$plan.Version;$changes[$entry.FullName]=$core.OuterXml
 }
 if($source.GetEntry('crestron-driver-package.json')){
  $driver=ReadEntry $source.GetEntry('crestron-driver-package.json')|ConvertFrom-Json
  if($driver.packageVersion -ne $plan.BaseVersion){throw 'Unexpected driver wrapper version.'}
  $driver.packageVersion=$plan.Version
  $changes['crestron-driver-package.json']=$driver|ConvertTo-Json -Depth 15
 }
 $target=[IO.Compression.ZipFile]::Open($output,[IO.Compression.ZipArchiveMode]::Create)
 try{
  foreach($entry in $source.Entries){
   if($entry.FullName -eq '.signature.p7s'){continue} # The old repository signature cannot sign changed metadata.
   $new=$target.CreateEntry($entry.FullName,[IO.Compression.CompressionLevel]::Optimal)
   $stream=$new.Open()
   try{
    if($changes.ContainsKey($entry.FullName)){$bytes=[Text.UTF8Encoding]::new($false).GetBytes($changes[$entry.FullName]);$stream.Write($bytes)}
    else{$original=$entry.Open();try{$original.CopyTo($stream)}finally{$original.Dispose()}}
   }finally{$stream.Dispose()}
  }
 }finally{$target.Dispose()}
 $check=[IO.Compression.ZipFile]::OpenRead($output)
 try{
  $unchanged=@()
  foreach($entry in $source.Entries){
   if($entry.FullName -eq '.signature.p7s' -or $changes.ContainsKey($entry.FullName)){continue}
   $copy=$check.GetEntry($entry.FullName)
   $hash=EntryHash $entry
   if(!$copy -or (EntryHash $copy) -ne $hash){throw "Payload changed: $($entry.FullName)"}
   $unchanged+=@{Path=$entry.FullName;Sha256=$hash}
  }
  if($check.Entries.Count -ne $source.Entries.Count-@($source.Entries|Where-Object FullName -eq '.signature.p7s').Count){throw 'Unexpected package files.'}
  [xml]$newSpec=ReadEntry $check.GetEntry($specs[0].FullName)
  if($newSpec.package.metadata.version -ne $plan.Version){throw 'New version missing.'}
  @{Id=$plan.Id;BaseVersion=$plan.BaseVersion;Version=$plan.Version;BaseSha256=$plan.BaseSha256;PackageSha256=(Get-FileHash $output).Hash;ChangedFiles=@($changes.Keys|Sort-Object);UnchangedPayload=$unchanged}|ConvertTo-Json -Depth 8|Set-Content (Join-Path $OutputDirectory 'documentation-patch-verification.json') -Encoding utf8NoBOM
 }finally{$check.Dispose()}
}finally{$source.Dispose()}
& (Join-Path $PSScriptRoot 'Test-NuGetDocumentation.ps1') -PackagePath $output
Remove-Item -LiteralPath $base
Write-Output $output
