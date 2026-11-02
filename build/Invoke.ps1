# Copyright (c) 2026 Neil Colvin. Build automation only; see repository LICENSE.
[CmdletBinding()]
param([Parameter(Mandatory)][string]$Task)
$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path $PSScriptRoot -Parent
$component = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'component.json') -Raw | ConvertFrom-Json
if ($component.repository -ne 'https://github.com/oznetmaster/BuildAutomation.git' -or $component.commit -notmatch '^[a-f0-9]{40}$') { throw 'Invalid shared build component pin.' }
$cacheRoot = Join-Path $repositoryRoot 'artifacts/build-components'
$source = Join-Path $cacheRoot $component.commit
New-Item -ItemType Directory -Path $cacheRoot -Force | Out-Null
if (!(Test-Path -LiteralPath $source)) {
    $temporary = Join-Path $cacheRoot ([Guid]::NewGuid().ToString('N'))
    git init --quiet $temporary
    if ($LASTEXITCODE -ne 0) { throw 'Cannot initialize build component checkout.' }
    git -C $temporary fetch --quiet --depth 1 $component.repository $component.commit
    if ($LASTEXITCODE -ne 0) { throw 'Cannot fetch pinned build component.' }
    git -C $temporary checkout --quiet --detach FETCH_HEAD
    if ($LASTEXITCODE -ne 0) { throw 'Cannot check out build component.' }
    # Another local invocation can finish the same immutable download first.
    try { [IO.Directory]::Move($temporary, $source) }
    catch { if (!(Test-Path -LiteralPath $source)) { throw } }
}
$actual = git -C $source rev-parse HEAD
if ($LASTEXITCODE -ne 0 -or $actual -ne $component.commit) { throw 'Build component revision mismatch.' }
if (git -C $source status --porcelain --untracked-files=no) { throw 'Cached build component has local modifications.' }
& (Join-Path $source 'Invoke-Build.ps1') -Root $repositoryRoot -Task $Task
