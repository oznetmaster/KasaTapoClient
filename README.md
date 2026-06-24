# KasaTapoClient

A .NET client library for TP-Link Kasa and Tapo devices, enabling local-network discovery, monitoring, inspection, and control of supported plugs, bulbs, light strips, power strips, hubs, and selected child devices.

TP-Link, Kasa, and Tapo are trademarks of their respective owners. This project is an independent, unofficial .NET library and is not affiliated with or endorsed by TP-Link.

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

## Supported Platforms

| Target Framework | Supported |
| --- | --- |
| .NET 10 | ✅ |
| .NET Framework 4.7.2 | ✅ |

## Overview

`KasaTapoClient` provides a strongly typed .NET wrapper around local Kasa and Tapo device protocols. It supports:

- Local discovery and direct connection by host address
- State refresh and normalized device features
- Smart and legacy transport handling
- Host, light, child-device, and effect control where supported
- Optional live device tests and Benchmark.NET suites for transport and latency analysis
- TPAP keepalive support to reduce reconnect penalties after long idle periods
- Per-device operation serialization so concurrent commands, refreshes, and child operations against one physical endpoint run one-at-a-time
- Raw and smart-method command execution helpers for diagnostics and advanced integrations

This .NET library was developed with compatibility and behavior reference material from the upstream `python-kasa` project. See [ATTRIBUTIONS.md](ATTRIBUTIONS.md).

`KasaTapoClient` is for local device communication only. It does not implement TP-Link cloud control or remote cloud APIs.

## TPAP Status

This repository currently contains the only known working implementation in this codebase of the TPAP protocol path used for supported Tapo devices.

In practical use, this means local TPAP communication can work without enabling the Tapo third-party compatibility option on the device. The current implementation and validation work were performed with that option left disabled.

## TPAP Keepalive and Idle Reconnect Behavior

Long-lived TPAP sessions are sensitive to idle time. Without keepalive traffic, the device-side session can age out and the next command may incur a noticeable reconnect penalty.

To address this, the TPAP connection includes a keepalive mechanism so an established session remains warm during idle periods. This materially improves the latency of the first command issued after an idle interval and is especially important for command-oriented or automation-driven scenarios where responsiveness after a quiet period matters.

During the benchmark and live-device validation work in this repository, the keepalive-backed path reduced long-idle reconnect behavior from a clearly noticeable delay to sub-100 ms behavior for the next command in the measured scenarios. The exact timing will still depend on device model, network conditions, and idle duration, but the keepalive was a significant improvement in observed real-device timings.

## Installation

The library is available as the `KasaTapoClient` NuGet package.

```powershell
dotnet add package KasaTapoClient
```

## Quick Start

### Discover devices

```powershell
dotnet run --project KasaClient.Console/KasaClient.Console.csproj --framework net10.0 -- discover
```

### Inspect a device

```powershell
dotnet run --project KasaClient.Console/KasaClient.Console.csproj --framework net10.0 -- host device-host-or-ip state
```

### Use the library

```csharp
using KasaTapoClient;

IReadOnlyList<DiscoveryResult> discoveredDevices = await Discover.DiscoverDevicesAsync().ConfigureAwait(false);
DiscoveryResult firstDevice = discoveredDevices[0];

using KasaDevice discoveredDevice = await Discover.ConnectAsync(firstDevice.Configuration).ConfigureAwait(false);
await discoveredDevice.UpdateAsync().ConfigureAwait(false);
```

If you already know the device host or want a deterministic connection path, resolve the configuration directly:

```csharp
using KasaTapoClient;

DeviceConfiguration configuration = await Discover.ResolveConfigurationAsync(
    new DeviceConfiguration("device-host-or-ip")).ConfigureAwait(false);

using KasaDevice device = await Discover.ConnectAsync(configuration).ConfigureAwait(false);
await device.TurnLightOnAsync().ConfigureAwait(false);
await device.UpdateAsync().ConfigureAwait(false);
```

### Device operation serialization

Operations on a single `KasaDevice` are serialized internally. This means concurrent calls against the same physical endpoint, including hub children and power-strip outlets that share a parent device session, run one-at-a-time.

Different `KasaDevice` instances for different physical hosts can still run in parallel. The serialization is intended to prevent overlapping transport/session access and command/refresh interleaving on the same device.

Existing public APIs remain source-compatible, but callers that previously issued concurrent operations against the same `KasaDevice` may now observe those operations completing sequentially.

### Raw and smart command helpers

`ExecuteCommandAsync` sends a complete JSON payload exactly as supplied. It is useful for legacy Kasa JSON modules or diagnostics where the full request body is already known.

```csharp
string response = await device.ExecuteCommandAsync(
    "{\"system\":{\"get_sysinfo\":{}}}").ConfigureAwait(false);
```

For raw command scenarios that need the cached state refreshed before returning, use `DeviceStateUpdateMode.UpdateAfterCommand`:

```csharp
string response = await device.ExecuteCommandAsync(
    "{\"system\":{\"set_relay_state\":{\"state\":1}}}",
    DeviceStateUpdateMode.UpdateAfterCommand).ConfigureAwait(false);
```

`ExecuteSmartCommandAsync` is for TP-Link smart-protocol methods such as `get_device_info` and `set_device_info`. It builds the required smart request envelope, including request timestamp and terminal UUID fields, before sending the command.

```csharp
string response = await device.ExecuteSmartCommandAsync(
    "set_device_info",
    new System.Text.Json.Nodes.JsonObject { ["device_on"] = true },
    DeviceStateUpdateMode.UpdateAfterCommand).ConfigureAwait(false);
```

## Test Console

The solution includes `KasaClient.Console`, a console application for discovery and command execution against real devices.

Useful diagnostic commands include:

```powershell
dotnet run --project KasaClient.Console/KasaClient.Console.csproj --framework net10.0 -- host device-host-or-ip raw "{\"system\":{\"get_sysinfo\":{}}}"
```

```powershell
dotnet run --project KasaClient.Console/KasaClient.Console.csproj --framework net10.0 -- host device-host-or-ip smart get_device_info
```

```powershell
dotnet run --project KasaClient.Console/KasaClient.Console.csproj --framework net10.0 -- host device-host-or-ip smart set_device_info "{""device_on"":true}" --update
```

`raw` sends the JSON exactly as supplied. `smart` accepts a smart method name and optional parameters JSON, then builds the smart-protocol request envelope for TPAP/KLAP/AES smart devices. Add `--update` to refresh cached device state under the same device operation lock after the command completes.

To exercise per-device serialization from the console, run concurrent updates against one connected device:

```powershell
dotnet run --project KasaClient.Console/KasaClient.Console.csproj --framework net10.0 -- host device-host-or-ip serialize 4
```

## Testing and Benchmark Scaffolding

The repository includes MSTest-based test scaffolding as part of the committed solution layout.

- `KasaClient.Tests` contains deterministic unit coverage and optional live-device integration coverage
- Live-test scaffolding is included for exercising real hardware paths when a compatible device environment is available
- `BenchmarkSuite1`, `BenchmarkSuite2`, and `BenchmarkSuite3` contain Benchmark.NET measurement artifacts used for transport, latency, and keepalive investigation

This means the repository includes not just the production library and console app, but also the test and measurement infrastructure used to validate protocol behavior and performance characteristics.

## Devices Seen During Console Discovery

When the test console was run in the current validation environment, the following devices were discovered:

- `KP115(UK)` — Kasa Smart Plug with Energy Monitoring
- `KL130(UN)` — Kasa Smart Wi-Fi Full Color Bulb
- `H100(UK)` — Tapo hub
  - Tapo Smart Button
  - Tapo Smart Temperature & Humidity Sensor
  - Tapo Smart Motion Sensor
  - Tapo Smart Temperature & Humidity Monitor
- `KP105(UK)` — Kasa Mini Smart Wi-Fi Plug
- `KP105(UK)` — Kasa Mini Smart Wi-Fi Plug
- `KP303(UK)` — Kasa Smart Wi-Fi Power Strip
  - outlet 1
  - outlet 2
  - outlet 3
- `L900-5(EU)` — Tapo Smart Light Strip
- `P110(UK)` — Tapo Mini Smart Wi-Fi Plug with Energy Monitoring
- `L530E(EU)` — Tapo Smart Wi-Fi Multicolor Light Bulb

This list reflects real console discovery output from the development environment used during release preparation and is useful as a practical indication of the device families currently exercised by the project.

## Documentation

Public documentation for this repository is available on GitHub Pages:

- https://oznetmaster.github.io/KasaTapoClient/

## Repository Contents

- `KasaClient` — the main library project published to NuGet
- `KasaClient.Console` — a console-based client for discovery and control
- `KasaClient.Tests` — MSTest-based deterministic tests, optional live integration tests, and supporting scaffolding
- `BenchmarkSuite1`, `BenchmarkSuite2`, `BenchmarkSuite3` — Benchmark.NET measurement suites used during transport and latency investigation

## Acknowledgements

Behavioral and compatibility reference work in this project draws on the upstream [python-kasa](https://github.com/python-kasa/python-kasa) project and its public documentation.

## License

MIT © 2026 Neil Colvin — see [LICENSE](LICENSE).
