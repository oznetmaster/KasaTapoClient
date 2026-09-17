# Development and validation history

See the [product changelog](CHANGELOG.md) for shipped changes. This document preserves test, CI and build history. Dated development entries describe work at that time, not a published product version or completed acceptance. Version headings identify the release alongside which development work was recorded.

## Where changes belong

- Product changelog and product release notes: shipped behavior, API, compatibility, fixes and runtime dependencies. Mention validation briefly when it helps explain a fix.
- This history: test coverage, CI, build tooling, work on pending versions. Split mixed entries so the product effect remains easy to find.
- Testing and workflow guides: current setup and operating instructions.
- Test-only or documentation-only changes do not require a product release.

<!-- development-history -->

## Offline release workflow option - 2026-09-15 (no package release)

- Allow an explicit manual release when local hardware or the self-hosted runner is unavailable, with the reason and exact source recorded in the workflow summary.
- Keep hosted source validation mandatory and preserve all build, test and packaging steps. No runtime, API or package-version changes.

## CI validation - 2026-09-15 (no package release)

- Revalidate the current default-branch source after successful release workflows, including version commits created by GitHub Actions.
- Allow maintainers to configure exact-source, App-specific checks that must pass before publishing through `RELEASE_REQUIRED_CHECKS`; missing, failed or unconfirmed checks block the release.

## Test and development tooling - 2026-09-15 (no library release)

- Add dedicated pull-request and branch CI tests on net472 and .NET 10, excluding live tests and retaining per-runtime results. Test/CI-only change; no library behavior or package release.

- Correct unavailable API references in the README and documentation site. Add a direct Tapo plug connection example pinned to NuGet 1.8.1, explain `/app`, explicit protocol selection versus Auto discovery, and compile the example for both target frameworks in documentation CI (issue #6).
- Converted test fixtures from MSTest to NUnit 4.6.1 with NUnit3TestAdapter for Visual Studio and `dotnet test`.
- Added stable live-device selectors, per-run discovery caching, original-state restoration and a read-only hub temperature test. Live settings remain private and live tests are opt-in.
- No new library or NuGet release accompanies these test changes.

## [1.8.0] - Report-interval enablement for hub child sensors

- **Tests**: Added `ReportIntervalCommandTests` (2 tests) pinning the `control_child` envelope shape for the interval setter and verifying the report-mode read method omits `params`. Full suite passes on both `net472` and `net10.0` (83 tests each).

## [1.7.0] - Double-click enablement for button child devices

- **Tests**: Added `DoubleClickCommandTests` (3 tests) pinning the `control_child` envelope shape for enable and disable, and verifying that the read method omits the `params` member. Full suite passes on both `net472` and `net10.0` (81 tests each).

## [1.6.1] - TPAP hub authentication and discovery preference fixes

- **Tests**: Added `DiscoverTpapPreferenceTests` (12 tests) covering TPAP promotion via both `tpap_preferred` and TPAP metadata, endpoint derivation from the advertised TLS mode and port, preservation of the device family and login version, pass-through of already-TPAP and non-TPAP-advertising devices, and the strict-encryption family exclusions. Full suite passes on both `net472` and `net10.0`.

## [1.6.0] - Value equality for device/child state models

- **Tests**: Full existing test suite (132 tests) re-verified with no changes required.

## [1.5.0] - Full child device list retrieval for paginated hubs/strips

- **Tests**: Added `UpdateAsync_WithPaginatedChildDeviceList_FetchesRemainingPagesAndMergesFullList`, which simulates a hub reporting `sum: 3` with only one child in the initial response and verifies the remaining two pages are fetched and merged into `Children`.

## [1.4.0] - Shared-device cache now honors updateState on cache hits

- **Tests**: Added `GetOrConnectSharedAsync_CacheHitWithUpdateStateTrue_RefreshesSharedInstanceState`, which simulates a device whose reported `Children` list changes between the first and second `get_sysinfo` response, and verifies a `updateState: true` cache hit observes the refreshed data while still reusing the single underlying connection.

## [1.3.0] - Brightness control keyed to advertised device capability

- **Tests**: Added dimmer coverage in `DimmerRegressionTests` and `DimmerSupportTests`, covering the SMART brightness path, color-temperature and HSV refusal on dimmers, the KS240 and P135 classification cases, and the legacy dimmer on/off guard. Behavior was additionally validated against KS225 hardware (brightness accepted) and KS205 hardware (brightness refused locally, with no request sent, as it advertises no `brightness` component).

[1.1.10]: https://github.com/oznetmaster/KasaTapoClient/compare/v1.1.9...v1.1.10
[1.1.4 – 1.1.9]: https://github.com/oznetmaster/KasaTapoClient/compare/v1.1.3...v1.1.9
[1.1.3]: https://github.com/oznetmaster/KasaTapoClient/compare/v1.1.2...v1.1.3
[1.1.2]: https://github.com/oznetmaster/KasaTapoClient/compare/v1.1.1...v1.1.2
[1.1.1]: https://github.com/oznetmaster/KasaTapoClient/compare/v1.1.0...v1.1.1
[1.1.0]: https://github.com/oznetmaster/KasaTapoClient/compare/v1.0.3...v1.1.0
[1.0.3]: https://github.com/oznetmaster/KasaTapoClient/compare/v1.0.2...v1.0.3
[1.0.2]: https://github.com/oznetmaster/KasaTapoClient/compare/v1.0.1...v1.0.2