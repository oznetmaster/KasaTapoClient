# Library usage

## Discover and update

```csharp
using KasaTapoClient;

IReadOnlyList<DiscoveryResult> discoveredDevices = await Discover.DiscoverDevicesAsync().ConfigureAwait(false);
DiscoveryResult firstDevice = discoveredDevices[0];

using KasaDevice discoveredDevice = await Discover.ConnectAsync(firstDevice.Configuration).ConfigureAwait(false);
await discoveredDevice.UpdateAsync().ConfigureAwait(false);
```

Discovery is the easiest first step when you want to inspect what supported devices are visible on the local network before deciding which one to control.

## Connect to a known device

```csharp
using KasaTapoClient;

DeviceConfiguration configuration = await Discover.ResolveConfigurationAsync(
	new DeviceConfiguration("device-host-or-ip")).ConfigureAwait(false);

using KasaDevice device = await Discover.ConnectAsync(configuration).ConfigureAwait(false);
await device.UpdateAsync().ConfigureAwait(false);
```

## Control a light

```csharp
await device.TurnLightOnAsync().ConfigureAwait(false);
await device.SetBrightnessAsync(25).ConfigureAwait(false);
await device.TurnLightOffAsync().ConfigureAwait(false);
```

## Notes

Modern TPAP devices can maintain a warm session through the built-in keepalive support, reducing the reconnect penalty for long idle gaps.
