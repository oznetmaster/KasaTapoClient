# Copyright (c) 2026 Neil Colvin.
# Licensed under the terms in LICENSE in the repository root.
[CmdletBinding()]
param([switch]$DefinitionsOnly)
$ErrorActionPreference = 'Stop'

function Get-RequiredReleaseCheckState([object[]]$Runs, [object[]]$Required, [string]$Revision) {
    $pending = $false
    foreach ($requirement in $Required) {
        if ([string]::IsNullOrWhiteSpace($requirement.context) -or $requirement.appId -isnot [long] -and $requirement.appId -isnot [int] -or $requirement.appId -le 0) {
            throw 'Each required check needs a context and a positive numeric appId.'
        }
        $matching = @($Runs | Where-Object { $_.name -ceq $requirement.context -and $_.app.id -eq $requirement.appId -and $_.head_sha -ceq $Revision } | Sort-Object id -Descending)
        if (!$matching.Count -or $matching[0].status -ne 'completed') { $pending = $true; continue }
        if ($matching[0].conclusion -ne 'success') { return 'Failed' }
    }
    if ($pending) { return 'Pending' }
    return 'Passed'
}
if ($DefinitionsOnly) { return }
if ([string]::IsNullOrWhiteSpace($env:REQUIRED_RELEASE_CHECKS)) {
    Write-Output 'No additional release checks are configured.'
    return
}
$required = @(ConvertFrom-Json $env:REQUIRED_RELEASE_CHECKS)
if (!$required.Count) { throw 'Configured release checks cannot be empty.' }
if ($env:GITHUB_REPOSITORY -notmatch '^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$') { throw 'Invalid repository identity.' }
$revision = (git rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0 -or $revision -notmatch '^[0-9a-f]{40}$') { throw 'Cannot identify the exact checked-out release source.' }
$deadline = [DateTime]::UtcNow.AddMinutes(10)
do {
    $json = gh api "repos/$env:GITHUB_REPOSITORY/commits/$revision/check-runs?filter=all&per_page=100" --paginate --slurp
    if ($LASTEXITCODE) { throw 'Required-check lookup failed; release is blocked.' }
    $pages = @(ConvertFrom-Json ($json | Out-String))
    $runs = @($pages | ForEach-Object { $_.check_runs })
    $state = Get-RequiredReleaseCheckState $runs $required $revision
    if ($state -eq 'Passed') { Write-Output "Required release checks passed for $revision."; return }
    if ($state -eq 'Failed') { throw 'A required check failed or did not establish success; release is blocked.' }
    if ([DateTime]::UtcNow -ge $deadline) { throw 'Required checks are still missing or pending; release is blocked.' }
    Start-Sleep -Seconds 15
} while ($true)
