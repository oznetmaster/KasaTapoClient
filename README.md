# KasaTapoClient

A .NET client library for TP-Link Kasa and Tapo devices, enabling local-network discovery, monitoring, inspection, and control of supported plugs, bulbs, light strips, power strips, hubs, and selected child devices.

TP-Link, Kasa, and Tapo are trademarks of their respective owners. This project is an independent, unofficial .NET library and is not affiliated with or endorsed by TP-Link.

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

See [CHANGELOG.md](CHANGELOG.md) for a summary of all release history, or the [GitHub releases](https://github.com/oznetmaster/KasaTapoClient/releases) page for full per-version details and build assets.

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
- Automatic de-duplication of concurrent `Discover.ConnectAsync` calls for the same device, so only one physical connection is ever dialed at a time per host/port
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

## Connection Reuse and Network Resource Usage

Transport implementations minimize redundant connection setup:

- `HttpTokenTransport`, `KlapTransport`, and `TpapTransport` each share a single static `HttpClient` instance per transport type, allowing the underlying handler to pool and reuse TCP/TLS connections across requests instead of establishing a new connection per device instance.
- `LegacyTransport` (the raw XOR/TCP protocol on port 9999) maintains a persistent socket connection per device instance, reconnecting only when a failure is detected or when the connection has been idle for more than 10 seconds, instead of opening a new TCP connection for every command. The idle timeout protects against the device silently closing the socket on its end after a period of inactivity.

These changes reduce TCP and TLS handshake overhead and OS-level socket churn without changing observed command latency or benchmark throughput.

`Discover.ConnectAsync` also coordinates concurrent connect attempts for the same device identity (host/port): if a connect is already in flight when another call for the same device arrives, the second call awaits the first instead of opening its own independent connection, and both callers receive the same resulting `KasaDevice` instance. This is purely a connect-time optimization - the cache entry exists only while a connect is in flight and is removed as soon as it completes, so it never behaves as a long-lived device registry, and the returned `KasaDevice` is still owned and disposed entirely by the caller.

## Request Timeouts and Cancellation

All transports honor the configured `DeviceConfiguration.Timeout` and an external `CancellationToken` on every network request, including the .NET Framework 4.7.2 `HttpWebRequest`-based fallback path used by `KlapTransport` and the periodic keepalive requests sent by `TpapTransport`. Previously, these two code paths could fall back to a runtime-default timeout (around 100 seconds) instead of the caller's configured timeout, and were not reliably cancellable once a request was in flight.

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

IReadOnlyList<DiscoveryResult> discoveredDevices = await Discover.DiscoverAsync().ConfigureAwait(false);
DiscoveryResult firstDevice = discoveredDevices[0];

using KasaDevice discoveredDevice = await Discover.ConnectAsync(firstDevice.Configuration).ConfigureAwait(false);
await discoveredDevice.UpdateAsync().ConfigureAwait(false);
```

To broadcast only the legacy (port 9999) discovery request, e.g. when smart/Tapo discovery is not needed or not desired on a given network:

```csharp
IReadOnlyList<DiscoveryResult> legacyDevices = await Discover.DiscoverLegacyAsync().ConfigureAwait(false);
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

Optional transition durations for supported legacy light on/off/brightness commands are available through additive overloads, while preserving the original public method signatures:

```csharp
await device.TurnLightOnAsync(1500).ConfigureAwait(false);
await device.SetBrightnessAsync(60, 1500).ConfigureAwait(false);
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
    new Newtonsoft.Json.Linq.JObject { ["device_on"] = true },
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

```powershell
dotnet run --project KasaClient.Console/KasaClient.Console.csproj --framework net10.0 -- host device-host-or-ip light on --t 1500
```

```powershell
dotnet run --project KasaClient.Console/KasaClient.Console.csproj --framework net10.0 -- host device-host-or-ip light brightness 60 --t 1500
```

```powershell
dotnet run --project KasaClient.Console/KasaClient.Console.csproj --framework net10.0 -- host device-host-or-ip light transition on
dotnet run --project KasaClient.Console/KasaClient.Console.csproj --framework net10.0 -- host device-host-or-ip light tr on
dotnet run --project KasaClient.Console/KasaClient.Console.csproj --framework net10.0 -- host device-host-or-ip light tr-on 12 tr-off 8
dotnet run --project KasaClient.Console/KasaClient.Console.csproj --framework net10.0 -- host device-host-or-ip light transition-on 12
dotnet run --project KasaClient.Console/KasaClient.Console.csproj --framework net10.0 -- host device-host-or-ip light transition-off 8
```

`raw` sends the JSON exactly as supplied. `smart` accepts a smart method name and optional parameters JSON, then builds the smart-protocol request envelope for TPAP/KLAP/AES smart devices. Add `--update` to refresh cached device state under the same device operation lock after the command completes.

For supported legacy light on/off/brightness commands, the console accepts `--t[ransition] <ms>` to pass a transition duration in milliseconds.

For supported smart light devices that expose persistent smooth transitions, the console also accepts `light tr|transition <on|off>`, `light tr-on|transition-on <seconds>`, and `light tr-off|transition-off <seconds>`.

For smart transition v2+ behavior, this matches `python-kasa` semantics:

- `transition` controls the effective overall enabled state
- `transition-on` and `transition-off` configure the directional transition behavior in seconds
- the stored directional durations are preserved internally when transitions are disabled
- the effective public directional values read as `0` when that direction is disabled
- the effective public overall enabled state is `True` when either directional transition is enabled, and `False` when both are disabled

The console status output also surfaces the negotiated smart component versions as a `Smart Modules:` line, for example `on_off_gradually=v4, preset=v3`. When light transition state is available, the console prints the overall enabled state, the per-direction enabled states, the effective on/off durations, and the stored on/off durations so `v2+` directional behavior is visible without inspecting raw JSON.

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
