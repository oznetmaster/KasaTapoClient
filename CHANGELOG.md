# Changelog

All notable changes to this project are documented here. Each entry summarizes the corresponding [GitHub release](https://github.com/oznetmaster/KasaTapoClient/releases), which remains the authoritative, detailed record (including build assets) for that version. This file exists as a single, scannable index of the full version history.

The format is loosely based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project follows [Semantic Versioning](https://semver.org/).

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
