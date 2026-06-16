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
