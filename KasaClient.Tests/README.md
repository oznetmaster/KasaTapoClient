# KasaTapoClient tests

The NUnit suite targets `net472` and `net10.0`. Use the .NET 10 SDK; executing `net472` locally requires Windows and .NET Framework 4.7.2 or later. Restore installs the following test dependencies:

| Package | Version | Purpose |
| --- | --- | --- |
| NUnit | 4.6.1 | Test framework and assertions |
| NUnit3TestAdapter | 6.3.0 | Visual Studio Test Explorer and `dotnet test` discovery/execution |
| Microsoft.NET.Test.Sdk | 18.10.1 | Desktop test host |
| NUnit.Analyzers | 4.15.0 | NUnit compile-time checks |
| coverlet.collector | 10.0.1 | Optional coverage collection |

The project also references the library and its net472 compatibility dependencies. Test tooling is private and is not a runtime dependency of the published client package.

## Offline tests

From the repository root:

```powershell
dotnet test KasaClient.Tests/KasaClient.Tests.csproj -c Debug -f net472
dotnet test KasaClient.Tests/KasaClient.Tests.csproj -c Debug -f net10.0
```

The default `.runsettings` excludes the `Live` category. The 123 deterministic cases cover protocol requests/responses, child modules, discovery, concurrency, retries, numeric firmware variants, typed response dispatch, legacy response merging and public API boundaries. Some tests inspect generated wire JSON independently to detect serialization regressions; the library itself uses typed contracts without a JSON DOM.

## Live tests

Create a private `KasaClient.Tests/LiveTestSettings.json` from `LiveTestSettings.sample.json`. Keep `enabled` false. Supply local credentials and stable device IDs or unique aliases for the configured test equipment. Never commit this file or publish live result files.

Explicitly selecting `.runsettings.live` supplies `EnableLiveTests=true` for that operation:

```powershell
dotnet test KasaClient.Tests/KasaClient.Tests.csproj -c Debug -f net472 --settings .runsettings.live
dotnet test KasaClient.Tests/KasaClient.Tests.csproj -c Debug -f net10.0 --settings .runsettings.live
```

Seven fixture cases cover plug power, light power/brightness, a strip outlet and hub metadata/temperature. Configuration can expand the case count. Tests that change state capture the original value, restore it in `finally`, and verify restoration. Live tests run sequentially. `TestDataDirectory` can point to another private settings directory; `EnableLiveTests` overrides the JSON switch for one run. Missing configuration causes inconclusive cases and does not count as a successful live validation.

Use `--list-tests` to inspect cases without operating devices. The public [README](../README.md#testing-and-benchmark-scaffolding) contains further configuration and benchmark guidance.
