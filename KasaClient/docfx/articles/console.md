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
