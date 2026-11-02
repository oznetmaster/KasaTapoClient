# Build and test

This repository uses [the shared Cake build component](https://github.com/oznetmaster/BuildAutomation/tree/c0a215e202dc1e05a37707720ab12032be610518) pinned in `component.json`. It is build tooling only; product dependencies and versions are unchanged.

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

`documentation` builds the repository site and checks [the common Pages standard](https://github.com/oznetmaster/BuildAutomation/blob/c0a215e202dc1e05a37707720ab12032be610518/DOCUMENTATION-STANDARD.md): Home, Guides, API Reference, GitHub and NuGet navigation, modern theme, consistent title/footer, searchable content and every API output group. DocFX is locally pinned to 2.81.0. Existing project-specific generation and coverage checks remain in the recipe. GitHub Actions uploads/deploys the verified site; local builds publish nothing.
