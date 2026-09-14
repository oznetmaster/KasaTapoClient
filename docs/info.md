# KasaTapoClient

KasaTapoClient is a reusable .NET library and console client for TP-Link Kasa and Tapo devices.
It targets `.NET Framework 4.7.2` and `.NET 10`, and is intended to provide a practical programmable surface for discovery, inspection, and device control across plugs, bulbs, light strips, strips, hubs, and supported child devices.

## Release status

The library is available publicly as the `KasaTapoClient` NuGet package. The direct-connection example is checked against version 1.8.1.

## Installation

### Library package

The main package is produced from `KasaClient/KasaTapoClient.csproj`.
Install it with `dotnet add package KasaTapoClient --version 1.8.1`.

### Build from source

```powershell
# restore
dotnet restore KasaClient.slnx

# build
dotnet build KasaClient.slnx --configuration Release
```

## Solution layout

- `KasaClient` - reusable client library
- `KasaClient.Console` - console app for discovery and command execution
- `KasaClient.Tests` - NUnit coverage including optional live tests
- `BenchmarkSuite1`, `BenchmarkSuite2`, `BenchmarkSuite3` - Benchmark.NET measurement artifacts used during transport and latency investigation

## Basic console usage

The console project can discover devices and execute host/light/child commands.

```powershell
# discovery
dotnet run --project KasaClient.Console/KasaClient.Console.csproj --framework net10.0 -- discover

# inspect a host
dotnet run --project KasaClient.Console/KasaClient.Console.csproj --framework net10.0 -- host device-host-or-ip state

# switch a device on or off
dotnet run --project KasaClient.Console/KasaClient.Console.csproj --framework net10.0 -- host device-host-or-ip on
dotnet run --project KasaClient.Console/KasaClient.Console.csproj --framework net10.0 -- host device-host-or-ip off

# light-specific control
dotnet run --project KasaClient.Console/KasaClient.Console.csproj --framework net10.0 -- host device-host-or-ip light brightness 25
```

## Basic library usage

See the [complete direct connection by IP example](../README.md#connect-to-a-tapo-plug-by-ip) and [runnable console project](../examples/DirectTapoPlug/Program.cs). They cover credentials, protocol-specific parameters, the `/app` HTTP endpoint, connecting, refreshing state and controlling a plug with the published 1.8.1 API.

## Transport notes

The library supports legacy XOR devices as well as modern HTTP-family devices including TPAP.
For supported TPAP devices, the transport now includes a keepalive mechanism intended to avoid long-idle session expiry costs.

## Testing and benchmarks

The solution includes:

- deterministic unit tests in `KasaClient.Tests`
- optional live device integration tests driven by local settings
- Benchmark.NET suites used to investigate connection, command, refresh, and TPAP idle-session behavior

## Documentation and support material

This repository intentionally follows a lightweight public shape similar to the local `wiserHeatAPIv2` model repository:

- root `README.md`
- root `LICENSE`
- `.github/workflows/*`
- `docs/info.md`

## Attribution and upstream references

Compatibility, protocol behavior, and device-support reference material in this project were developed with reference to the `python-kasa` project.
See `ATTRIBUTIONS.md` for upstream attribution, licensing references, and repository links.
