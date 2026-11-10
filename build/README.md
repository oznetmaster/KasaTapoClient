# Build and test

This repository uses [the shared Cake build component](https://github.com/oznetmaster/BuildAutomation/tree/38d25666f55186e6ddbb73d256ac26524c1054bd) pinned in `component.json`. It is build tooling only; product dependencies and versions are unchanged.

The CI workflow retains its jobs, framework matrix, filters, prerequisites and artifact upload rules. Repository-owned recipes preserve the build/test/package commands. Publication, validation reuse, approvals, runner selection and hardware reservations remain in GitHub Actions.

Run the same task locally with .NET 10, Git and PowerShell:

```powershell
./build/Invoke.ps1 -Task TASK_NAME
```

For a matrix task, set its `BUILD_INPUT_*` environment values as shown in the workflow before invoking it. These are project/framework selections, not credentials. The first local invocation downloads the immutable component into ignored `artifacts/build-components/`.

Available tasks:

- `unit-tests-test-run-unit-tests`

The shared component rejects missing/stale/empty/failed declared TRX results and writes one receipt per invocation beneath `artifacts/build-automation/`. It does not publish packages or invoke live hardware tests. Existing specialized scripts remain authoritative.

## Pages documentation

`documentation` builds the repository site and checks [the common Pages standard](https://github.com/oznetmaster/BuildAutomation/blob/38d25666f55186e6ddbb73d256ac26524c1054bd/DOCUMENTATION-STANDARD.md): Home, Guides, API Reference, GitHub and NuGet navigation, modern theme, consistent title/footer, searchable content and every API output group. DocFX is locally pinned to 2.81.0. Existing project-specific generation and coverage checks remain in the recipe. GitHub Actions uploads/deploys the verified site; local builds publish nothing.

## Strict XML documentation

Every shared Cake task first checks the public and protected API documentation in the production projects listed in `recipe.json`. The check evaluates the actual Compile items and compiler symbols for every target framework in Debug and Release. Summaries, parameter and type-parameter descriptions, non-void return descriptions, and property/indexer `<value>` descriptions must be present and non-empty. Explicit inheritance is accepted; undocumented overrides are rejected. Compiler XML diagnostics are errors in every production build.

Run `./build/Invoke.ps1 -Task xml-documentation` to check documentation without running the test suites. The dedicated CI workflow and the unconditional gate in every publishing job use the same check, including when recent test evidence is reused. Existing repository-specific documentation checks remain in place.


## Public library publication

The common Cake task `release-assets` runs the configured package and app preparation. See `build/release.json` and the [shared publication protocol](https://github.com/oznetmaster/BuildAutomation/blob/38d25666f55186e6ddbb73d256ac26524c1054bd/ReleasePublishing/README.md). Publication runs in a separate job from retained, hash-verified artifacts. Keep the original run when retrying; use Re-run failed jobs so existing NuGet payloads can be verified against the same bytes.

If the publisher itself needs correction, dispatch this workflow from the default branch with the original `artifact_run_id` and exact `source_commit`. The recovery job verifies that successful build and publishes its frozen assets without rebuilding. Publication uses no GitHub environment binding, matching the shared standard and its package-scoped NuGet policy. No environment or deployment restriction setup is required.
