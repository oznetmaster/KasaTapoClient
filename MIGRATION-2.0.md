# Migrating to KasaTapoClient 2.0

Version 2.0 is a major release because it removes public raw JSON APIs. Both .NET Framework 4.7.2 and .NET 10 remain supported.

## State and commands

`RawJson` has been removed from discovery, device, child and module state models. Read their typed properties and module state instead. Child button events are available through `ChildDevice.TriggerLogs.Logs`; temperature, humidity, contact, motion and water-leak values are available through the corresponding child modules.

`KasaDevice.ExecuteCommandAsync` and `ExecuteSmartCommandAsync` are no longer public. Replace raw state queries with `UpdateAsync()`, then read the device or module state. Replace relay writes with `TurnOnAsync()` or `TurnOffAsync()` and lighting writes with the typed light operations. These control methods refresh cached state before returning and preserve per-device operation serialization.

Arbitrary vendor commands are no longer a public extension point. A new supported operation should have a typed request, typed result and dedicated method. No public JSON-library replacement API is provided.

The console's `raw` and `smart` commands were removed. Use its `host`, `light` and `child` operations. Benchmarks now measure typed control/refresh operations, including their refresh cost.

## Dependencies

Newtonsoft.Json and log4net are no longer referenced by the library. System.Text.Json 10.0.12 implements serialization on both targets. Consumers that used these packages only through this library should remove their direct references; retain them if other application code still uses them. On .NET Framework, restore the complete dependency graph and regenerate binding redirects as usual.

Wire property names are explicitly controlled with attributes. The library retains no JSON DOM or raw payload on public state objects. Numeric strings and the existing flexible sensor-warning representations remain supported.

See the [test README](KasaClient.Tests/README.md) for framework, adapter and runner versions and local validation commands.
