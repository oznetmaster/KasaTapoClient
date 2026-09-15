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
