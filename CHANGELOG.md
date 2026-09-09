# Changelog

All notable changes to this project are documented here. Each entry summarizes the corresponding [GitHub release](https://github.com/oznetmaster/KasaTapoClient/releases), which remains the authoritative, detailed record (including build assets) for that version. This file exists as a single, scannable index of the full version history.

The format is loosely based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project follows [Semantic Versioning](https://semver.org/).

## [1.8.1] - Dependency version alignment

- **Build**: Fixed a NuGet restore failure (NU1605: package downgrade) caused by Microsoft.Bcl.Memory being pinned to 10.0.12 in KasaTapoClient.csproj while KasaClient.Console, KasaClient.Tests, and the BenchmarkSuite* projects pinned 10.0.11. All projects now reference Microsoft.Bcl.Memory 10.0.12.
- **Compatibility**: No public API or behavior changes.

## [1.8.0] - Report-interval enablement for hub child sensors

- **KasaDevice**: Added `SetChildReportIntervalAsync (childDeviceId, reportIntervalSeconds)` for Tapo hub child sensors (T100, T110, T310, T315, S200B, and similar). `ChildReportModeModule.ReportInterval` was previously read-only. The setter issues `set_device_info` with a `report_interval` parameter through the existing `control_child` envelope - the same generic method already used for `device_on` - and then refreshes parent state so the module and feature reflect the new value.
- **Modules**: Added `ChildReportModeModule.SetIntervalAsync (reportIntervalSeconds)` and `ChildDevice.SetReportIntervalAsync (reportIntervalSeconds)`, which delegate to the parent device action, mirroring the double-click module's read/write pattern from 1.7.0.
- **Validation**: A non-positive `reportIntervalSeconds` throws `ArgumentOutOfRangeException` before any request is sent. Calling the setter on a child that does not report an interval throws `NotSupportedException`. An unknown child identifier continues to throw `InvalidOperationException`.
- **Console**: Added a `ho[st] <address> c[hild] <childId|index> interval <seconds>` action. An invalid or missing seconds value is rejected with usage guidance before a connection is made.
- **Protocol note**: `report_interval` is a field on `get_child_device_list`, not on `get_report_mode` (which returns an unrelated `report_mode` string, e.g. `high_sensitivity`, and has no setter upstream or in this library). The `set_device_info` method and its `{ "report_interval": int }` payload were confirmed against a live T100 and S200B on the same hub by writing a new interval and reading it back, then restoring the original value.
- **Tests**: Added `ReportIntervalCommandTests` (2 tests) pinning the `control_child` envelope shape for the interval setter and verifying the report-mode read method omits `params`. Full suite passes on both `net472` and `net10.0` (83 tests each).
- **Compatibility**: Additive only. No existing public API signatures changed.

## [1.7.0] - Double-click enablement for button child devices

- **KasaDevice**: Added `SetChildDoubleClickEnabledAsync (childDeviceId, enabled)` for Tapo button child devices (S200B and similar). Double-click was previously read-only - `ChildDoubleClickModule.Enabled` and the `double_click_enabled` feature reported the current value, and `get_double_click_info` was already issued during child refresh, but the library exposed no way to change it. The setter issues `set_double_click_info` through the existing `control_child` envelope and then refreshes parent state, so the module and feature reflect the new value immediately after the call returns.
- **Modules**: Added `ChildDoubleClickModule.SetEnabledAsync (enabled)` and `ChildDevice.SetDoubleClickEnabledAsync (enabled)`, which delegate to the parent device action. This mirrors how `ChildDevice.TurnOnAsync`/`TurnOffAsync` already delegate to `KasaDevice.TurnChildOnAsync`/`TurnChildOffAsync`, so the module surface is now read/write rather than read-only.
- **Capability gate**: Calling the setter on a child that does not report `double_click_info` throws `NotSupportedException` before any request is sent, rather than relying on the device to reject the command. An unknown child identifier continues to throw `InvalidOperationException`, matching the existing child relay-state behavior.
- **Console**: Added a `ho[st] <address> c[hild] <childId|index> d[oubleclick] on|off` action for setting and resetting the option. Attempting it on an unsupported child reports a normal command error rather than surfacing an unhandled exception, and omitting the `on`/`off` value is rejected with usage guidance.
- **Protocol note**: The double-click methods use snake_case (`get_double_click_info`/`set_double_click_info`), unlike the camelCase child-lock methods (`getChildLockInfo`/`setChildLockInfo`). The setter name and its `{ "enable": bool }` payload were confirmed against an S200B on firmware `1.13.0 Build 250226 Rel.134253` by writing the value and reading it back. Note that upstream python-kasa exposes no SMART double-click setter, so this is new behavior rather than a port.
- **Tests**: Added `DoubleClickCommandTests` (3 tests) pinning the `control_child` envelope shape for enable and disable, and verifying that the read method omits the `params` member. Full suite passes on both `net472` and `net10.0` (81 tests each).
- **Compatibility**: Additive only. No existing public API signatures changed.

## [1.6.1] - TPAP hub authentication and discovery preference fixes

- **TpapTransport**: Tapo hubs (H100 and similar) could not complete TPAP authentication, failing the PAKE exchange with `-2203` even when the TP-Link account credentials were correct. `SmartTapoHub` was listed in the transport's camera-authentication family set, so hubs took the camera/doorbell path that blanks the username and hashes a local device passcode, rather than authenticating with the account credentials used by plugs, bulbs, and light strips. `SmartTapoHub` has been removed from that set, so hubs now authenticate exactly like other non-camera TPAP devices. Cameras and doorbells are unaffected and retain the passcode path. This bug predates this release and was not a regression: a pristine 1.6.0 build reproduces the identical failure.
- **Discover**: Discovery now honors a device's advertised TPAP preference when building a `DeviceConfiguration`. Previously, a cached or stale non-TPAP `ConnectionParameters` value would drive the connection even when discovery reported `tpap_preferred` or returned TPAP metadata, causing handshakes to be attempted against endpoints the device does not serve (for example a KLAP handshake against a TPAP-only hub). When a device advertises TPAP, the connection parameters are now promoted to the TPAP encryption kind, with the transport endpoint derived from the advertised TLS mode and port. Devices that do not advertise TPAP are left untouched.
- **Discover**: TPAP promotion deliberately skips the device families that `DeviceTransportFactory` pins to a specific encryption kind - `IotIpCamera` (XOR), and `SmartIpCamera`, `SmartTapoDoorbell`, and `SmartTapoRobovac` (AES). Promoting those families would have replaced a working connection with a `NotSupportedException`, so they are excluded from the override.
- **TpapTransport**: TPAP error classification now treats `-2202`, `-2203`, and `-2101` as authentication failures instead of generic protocol errors. When a device reports an `error_info` block alongside a failed PAKE exchange, the failed-attempt count, remaining attempts before lockout, and any active lockout duration are appended to the thrown exception message, so a credential problem is distinguishable from a transport problem and repeated retries can be stopped before the device locks out.
- **Console**: Saved connection profiles are now schema-versioned. Profiles written by earlier versions could pin a device to an obsolete transport/encryption kind indefinitely, bypassing discovery entirely; this is what masked the hub issue above by forcing KLAP onto a TPAP-preferred device. Profiles from older schema versions now have their cached connection parameters discarded on load while credentials are preserved, so the transport is re-resolved from current discovery data.
- **Tests**: Added `DiscoverTpapPreferenceTests` (12 tests) covering TPAP promotion via both `tpap_preferred` and TPAP metadata, endpoint derivation from the advertised TLS mode and port, preservation of the device family and login version, pass-through of already-TPAP and non-TPAP-advertising devices, and the strict-encryption family exclusions. Full suite passes on both `net472` and `net10.0`.

## [1.6.0] - Value equality for device/child state models

- **Models**: All public device/child state model types in `Models.cs` (e.g. `LightState`, `FanState`, `AlarmState`, `ChildBatterySensorState`, `ChildContactSensorState`, `ChildMotionSensorState`, `ChildLockState`, `ChildThermostatState`, and similar snapshot types for sensors, buttons, and other state) are now declared as `sealed record` instead of `sealed class`. This gives them compiler-generated value-based `Equals`/`GetHashCode`/`==`/`!=` plus a descriptive `ToString()`, instead of default reference identity. Most of these state snapshots (for example `ChildDevice.TriggerLogState`, `FrostProtectionState`, and the various `KasaDevice`/`Modules.cs` state properties) are recomputed on every access rather than cached, so two consecutive reads previously always compared as unequal via `==`/`Equals`/`ReferenceEquals` even when the underlying device state had not changed. Consumers can now meaningfully compare snapshots (e.g. to detect whether a sensor or button's reported state changed between polls) without manually comparing every property.
- **Compatibility**: No properties, constructors, or public API signatures changed. All affected types remain `sealed` (no subclassing was possible before or after), and none of them are serialized directly by the library (only their originating raw JSON/DTOs are). This is a source- and binary-compatible change for all reasonable existing usage; no functional/behavioral changes to device communication were made in this release.
- **Tests**: Full existing test suite (132 tests) re-verified with no changes required.

## [1.5.0] - Full child device list retrieval for paginated hubs/strips

- **KasaDevice**: `get_child_device_list` responses from hubs/strips with more children than fit in a single page previously only returned the first page's children; the `sum`/`start_index` pagination fields the device reports were parsed but never acted on. `UpdateAsync` now detects when `sum` exceeds the number of children already received and issues additional `get_child_device_list` requests with increasing `start_index` until every child has been retrieved (stopping early if a page comes back empty), merging all pages into a single, complete child list before child-state enrichment runs. Devices with child lists that fit in one page are unaffected.
- **Tests**: Added `UpdateAsync_WithPaginatedChildDeviceList_FetchesRemainingPagesAndMergesFullList`, which simulates a hub reporting `sum: 3` with only one child in the initial response and verifies the remaining two pages are fetched and merged into `Children`.

## [1.4.0] - Shared-device cache now honors updateState on cache hits

- **Discover**: `GetOrConnectSharedAsync (..., updateState: true)` previously only loaded device state the very first time a shared instance was created for a given device identity (host/port); every subsequent cache hit silently returned that same instance with whatever state it captured at first connect, regardless of the caller's own `updateState` value. This was most visible on hubs/strips (e.g. KP303) whose `Children` list was empty at first connect and then stayed permanently empty for every later caller, even after child devices were paired. `GetOrConnectSharedAsync` now calls `KasaDevice.UpdateAsync` on a cache-hit shared instance before returning it when `updateState: true` is requested, so `LightState`, `IsOn`, `Children`, and other state are refreshed for every caller that asks for it, not just the first. Callers that pass `updateState: false` are unaffected and continue to receive the cached instance without an extra refresh.
- **Tests**: Added `GetOrConnectSharedAsync_CacheHitWithUpdateStateTrue_RefreshesSharedInstanceState`, which simulates a device whose reported `Children` list changes between the first and second `get_sysinfo` response, and verifies a `updateState: true` cache hit observes the refreshed data while still reusing the single underlying connection.

## [1.3.1] - Correct NuGet release notes

- **Packaging**: The `PackageReleaseNotes` shipped with the 1.3.0 NuGet package was stale (it described the 1.2.8 span-buffer work instead of 1.3.0's brightness-control changes). This release corrects `PackageReleaseNotes` to describe the 1.3.0 brightness-control changes; there are no code, behavior, or API changes beyond the 1.3.0 release.

## [1.3.0] - Brightness control keyed to advertised device capability

- **KasaDevice**: Brightness control is now gated on the capability a device actually advertises rather than on `DeviceType`. `SupportsLightControl` has been split into `SupportsBrightnessControl`, `SupportsColorControl`, and `SupportsColorTemperatureControl`; brightness is permitted whenever the device is a bulb or light strip, or when a SMART-protocol device negotiates the `brightness` component. `SetBrightnessAsync` consequently works on SMART dimmers and dimmable switches that were previously refused, including KS225, KS240, P135, S500D, S505D, S515D, and HS220 hardware revision 3.26. Color temperature and HSV remain restricted to bulbs and light strips, and legacy (IOT) dimmer on/off continues to use `system.set_relay_state` rather than the smartbulb lighting service.
- **KasaDevice**: Keying on the negotiated component rather than the device type also covers devices that dim but are never classified as `DeviceType.Dimmer`. KS240 reports `SMART.KASASWITCH` with a `child_device` component and classifies as `WallSwitch`; P135 reports `SMART.TAPOPLUG` and classifies as `Plug`. Both advertise `brightness` and both are now controllable. This classification behavior is unchanged and matches the reference implementation.
- **KasaDevice**: Light-state parameters are now validated individually, so an unsupported request reports the specific capability that is missing - `does not support brightness control`, `does not support color-temperature control`, or `does not support color control` - instead of the single `does not support light-state control` message used previously. The exception type is unchanged (`InvalidOperationException`); only the message text is more specific. A light-state call specifying no parameters at all now throws `ArgumentException` rather than composing an empty request.
- **Tests**: Added dimmer coverage in `DimmerRegressionTests` and `DimmerSupportTests`, covering the SMART brightness path, color-temperature and HSV refusal on dimmers, the KS240 and P135 classification cases, and the legacy dimmer on/off guard. Behavior was additionally validated against KS225 hardware (brightness accepted) and KS205 hardware (brightness refused locally, with no request sent, as it advertises no `brightness` component).

Legacy IOT dimmer brightness (the `smartlife.iot.dimmer` command surface and `dev_name`-based dimmer detection) remains unimplemented and is tracked in [#4](https://github.com/oznetmaster/KasaTapoClient/issues/4). The semantics of `SetBrightnessAsync (0)` are unchanged in this release and are under discussion in [#5](https://github.com/oznetmaster/KasaTapoClient/issues/5).

Contributed by [@xeys](https://github.com/xeys) in [#3](https://github.com/oznetmaster/KasaTapoClient/pull/3).

## [1.2.8] - KLAP/TPAP transport micro-optimizations

- **Internal**: `TpapTransport` now uses `BinaryPrimitives.WriteInt32BigEndian`/`ReadInt32BigEndian` instead of `BitConverter` combined with `Array.Reverse`, and uses span-based hex parsing in `HexToBytes` on net10.0 (with the `Substring`-based implementation retained for the net472 fallback).
- **Internal**: `KlapTransport`'s session-cookie header parsing (`CaptureSessionCookieFromHeader`) now compares cookie names as spans before allocating a string, only calling `Substring` once a matching cookie (`TP_SESSIONID`/`SESSIONID`) is found, avoiding a discarded string allocation per non-matching cookie in the header. The `"SESSIONID"` literal was also promoted to a `SESSIONID_COOKIE_NAME` constant. No public API or behavior changes.

## [1.2.7] - Span-based buffer handling in transport/crypto layer

- **Internal**: The KLAP, TPAP, HTTP AES, discovery, and legacy protocol transports now compose and slice byte buffers using `Span<byte>`/`ReadOnlySpan<byte>` instead of `Buffer.BlockCopy` and intermediate array allocations, reducing allocations on the request/response hot path. `Microsoft.Bcl.Memory` (already referenced) provides the required span APIs on net472. No public API or behavior changes.

## [1.2.6] - SMART energy monitoring v2 compatibility

- **KasaDevice**: Extended `UpdateEnergyUsageAsync` for SMART energy-monitoring v2 devices, including models that implement only a subset of the advertised methods. It now prefers `get_emeter_data`, falls back to `get_energy_usage`, and requests `get_current_power` only when required. Per-method `unknown method` and `invalid parameters` responses are treated as optional-method failures; other device errors continue to surface. This extends the SMART protocol fix released in 1.2.5 while preserving legacy and SMART v1 behavior.
- **Light features**: Added the reported 2500–9000 K color-temperature range for KL400L5 light strips with newer variable-color-temperature firmware.

## [1.2.5] - SMART energy monitoring support

- **KasaDevice**: Fixed `UpdateEnergyUsageAsync` for SMART/KLAP/TPAP devices that expose energy monitoring, such as the KP125M. The method previously always issued legacy `emeter` commands, which SMART devices do not support; it now uses the SMART `energy_monitoring` requests and parses their results through the existing SMART energy parser. The SMART path also negotiates component support when energy usage is requested before an initial `UpdateAsync`. Legacy-device energy monitoring behavior is unchanged.

## [1.2.4] - Configuration-mismatch-safe connect coalescing and shared reuse

- **Discover**: \`ConnectAsync\`'s in-flight connect de-duplication and \`GetOrConnectSharedAsync\`'s shared-instance reuse are both keyed only by device identity (host/port), which could previously coalesce or reuse a connection for a caller that supplied a materially different \`DeviceConfiguration\` (different credentials, timeout, or connection options) for the same host/port. Both now perform an internal, field-by-field configuration equivalence check before joining an in-flight connect or returning a cached shared instance; a mismatch falls through to an independent connect rather than silently sharing a connection built from a different caller's settings. \`DeviceConfiguration\` itself gains no public equality contract or API changes - the check is internal to \`Discover\`'s coalescing/reuse decision.

## [1.2.3] - Made shared device reuse explicit and opt-in

- **Discover**: `Discover.ConnectAsync` no longer caches connected devices across separate (non-concurrent) calls - this reverts the ambient, always-on persistent cache introduced in 1.2.2. `ConnectAsync` once again always returns an instance exclusively owned by the calling code (aside from in-flight concurrent-connect coalescing, which is unchanged), matching the standard connect/use/dispose ownership pattern and avoiding the risk of one caller's `Dispose()` unexpectedly affecting another, unrelated caller sharing the same instance.
- Added `Discover.GetOrConnectSharedAsync` as an explicit, opt-in alternative for call sites that are known to target the same device and want to reuse one connection instead of each dialing their own (useful for devices that reject or reset additional concurrent sessions). It returns a long-lived shared instance keyed by device identity (host/port), and transparently reconnects and re-caches if the previous shared instance was disposed. As before, there is no reference counting, so this method should only be used by coordinated call sites that understand the returned instance is shared.
- **KasaDevice**: `IsDisposed` (added in 1.2.2) is retained and now backs `GetOrConnectSharedAsync`'s disposal detection.

## [1.2.2] - Persistent shared device cache per host/port

- **Discover**: `Discover.ConnectAsync` now maintains a persistent, shared `KasaDevice` cache keyed by device identity (host/port). Previously, only concurrent in-flight connect calls for the same device were coalesced; the bookkeeping was discarded as soon as each connect completed, so separate (non-concurrent) calls each opened their own connection. Now, once a device has been connected, later `ConnectAsync` calls for that same identity - from any caller or module, at any time - reuse the same live instance instead of dialing a new connection.
- Concurrent connect de-duplication (introduced in 1.2.1) is unchanged and still applies when multiple callers race to connect to the same identity for the first time.
- **KasaDevice**: Added a public `IsDisposed` property. There is no reference counting on the shared cache entry - any caller may `Dispose()` the shared instance, and the next `ConnectAsync` call for that identity simply detects `IsDisposed` and transparently creates and caches a fresh replacement, mirroring the existing stale/idle-connection recovery model already used by `LegacyTransport`. No breaking API changes.

## [1.2.1] - Concurrent connect de-duplication

- **Discover**: `Discover.ConnectAsync` now de-duplicates concurrent connect calls for the same device identity (host/port). If a connect is already in flight, other concurrent callers no longer start a second, independent connection; instead they await the in-flight connect and receive the same `KasaDevice` instance once it completes. This prevents opening multiple simultaneous TCP connections to devices that only tolerate one or a small number of concurrent connections when callers race to (re)connect, and avoids silently orphaning/leaking the socket of whichever instance loses the race.
- Only the connect itself is de-duplicated; the returned `KasaDevice` is still owned and disposed entirely by the caller, exactly as before. No public API changes.

## [1.2.0] - Migrated to Newtonsoft.Json

- **JSON stack**: Replaced `System.Text.Json` with `Newtonsoft.Json` (13.0.3) across the core library, `KasaClient.Console`, tests, and benchmarks. This avoids the extra binding-redirect dependencies (`System.Buffers`, `System.Memory`, `System.Runtime.CompilerServices.Unsafe`, `System.Text.Encodings.Web`) that `System.Text.Json` requires on .NET Framework 4.7.2 and improves reliability on embedded Mono hosts.
- No public API or behavior changes. Validated with a full solution build across `net472`/`net10.0` and live device integration tests against real plugs, bulbs, light strips, and hubs.

## [1.1.11] - Discovery result fix, legacy-only discovery, transport reliability fixes

- **Discovery**: Fixed a bug where a device that replied to both the legacy (port 9999) and smart/Tapo (port 20002) discovery broadcasts would only have one of the two results kept; results are now kept per (host, transport kind), so both are retained. Added `Discover.DiscoverLegacyAsync` / `DiscoveryClient.DiscoverLegacyAsync` for broadcasting only the legacy discovery request.
- **LegacyTransport**: A reused idle connection that had already been closed by the device is now transparently reconnected and retried once, rather than surfacing as a failure. Read/write timeouts on .NET Framework now reliably drop the connection instead of leaving it in an unusable half-open state.
- **TpapTransport**: Disabled system proxy auto-detection (`WebRequest.Proxy`), which could stall the first connect to a device for up to the full startup timeout on embedded Mono/Linux hosts due to WPAD probing. Raised the per-host `ServicePoint` connection limit to prevent connection-pool starvation after repeated connect-timeout/abort cycles.

## [1.1.10] - TPAP SecureRandom startup performance fix

Fixed a startup performance issue in the TPAP transport where the first secure handshake per process could take several minutes on slower/embedded CPU hosts. The internal `SecureRandom` instance previously relied on BouncyCastle's default timing-based entropy seeding, which is CPU-speed dependent; it now seeds from the platform's cryptographically secure RNG (`CryptoApiRandomGenerator`) instead, eliminating the delay with no change to cryptographic security. Internal-only change, no public API impact. Added unit tests verifying fast, non-degenerate `SecureRandom` seeding.

## [1.1.4 – 1.1.9] - Cancellation/timeout patch series (KLAP, TPAP, Legacy)

A rapid same-day series of fixes completing consistent, prompt cancellation/timeout propagation across all three transports (KLAP, TPAP, Legacy), following the gaps identified starting in v1.1.3. See individual [GitHub releases](https://github.com/oznetmaster/KasaTapoClient/releases) for full per-version diagnostic detail.

- **v1.1.9** — `TpapTransport`'s internal per-request timeout could still be misclassified as a retryable transport reset (rather than propagating as `OperationCanceledException`) because `SocketsHttpHandler` throws the same `IOException`/`HttpRequestException` shape for both cases. Internal-timeout exceptions are now translated to `OperationCanceledException` before retry classification. Added `TpapTransportRetryPolicyTests`.
- **v1.1.8** — `TpapTransport`'s internal per-request timeout (separate from the caller's outer `CancellationToken`) could still trigger a full session reset and non-cancellable PAKE handshake instead of propagating promptly. `ShouldRetryLiveSession` no longer treats cancellation/timeout exceptions as retryable.
- **v1.1.7** — `KlapTransport` now wraps every non-net472 request with a per-call timeout derived from `DeviceConfiguration.Timeout`, matching `TpapTransport` and the net472 KLAP fallback.
- **v1.1.6** — `LegacyTransport` (KL130 and other legacy XOR/TCP devices) didn't honor connect timeout/cancellation promptly because .NET Framework's `TcpClient.ConnectAsync(string, int)` performs synchronous DNS resolution before the timeout race is set up. Connect is now dispatched to a background thread on net472; .NET 10+ uses the cancellation-aware overload directly.
- **v1.1.5** — `TpapTransport.SendAsync` and the keepalive path could absorb the caller's own external cancellation into a retry-with-full-handshake cycle. Both paths now check `cancellationToken.IsCancellationRequested` before retrying.
- **v1.1.4** — On .NET Framework 4.7.2, `KlapTransport`'s `HttpWebRequest` fallback's `Abort()` didn't always promptly unblock an in-flight request. The net472 fallback now races the request directly against the `CancellationToken` via `Task.WhenAny`.

## [1.1.3] - Missing HTTP timeouts and cancellation support

Fixed missing request timeouts and cancellation support in two HTTP code paths:
- **KlapTransport (.NET Framework 4.7.2 fallback)**: now sets `Timeout`/`ReadWriteTimeout` from `DeviceConfiguration.Timeout`, aborts on cancellation, and translates `WebException` into `OperationCanceledException` only when cancellation was actually requested.
- **TpapTransport keepalive**: periodic keepalive requests now use the same per-call timeout/cancellation wrapper used by other TPAP requests, instead of the shared `HttpClient`'s infinite timeout.

See README.md "Request Timeouts and Cancellation" section for details.

## [1.1.2] - LegacyTransport stale connection reuse fix

Fixed the persistent-connection reuse added for `LegacyTransport` in v1.1.1, which could fail once a remote device silently closed its end of the socket after inactivity. `LegacyTransport` now tracks time since last successful activity and proactively closes/reopens the connection if it has been idle for more than 10 seconds, rather than risking a failed write/read against a half-open socket.

## [1.1.1] - Connection pooling and reuse

- `HttpTokenTransport`, `KlapTransport`, and `TpapTransport` now each share a single static `HttpClient` per transport type, enabling connection pooling/reuse.
- `LegacyTransport` (raw XOR/TCP protocol on port 9999) now maintains a persistent per-instance TCP connection with reconnect-on-failure.
- Net effect: fewer sockets, fewer TCP/TLS handshakes, less port churn under concurrent/repeated device operations.
- Benchmark suites now also target `net472` in addition to `net10.0`.

## [1.1.0] - Smart light transition support

- Added smart light transition support aligned with python-kasa semantics for v1 and v2+ devices.
- Exposed negotiated smart module versions in console status output.
- Showed explicit transition enabled state, per-direction enabled state, and effective vs stored transition durations in the console.
- Updated package metadata and docs.

## [1.0.3] - Concurrency safety

Existing public APIs remain source-compatible. Concurrent operations against the same physical device may now complete sequentially by design.

## [1.0.2] - Discovery resilience

Improved UDP discovery resilience for Mono and Crestron-style runtime environments by continuing discovery after transient socket receive errors, increasing receive buffer sizing, and keeping diagnostics debug-only. Also stopped tracking the generated `KasaTapoClient.xml` documentation file so release XML assets come only from workflow build outputs.

[1.1.10]: https://github.com/oznetmaster/KasaTapoClient/compare/v1.1.9...v1.1.10
[1.1.4 – 1.1.9]: https://github.com/oznetmaster/KasaTapoClient/compare/v1.1.3...v1.1.9
[1.1.3]: https://github.com/oznetmaster/KasaTapoClient/compare/v1.1.2...v1.1.3
[1.1.2]: https://github.com/oznetmaster/KasaTapoClient/compare/v1.1.1...v1.1.2
[1.1.1]: https://github.com/oznetmaster/KasaTapoClient/compare/v1.1.0...v1.1.1
[1.1.0]: https://github.com/oznetmaster/KasaTapoClient/compare/v1.0.3...v1.1.0
[1.0.3]: https://github.com/oznetmaster/KasaTapoClient/compare/v1.0.2...v1.0.3
[1.0.2]: https://github.com/oznetmaster/KasaTapoClient/compare/v1.0.1...v1.0.2
