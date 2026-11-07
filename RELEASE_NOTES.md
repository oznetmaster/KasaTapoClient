# KasaTapoClient 2.2.0

This minor release adds typed outlet reads and confirmed relay writes so consumers can refresh switch state frequently without fetching every energy module. Existing APIs and full-refresh behavior remain available.

## Added

- `ReadOutletStateAsync` returns the current standalone plug relay state without refreshing energy or other modules. A missing state is returned as `null`; callers must not turn that into an invented off state.
- `SetOutletStateAndReadBackAsync` serializes a standalone relay write and its actual readback. The returned value is device feedback, rather than the requested state.
- `RefreshStripOutletStatesAsync` refreshes legacy strip sockets through system information. SMART strips retain their full child-list refresh; this API does not promise a lightweight SMART-strip energy read.

These operations use the existing typed protocol models and per-device serialization. State-only reads return their own result and do not replace the full `SystemInfo` or energy snapshots supplied by `UpdateAsync`. Applications choose their scheduling and concurrency limits; the library does not create a background poller.

## Compatibility

- Public 2.1.0 lighting features and legacy KLAP v2 support are retained. No public member was removed or changed.
- Targets remain .NET 10 and .NET Framework 4.7.2; runtime dependencies are unchanged.
- The APIs are for supported standalone plugs or the explicitly documented strip operation. They do not replace bulb/dimmer power control or individual strip-socket commands.

## Validation

The public 2.2.0 source passed all 173 non-live tests on each supported runtime and the shared strict XML documentation gate.

The customer test driver used these polling APIs with two real outlets plus eighteen simulated endpoints on workstation and processor. Its customer subsequently confirmed working outlet-tile feedback and substantially reduced log activity on CP4-R. These are attributed customer observations and mixed-load test results, not a claim of twenty physical outlets or a measured customer processor-load reduction. Final public-source test results are recorded in the release validation history.
