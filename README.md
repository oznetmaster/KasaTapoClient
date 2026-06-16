# KasaTapoClient

A .NET client library for TP-Link Kasa and Tapo devices, enabling discovery, monitoring, inspection, and control of supported plugs, bulbs, light strips, power strips, hubs, and selected child devices.

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

This .NET library was developed with compatibility and behavior reference material from the upstream `python-kasa` project. See [ATTRIBUTIONS.md](ATTRIBUTIONS.md).

## TPAP Status

This repository currently contains the only known working implementation in this codebase of the TPAP protocol path used for supported Tapo devices.

In practical use, this means local TPAP communication can work without enabling the Tapo third-party compatibility option on the device. The current implementation and validation work were performed with that option left disabled.

## TPAP Keepalive and Idle Reconnect Behavior

Long-lived TPAP sessions are sensitive to idle time. Without keepalive traffic, the device-side session can age out and the next command may incur a noticeable reconnect penalty.

To address this, the TPAP connection includes a keepalive mechanism so an established session remains warm during idle periods. This materially improves the latency of the first command issued after an idle interval and is especially important for command-oriented or automation-driven scenarios where responsiveness after a quiet period matters.

During the benchmark and live-device validation work in this repository, the keepalive-backed path reduced long-idle reconnect behavior from a clearly noticeable delay to sub-100 ms behavior for the next command in the measured scenarios. The exact timing will still depend on device model, network conditions, and idle duration, but the keepalive was a significant improvement in observed real-device timings.

## Installation

The library is intended to be available as the `KasaTapoClient` NuGet package before this repository is made public.

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

## Test Console

The solution includes `KasaClient.Console`, a console application for discovery and command execution against real devices.

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

Public documentation for this repository is intended to be available on GitHub Pages:

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
