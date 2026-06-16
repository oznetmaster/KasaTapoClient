# Getting started

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
- `KasaClient.Tests` - MSTest coverage

## Basic flow

1. Discover or resolve a device configuration.
2. Connect to the device.
3. Refresh device state.
4. Invoke control or inspection operations.

## Discover devices with the library

```csharp
using KasaTapoClient;

IReadOnlyList<DiscoveryResult> discoveredDevices = await Discover.DiscoverDevicesAsync().ConfigureAwait(false);

foreach (DiscoveryResult result in discoveredDevices)
{
	Console.WriteLine($"{result.Host} -> {result.Model}");
}

DiscoveryResult firstDevice = discoveredDevices[0];
using KasaDevice device = await Discover.ConnectAsync(firstDevice.Configuration).ConfigureAwait(false);
await device.UpdateAsync().ConfigureAwait(false);
```

Discovery is the easiest first step when you want to inspect what supported devices are visible on the local network before deciding which one to control.

## Connect to a known device

```csharp
using KasaTapoClient;

DeviceConfiguration configuration = await Discover.ResolveConfigurationAsync(
	new DeviceConfiguration("device-host-or-ip")).ConfigureAwait(false);

using KasaDevice device = await Discover.ConnectAsync(configuration).ConfigureAwait(false);
await device.TurnLightOnAsync().ConfigureAwait(false);
await device.UpdateAsync().ConfigureAwait(false);
```

Use the direct configuration path when you already know the target device and want a deterministic connection flow for applications or automation.
