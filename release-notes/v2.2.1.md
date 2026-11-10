# KasaTapoClient 2.2.1

This patch corrects the audited reliability issues while retaining the existing public API and supported frameworks.

- Device disposal closes admission, cancels queued work and prevents late state publication. Owned transports are released after admitted work exits. Cancellable connection initiators use independently owned attempts unless they can join an existing uncancellable attempt; cancelling one caller cannot cancel another connection. Failed connection initialization releases its transport.

## Validation

- Offline workstation tests passed in Debug and Release on .NET 10 and .NET Framework 4.7.2: Kasa net10.0: 175; Kasa net472: 175. No failures or skips.
- 11 selected tests passed in the shared 374-case processor run on Mono 6.12.0.107 (32-bit), with no failures or skips. The merged host's resolved dependency graph was tested; these are not eight independent release-package tests.
- Fake transports and credentials exercised ordering, cancellation, shutdown and failure recovery. No new household-device or cloud control was performed. Pairing storage is unchanged.
- Strict XML, formatting and style checks passed. Package and companion-app preparation is verified separately by the shared release pipeline.

See [the changelog](https://github.com/oznetmaster/KasaTapoClient/blob/v2.2.1/CHANGELOG.md) and [validation provenance](https://github.com/oznetmaster/KasaTapoClient/blob/v2.2.1/release-validation/v2.2.1.json).
