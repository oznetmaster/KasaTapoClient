# KasaTapoClient

For shipped changes, see the [changelog](CHANGELOG.md). Test, CI and build history is recorded separately in [development and validation history](DEVELOPMENT-HISTORY.md).


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
- SMART energy-monitoring compatibility across v1 and v2 devices, including optional-method fallback for partial v2 implementations
- Host, light, child-device, and effect control where supported
- Double-click enable/disable on Tapo button child devices (S200B and similar) via `SetChildDoubleClickEnabledAsync`
- Sensor reporting-interval configuration on Tapo hub child sensors via `SetChildReportIntervalAsync`
- Optional live device tests and Benchmark.NET suites for transport and latency analysis
- TPAP keepalive support to reduce reconnect penalties after long idle periods
- Per-device operation serialization so concurrent commands, refreshes, and child operations against one physical endpoint run one-at-a-time
- Automatic de-duplication of concurrent `Discover.ConnectAsync` calls for the same device, so only one physical connection is ever dialed at a time per host/port
- Value-equality device and child state models (e.g. `LightState`, `FanState`, sensor/button states), so consumers can compare state snapshots directly to detect changes
- Optional, explicit shared-connection reuse via `Discover.GetOrConnectSharedAsync` for call sites that are known to target the same device and want to avoid each opening an independent connection
- Attribute-controlled System.Text.Json wire models with typed public state and control operations

This .NET library was developed with compatibility and behavior reference material from the upstream `python-kasa` project. See [ATTRIBUTIONS.md](ATTRIBUTIONS.md).

`KasaTapoClient` is for local device communication only. It does not implement TP-Link cloud control or remote cloud APIs.

## TPAP Status

This repository currently contains the only known working implementation in this codebase of the TPAP protocol path used for supported Tapo devices.

In practical use, this means local TPAP communication can work without enabling the Tapo third-party compatibility option on the device. The current implementation and validation work were performed with that option left disabled.

TPAP hubs (H100 and similar) authenticate with the same TP-Link account credentials used by plugs, bulbs, and light strips. Only cameras and doorbells use the local device-passcode authentication path. Devices that advertise a TPAP preference during discovery are connected over TPAP even when an older cached profile recorded a different transport; camera, doorbell, and robot vacuum families are excluded from that behavior because they are pinned to a specific encryption kind.

## TPAP Keepalive and Idle Reconnect Behavior

Long-lived TPAP sessions are sensitive to idle time. Without keepalive traffic, the device-side session can age out and the next command may incur a noticeable reconnect penalty.

To address this, the TPAP connection includes a keepalive mechanism so an established session remains warm during idle periods. This materially improves the latency of the first command issued after an idle interval and is especially important for command-oriented or automation-driven scenarios where responsiveness after a quiet period matters.

During the benchmark and live-device validation work in this repository, the keepalive-backed path reduced long-idle reconnect behavior from a clearly noticeable delay to sub-100 ms behavior for the next command in the measured scenarios. The exact timing will still depend on device model, network conditions, and idle duration, but the keepalive was a significant improvement in observed real-device timings.

## Connection Reuse and Network Resource Usage

Transport implementations minimize redundant connection setup:

- `HttpTokenTransport`, `KlapTransport`, and `TpapTransport` each share a single static `HttpClient` instance per transport type, allowing the underlying handler to pool and reuse TCP/TLS connections across requests instead of establishing a new connection per device instance.
- `LegacyTransport` (the raw XOR/TCP protocol on port 9999) maintains a persistent socket connection per device instance, reconnecting only when a failure is detected or when the connection has been idle for more than 10 seconds, instead of opening a new TCP connection for every command. The idle timeout protects against the device silently closing the socket on its end after a period of inactivity.

These changes reduce TCP and TLS handshake overhead and OS-level socket churn without changing observed command latency or benchmark throughput.

`Discover.ConnectAsync` coordinates concurrent connect attempts for the same device identity (host/port): if a connect is already in flight when another call for the same device arrives, the second call awaits the first instead of opening its own independent connection, and both callers receive the same resulting `KasaDevice` instance. This coalescing only applies while a connect is actively in progress; each call that does not overlap an in-flight connect for the same identity receives its own new, independently owned `KasaDevice` instance, which that caller is responsible for disposing - matching the usual connect/use/dispose pattern and avoiding any ambiguity about who owns the instance's lifetime. Because coalescing is keyed only by host/port, concurrent calls are also checked for configuration equivalence (credentials, timeout, and connection options) before joining an in-flight connect - a materially different configuration for the same host/port falls through to its own independent connect instead of silently reusing a connection built from a different caller's settings.

For the narrower case where multiple independent call sites are known to target the same device and want to share one connection instead of each dialing their own (for devices that reject or reset additional concurrent sessions), use `Discover.GetOrConnectSharedAsync` instead. This is an explicit, opt-in API: it returns a long-lived instance shared by every caller requesting the same device identity, connecting only if no live shared instance already exists. There is no reference counting - any caller may `Dispose()` the shared instance, and the next `GetOrConnectSharedAsync` call for that identity simply detects the cached instance has been disposed and transparently creates and caches a fresh replacement, mirroring the same stale-connection recovery model `LegacyTransport` already uses for idle/closed sockets. The same configuration-equivalence check applies here too: a cached shared instance is only reused if the requesting configuration matches the one it was created from, otherwise a fresh, independent connection is made and becomes the new shared instance for that identity. Because disposing a shared instance affects every other holder, only use `GetOrConnectSharedAsync` when the call sites sharing it are coordinated about that fact; `ConnectAsync` remains the safe default for callers that want their own, exclusively-owned instance. `updateState` applies whether a fresh connection is created or an existing shared instance is reused: when `true`, a cache hit refreshes the shared instance's state (`LightState`, `IsOn`, `Children`, etc.) via `KasaDevice.UpdateAsync` before returning it, rather than only the very first caller for that identity ever seeing device state loaded.

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
if (discoveredDevices.Count == 0)
    throw new InvalidOperationException ("No devices discovered; try targeted discovery or a known protocol configuration.");

var credentials = new DeviceCredentials (
    Environment.GetEnvironmentVariable ("TAPO_USERNAME"),
    Environment.GetEnvironmentVariable ("TAPO_PASSWORD"));
DiscoveryResult firstDevice = discoveredDevices[0];

using KasaDevice discoveredDevice = await Discover.ConnectAsync(firstDevice, credentials: credentials).ConfigureAwait(false);
await discoveredDevice.UpdateAsync().ConfigureAwait(false);
```

To broadcast only the legacy (port 9999) discovery request, e.g. when smart/Tapo discovery is not needed or not desired on a given network:

```csharp
IReadOnlyList<DiscoveryResult> legacyDevices = await Discover.DiscoverLegacyAsync().ConfigureAwait(false);
```

### Connect to a Tapo plug by IP

These examples use the published **KasaTapoClient 1.8.1** API. Earlier documentation incorrectly called `Discover.ResolveConfigurationAsync`; that is not a public API in 1.8.1 or the current source. There is no missing NuGet method to install.

#### Known IP, protocol discovered automatically

Start with targeted discovery when you know the IP but not the device's protocol. It does not rely on broadcast discovery. This complete console program reads state without switching the plug; supply the address and account credentials through your local environment:

```csharp
using System;
using KasaTapoClient;

string host = Environment.GetEnvironmentVariable ("TAPO_HOST")
	?? throw new InvalidOperationException ("Set TAPO_HOST to the plug's IP address.");
string username = Environment.GetEnvironmentVariable ("TAPO_USERNAME")
	?? throw new InvalidOperationException ("Set TAPO_USERNAME to the TP-Link account email.");
string password = Environment.GetEnvironmentVariable ("TAPO_PASSWORD")
	?? throw new InvalidOperationException ("Set TAPO_PASSWORD to the account password.");
var credentials = new DeviceCredentials (username, password);

using KasaDevice device = await Discover.DiscoverSingleAsync (
	host,
	credentials: credentials,
	timeout: TimeSpan.FromSeconds (10));

// The connection loads current state by default; UpdateAsync refreshes it later.
Console.WriteLine ($"Device: {device.Alias}");
Console.WriteLine ($"Is on: {device.IsOn}");

// When you want to operate the plug:
// await device.TurnOnAsync ();
// await device.UpdateAsync ();
// await device.TurnOffAsync ();
```

Likewise, `Discover.ConnectAsync` with `DeviceTransportKind.Auto` performs targeted discovery before connecting, even when you supply an IP address and connection parameters. This explains how an Auto connection may succeed after broadcast discovery returned no devices. Auto is not a discovery-free path.

#### No UDP discovery: specify the known protocol

If targeted discovery is also unavailable, explicit protocol configuration bypasses it. **Do not infer the protocol from the P110 model name.** Obtain the actual encryption, port and TLS requirements from device discovery on a working network or other verified device information. The locally checked P110 reports TPAP over HTTP port 80; that does not establish what every P110 uses.

For a plug **known to use TPAP on HTTP port 80**, replace the connection call above with this configuration, using the same `host` and `credentials`:

```csharp
var parameters = new DeviceConnectionParameters (
	deviceFamily: DeviceFamilyKind.SmartTapoPlug,
	encryptionKind: DeviceEncryptionKind.Tpap,
	loginVersion: 2,
	useHttps: false,
	httpPort: 80);
var options = new DeviceConnectionOptions (
	transportKind: DeviceTransportKind.HttpToken,
	connectionParameters: parameters,
	useSsl: false,
	useDefaultCredentials: false,
	defaultCredentialProfile: DefaultCredentialProfile.None,
	applicationPath: "/",
	useSecurePassthrough: true,
	tpapKeepAliveInterval: null);
var configuration = new DeviceConfiguration (
	host: host,
	port: 80,
	credentials: credentials,
	connectionOptions: options,
	timeout: TimeSpan.FromSeconds (10));

using KasaDevice device = await Discover.ConnectAsync (configuration);
Console.WriteLine ($"Device: {device.Alias}");
Console.WriteLine ($"Is on: {device.IsOn}");
```

`DeviceTransportKind.HttpToken` selects the HTTP transport family; `DeviceEncryptionKind.Tpap` selects TPAP within that family. The explicit transport prevents ConnectAsync from running UDP discovery. The TPAP transport still performs its required HTTP protocol bootstrap and authentication.

`ApplicationPath` means the device's HTTP API path, not an application name, local folder or credential-storage path. It defaults to `/app`. Empty or whitespace values are rejected by the options constructor, so passing an empty string is not equivalent to omitting the argument. Use `/` for TPAP configuration and `/app` for AES. KLAP uses its own `/app/handshake1`, `/app/handshake2` and `/app/request` endpoints, so an application name is unnecessary even if it appeared to work with that protocol.

| Setting | Guidance |
| --- | --- |
| Family | `SmartTapoPlug` is for a Tapo plug, not a bulb, hub, camera or legacy Kasa device. |
| Encryption | Use the actual `Tpap`, `Aes` or `Klap` protocol. `Unknown` is not universal protocol negotiation. For a known AES device use `/app` and secure passthrough. KLAP hardware has not been available for local validation; compilation is not a hardware compatibility claim. |
| Login version | Use advertised metadata when available. `null` leaves the selected transport's default behavior in place. |
| Port and HTTPS | Match the actual device metadata. Port 80 and HTTP in the example apply to that configuration, not every plug. Do not substitute the legacy Kasa port 9999. |
| Credentials | Use the TP-Link account credentials used by the plug. They authenticate the local connection; this is not TP-Link cloud control. |
| Discovery result | Use `Discover.CreateConfiguration (result, credentials)` to retain the discovered protocol metadata, or call `Discover.ConnectAsync (result, credentials: credentials)` directly. `DiscoveryResult.Configuration` is not a public property. |

The runnable [DirectTapoPlug example](examples/DirectTapoPlug/Program.cs) requires the protocol, port and HTTP/HTTPS scheme explicitly; it has no default protocol. It references NuGet **1.8.1**, rather than the local library project, and builds for both supported frameworks. With `TAPO_USERNAME` and `TAPO_PASSWORD` supplied privately, a known TPAP/HTTP/80 configuration is:

```powershell
dotnet run --project examples/DirectTapoPlug --framework net10.0 -- 192.0.2.10 tpap 80 http state
```

Replace the example address and protocol settings with your verified device information. The default action is `state`; use `on` or `off` only when you intend to change the plug's output. Keep real credentials out of source files, committed launch profiles and shell scripts.


### When discovery returns no devices

`Discover.DiscoverAsync()` sends legacy and smart UDP discovery to `255.255.255.255` by default. `Discover.DiscoverLegacyAsync()` sends only the legacy request on UDP 9999; an empty legacy result does not establish that a Tapo plug is unreachable. Smart discovery sends to UDP 20002 and accepts smart replies from UDP 20002 or 20004. This is UDP discovery, not mDNS.

Compare broadcast with a request to the device's current IPv4 address:

```csharp
IReadOnlyList<DiscoveryResult> broadcast = await Discover.DiscoverAsync (
	timeout: TimeSpan.FromSeconds (5));
IReadOnlyList<DiscoveryResult> targeted = await Discover.DiscoverAsync (
	timeout: TimeSpan.FromSeconds (5), target: "192.0.2.10");
Console.WriteLine ($"Broadcast: {broadcast.Count}; targeted: {targeted.Count}");
```

Replace the example address with the plug's address. The runnable [DiscoveryDiagnostics example](examples/DiscoveryDiagnostics/Program.cs) performs both checks plus legacy-only discovery, and lists active IPv4 adapters and the returned protocol configuration. It needs no credentials and does not switch devices:

```powershell
dotnet run --project examples/DiscoveryDiagnostics --framework net10.0 -- 192.0.2.10
```

The 1.8.1 implementation binds its discovery sockets to any local IPv4 address and lets the OS choose the outbound route. It does not explicitly send a separate broadcast on every adapter. If targeted discovery works but default broadcast does not, inspect the adapter/route selection, VPNs or virtual adapters, host firewall, and whether the computer and device are on different subnets or an isolated Wi-Fi network. These are diagnostic possibilities, not a conclusion that the network is at fault. A subnet-directed broadcast can be supplied as the diagnostic program's optional second argument; derive that address from the actual subnet mask, not simply by replacing the last octet with 255.

If both fail, verify the device address and local connectivity. A known-protocol direct connection can help distinguish UDP discovery from HTTP control reachability. Successful app control alone does not demonstrate local UDP reachability.

When reporting a failure, include the package version, OS/runtime, device model and firmware, whether a VPN/multiple adapters are present, and the broadcast/targeted counts. Review diagnostic output before sharing local addresses. A packet capture restricted to UDP 9999, 20002 and 20004 can distinguish queries that did not leave the computer, replies that did not arrive, and replies the library did not parse; do not include account credentials or unrelated traffic. Discovery working on another network does not resolve a failure on the reporting user's machine.

### Light transitions

Optional transition durations for supported legacy light on/off/brightness commands are available through additive overloads, while preserving the original public method signatures:

```csharp
await device.TurnLightOnAsync(1500).ConfigureAwait(false);
await device.SetBrightnessAsync(60, 1500).ConfigureAwait(false);
```

### Device operation serialization

Operations on a single `KasaDevice` are serialized internally. This means concurrent calls against the same physical endpoint, including hub children and power-strip outlets that share a parent device session, run one-at-a-time.

Different `KasaDevice` instances for different physical hosts can still run in parallel. The serialization is intended to prevent overlapping transport/session access and command/refresh interleaving on the same device.

Version 2.0 removes public raw-payload APIs. See the [2.0 migration guide](MIGRATION-2.0.md) before upgrading from 1.x.

### Typed state and commands

Refresh a device with `await device.UpdateAsync()` and read `SystemInfo`, `EnergyUsage`, `Light.State`, or its child modules. Use operations such as `TurnOnAsync()`, `TurnOffAsync()`, `SetBrightnessAsync()` and the typed module methods to change state. Control operations refresh their cached state before returning.

The library handles wire JSON internally using attribute-controlled models. Public state models expose values rather than raw JSON strings; public commands do not accept JSON payloads or JSON-library types. Newtonsoft.Json and log4net are no longer dependencies. Debug builds retain diagnostic `System.Diagnostics.Debug` tracing; no logging provider is configured by the library.

## Test Console

The solution includes `KasaClient.Console`, a console application for discovery and command execution against real devices.

Useful diagnostic commands include:

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

`KasaClient.Tests` targets `net472` and `net10.0`, using NUnit 4.6.1, NUnit3TestAdapter 6.3.0, Microsoft.NET.Test.Sdk 18.10.1, NUnit.Analyzers 4.15.0 and coverlet.collector 10.0.1. See the [test README](KasaClient.Tests/README.md) for dependencies, offline and live commands, and configuration handling.

Run the deterministic tests on both targets with:

```powershell
dotnet test KasaClient.Tests/KasaClient.Tests.csproj --configuration Debug
```

The shared `.runsettings` excludes the `Live` category by default. Existing CI filters (`TestCategory!=Live`) also work with NUnit. To opt into live-device tests after configuring `LiveTestSettings.json`, use `--settings .runsettings.live`; these tests can change device state. To check discovery without running them, add `--list-tests`. A separate settings file is necessary because the default settings exclusion is combined with command-line filters.

Fixtures use `LifeCycle.InstancePerTestCase` to preserve a fresh instance for every test. Live fixtures remain non-parallel. The migration retains data-driven cases and exact asynchronous exception checks.

- `KasaClient.Tests` contains deterministic unit coverage and optional live-device integration coverage
- Live-test scaffolding is included for exercising real hardware paths when a compatible device environment is available
- `BenchmarkSuite1`, `BenchmarkSuite2`, and `BenchmarkSuite3` measure typed control and refresh operations. Version 2.0 measurements include state refresh, so they are not directly comparable with earlier raw-command round-trip measurements.

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
- `KasaClient.Tests` — NUnit-based deterministic tests, optional live integration tests, and supporting scaffolding
- `BenchmarkSuite1`, `BenchmarkSuite2`, `BenchmarkSuite3` — Benchmark.NET measurement suites used during transport and latency investigation

### Live device identity and private configuration

Keep the real `KasaClient.Tests/LiveTestSettings.json` in your local Git exclusions (`.git/info/exclude`). Publish only `LiveTestSettings.sample.json` with placeholders. Credentials, device identities, aliases, addresses, child-device IDs, and live-test output are local configuration and results, not release assets. The normal CI workflows explicitly exclude the `Live` category.

Each device entry may use `deviceId` (preferred), a unique discovery `alias`, or the existing `host`. For example, a light entry can be `{ "deviceId": "replace-with-discovered-device-id" }`, or `{ "alias": "My test light" }`. Obtain IDs from `DiscoveryResult.DeviceId`. If a device ID is provided it takes precedence over alias and host. Missing or ambiguous matches fail without falling back to another device. Aliases must be unique and can change when a device is renamed.

With a stable selector, the fixture uses `Discover.DiscoverAsync` to resolve the current address and advertised connection parameters before connecting, including after a retry. Saved addresses and explicit transport options are used only for host-based entries. NUnit case names use the selector instead of the changing address. Test-case enumeration reads configuration without contacting equipment; only live-test execution resolves and connects to the selected device. Device IDs and aliases remain in the local configuration, never in test source.

`TestDataDirectory` is an optional NUnit parameter for the settings directory. `EnableLiveTests` overrides the JSON `enabled` value for that operation. When no override is supplied, the JSON value remains the default. Keep it false unless explicitly opting into live execution. These fixtures can change device state, so use equipment reserved for testing.

Live tests that change a device capture the reported state before sending control commands and restore that state in `finally`, including after a failed assertion. Power tests restore the original on/off state; the brightness test restores both brightness and power. If the required initial state is unavailable, the test fails before changing the device. Restoration is verified by refreshing the device, and a restoration failure fails the test. The hub tests only read state.

Discovery results are shared only within one NUnit run. Every new run, including a filtered selection, starts with an empty discovery cache. A missing device or a transient connection failure triggers a fresh scan. Each test still establishes its own connection and reads the device's current state before any changes.

Live progress includes elapsed time for discovery, connection and initial state, test actions, and restoration. Set `observationDelayMilliseconds` in private settings to `0` (the default) to omit deliberate observation pauses, or `2000` to keep each visible state for two seconds. State verification still waits for the device to report the expected result. Values from 0 to 60000 are accepted.

For unattended temperature checks, set `temperatureChildDeviceId` on the hub entry to a T310 or T315 child ID. `Hub_TemperatureSensor_RefreshesReportedReading` refreshes the sensor twice, checks that its reading is finite and has a supported, consistent unit, and logs both readings. It does not require a temperature change or modify any device settings. It has both `Live` and `Unattended` categories; select this test or filter by both categories for unattended runs. A successful refresh reads the hub's latest reported value; it does not prove that the battery sensor transmitted a new sample between requests.

## Acknowledgements

Behavioral and compatibility reference work in this project draws on the upstream [python-kasa](https://github.com/python-kasa/python-kasa) project and its public documentation.

## License

MIT © 2026 Neil Colvin — see [LICENSE](LICENSE).

## Continuous integration tests

The [Unit tests workflow](.github/workflows/unit-tests.yml) runs on pull requests and pushes to the main development branch. Separate Windows jobs test **net472** and **.NET 10**, retaining a result file for each suite/runtime. Live tests are excluded; no account credentials or physical devices are needed. These checks do not publish packages or releases.

## Publishing when local hardware is unavailable

The publish/release workflows support an explicit manual override when the processor or local self-hosted GitHub Actions runner is unavailable. Select `skip_hardware_checks` and provide a single-line `hardware_skip_reason`. Use the workflow's normal source and version controls. The override applies only to that invocation and is recorded with the exact source revision in its warning and job summary; it does not create a passing hardware-test result.

GitHub-hosted validation remains mandatory for the checked-out source, and the normal build, tests and packaging steps still run. Wait for the configured hosted workflows to pass, or run them on the same source revision first. None of these hosted checks needs the local runner or processor. Automatic tag/release-triggered runs retain the normal hardware checks; use a manual invocation of the updated release workflow when an offline override is needed.