# Changelog

All notable changes to this project are documented here. Each entry summarizes the corresponding [GitHub release](https://github.com/oznetmaster/KasaTapoClient/releases), which remains the authoritative, detailed record (including build assets) for that version. This file exists as a single, scannable index of the full version history.

The format is loosely based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project follows [Semantic Versioning](https://semver.org/).

## [1.1.10] - TPAP SecureRandom startup performance fix

Fixed a startup performance issue in the TPAP transport where the first secure handshake per process could take several minutes on slower/embedded CPU hosts. The internal `SecureRandom` instance previously relied on BouncyCastle's default timing-based entropy seeding, which is CPU-speed dependent; it now seeds from the platform's cryptographically secure RNG (`CryptoApiRandomGenerator`) instead, eliminating the delay with no change to cryptographic security. Internal-only change, no public API impact. Added unit tests verifying fast, non-degenerate `SecureRandom` seeding.

## [1.1.9] - TpapTransport internal-timeout misclassification fix

Fixed `TpapTransport` still absorbing an internal per-request timeout after the 1.1.8 fix. `ShouldRetryLiveSession` no longer treated `TaskCanceledException`/`OperationCanceledException` as retryable, but still treated `IOException`/`HttpRequestException` messages containing "operation has been aborted" as retryable. When `SendOnceAsync`'s internal per-request timeout elapsed, `SocketsHttpHandler` threw that exact same exception shape rather than an `OperationCanceledException`, so the internal timeout was still misclassified as a retryable transport reset, triggering a full non-cancellable PAKE handshake instead of propagating the timeout. `SendOnceAsync`, `PostLoginAsync`, and `SendKeepAliveCoreAsync` now translate internal-timeout exceptions into `OperationCanceledException` before retry classification. Added `TpapTransportRetryPolicyTests` covering retry classification and timeout-to-cancellation translation.

## [1.1.8] - TpapTransport caller cancellation/timeout fix

Fixed `TpapTransport` still absorbing a caller's cancellation/timeout. `SendOnceAsync` races each request against its own internal per-request timeout, separate from the caller's outer token; when that internal timeout fired first, the retry path still triggered a full session reset and a brand-new PAKE handshake (whose PBKDF2/EC math performs no cancellation checks), potentially running far longer than the caller's configured deadline. `TaskCanceledException`/`OperationCanceledException` (and the related `HttpRequestException` message) are no longer treated as retryable by `ShouldRetryLiveSession`; only genuine connection-reset/abort signals and explicit TPAP protocol session-error codes trigger a retry.

## [1.1.7] - KlapTransport timeout coverage

`KlapTransport` now wraps every non-net472 request with a per-call timeout derived from `DeviceConfiguration.Timeout`, matching behavior already present in `TpapTransport` and the net472 KLAP fallback. Completes timeout coverage across all three transports (KLAP, TPAP, Legacy) on every supported target framework.

## [1.1.6] - LegacyTransport connect timeout/cancellation fix

Fixed `LegacyTransport` (used by KL130 and other legacy XOR/TCP-protocol devices) not honoring a connect timeout/cancellation promptly. On .NET Framework, `TcpClient.ConnectAsync(string, int)` performs synchronous DNS resolution on the calling thread before the timeout race is even set up, so slow/hanging DNS resolution bypassed the configured timeout entirely. The connect call is now dispatched to a background thread on net472, while .NET 10+ uses the cancellation-aware `TcpClient.ConnectAsync` overload directly. Completes cancellation/timeout coverage across KLAP, TPAP, and Legacy transports.

## [1.1.5] - TpapTransport caller cancellation fix

Fixed `TpapTransport` swallowing the caller's own cancellation request. `SendAsync` and the keepalive path caught `TaskCanceledException` as a generically "retryable" live-session condition and unconditionally reset the session and retried — including a full handshake — even when the exception was caused by the caller's own external `CancellationToken`. Both `SendAsync` and `SendKeepAliveCoreAsync` now check `cancellationToken.IsCancellationRequested` before retrying, so genuine external cancellation propagates immediately.

## [1.1.4] - KlapTransport cancellation gap fix

Fixed a cancellation gap introduced in v1.1.3. On .NET Framework 4.7.2, `KlapTransport`'s `HttpWebRequest`-based fallback registered `Abort()` against the caller's `CancellationToken`, but `Abort()` does not always promptly unblock an in-flight request. The net472 fallback now races the underlying request calls directly against the `CancellationToken` (via `Task.WhenAny`) and throws `OperationCanceledException` immediately once the token fires.

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
[1.1.9]: https://github.com/oznetmaster/KasaTapoClient/compare/v1.1.8...v1.1.9
[1.1.8]: https://github.com/oznetmaster/KasaTapoClient/compare/v1.1.7...v1.1.8
[1.1.7]: https://github.com/oznetmaster/KasaTapoClient/compare/v1.1.6...v1.1.7
[1.1.6]: https://github.com/oznetmaster/KasaTapoClient/compare/v1.1.5...v1.1.6
[1.1.5]: https://github.com/oznetmaster/KasaTapoClient/compare/v1.1.4...v1.1.5
[1.1.4]: https://github.com/oznetmaster/KasaTapoClient/compare/v1.1.3...v1.1.4
[1.1.3]: https://github.com/oznetmaster/KasaTapoClient/compare/v1.1.2...v1.1.3
[1.1.2]: https://github.com/oznetmaster/KasaTapoClient/compare/v1.1.1...v1.1.2
[1.1.1]: https://github.com/oznetmaster/KasaTapoClient/compare/v1.1.0...v1.1.1
[1.1.0]: https://github.com/oznetmaster/KasaTapoClient/compare/v1.0.3...v1.1.0
[1.0.3]: https://github.com/oznetmaster/KasaTapoClient/compare/v1.0.2...v1.0.3
[1.0.2]: https://github.com/oznetmaster/KasaTapoClient/compare/v1.0.1...v1.0.2
