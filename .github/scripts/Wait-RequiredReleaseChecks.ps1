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
function Get-HostedReleaseCheckState([object[]]$Runs, [string]$Revision, [string]$Repository) {
    $matching = @($Runs | Where-Object {
        $_.head_sha -ceq $Revision -and $_.head_repository.full_name -ceq $Repository -and
        $_.event -in @('push','pull_request','workflow_dispatch')
    } | Sort-Object id -Descending)
    if (!$matching.Count -or $matching[0].status -ne 'completed') { return 'Pending' }
    if ($matching[0].conclusion -ne 'success') { return 'Failed' }
    return 'Passed'
}
function Get-HardwareReleaseOverride([string]$Selected, [string]$Reason, [string]$EventName) {
    if ([string]::IsNullOrWhiteSpace($Selected) -or $Selected -ceq 'false') { return $null }
    if ($Selected -cne 'true') { throw 'skip_hardware_checks must be true or false.' }
    if ($EventName -cne 'workflow_dispatch') { throw 'Hardware overrides require an explicit manual release invocation.' }
    if ([string]::IsNullOrWhiteSpace($Reason) -or $Reason.Length -gt 500 -or $Reason -match '[\x00-\x1f\x7f]') {
        throw 'Supply hardware_skip_reason as a nonempty single line of at most 500 characters.'
    }
    return $Reason.Trim()
}
function Select-ReleaseChecks([object[]]$Required, [bool]$SkipHardware) {
    return @($Required | Where-Object { !$SkipHardware -or !$_.context.StartsWith('Processor tests / ', [StringComparison]::Ordinal) })
}
if ($DefinitionsOnly) { return }
$required = @()
if (![string]::IsNullOrWhiteSpace($env:REQUIRED_RELEASE_CHECKS)) {
    $required = @(ConvertFrom-Json $env:REQUIRED_RELEASE_CHECKS)
    if (!$required.Count) { throw 'Configured release checks cannot be empty.' }
}
$hosted = @()
if (![string]::IsNullOrWhiteSpace($env:REQUIRED_HOSTED_WORKFLOWS)) {
    $hosted = @(ConvertFrom-Json $env:REQUIRED_HOSTED_WORKFLOWS)
    if (!$hosted.Count -or @($hosted | Where-Object { $_ -isnot [string] -or $_ -notmatch '^[A-Za-z0-9_.-]+\.ya?ml$' }).Count) {
        throw 'Configure hosted workflow filenames as a nonempty JSON array.'
    }
}
$reason = Get-HardwareReleaseOverride $env:RELEASE_SKIP_HARDWARE_CHECKS $env:RELEASE_HARDWARE_SKIP_REASON $env:GITHUB_EVENT_NAME
if (!$required.Count -and !$hosted.Count -and !$reason) { Write-Output 'No additional release checks are configured.'; return }
if ($env:GITHUB_REPOSITORY -notmatch '^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$') { throw 'Invalid repository identity.' }
$revision = (git rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0 -or $revision -notmatch '^[0-9a-f]{40}$') { throw 'Cannot identify the exact checked-out release source.' }
# Validate every requirement even when an explicit hardware override is requested.
$null = Get-RequiredReleaseCheckState @() $required $revision
if ($reason) {
    if (!$hosted.Count) { throw 'A hardware override requires configured hosted validation workflows.' }
    $remaining = @(Select-ReleaseChecks $required $true)
    $skipped = @($required | Where-Object { $_.context.StartsWith('Processor tests / ', [StringComparison]::Ordinal) })
    if ($skipped.Count) {
        $safeReason = $reason.Replace('%','%25')
        Write-Output "::warning::Hardware validation explicitly skipped for $revision. Reason: $safeReason. Hosted validation remains required."
        if ($env:GITHUB_STEP_SUMMARY) {
            $summary = "## Hardware validation override`n`nThis invocation does not establish a hardware-test pass.`n`nSource: $revision`n`nReason:`n`n    $reason`n`nHosted validation remains mandatory. Bypassed checks:`n`n"
            foreach ($check in $skipped) { $summary += "    $($check.context) (App $($check.appId))`n" }
            [IO.File]::AppendAllText($env:GITHUB_STEP_SUMMARY, $summary + "`n", [Text.UTF8Encoding]::new($false))
        }
    }
    $required = $remaining
}
if (!$required.Count -and !$hosted.Count) { Write-Output 'No additional release checks are configured.'; return }
$deadline = [DateTime]::UtcNow.AddMinutes(10)
do {
    $pending = $false
    foreach ($workflow in $hosted) {
        $json = gh api "repos/$env:GITHUB_REPOSITORY/actions/workflows/$workflow/runs?head_sha=$revision&per_page=100" --paginate --slurp
        if ($LASTEXITCODE) { throw 'Hosted-check lookup failed; release is blocked.' }
        $pages = @(ConvertFrom-Json ($json | Out-String))
        $runs = @($pages | ForEach-Object { $_.workflow_runs })
        $state = Get-HostedReleaseCheckState $runs $revision $env:GITHUB_REPOSITORY
        if ($state -eq 'Failed') { throw "Hosted validation failed: $workflow. Hardware overrides do not bypass hosted tests." }
        if ($state -eq 'Pending') { $pending = $true }
    }
    if ($required.Count) {
        $json = gh api "repos/$env:GITHUB_REPOSITORY/commits/$revision/check-runs?filter=all&per_page=100" --paginate --slurp
        if ($LASTEXITCODE) { throw 'Required-check lookup failed; release is blocked.' }
        $pages = @(ConvertFrom-Json ($json | Out-String))
        $runs = @($pages | ForEach-Object { $_.check_runs })
        $state = Get-RequiredReleaseCheckState $runs $required $revision
        if ($state -eq 'Failed') { throw 'A required check failed or did not establish success; release is blocked.' }
        if ($state -eq 'Pending') { $pending = $true }
    }
    if (!$pending) { Write-Output "Configured release checks satisfied for $revision; see any explicit hardware override above."; return }
    if ([DateTime]::UtcNow -ge $deadline) { throw 'Required checks are still missing or pending; release is blocked.' }
    Start-Sleep -Seconds 15
} while ($true)
