# Concurrent releases and completed validation

Independent tasks may initiate releases at the same time. All publication workflows in this repository share `public-release` with `cancel-in-progress: false` and `queue: max`. GitHub retains up to 100 queued requests per group; overflow/cancellation is not a successful release. Different repositories publish independently. Do not invoke another workflow requiring the same publication lock while holding it.

On manual release workflows, desktop and processor evidence can be reused independently. Both options default to false:

- `skip_desktop_checks` requires `desktop_skip_reason`, identifying the tested source, completion time, frameworks, pass counts and report location.
- `skip_hardware_checks` requires `hardware_skip_reason`, identifying processor results, source, completion time, cleanup and reservation release. It also supports an explicitly recorded unavailable-hardware exception.
- `validation_commit` selects recent successful CI for a full ancestor SHA. The shared policy verifies the source relationship, approved documentation/release-automation-only differences, exact check identity and completion within 24 hours. Runtime, tests, projects, dependencies and other build/validation inputs require new evidence.

The pinned shared action validates and records each selection. Manual reasons are operator attestations, not automatic verification of private reports. Tag-triggered releases cannot select manual overrides. Reuse does not establish a new test pass. Selecting one override retains the other configured checks.

Existing source test commands remain available and run by default. Explicit desktop reuse avoids the repeated source suites where the release workflow invokes them. Builds, fresh package consumers, package execution/integrity, metadata, API, license and distribution-version checks remain mandatory where already configured. Discovery may still run to create a current inventory for new-package verification. DevTools and NUnit release builders accept `SkipDesktopTests` only with nonempty single-line `DesktopValidationEvidence`; their new-package checks remain outside that branch.

Use an immutable source commit, isolated checkout and run-scoped artifacts. Recheck version/tag/package ownership under the publication lock; never overwrite a different release's assets or infer success merely from a duplicate package response. A partially published version requires provenance inspection before resuming. Publication serialization does not replace processor reservations.

Processor ownership uses the processor-resident shared lease protocol across cooperating hosts. Availability observation is advisory; atomic acquisition is decisive. Uncertain cleanup retains the lease. A busy runner or processor must not be reported as a test pass. Central hardware routing still needs qualified capability-based workers, shared version allocation and independent processor admission before removing its current global serialization. VM availability alone does not establish runner/tool qualification.
