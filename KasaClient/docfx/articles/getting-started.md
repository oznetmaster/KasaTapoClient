# Getting started

`KasaTapoClient` is for local device communication only. It does not implement TP-Link cloud control or remote cloud APIs.

## Build the solution

```powershell
# restore
dotnet restore KasaClient.slnx

# build
dotnet build KasaClient.slnx --configuration Release
```

## Projects

- `KasaClient` - the main library
- `KasaClient.Console` - the console client
- `KasaClient.Tests` - NUnit coverage

## Basic flow

1. Discover a device, or construct a configuration with its known protocol settings.
2. Connect to the device.
3. Refresh device state.
4. Invoke control or inspection operations.

## Discover devices with the library

```csharp
using KasaTapoClient;

IReadOnlyList<DiscoveryResult> discoveredDevices = await Discover.DiscoverAsync().ConfigureAwait(false);

foreach (DiscoveryResult result in discoveredDevices)
{
	Console.WriteLine($"{result.Host} -> {result.Model}");
}

if (discoveredDevices.Count == 0)
    throw new InvalidOperationException ("No devices discovered; try targeted discovery or a known protocol configuration.");

var credentials = new DeviceCredentials (
    Environment.GetEnvironmentVariable ("TAPO_USERNAME"),
    Environment.GetEnvironmentVariable ("TAPO_PASSWORD"));
DiscoveryResult firstDevice = discoveredDevices[0];
using KasaDevice device = await Discover.ConnectAsync(firstDevice, credentials: credentials).ConfigureAwait(false);
await device.UpdateAsync().ConfigureAwait(false);
```

Discovery is the easiest first step when you want to inspect what supported devices are visible on the local network before deciding which one to control.

## Connect to a Tapo plug by IP

These examples use the published **KasaTapoClient 1.8.1** API. Earlier documentation incorrectly called `Discover.ResolveConfigurationAsync`; that is not a public API in 1.8.1 or the current source. There is no missing NuGet method to install.

### Known IP, protocol discovered automatically

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

### No UDP discovery: specify the known protocol

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

The runnable [DirectTapoPlug example](https://github.com/oznetmaster/KasaTapoClient/tree/main/examples/DirectTapoPlug) requires the protocol, port and HTTP/HTTPS scheme explicitly; it has no default protocol. It references NuGet **1.8.1**, rather than the local library project, and builds for both supported frameworks. With `TAPO_USERNAME` and `TAPO_PASSWORD` supplied privately, a known TPAP/HTTP/80 configuration is:

```powershell
dotnet run --project examples/DirectTapoPlug --framework net10.0 -- 192.0.2.10 tpap 80 http state
```

Replace the example address and protocol settings with your verified device information. The default action is `state`; use `on` or `off` only when you intend to change the plug's output. Keep real credentials out of source files, committed launch profiles and shell scripts.


## When discovery returns no devices

`Discover.DiscoverAsync()` sends legacy and smart UDP discovery to `255.255.255.255` by default. `Discover.DiscoverLegacyAsync()` sends only the legacy request on UDP 9999; an empty legacy result does not establish that a Tapo plug is unreachable. Smart discovery sends to UDP 20002 and accepts smart replies from UDP 20002 or 20004. This is UDP discovery, not mDNS.

Compare broadcast with a request to the device's current IPv4 address:

```csharp
IReadOnlyList<DiscoveryResult> broadcast = await Discover.DiscoverAsync (
	timeout: TimeSpan.FromSeconds (5));
IReadOnlyList<DiscoveryResult> targeted = await Discover.DiscoverAsync (
	timeout: TimeSpan.FromSeconds (5), target: "192.0.2.10");
Console.WriteLine ($"Broadcast: {broadcast.Count}; targeted: {targeted.Count}");
```

Replace the example address with the plug's address. The runnable [DiscoveryDiagnostics example](https://github.com/oznetmaster/KasaTapoClient/tree/main/examples/DiscoveryDiagnostics) performs both checks plus legacy-only discovery, and lists active IPv4 adapters and the returned protocol configuration. It needs no credentials and does not switch devices:

```powershell
dotnet run --project examples/DiscoveryDiagnostics --framework net10.0 -- 192.0.2.10
```

The 1.8.1 implementation binds its discovery sockets to any local IPv4 address and lets the OS choose the outbound route. It does not explicitly send a separate broadcast on every adapter. If targeted discovery works but default broadcast does not, inspect the adapter/route selection, VPNs or virtual adapters, host firewall, and whether the computer and device are on different subnets or an isolated Wi-Fi network. These are diagnostic possibilities, not a conclusion that the network is at fault. A subnet-directed broadcast can be supplied as the diagnostic program's optional second argument; derive that address from the actual subnet mask, not simply by replacing the last octet with 255.

If both fail, verify the device address and local connectivity. A known-protocol direct connection can help distinguish UDP discovery from HTTP control reachability. Successful app control alone does not demonstrate local UDP reachability.

When reporting a failure, include the package version, OS/runtime, device model and firmware, whether a VPN/multiple adapters are present, and the broadcast/targeted counts. Review diagnostic output before sharing local addresses. A packet capture restricted to UDP 9999, 20002 and 20004 can distinguish queries that did not leave the computer, replies that did not arrive, and replies the library did not parse; do not include account credentials or unrelated traffic. Discovery working on another network does not resolve a failure on the reporting user's machine.
