# KasaTapoClient 2.1.0

This minor release adds optional lighting configuration and fixes KLAP authentication for updated legacy devices. Existing public APIs remain compatible.

## Added

- **Physical strip length:** `LightStripSegment` provides typed state and `SetSegmentCountAsync` for devices advertising the `segment` component. Configure 0–50 segments of 10 cm per run. This is separate from lighting-effect segment arrays and does not configure individual segment colors.
- **Fade between settings:** `LightTransition.ChangeTransitionEnabled` and `SetChangeTransitionEnabledAsync` control fading between brightness or color settings while a light is on. Existing turn-on and turn-off durations are preserved. A nullable value distinguishes unavailable state from a supported but disabled setting.
- **Console support:** `host <address> light segments <0-50>` and `host <address> light transition-change <on|off>`, with corresponding status output. Existing command abbreviations retain their meaning.

Both new setters check reported capabilities, honor cancellation, report device rejection and refresh typed state after successful writes.

## Fixed

- Legacy IOT.KLAP devices advertising login version 2 or later now use KLAP v2 authentication. Legacy devices with no login version or an older version retain v1 authentication; SMART-family KLAP behavior is unchanged.

## Compatibility

- No existing public signatures were removed or changed. No new raw JSON API was introduced.
- Targets remain .NET 10 and .NET Framework 4.7.2. Runtime dependencies are unchanged.
- Existing thermostat `States` lists continue to preserve hold, summer-mode and other TRV flags.

## Validation

- All 169 offline tests pass on each supported runtime.
- Microsoft APICompat reports no breaking public API changes against the released 2.0.1 package on either target, including parameter-name and attribute checks.
- Live fade-on-change write, readback and restoration passed on L900 and L530 devices under both runtimes. Console builds passed for both targets.
- Legacy IOT.KLAP v2 and physical strip-length configuration have deterministic offline coverage; matching hardware was unavailable for live validation.
