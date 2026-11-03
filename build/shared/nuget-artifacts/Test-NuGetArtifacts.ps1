# Copyright (c) 2026 Neil Colvin. MIT License; see LICENSE.
$ErrorActionPreference='Stop'
$tempRoot=[IO.Path]::GetFullPath([IO.Path]::GetTempPath())
$fixture=Join-Path $tempRoot ('NuGetArtifactChecks-'+[guid]::NewGuid().ToString('N'))
$null=[IO.Directory]::CreateDirectory($fixture)
$checks=0
try {
    $global:NuGetArtifactFixtureBase=Join-Path $fixture 'base.nupkg'
    function New-Base([string]$Readme='[Guide](guide.md)',[string]$Notes='Original release') {
        if(Test-Path $global:NuGetArtifactFixtureBase){Remove-Item -LiteralPath $global:NuGetArtifactFixtureBase}
        $zip=[IO.Compression.ZipFile]::Open($global:NuGetArtifactFixtureBase,[IO.Compression.ZipArchiveMode]::Create)
        try {
            $files=@{
                'Example.nuspec'="<package><metadata><id>Example</id><version>1.0.0</version><readme>README.md</readme><releaseNotes>$Notes</releaseNotes></metadata></package>"
                'README.md'=$Readme
                'payload.bin'='immutable product payload'
                '.signature.p7s'='synthetic old repository signature'
                'crestron-driver-package.json'='{"packageVersion":"1.0.0","driverVersion":"1.0.000.0000"}'
                'package/services/metadata/core-properties/example.psmdcp'='<coreProperties><version>1.0.0</version></coreProperties>'
            }
            foreach($entry in $files.GetEnumerator()){
                $writer=[IO.StreamWriter]::new($zip.CreateEntry($entry.Key).Open());try{$writer.Write($entry.Value)}finally{$writer.Dispose()}
            }
        }finally{$zip.Dispose()}
    }
    function Reject([scriptblock]$Action){
        $failed=$false;try{& $Action}catch{$failed=$true}
        if(!$failed){throw 'Invalid NuGet artifact preparation was accepted.'};$script:checks++
    }
    New-Base
    Reject { & "$PSScriptRoot/Test-NuGetDocumentation.ps1" -PackagePath $global:NuGetArtifactFixtureBase }
    New-Base -Readme '[Guide](https://example.invalid/guide)'
    & "$PSScriptRoot/Test-NuGetDocumentation.ps1" -PackagePath $global:NuGetArtifactFixtureBase;$checks++
    New-Base -Readme ''
    Reject { & "$PSScriptRoot/Test-NuGetDocumentation.ps1" -PackagePath $global:NuGetArtifactFixtureBase }
    New-Base -Readme '[Guide](https://example.invalid/guide)' -Notes ''
    Reject { & "$PSScriptRoot/Test-NuGetDocumentation.ps1" -PackagePath $global:NuGetArtifactFixtureBase }
    New-Base
    'Documentation fix'|Set-Content (Join-Path $fixture 'notes.md')
    $plan=@{Id='Example';BaseVersion='1.0.0';Version='1.0.1';BaseSha256=(Get-FileHash $global:NuGetArtifactFixtureBase).Hash;ReleaseNotesFile='notes.md';ReadmeBaseUrl='https://example.invalid/source/'}
    $planPath=Join-Path $fixture 'plan.json'
    $plan|ConvertTo-Json|Set-Content $planPath
    function Invoke-WebRequest {param([string]$Uri,[string]$OutFile) Copy-Item -LiteralPath $global:NuGetArtifactFixtureBase -Destination $OutFile}
    $output=Join-Path $fixture 'repaired'
    & "$PSScriptRoot/New-NuGetDocumentationPatch.ps1" -Root $fixture -PlanPath $planPath -OutputDirectory $output
    $proof=Get-Content (Join-Path $output 'documentation-patch-verification.json') -Raw|ConvertFrom-Json
    if($proof.Version -cne '1.0.1' -or @($proof.UnchangedPayload|Where-Object Path -eq 'payload.bin').Count -ne 1){throw 'Immutable payload verification missing.'};$checks++
    $zip=[IO.Compression.ZipFile]::OpenRead((Join-Path $output 'Example.1.0.1.nupkg'))
    try {
        if($zip.GetEntry('.signature.p7s')){throw 'Changed metadata retained the old repository signature.'};$checks++
        $reader=[IO.StreamReader]::new($zip.GetEntry('README.md').Open());try{$readme=$reader.ReadToEnd()}finally{$reader.Dispose()}
        if(!$readme.Contains('https://example.invalid/source/guide.md') -or !$readme.Contains('Documentation fix')){throw 'README repair failed.'};$checks++
        $reader=[IO.StreamReader]::new($zip.GetEntry('crestron-driver-package.json').Open());try{$delivery=$reader.ReadToEnd()|ConvertFrom-Json}finally{$reader.Dispose()}
        if($delivery.packageVersion -cne '1.0.1' -or $delivery.driverVersion -cne '1.0.000.0000'){throw 'Wrapper metadata changed the driver version.'};$checks++
    }finally{$zip.Dispose()}
    $plan.BaseSha256='0'*64;$plan|ConvertTo-Json|Set-Content $planPath
    Reject { & "$PSScriptRoot/New-NuGetDocumentationPatch.ps1" -Root $fixture -PlanPath $planPath -OutputDirectory (Join-Path $fixture 'bad-hash') }
    $plan.BaseSha256=(Get-FileHash $global:NuGetArtifactFixtureBase).Hash;$plan.Version='1.1.0';$plan|ConvertTo-Json|Set-Content $planPath
    Reject { & "$PSScriptRoot/New-NuGetDocumentationPatch.ps1" -Root $fixture -PlanPath $planPath -OutputDirectory (Join-Path $fixture 'bad-version') }
    Write-Host "Passed $checks NuGet documentation and unchanged-payload checks; no network or publication."
} finally {
    Remove-Item Function:Invoke-WebRequest -ErrorAction SilentlyContinue
    Remove-Variable NuGetArtifactFixtureBase -Scope Global -ErrorAction SilentlyContinue
    $resolved=[IO.Path]::GetFullPath($fixture)
    if([IO.Path]::GetDirectoryName($resolved).TrimEnd('\','/') -ne $tempRoot.TrimEnd('\','/') -or [IO.Path]::GetFileName($resolved) -notlike 'NuGetArtifactChecks-*'){throw 'Unexpected fixture cleanup path.'}
    Remove-Item -LiteralPath $resolved -Recurse -Force
}
