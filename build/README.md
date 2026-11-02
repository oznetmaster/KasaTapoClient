# Build and test

This repository uses [the shared Cake build component](https://github.com/oznetmaster/BuildAutomation/tree/ae6f311e530a18dc705971dcc655fc97de03e368) pinned in `component.json`. It is build tooling only; product dependencies and versions are unchanged.

The CI workflow retains its jobs, framework matrix, filters, prerequisites and artifact upload rules. Repository-owned recipes preserve the build/test/package commands. Publication, validation reuse, approvals, runner selection and hardware reservations remain in GitHub Actions.

Run the same task locally with .NET 10, Git and PowerShell:

```powershell
./build/Invoke.ps1 -Task TASK_NAME
```

For a matrix task, set its `BUILD_INPUT_*` environment values as shown in the workflow before invoking it. These are project/framework selections, not credentials. The first local invocation downloads the immutable component into ignored `artifacts/build-components/`.

Available tasks:

- `unit-tests-test-run-unit-tests`

The shared component rejects missing/stale/empty/failed declared TRX results and writes one receipt per invocation beneath `artifacts/build-automation/`. It does not publish packages or invoke live hardware tests. Existing specialized scripts remain authoritative.
