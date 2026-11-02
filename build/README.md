# Build and test

This repository uses [the shared Cake build component](https://github.com/oznetmaster/BuildAutomation/tree/6f20e1937d3bab8ba7e545a3a77b6fe689f63f2e) pinned in `component.json`. It is build tooling only; product dependencies and versions are unchanged.

The CI workflow retains its jobs, framework matrix, filters, prerequisites and artifact upload rules. Repository-owned recipes preserve the build/test/package commands. Publication, validation reuse, approvals, runner selection and hardware reservations remain in GitHub Actions.

Run the same task locally with .NET 10, Git and PowerShell:

```powershell
./build/Invoke.ps1 -Task TASK_NAME
```

For a matrix task, set its `BUILD_INPUT_*` environment values as shown in the workflow before invoking it. These are project/framework selections, not credentials. The first local invocation downloads the immutable component into ignored `artifacts/build-components/`.

Available tasks:

- `unit-tests-test-run-unit-tests`

The shared component rejects missing/stale/empty/failed declared TRX results and writes one receipt per invocation beneath `artifacts/build-automation/`. It does not publish packages or invoke live hardware tests. Existing specialized scripts remain authoritative.
