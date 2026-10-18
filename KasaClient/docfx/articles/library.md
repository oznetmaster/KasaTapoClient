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

## Optional lighting configuration (2.1.0)

```csharp
if (device.LightStripSegment.IsAvailable)
    await device.LightStripSegment.SetSegmentCountAsync (47).ConfigureAwait (false);

if (device.LightTransition.SupportsChangeTransition)
    await device.LightTransition.SetChangeTransitionEnabledAsync (true).ConfigureAwait (false);
```

`LightStripSegment.SegmentCount` is the physical cut length in 10 cm segments per run, with setter values from 0 to 50. It is independent of lighting-effect segment arrays. State is queried only for advertised `segment` components. `ChangeTransitionEnabled` reports fading between brightness or color settings while the light is on: `null` means unavailable, while `false` means supported and disabled. Existing on/off transition durations are preserved.

Both setters reject unavailable capabilities, honor cancellation, report device errors and refresh typed state after successful writes. Existing public APIs, target frameworks and runtime dependencies are unchanged.
