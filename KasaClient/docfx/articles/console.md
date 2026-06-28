# Console usage

The console client provides a practical way to discover devices and exercise host, light, and child commands.

## Discover

```powershell
dotnet run --project KasaClient.Console/KasaClient.Console.csproj --framework net10.0 -- discover
```

## Inspect a device

```powershell
dotnet run --project KasaClient.Console/KasaClient.Console.csproj --framework net10.0 -- host device-host-or-ip state
```

## Control a device

```powershell
dotnet run --project KasaClient.Console/KasaClient.Console.csproj --framework net10.0 -- host device-host-or-ip on
dotnet run --project KasaClient.Console/KasaClient.Console.csproj --framework net10.0 -- host device-host-or-ip off
```

## Control a light

```powershell
dotnet run --project KasaClient.Console/KasaClient.Console.csproj --framework net10.0 -- host device-host-or-ip light brightness 25
```

## Control smart light transitions

```powershell
dotnet run --project KasaClient.Console/KasaClient.Console.csproj --framework net10.0 -- host device-host-or-ip light transition on
dotnet run --project KasaClient.Console/KasaClient.Console.csproj --framework net10.0 -- host device-host-or-ip light tr on
dotnet run --project KasaClient.Console/KasaClient.Console.csproj --framework net10.0 -- host device-host-or-ip light tr-on 12 tr-off 8
dotnet run --project KasaClient.Console/KasaClient.Console.csproj --framework net10.0 -- host device-host-or-ip light transition-on 12
dotnet run --project KasaClient.Console/KasaClient.Console.csproj --framework net10.0 -- host device-host-or-ip light transition-off 8
```

Use `--t[ransition] <ms>` for supported legacy light on/off/brightness commands, and use `tr|transition`, `tr-on|transition-on`, or `tr-off|transition-off` for supported smart bulbs that expose persistent smooth transition settings.

For smart transition v2+ behavior, this matches `python-kasa` semantics:

- `transition` controls the effective overall enabled state
- `transition-on` and `transition-off` configure the directional transition behavior in seconds
- the stored directional durations are preserved internally when transitions are disabled
- the effective public directional values read as `0` when that direction is disabled
- the effective public overall enabled state is `True` when either directional transition is enabled, and `False` when both are disabled

Console status output also prints a `Smart Modules:` summary with the negotiated smart component versions, such as `on_off_gradually=v4, preset=v3`. When light transition state is reported, the console shows the overall enabled state, the per-direction enabled states, the effective transition durations, and the stored transition durations.
