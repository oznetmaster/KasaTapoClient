# Library usage

## Connect and update

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
