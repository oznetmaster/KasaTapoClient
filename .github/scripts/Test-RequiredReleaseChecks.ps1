# Copyright (c) 2026 Neil Colvin.
# Licensed under the terms in LICENSE in the repository root.
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot/Wait-RequiredReleaseChecks.ps1" -DefinitionsOnly
$revision = 'a' * 40
$required = @([pscustomobject]@{context='External tests';appId=17})
$passing = [pscustomobject]@{id=1;name='External tests';app=@{id=17};head_sha=$revision;status='completed';conclusion='success'}
if ((Get-RequiredReleaseCheckState @($passing) $required $revision) -ne 'Passed') { throw 'Expected a passing exact-source check.' }
if ((Get-RequiredReleaseCheckState @() $required $revision) -ne 'Pending') { throw 'Missing checks must block release.' }
if ((Get-RequiredReleaseCheckState @($passing) $required ('b'*40)) -ne 'Pending') { throw 'Another source revision cannot pass.' }
if ((Get-RequiredReleaseCheckState @($passing) @([pscustomobject]@{context='External tests';appId=99}) $revision) -ne 'Pending') { throw 'Another App cannot pass.' }
foreach ($conclusion in @('failure','cancelled','skipped','neutral','timed_out','action_required')) {
    $failed = [pscustomobject]@{id=2;name='External tests';app=@{id=17};head_sha=$revision;status='completed';conclusion=$conclusion}
    if ((Get-RequiredReleaseCheckState @($passing,$failed) $required $revision) -ne 'Failed') { throw 'A newer failed or incomplete check must block release.' }
}
$pending = [pscustomobject]@{id=2;name='External tests';app=@{id=17};head_sha=$revision;status='in_progress';conclusion=$null}
if ((Get-RequiredReleaseCheckState @($passing,$pending) $required $revision) -ne 'Pending') { throw 'A newer pending check must block release.' }
$rejected = $false
try { Get-RequiredReleaseCheckState @($passing) @([pscustomobject]@{context='External tests';appId='17'}) $revision | Out-Null } catch { $rejected = $true }
if (!$rejected) { throw 'Invalid check configuration must be rejected.' }
Write-Output 'Release check policy: 12 scenarios passed.'

$checks = 0
function Verify([bool]$Condition, [string]$Message) {
    if (!$Condition) { throw $Message }
    $script:checks++
}
function Reject([scriptblock]$Action) {
    $rejected = $false
    try { & $Action | Out-Null } catch { $rejected = $true }
    Verify $rejected 'An invalid release override was accepted.'
}
$repository = 'example/library'
$hostedPass = [pscustomobject]@{ id=1; head_sha=$revision; head_repository=@{full_name=$repository}; event='push'; status='completed'; conclusion='success' }
Verify ((Get-HostedReleaseCheckState @($hostedPass) $revision $repository) -eq 'Passed') 'Hosted checks should pass on the exact source.'
Verify ((Get-HostedReleaseCheckState @() $revision $repository) -eq 'Pending') 'Missing hosted tests must not pass.'
Verify ((Get-HostedReleaseCheckState @($hostedPass) ('b'*40) $repository) -eq 'Pending') 'Another revision cannot supply hosted validation.'
Verify ((Get-HostedReleaseCheckState @($hostedPass) $revision 'other/library') -eq 'Pending') 'Another repository cannot supply hosted validation.'
foreach ($conclusion in @('failure','cancelled','skipped','neutral','timed_out','action_required')) {
    $newer = [pscustomobject]@{ id=2; head_sha=$revision; head_repository=@{full_name=$repository}; event='workflow_dispatch'; status='completed'; conclusion=$conclusion }
    Verify ((Get-HostedReleaseCheckState @($hostedPass,$newer) $revision $repository) -eq 'Failed') 'Newer nonpassing hosted results must block.'
}
$newer = [pscustomobject]@{ id=2; head_sha=$revision; head_repository=@{full_name=$repository}; event='push'; status='in_progress'; conclusion=$null }
Verify ((Get-HostedReleaseCheckState @($hostedPass,$newer) $revision $repository) -eq 'Pending') 'An older hosted pass cannot hide a current run.'
$unsupported = [pscustomobject]@{ id=1; head_sha=$revision; head_repository=@{full_name=$repository}; event='pull_request_target'; status='completed'; conclusion='success' }
Verify ((Get-HostedReleaseCheckState @($unsupported) $revision $repository) -eq 'Pending') 'Unsupported event cannot supply hosted validation.'
Verify ($null -eq (Get-HardwareReleaseOverride '' '' 'push')) 'No override must remain the default.'
Verify ($null -eq (Get-HardwareReleaseOverride 'false' '' 'workflow_dispatch')) 'An unchecked input must retain hardware checks.'
Verify ((Get-HardwareReleaseOverride 'true' 'Local runner offline' 'workflow_dispatch') -eq 'Local runner offline') 'An explicit offline-runner override must be supported.'
Reject { Get-HardwareReleaseOverride 'yes' 'Reason' 'workflow_dispatch' }
Reject { Get-HardwareReleaseOverride 'true' '' 'workflow_dispatch' }
Reject { Get-HardwareReleaseOverride 'true' 'Reason' 'push' }
Reject { Get-HardwareReleaseOverride 'true' 'Reason' 'release' }
Reject { Get-HardwareReleaseOverride 'true' "Reason`n::error::injected" 'workflow_dispatch' }
Reject { Get-HardwareReleaseOverride 'true' ('a'*501) 'workflow_dispatch' }
$hardware = [pscustomobject]@{context='Processor tests / library';appId=17}
$other = [pscustomobject]@{context='Hosted approval';appId=19}
Verify (@(Select-ReleaseChecks @($hardware,$other) $false).Count -eq 2) 'Default policy must retain every check.'
$remaining = @(Select-ReleaseChecks @($hardware,$other) $true)
Verify ($remaining.Count -eq 1 -and $remaining[0].context -eq 'Hosted approval') 'Hardware override must retain other required checks.'

# Exercise the actual script entry point with fake GitHub responses. No network or
# processor is used; an unexpected hardware lookup fails the test immediately.
$variables = @('REQUIRED_RELEASE_CHECKS','REQUIRED_HOSTED_WORKFLOWS','RELEASE_SKIP_HARDWARE_CHECKS','RELEASE_HARDWARE_SKIP_REASON','GITHUB_EVENT_NAME','GITHUB_REPOSITORY','GITHUB_STEP_SUMMARY')
$saved = @{}
foreach ($name in $variables) { $saved[$name] = [Environment]::GetEnvironmentVariable($name) }
$summaryPath = Join-Path ([IO.Path]::GetTempPath()) ('release-policy-' + [Guid]::NewGuid().ToString('N') + '.md')
$releasePolicyApi = [pscustomobject]@{ Calls=0; Outcome='success' }
function git { $global:LASTEXITCODE = 0; return ('a'*40) }
function gh {
    param([Parameter(ValueFromRemainingArguments=$true)][string[]]$Arguments)
    $route = $Arguments -join ' '
    if ($route -notmatch '/actions/workflows/ci\.yml/runs\?') { throw 'Hardware lookup occurred despite the explicit override.' }
    $releasePolicyApi.Calls++
    $global:LASTEXITCODE = 0
    return ConvertTo-Json -Depth 6 -InputObject @(@{workflow_runs=@(@{id=1;head_sha=('a'*40);head_repository=@{full_name='example/library'};event='push';status='completed';conclusion=$releasePolicyApi.Outcome})})
}
try {
    $env:REQUIRED_RELEASE_CHECKS = '[{"context":"Processor tests / library","appId":17}]'
    $env:REQUIRED_HOSTED_WORKFLOWS = '["ci.yml"]'
    $env:RELEASE_SKIP_HARDWARE_CHECKS = 'true'
    $env:RELEASE_HARDWARE_SKIP_REASON = 'Processor and local GitHub runner offline'
    $env:GITHUB_EVENT_NAME = 'workflow_dispatch'
    $env:GITHUB_REPOSITORY = 'example/library'
    $env:GITHUB_STEP_SUMMARY = $summaryPath
    $releasePolicyApi.Outcome = 'success'
    $releasePolicyApi.Calls = 0
    & "$PSScriptRoot/Wait-RequiredReleaseChecks.ps1" | Out-Null
    Verify ($releasePolicyApi.Calls -eq 1) 'Offline release must still check hosted validation.'
    $summary = [IO.File]::ReadAllText($summaryPath)
    Verify ($summary.Contains($revision) -and $summary.Contains($env:RELEASE_HARDWARE_SKIP_REASON) -and $summary.Contains('does not establish a hardware-test pass')) 'Override evidence must identify the source, reason and unverified hardware status.'
    $releasePolicyApi.Outcome = 'failure'
    Reject { & "$PSScriptRoot/Wait-RequiredReleaseChecks.ps1" }
    $env:REQUIRED_HOSTED_WORKFLOWS = ''
    Reject { & "$PSScriptRoot/Wait-RequiredReleaseChecks.ps1" }
    $env:REQUIRED_HOSTED_WORKFLOWS = '["../unexpected.yml"]'
    Reject { & "$PSScriptRoot/Wait-RequiredReleaseChecks.ps1" }
} finally {
    foreach ($name in $variables) { [Environment]::SetEnvironmentVariable($name,$saved[$name]) }
    if (Test-Path -LiteralPath $summaryPath) { Remove-Item -LiteralPath $summaryPath }
    Remove-Item Function:\git,Function:\gh
}
Write-Output "Hosted validation and offline release policy: $checks scenarios passed."
