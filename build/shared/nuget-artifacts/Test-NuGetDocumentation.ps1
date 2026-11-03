# Copyright (c) 2026 Neil Colvin. MIT licensed.
[CmdletBinding()]
param([Parameter(Mandatory)][string]$PackagePath)
$ErrorActionPreference='Stop'
$zip=[IO.Compression.ZipFile]::OpenRead((Resolve-Path -LiteralPath $PackagePath))
try {
    $specs=@($zip.Entries|Where-Object FullName -like '*.nuspec')
    if($specs.Count -ne 1){throw 'Expected one NuGet manifest.'}
    $reader=[IO.StreamReader]::new($specs[0].Open())
    try{[xml]$spec=$reader.ReadToEnd()}finally{$reader.Dispose()}
    $metadata=$spec.package.metadata
    if([string]::IsNullOrWhiteSpace([string]$metadata.releaseNotes)){throw 'Package has no release notes in its NuGet metadata.'}
    $entry=$zip.GetEntry([string]$metadata.readme)
    if(!$entry){throw 'Package README is absent.'}
    $reader=[IO.StreamReader]::new($entry.Open())
    try{$readme=$reader.ReadToEnd()}finally{$reader.Dispose()}
    if([string]::IsNullOrWhiteSpace($readme)){throw 'Package README is empty.'}
    # Ignore fenced code examples. Inline destinations, reference definitions and HTML
    # href/src must work on NuGet without relying on the repository directory structure.
    $text=[regex]::Replace($readme,'(?ms)^\s*(```|~~~).*?^\s*\1\s*$','')
    $links=@([regex]::Matches($text,'\]\(\s*<?([^\s)>]+)')|ForEach-Object {$_.Groups[1].Value})
    $links+=@([regex]::Matches($text,'(?m)^\s*\[[^\]]+\]:\s*<?([^\s>]+)')|ForEach-Object {$_.Groups[1].Value})
    $links+=@([regex]::Matches($text,'(?i)(?:href|src)\s*=\s*["'']([^"'']+)["'']')|ForEach-Object {$_.Groups[1].Value})
    $broken=@($links|Where-Object {$_ -notmatch '^(https?://|mailto:|#)'})
    if($broken.Count){throw ('Package README contains relative or unsupported links: '+(($broken|Select-Object -Unique)-join ', '))}
    foreach($link in $links|Where-Object {$_ -match '^https?://'}){if(![Uri]::IsWellFormedUriString($link,[UriKind]::Absolute)){throw "Malformed README URL: $link"}}
    "Verified NuGet documentation: $($metadata.id) $($metadata.version); $($links.Count) README links; release notes present."
} finally {$zip.Dispose()}
