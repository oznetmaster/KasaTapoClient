# Library usage

`KasaTapoClient` is for local device communication only. It does not implement TP-Link cloud control or remote cloud APIs.

## Discover and update

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

Discovery is the easiest first step when you want to inspect what supported devices are visible on the local network before deciding which one to control.

## Connect to a known device

See the complete [direct Tapo plug connection example](getting-started.md#connect-to-a-tapo-plug-by-ip), including credentials, HTTP endpoint paths, protocol selection and a runnable project compiled against NuGet 1.8.1. Explicit HTTP transport configuration bypasses discovery; Auto still performs targeted discovery.

## Control a light

```csharp
await device.TurnLightOnAsync().ConfigureAwait(false);
await device.SetBrightnessAsync(25).ConfigureAwait(false);
await device.TurnLightOffAsync().ConfigureAwait(false);
```

## Notes

Modern TPAP devices can maintain a warm session through the built-in keepalive support, reducing the reconnect penalty for long idle gaps.
