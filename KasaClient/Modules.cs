// Copyright © 2026 Neil Colvin.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// Adapted from python-kasa (https://github.com/python-kasa/python-kasa)
// Original work Copyright (c) python-kasa contributors, MIT License

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using KasaTapoClient.Internal;

namespace KasaTapoClient;

/// <summary>
/// Provides module-style access to system information for a device.
/// </summary>
public sealed class SystemModule
	{
	private readonly KasaDevice _device;

	internal SystemModule (KasaDevice device) => _device = device;

	/// <summary>
	/// Gets the latest raw system information payload.
	/// </summary>
	public DeviceSystemInfo? Info => _device.SystemInfo;

	/// <summary>
	/// Gets the latest device alias.
	/// </summary>
	public string Alias => _device.Alias;

	/// <summary>
	/// Gets the latest device model.
	/// </summary>
	public string? Model => _device.SystemInfo?.Model;

	/// <summary>
	/// Gets the latest hardware version.
	/// </summary>
	public string? HardwareVersion => _device.SystemInfo?.HardwareVersion;

	/// <summary>
	/// Gets the latest software version.
	/// </summary>
	public string? SoftwareVersion => _device.SystemInfo?.SoftwareVersion;

	/// <summary>
	/// Gets the latest device MAC address.
	/// </summary>
	public string? MacAddress => _device.SystemInfo?.MacAddress;

	/// <summary>
	/// Gets the reported device RSSI in dBm.
	/// </summary>
	public int? Rssi => _device.Rssi;

	/// <summary>
	/// Gets the reported device on-time duration.
	/// </summary>
	public TimeSpan? OnTime => _device.SystemInfo?.OnTime;

	/// <summary>
	/// Gets the derived timestamp for when the device was turned on.
	/// </summary>
	public DateTimeOffset? OnSince => _device.OnSince;

	/// <summary>
	/// Refreshes system information from the device.
	/// </summary>
	/// <param name="cancellationToken">The cancellation token for the operation.</param>
	/// <returns>A task that completes when the system information is refreshed.</returns>
	public Task UpdateAsync (CancellationToken cancellationToken = default) => _device.UpdateAsync (cancellationToken);
	}

/// <summary>
/// Provides child setup and pairing operations for smart hub devices.
/// </summary>
public sealed class ChildSetupModule
	{
	private readonly KasaDevice _device;

	internal ChildSetupModule (KasaDevice device) => _device = device;

	/// <summary>
	/// Gets the categories supported for child pairing, when reported.
	/// </summary>
	public IReadOnlyList<string> SupportedCategories => _device.GetSupportedChildSetupCategories ();

	/// <summary>
	/// Starts a child-device scan, waits for the specified timeout, and returns detected devices.
	/// </summary>
	/// <param name="timeoutSeconds">The number of seconds to wait before reading the hub's detected child-device list.</param>
	/// <param name="cancellationToken">The cancellation token for the operation.</param>
	/// <returns>The detected child devices and supported categories reported by the hub.</returns>
	/// <exception cref="InvalidOperationException">Thrown when the parent device does not support smart hub child setup operations.</exception>
	public Task<ChildSetupScanResult> ScanAsync (int timeoutSeconds = 10, CancellationToken cancellationToken = default) =>
		_device.ScanForChildDevicesAsync (timeoutSeconds, cancellationToken);

	/// <summary>
	/// Gets the current detected child devices from the hub scan state.
	/// </summary>
	/// <param name="cancellationToken">The cancellation token for the operation.</param>
	/// <returns>The detected child devices and supported categories reported by the hub.</returns>
	/// <exception cref="InvalidOperationException">Thrown when the parent device does not support smart hub child setup operations.</exception>
	public Task<ChildSetupScanResult> GetDetectedDevicesAsync (CancellationToken cancellationToken = default) =>
		_device.GetScannedChildDevicesAsync (cancellationToken);

	/// <summary>
	/// Pairs the specified detected child devices.
	/// </summary>
	/// <param name="devices">The detected child devices to pair with the hub.</param>
	/// <param name="cancellationToken">The cancellation token for the operation.</param>
	/// <returns>The subset of requested devices that the hub confirmed after refresh.</returns>
	/// <exception cref="InvalidOperationException">Thrown when the parent device does not support smart hub child setup operations.</exception>
	public Task<IReadOnlyList<DetectedChildDevice>> PairAsync (IReadOnlyList<DetectedChildDevice> devices, CancellationToken cancellationToken = default) =>
		_device.PairScannedChildDevicesAsync (devices, cancellationToken);

	/// <summary>
	/// Removes a child device from the hub.
	/// </summary>
	/// <param name="childDeviceId">The child device identifier to remove from the hub.</param>
	/// <param name="cancellationToken">The cancellation token for the operation.</param>
	/// <returns>A task that completes when the child device has been removed and the parent state refreshed.</returns>
	/// <exception cref="InvalidOperationException">Thrown when the parent device does not support smart hub child setup operations.</exception>
	public Task UnpairAsync (string childDeviceId, CancellationToken cancellationToken = default) =>
		_device.UnpairChildDeviceAsync (childDeviceId, cancellationToken);
	}

/// <summary>
/// Provides module-style access to firmware metadata.
/// </summary>
public sealed class FirmwareModule
	{
	private readonly KasaDevice _device;

	internal FirmwareModule (KasaDevice device) => _device = device;

	/// <summary>
	/// Gets the latest normalized firmware state.
	/// </summary>
	public FirmwareState? State => _device.FirmwareState;

	/// <summary>
	/// Gets a value indicating whether firmware metadata is currently available.
	/// </summary>
	public bool IsAvailable => _device.SupportsFirmwareModule;

	/// <summary>
	/// Gets the current firmware version.
	/// </summary>
	public string? CurrentVersion => _device.FirmwareState?.CurrentFirmwareVersion;

	/// <summary>
	/// Gets the current hardware version.
	/// </summary>
	public string? HardwareVersion => _device.FirmwareState?.CurrentHardwareVersion;

	/// <summary>
	/// Gets whether automatic firmware updates are enabled.
	/// </summary>
	public bool? AutoUpdateEnabled => _device.FirmwareState?.AutoUpdateEnabled;

	/// <summary>
	/// Gets the available firmware version, when known.
	/// </summary>
	public string? AvailableVersion => _device.FirmwareState?.AvailableFirmwareVersion;

	/// <summary>
	/// Gets whether a newer firmware version is available, when known.
	/// </summary>
	public bool? UpdateAvailable => _device.FirmwareState?.UpdateAvailable;

	/// <summary>
	/// Refreshes firmware-related state.
	/// </summary>
	/// <param name="cancellationToken">The cancellation token for the operation.</param>
	/// <returns>A task that completes when the device state has been refreshed.</returns>
	public Task UpdateAsync (CancellationToken cancellationToken = default) => _device.UpdateAsync (cancellationToken);
	}

/// <summary>
/// Provides module-style access to cloud connectivity information.
/// </summary>
public sealed class CloudModule
	{
	private readonly KasaDevice _device;

	internal CloudModule (KasaDevice device) => _device = device;

	/// <summary>
	/// Gets the latest normalized cloud state.
	/// </summary>
	public CloudConnectionState? State => _device.CloudState;

	/// <summary>
	/// Gets a value indicating whether cloud metadata is currently available.
	/// </summary>
	public bool IsAvailable => _device.SupportsCloudConnection;

	/// <summary>
	/// Gets whether the device reports an active cloud connection.
	/// </summary>
	public bool? IsConnected => _device.CloudState?.IsConnected;

	/// <summary>
	/// Gets whether the device reports being provisioned to a cloud account.
	/// </summary>
	public bool? IsProvisioned => _device.CloudState?.IsProvisioned;

	/// <summary>
	/// Gets the configured cloud server, when reported.
	/// </summary>
	public string? Server => _device.CloudState?.Server;

	/// <summary>
	/// Gets the cloud account user name, when reported.
	/// </summary>
	public string? UserName => _device.CloudState?.UserName;

	/// <summary>
	/// Refreshes cloud-related state.
	/// </summary>
	/// <param name="cancellationToken">The cancellation token for the operation.</param>
	/// <returns>A task that completes when the device state has been refreshed.</returns>
	public Task UpdateAsync (CancellationToken cancellationToken = default) => _device.UpdateAsync (cancellationToken);
	}

/// <summary>
/// Provides module-style access to device-local time information.
/// </summary>
public sealed class TimeModule
	{
	private readonly KasaDevice _device;

	internal TimeModule (KasaDevice device) => _device = device;

	/// <summary>
	/// Gets the latest normalized device time state.
	/// </summary>
	public DeviceTimeState? State => _device.TimeState;

	/// <summary>
	/// Gets a value indicating whether device time metadata is currently available.
	/// </summary>
	public bool IsAvailable => _device.SupportsDeviceTime;

	/// <summary>
	/// Gets the latest device-local time.
	/// </summary>
	public DateTime? LocalTime => _device.TimeState?.LocalTime;

	/// <summary>
	/// Gets the reported timezone region.
	/// </summary>
	public string? Region => _device.TimeState?.Region;

	/// <summary>
	/// Gets the reported UTC offset in minutes.
	/// </summary>
	public int? TimeDifferenceMinutes => _device.TimeState?.TimeDifferenceMinutes;

	/// <summary>
	/// Refreshes time-related state.
	/// </summary>
	/// <param name="cancellationToken">The cancellation token for the operation.</param>
	/// <returns>A task that completes when the device state has been refreshed.</returns>
	public Task UpdateAsync (CancellationToken cancellationToken = default) => _device.UpdateAsync (cancellationToken);
	}

/// <summary>
/// Provides module-style access to Matter setup information.
/// </summary>
public sealed class MatterModule
	{
	private readonly KasaDevice _device;

	internal MatterModule (KasaDevice device) => _device = device;

	/// <summary>
	/// Gets the latest Matter setup information.
	/// </summary>
	public MatterSetupInfo? Info => _device.MatterSetup;

	/// <summary>
	/// Gets a value indicating whether Matter setup information is currently available.
	/// </summary>
	public bool IsAvailable => _device.SupportsMatterSetup;

	/// <summary>
	/// Gets the Matter setup code.
	/// </summary>
	public string? SetupCode => _device.MatterSetup?.SetupCode;

	/// <summary>
	/// Gets the Matter setup payload.
	/// </summary>
	public string? SetupPayload => _device.MatterSetup?.SetupPayload;

	/// <summary>
	/// Refreshes Matter-related state.
	/// </summary>
	/// <param name="cancellationToken">The cancellation token for the operation.</param>
	/// <returns>A task that completes when the device state has been refreshed.</returns>
	public Task UpdateAsync (CancellationToken cancellationToken = default) => _device.UpdateAsync (cancellationToken);
	}

/// <summary>
/// Provides module-style access to HomeKit setup information.
/// </summary>
public sealed class HomeKitModule
	{
	private readonly KasaDevice _device;

	internal HomeKitModule (KasaDevice device) => _device = device;

	/// <summary>
	/// Gets the latest HomeKit setup information.
	/// </summary>
	public HomeKitSetupInfo? Info => _device.HomeKitSetup;

	/// <summary>
	/// Gets a value indicating whether HomeKit setup information is currently available.
	/// </summary>
	public bool IsAvailable => _device.SupportsHomeKitSetup;

	/// <summary>
	/// Gets the HomeKit setup code.
	/// </summary>
	public string? SetupCode => _device.HomeKitSetup?.SetupCode;

	/// <summary>
	/// Gets the HomeKit setup payload.
	/// </summary>
	public string? SetupPayload => _device.HomeKitSetup?.SetupPayload;

	/// <summary>
	/// Refreshes HomeKit-related state.
	/// </summary>
	/// <param name="cancellationToken">The cancellation token for the operation.</param>
	/// <returns>A task that completes when the device state has been refreshed.</returns>
	public Task UpdateAsync (CancellationToken cancellationToken = default) => _device.UpdateAsync (cancellationToken);
	}

/// <summary>
/// Provides module-style access to auto-off configuration and timer state.
/// </summary>
public sealed class AutoOffModule
	{
	private readonly KasaDevice _device;

	internal AutoOffModule (KasaDevice device) => _device = device;

	/// <summary>
	/// Gets the latest normalized auto-off state.
	/// </summary>
	public AutoOffState? State => _device.AutoOffState;

	/// <summary>
	/// Gets a value indicating whether auto-off state is currently available.
	/// </summary>
	public bool IsAvailable => _device.SupportsAutoOff;

	/// <summary>
	/// Gets whether auto-off is enabled.
	/// </summary>
	public bool? Enabled => _device.AutoOffState?.Enabled;

	/// <summary>
	/// Gets the configured auto-off delay in minutes.
	/// </summary>
	public int? DelayMinutes => _device.AutoOffState?.DelayMinutes;

	/// <summary>
	/// Gets whether an auto-off timer is currently active.
	/// </summary>
	public bool? TimerActive => _device.AutoOffState?.TimerActive;

	/// <summary>
	/// Gets the expected automatic shutoff time.
	/// </summary>
	public DateTime? AutoOffAt => _device.AutoOffState?.AutoOffAt;

	/// <summary>
	/// Refreshes auto-off-related state.
	/// </summary>
	/// <param name="cancellationToken">The cancellation token for the operation.</param>
	/// <returns>A task that completes when the device state has been refreshed.</returns>
	public Task UpdateAsync (CancellationToken cancellationToken = default) => _device.UpdateAsync (cancellationToken);
	}

/// <summary>
/// Provides module-style access to LED state information.
/// </summary>
public sealed class LedModule
	{
	private readonly KasaDevice _device;

	internal LedModule (KasaDevice device) => _device = device;

	/// <summary>
	/// Gets the latest normalized LED state.
	/// </summary>
	public LedState? State => _device.LedState;

	/// <summary>
	/// Gets a value indicating whether LED state is currently available.
	/// </summary>
	public bool IsAvailable => _device.SupportsLedControl;

	/// <summary>
	/// Gets whether the device LED is enabled.
	/// </summary>
	public bool? Enabled => _device.LedState?.Enabled;

	/// <summary>
	/// Gets the LED mode string.
	/// </summary>
	public string? Mode => _device.LedState?.Mode;

	/// <summary>
	/// Gets the LED night-mode settings, when reported.
	/// </summary>
	public LedNightModeSettings? NightModeSettings => _device.LedState?.NightModeSettings;

	/// <summary>
	/// Refreshes LED-related state.
	/// </summary>
	/// <param name="cancellationToken">The cancellation token for the operation.</param>
	/// <returns>A task that completes when the device state has been refreshed.</returns>
	public Task UpdateAsync (CancellationToken cancellationToken = default) => _device.UpdateAsync (cancellationToken);
	}

/// <summary>
/// Provides module-style access to child-lock state.
/// </summary>
public sealed class ChildLockModule
	{
	private readonly KasaDevice _device;

	internal ChildLockModule (KasaDevice device) => _device = device;

	/// <summary>
	/// Gets the latest normalized child-lock state.
	/// </summary>
	public ChildLockState? State => _device.ChildLockState;

	/// <summary>
	/// Gets a value indicating whether child-lock state is currently available.
	/// </summary>
	public bool IsAvailable => _device.SupportsChildLock;

	/// <summary>
	/// Gets whether child lock is enabled.
	/// </summary>
	public bool? Enabled => _device.ChildLockState?.Enabled;

	/// <summary>
	/// Refreshes child-lock-related state.
	/// </summary>
	/// <param name="cancellationToken">The cancellation token for the operation.</param>
	/// <returns>A task that completes when the device state has been refreshed.</returns>
	public Task UpdateAsync (CancellationToken cancellationToken = default) => _device.UpdateAsync (cancellationToken);
	}

/// <summary>
/// Provides module-style access to energy telemetry for a device.
/// </summary>
public sealed class EnergyModule
	{
	private readonly KasaDevice _device;

	internal EnergyModule (KasaDevice device) => _device = device;

	/// <summary>
	/// Gets the latest energy usage snapshot.
	/// </summary>
	public EnergyUsage? Usage => _device.EnergyUsage;

	/// <summary>
	/// Gets a value indicating whether energy telemetry is currently available.
	/// </summary>
	public bool IsAvailable => _device.EnergyUsage is not null;

	/// <summary>
	/// Gets the latest current power draw in watts.
	/// </summary>
	public double? CurrentPowerWatts => _device.EnergyUsage?.CurrentPowerWatts;

	/// <summary>
	/// Gets the latest line voltage in volts.
	/// </summary>
	public double? VoltageVolts => _device.EnergyUsage?.VoltageVolts;

	/// <summary>
	/// Gets the latest current draw in amps.
	/// </summary>
	public double? CurrentAmps => _device.EnergyUsage?.CurrentAmps;

	/// <summary>
	/// Gets the latest total measured energy in kilowatt-hours.
	/// </summary>
	public double? TotalKilowattHours => _device.EnergyUsage?.TotalKilowattHours;

	/// <summary>
	/// Gets today's measured energy in kilowatt-hours.
	/// </summary>
	public double? TodayKilowattHours => _device.EnergyUsage?.TodayKilowattHours;

	/// <summary>
	/// Gets this month's measured energy in kilowatt-hours.
	/// </summary>
	public double? MonthKilowattHours => _device.EnergyUsage?.MonthKilowattHours;

	/// <summary>
	/// Refreshes energy telemetry from the device.
	/// </summary>
	/// <param name="cancellationToken">The cancellation token for the operation.</param>
	/// <returns><see langword="true" /> when the device returned energy telemetry; otherwise, <see langword="false" />.</returns>
	public Task<bool> UpdateAsync (CancellationToken cancellationToken = default) => _device.UpdateEnergyUsageAsync (cancellationToken);
	}

/// <summary>
/// Provides module-style access to usage telemetry for a device.
/// </summary>
public sealed class UsageModule
	{
	private readonly KasaDevice _device;

	internal UsageModule (KasaDevice device) => _device = device;

	/// <summary>
	/// Gets the latest usage snapshot.
	/// </summary>
	public EnergyUsage? State => _device.EnergyUsage;

	/// <summary>
	/// Gets a value indicating whether usage telemetry is currently available.
	/// </summary>
	public bool IsAvailable => _device.EnergyUsage is not null;

	/// <summary>
	/// Gets the latest total measured energy in kilowatt-hours.
	/// </summary>
	public double? TotalKilowattHours => _device.EnergyUsage?.TotalKilowattHours;

	/// <summary>
	/// Gets today's measured energy in kilowatt-hours.
	/// </summary>
	public double? TodayKilowattHours => _device.EnergyUsage?.TodayKilowattHours;

	/// <summary>
	/// Gets this month's measured energy in kilowatt-hours.
	/// </summary>
	public double? MonthKilowattHours => _device.EnergyUsage?.MonthKilowattHours;

	/// <summary>
	/// Gets the latest current power draw in watts.
	/// </summary>
	public double? CurrentPowerWatts => _device.EnergyUsage?.CurrentPowerWatts;

	/// <summary>
	/// Refreshes usage telemetry from the device.
	/// </summary>
	/// <param name="cancellationToken">The cancellation token for the operation.</param>
	/// <returns><see langword="true" /> when the device returned usage telemetry; otherwise, <see langword="false" />.</returns>
	public Task<bool> UpdateAsync (CancellationToken cancellationToken = default) => _device.UpdateEnergyUsageAsync (cancellationToken);
	}

/// <summary>
/// Provides module-style access to light features for bulbs and light strips.
/// </summary>
public sealed class LightModule
	{
	private readonly KasaDevice _device;

	internal LightModule (KasaDevice device) => _device = device;

	/// <summary>
	/// Gets the latest normalized light state.
	/// </summary>
	public LightState? State => _device.LightState;

	/// <summary>
	/// Gets the latest normalized HSV color.
	/// </summary>
	public HsvColor? Hsv => _device.LightState?.Hsv;

	/// <summary>
	/// Gets the latest normalized light effect state.
	/// </summary>
	public LightEffectState? Effect => _device.LightEffect;

	/// <summary>
	/// Gets the available light effects reported by the device.
	/// </summary>
	public IReadOnlyList<LightEffectDefinition> AvailableEffects => _device.AvailableLightEffects;

	/// <summary>
	/// Gets a value indicating whether the light reports effect capability.
	/// </summary>
	public bool SupportsEffects => _device.SupportsLightEffects;

	/// <summary>
	/// Gets a value indicating whether light control is available.
	/// </summary>
	public bool IsAvailable => _device.LightState is not null || _device.DeviceType is DeviceType.Bulb or DeviceType.LightStrip;

	/// <summary>
	/// Refreshes light-related device state.
	/// </summary>
	/// <param name="cancellationToken">The cancellation token for the operation.</param>
	/// <returns>A task that completes when the device state has been refreshed.</returns>
	public Task UpdateAsync (CancellationToken cancellationToken = default) => _device.UpdateAsync (cancellationToken);

	/// <summary>
	/// Turns the light on and refreshes state.
	/// </summary>
	/// <param name="cancellationToken">The cancellation token for the operation.</param>
	/// <returns>A task that completes when the light state has been refreshed.</returns>
	public Task TurnOnAsync (CancellationToken cancellationToken = default) => _device.TurnLightOnAsync (cancellationToken);

	/// <summary>
	/// Turns the light on and refreshes state.
	/// </summary>
	/// <param name="transitionMilliseconds">The optional transition duration, in milliseconds, for supported legacy light devices.</param>
	/// <param name="cancellationToken">The cancellation token for the operation.</param>
	/// <returns>A task that completes when the light state has been refreshed.</returns>
	public Task TurnOnAsync (int transitionMilliseconds, CancellationToken cancellationToken = default) =>
		_device.TurnLightOnAsync (transitionMilliseconds, cancellationToken);

	/// <summary>
	/// Turns the light off and refreshes state.
	/// </summary>
	/// <param name="cancellationToken">The cancellation token for the operation.</param>
	/// <returns>A task that completes when the light state has been refreshed.</returns>
	public Task TurnOffAsync (CancellationToken cancellationToken = default) => _device.TurnLightOffAsync (cancellationToken);

	/// <summary>
	/// Turns the light off and refreshes state.
	/// </summary>
	/// <param name="transitionMilliseconds">The optional transition duration, in milliseconds, for supported legacy light devices.</param>
	/// <param name="cancellationToken">The cancellation token for the operation.</param>
	/// <returns>A task that completes when the light state has been refreshed.</returns>
	public Task TurnOffAsync (int transitionMilliseconds, CancellationToken cancellationToken = default) =>
		_device.TurnLightOffAsync (transitionMilliseconds, cancellationToken);

	/// <summary>
	/// Sets the brightness percentage and refreshes state.
	/// </summary>
	/// <param name="brightness">The brightness percentage from 0 through 100.</param>
	/// <param name="cancellationToken">The cancellation token for the operation.</param>
	/// <returns>A task that completes when the light state has been refreshed.</returns>
	/// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="brightness" /> is outside the inclusive range of 0 through 100.</exception>
	public Task SetBrightnessAsync (int brightness, CancellationToken cancellationToken = default) => _device.SetBrightnessAsync (brightness, cancellationToken);

	/// <summary>
	/// Sets the brightness percentage and refreshes state.
	/// </summary>
	/// <param name="brightness">The brightness percentage from 0 through 100.</param>
	/// <param name="transitionMilliseconds">The optional transition duration, in milliseconds, for supported legacy light devices.</param>
	/// <param name="cancellationToken">The cancellation token for the operation.</param>
	/// <returns>A task that completes when the light state has been refreshed.</returns>
	/// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="brightness" /> is outside the inclusive range of 0 through 100.</exception>
	public Task SetBrightnessAsync (int brightness, int transitionMilliseconds, CancellationToken cancellationToken = default) =>
		_device.SetBrightnessAsync (brightness, transitionMilliseconds, cancellationToken);

	/// <summary>
	/// Sets the color temperature in kelvin and refreshes state.
	/// </summary>
	/// <param name="colorTemperature">The color temperature, in kelvin, to apply to the light.</param>
	/// <param name="cancellationToken">The cancellation token for the operation.</param>
	/// <returns>A task that completes when the light state has been refreshed.</returns>
	/// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="colorTemperature" /> is zero or negative.</exception>
	public Task SetColorTemperatureAsync (int colorTemperature, CancellationToken cancellationToken = default) => _device.SetColorTemperatureAsync (colorTemperature, cancellationToken);

	/// <summary>
	/// Sets the HSV color and refreshes state.
	/// </summary>
	/// <param name="hue">The hue component from 0 through 360.</param>
	/// <param name="saturation">The saturation component from 0 through 100.</param>
	/// <param name="value">The value or brightness component from 0 through 100.</param>
	/// <param name="cancellationToken">The cancellation token for the operation.</param>
	/// <returns>A task that completes when the light state has been refreshed.</returns>
	/// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="hue" />, <paramref name="saturation" />, or <paramref name="value" /> falls outside its supported range.</exception>
	public Task SetHsvAsync (int hue, int saturation, int value, CancellationToken cancellationToken = default) =>
		_device.SetHsvAsync (hue, saturation, value, cancellationToken);

	/// <summary>
	/// Enables a lighting effect by device-specific name or identifier and refreshes state.
	/// </summary>
	/// <param name="effect">The device-specific light effect name or identifier.</param>
	/// <param name="cancellationToken">The cancellation token for the operation.</param>
	/// <returns>A task that completes when the light effect state has been refreshed.</returns>
	/// <exception cref="ArgumentException">Thrown when <paramref name="effect" /> is empty or whitespace.</exception>
	public Task SetEffectAsync (string effect, CancellationToken cancellationToken = default) =>
		_device.SetLightEffectAsync (effect, cancellationToken);

	/// <summary>
	/// Disables the current lighting effect and refreshes state.
	/// </summary>
	/// <param name="cancellationToken">The cancellation token for the operation.</param>
	/// <returns>A task that completes when the light effect state has been refreshed.</returns>
	public Task ClearEffectAsync (CancellationToken cancellationToken = default) =>
		_device.ClearLightEffectAsync (cancellationToken);
	}

/// <summary>
/// Provides module-style access to light presets.
/// </summary>
public sealed class LightPresetModule
	{
	private readonly KasaDevice _device;

	internal LightPresetModule (KasaDevice device) => _device = device;

	/// <summary>
	/// Gets the latest light preset module state.
	/// </summary>
	public LightPresetState? State => _device.LightPresetState;

	/// <summary>
	/// Gets the available presets.
	/// </summary>
	public IReadOnlyList<LightPresetDefinition> Presets => _device.LightPresetState?.Presets ?? Array.Empty<LightPresetDefinition> ();

	/// <summary>
	/// Gets the active preset name.
	/// </summary>
	public string? ActivePreset => _device.LightPresetState?.ActivePreset;

	/// <summary>
	/// Gets a value indicating whether light presets are currently available.
	/// </summary>
	public bool IsAvailable => _device.LightPresetState is not null;

	/// <summary>
	/// Refreshes light preset state.
	/// </summary>
	public Task UpdateAsync (CancellationToken cancellationToken = default) => _device.UpdateAsync (cancellationToken);
	}

/// <summary>
/// Provides module-style access to light transition configuration.
/// </summary>
public sealed class LightTransitionModule
	{
	private readonly KasaDevice _device;

	internal LightTransitionModule (KasaDevice device) => _device = device;

	/// <summary>
	/// Gets the latest light transition module state.
	/// </summary>
	public LightTransitionState? State => _device.LightTransitionState;

	/// <summary>
	/// Gets the transition-on duration in seconds.
	/// </summary>
	public int? TransitionOnSeconds => _device.LightTransitionState?.TransitionOnSeconds;

	/// <summary>
	/// Gets the transition-off duration in seconds.
	/// </summary>
	public int? TransitionOffSeconds => _device.LightTransitionState?.TransitionOffSeconds;

	/// <summary>
	/// Gets a value indicating whether transition configuration is currently available.
	/// </summary>
	public bool IsAvailable => _device.LightTransitionState is not null;

	/// <summary>
	/// Refreshes light transition state.
	/// </summary>
	public Task UpdateAsync (CancellationToken cancellationToken = default) => _device.UpdateAsync (cancellationToken);

	/// <summary>
	/// Enables or disables persistent smooth transitions when supported by the device.
	/// </summary>
	public Task SetEnabledAsync (bool enabled, CancellationToken cancellationToken = default) =>
		_device.SetLightTransitionsEnabledAsync (enabled, cancellationToken);

	/// <summary>
	/// Sets the persistent turn-on transition duration in seconds. Specify 0 to disable the turn-on transition.
	/// </summary>
	public Task SetTurnOnTransitionAsync (int seconds, CancellationToken cancellationToken = default) =>
		_device.SetLightTurnOnTransitionAsync (seconds, cancellationToken);

	/// <summary>
	/// Sets the persistent turn-off transition duration in seconds. Specify 0 to disable the turn-off transition.
	/// </summary>
	public Task SetTurnOffTransitionAsync (int seconds, CancellationToken cancellationToken = default) =>
		_device.SetLightTurnOffTransitionAsync (seconds, cancellationToken);
	}

/// <summary>
/// Provides module-style access to light strip effects.
/// </summary>
public sealed class LightStripEffectModule
	{
	private readonly KasaDevice _device;

	internal LightStripEffectModule (KasaDevice device) => _device = device;

	/// <summary>
	/// Gets the latest light strip effect module state.
	/// </summary>
	public LightStripEffectState? State => _device.LightStripEffectState;

	/// <summary>
	/// Gets the active light strip effect.
	/// </summary>
	public LightEffectState? Effect => _device.LightStripEffectState?.Effect;

	/// <summary>
	/// Gets the available light strip effects.
	/// </summary>
	public IReadOnlyList<LightEffectDefinition> AvailableEffects => _device.LightStripEffectState?.AvailableEffects ?? Array.Empty<LightEffectDefinition> ();

	/// <summary>
	/// Gets a value indicating whether light strip effect state is currently available.
	/// </summary>
	public bool IsAvailable => _device.LightStripEffectState is not null;

	/// <summary>
	/// Refreshes light strip effect state.
	/// </summary>
	public Task UpdateAsync (CancellationToken cancellationToken = default) => _device.UpdateAsync (cancellationToken);
	}

/// <summary>
/// Provides module-style access to device alarm state.
/// </summary>
public sealed class AlarmModule
	{
	private readonly KasaDevice _device;

	internal AlarmModule (KasaDevice device) => _device = device;

	/// <summary>
	/// Gets the latest alarm state.
	/// </summary>
	public AlarmState? State => _device.AlarmState;

	/// <summary>
	/// Gets whether the alarm is active.
	/// </summary>
	public bool? IsActive => _device.AlarmState?.IsActive;

	/// <summary>
	/// Gets the alarm source.
	/// </summary>
	public string? Source => _device.AlarmState?.Source;

	/// <summary>
	/// Gets the alarm sound.
	/// </summary>
	public string? Sound => _device.AlarmState?.Sound;

	/// <summary>
	/// Gets the alarm volume label.
	/// </summary>
	public string? Volume => _device.AlarmState?.Volume;

	/// <summary>
	/// Gets the alarm duration in seconds.
	/// </summary>
	public int? DurationSeconds => _device.AlarmState?.DurationSeconds;

	/// <summary>
	/// Gets a value indicating whether alarm state is currently available.
	/// </summary>
	public bool IsAvailable => _device.AlarmState is not null;

	/// <summary>
	/// Refreshes alarm state.
	/// </summary>
	public Task UpdateAsync (CancellationToken cancellationToken = default) => _device.UpdateAsync (cancellationToken);
	}

/// <summary>
/// Provides module-style access to overheat protection state.
/// </summary>
public sealed class OverheatProtectionModule
	{
	private readonly KasaDevice _device;

	internal OverheatProtectionModule (KasaDevice device) => _device = device;

	/// <summary>
	/// Gets the latest overheat protection state.
	/// </summary>
	public OverheatProtectionState? State => _device.OverheatProtectionState;

	/// <summary>
	/// Gets whether the device is overheated.
	/// </summary>
	public bool? Overheated => _device.OverheatProtectionState?.Overheated;

	/// <summary>
	/// Gets a value indicating whether overheat protection state is currently available.
	/// </summary>
	public bool IsAvailable => _device.OverheatProtectionState is not null;

	/// <summary>
	/// Refreshes overheat protection state.
	/// </summary>
	public Task UpdateAsync (CancellationToken cancellationToken = default) => _device.UpdateAsync (cancellationToken);
	}

/// <summary>
/// Provides module-style access to power protection state.
/// </summary>
public sealed class PowerProtectionModule
	{
	private readonly KasaDevice _device;

	internal PowerProtectionModule (KasaDevice device) => _device = device;

	/// <summary>
	/// Gets the latest power protection state.
	/// </summary>
	public PowerProtectionState? State => _device.PowerProtectionState;

	/// <summary>
	/// Gets whether power protection is active.
	/// </summary>
	public bool? ProtectionActive => _device.PowerProtectionState?.ProtectionActive;

	/// <summary>
	/// Gets a value indicating whether power protection state is currently available.
	/// </summary>
	public bool IsAvailable => _device.PowerProtectionState is not null;

	/// <summary>
	/// Refreshes power protection state.
	/// </summary>
	public Task UpdateAsync (CancellationToken cancellationToken = default) => _device.UpdateAsync (cancellationToken);
	}

/// <summary>
/// Provides module-style access to fan state.
/// </summary>
public sealed class FanModule
	{
	private readonly KasaDevice _device;

	internal FanModule (KasaDevice device) => _device = device;

	/// <summary>
	/// Gets the latest fan state.
	/// </summary>
	public FanState? State => _device.FanState;

	/// <summary>
	/// Gets whether the fan is on.
	/// </summary>
	public bool? IsOn => _device.FanState?.IsOn;

	/// <summary>
	/// Gets a value indicating whether fan state is currently available.
	/// </summary>
	public bool IsAvailable => _device.FanState is not null;

	/// <summary>
	/// Refreshes fan state.
	/// </summary>
	public Task UpdateAsync (CancellationToken cancellationToken = default) => _device.UpdateAsync (cancellationToken);
	}

/// <summary>
/// Provides module-style access to speaker capability state.
/// </summary>
public sealed class SpeakerModule
	{
	private readonly KasaDevice _device;

	internal SpeakerModule (KasaDevice device) => _device = device;

	/// <summary>
	/// Gets the latest speaker state.
	/// </summary>
	public SpeakerState? State => _device.SpeakerState;

	/// <summary>
	/// Gets whether speaker capability is available.
	/// </summary>
	public bool? Available => _device.SpeakerState?.IsAvailable;

	/// <summary>
	/// Gets a value indicating whether speaker state is currently available.
	/// </summary>
	public bool IsAvailable => _device.SpeakerState is not null;

	/// <summary>
	/// Refreshes speaker state.
	/// </summary>
	public Task UpdateAsync (CancellationToken cancellationToken = default) => _device.UpdateAsync (cancellationToken);
	}

/// <summary>
/// Provides module-style access to the countdown timer state.
/// </summary>
public sealed class CountdownModule
	{
	private readonly KasaDevice _device;

	internal CountdownModule (KasaDevice device) => _device = device;

	/// <summary>
	/// Gets the latest normalized countdown timer state.
	/// </summary>
	public CountdownRuleState? State => _device.RuleState?.Countdown;

	/// <summary>
	/// Gets a value indicating whether countdown state is currently available.
	/// </summary>
	public bool IsAvailable => _device.RuleState?.Countdown is not null;

	/// <summary>
	/// Gets a value indicating whether the countdown feature is enabled.
	/// </summary>
	public bool? IsEnabled => _device.RuleState?.Countdown?.IsEnabled;

	/// <summary>
	/// Gets a value indicating whether a countdown is currently active.
	/// </summary>
	public bool? IsActive => _device.RuleState?.Countdown?.IsActive;

	/// <summary>
	/// Gets the configured countdown delay in seconds.
	/// </summary>
	public int? DelaySeconds => _device.RuleState?.Countdown?.DelaySeconds;

	/// <summary>
	/// Gets a value indicating whether the countdown action turns the device on.
	/// </summary>
	public bool? ActionTurnsOn => _device.RuleState?.Countdown?.ActionTurnsOn;

	/// <summary>
	/// Refreshes countdown-related state.
	/// </summary>
	/// <param name="cancellationToken">The cancellation token for the operation.</param>
	/// <returns>A task that completes when the device state has been refreshed.</returns>
	public Task UpdateAsync (CancellationToken cancellationToken = default) => _device.UpdateAsync (cancellationToken);
	}

/// <summary>
/// Provides module-style access to countdown, schedule, and antitheft rules.
	/// </summary>
public sealed class RuleModule
	{
	private readonly KasaDevice _device;

	internal RuleModule (KasaDevice device) => _device = device;

	/// <summary>
	/// Gets the latest normalized rule state.
	/// </summary>
	public RuleModuleState? State => _device.RuleState;

	/// <summary>
	/// Gets the latest countdown timer state.
	/// </summary>
	public CountdownRuleState? Countdown => _device.RuleState?.Countdown;

	/// <summary>
	/// Gets the latest schedule rules.
	/// </summary>
	public IReadOnlyList<ScheduledRule> Schedules => _device.RuleState?.Schedules ?? Array.Empty<ScheduledRule> ();

	/// <summary>
	/// Gets the latest antitheft rules.
	/// </summary>
	public IReadOnlyList<ScheduledRule> AntitheftRules => _device.RuleState?.AntitheftRules ?? Array.Empty<ScheduledRule> ();

	/// <summary>
	/// Refreshes the device state, including rule-related modules.
	/// </summary>
	/// <param name="cancellationToken">The cancellation token for the operation.</param>
	/// <returns>A task that completes when the device state has been refreshed.</returns>
	public Task UpdateAsync (CancellationToken cancellationToken = default) => _device.UpdateAsync (cancellationToken);
	}

/// <summary>
/// Provides a child-device wrapper with delegated parent commands.
/// </summary>
public sealed class ChildDevice
	{
	private readonly KasaDevice _parent;

	internal ChildDevice (KasaDevice parent, ChildDeviceInfo childInfo)
		{
		_parent = parent;
		Id = childInfo.Id;
		}

	/// <summary>
	/// Gets the child device identifier.
	/// </summary>
	public string Id
		{
		get;
		}

	/// <summary>
	/// Gets the latest child device snapshot.
	/// </summary>
	public ChildDeviceInfo? Info => _parent.GetChild (Id);

	/// <summary>
	/// Gets the latest child alias.
	/// </summary>
	public string? Alias => Info?.Alias;

	/// <summary>
	/// Gets the latest child model.
	/// </summary>
	public string? Model => Info?.Model;

	/// <summary>
	/// Gets the latest child device family.
	/// </summary>
	public DeviceType DeviceType => Info?.DeviceType ?? DeviceType.Unknown;

	/// <summary>
	/// Gets the latest child power state.
	/// </summary>
	public bool? IsOn => Info?.IsOn;

	/// <summary>
	/// Gets the latest child features.
	/// </summary>
	public IReadOnlyList<DeviceFeature> Features => Info?.Features ?? Array.Empty<DeviceFeature> ();

	internal KasaResponseParser.SmartChildDeviceDto? RawState => _parent.GetChildRawState (Id);

	internal ChildTriggerLogState? TriggerLogState
		{
		get
			{
			if (RawState is not KasaResponseParser.SmartChildDeviceDto child)
				{
				return null;
				}

			List<KasaResponseParser.SmartTriggerLogDto>? logs = child.TriggerLogs?.Logs;
			if (logs is null || logs.Count == 0)
				{
				return new ChildTriggerLogState (Array.Empty<ChildTriggerLogEntry> ());
				}

			var entries = new List<ChildTriggerLogEntry> (logs.Count);
			foreach (KasaResponseParser.SmartTriggerLogDto log in logs)
				{
				entries.Add (new ChildTriggerLogEntry (log.Id, log.EventId, log.Timestamp, log.Event));
				}

			return new ChildTriggerLogState (entries);
			}
		}

	internal ChildFrostProtectionState? FrostProtectionState
		{
		get
			{
			if (RawState is not KasaResponseParser.SmartChildDeviceDto child)
				{
				return null;
				}

			if (child.FrostProtectionOn is null && child.FrostProtection is null)
				{
				return null;
				}

			return new ChildFrostProtectionState (
				child.FrostProtectionOn,
				child.FrostProtection?.MinimumTemperature,
				child.FrostProtection?.TemperatureUnit ?? child.TemperatureUnit);
			}
		}

	internal ChildProtectionState? ChildProtectionState
		{
		get
			{
			if (RawState is not KasaResponseParser.SmartChildDeviceDto child || child.ChildProtection is null)
				{
				return null;
				}

			return new ChildProtectionState (child.ChildProtection);
			}
		}

	internal ChildTemperatureControlState? TemperatureControlState
		{
		get
			{
			if (RawState is not KasaResponseParser.SmartChildDeviceDto child)
				{
				return null;
				}

			IReadOnlyList<string> states = child.TrvStates is List<string> trvStates
				? trvStates
				: Array.Empty<string> ();
			if (child.DeviceOn is null
				&& child.TargetTemperature is null
				&& child.MinimumControlTemperature is null
				&& child.MaximumControlTemperature is null
				&& child.TemperatureOffset is null
				&& states.Count == 0)
				{
				return null;
				}

			return new ChildTemperatureControlState (
				child.DeviceOn,
				child.TargetTemperature,
				child.MinimumControlTemperature,
				child.MaximumControlTemperature,
				child.TemperatureOffset,
				states);
			}
		}

	internal ChildThermostatState? ThermostatState
		{
		get
			{
			if (RawState is not KasaResponseParser.SmartChildDeviceDto child)
				{
				return null;
				}

			IReadOnlyList<string> states = child.TrvStates is List<string> trvStates
				? trvStates
				: Array.Empty<string> ();
			if (child.DeviceOn is null
				&& child.TargetTemperature is null
				&& child.MinimumControlTemperature is null
				&& child.MaximumControlTemperature is null
				&& child.TemperatureOffset is null
				&& child.FrostProtectionOn is null
				&& child.ChildProtection is null
				&& states.Count == 0)
				{
				return null;
				}

			return new ChildThermostatState (
				child.DeviceOn,
				child.TargetTemperature,
				child.CurrentTemperature,
				child.TemperatureUnit,
				states);
			}
		}

	internal ChildBatterySensorState? BatteryState
		{
		get
			{
			if (RawState is not KasaResponseParser.SmartChildDeviceDto child)
				{
				return null;
				}

			bool? batteryLow = child.AtLowBattery ?? child.IsLowBattery;
			if (child.BatteryPercentage is null && batteryLow is null)
				{
				return null;
				}

			return new ChildBatterySensorState (child.BatteryPercentage, batteryLow);
			}
		}

	internal ChildContactSensorState? ContactState
		{
		get
			{
			if (RawState is not KasaResponseParser.SmartChildDeviceDto child || child.Open is null)
				{
				return null;
				}

			return new ChildContactSensorState (child.Open);
			}
		}

	internal ChildMotionSensorState? MotionState
		{
		get
			{
			if (RawState is not KasaResponseParser.SmartChildDeviceDto child || child.Detected is null)
				{
				return null;
				}

			return new ChildMotionSensorState (child.Detected);
			}
		}

	internal ChildWaterLeakSensorState? WaterLeakState
		{
		get
			{
			if (RawState is not KasaResponseParser.SmartChildDeviceDto child)
				{
				return null;
				}

			if (child.WaterLeakStatus is null && child.InAlarm is null && child.TriggerTimestamp is null)
				{
				return null;
				}

			return new ChildWaterLeakSensorState (child.WaterLeakStatus, child.InAlarm, child.TriggerTimestamp);
			}
		}

	internal ChildTemperatureSensorState? TemperatureState
		{
		get
			{
			if (RawState is not KasaResponseParser.SmartChildDeviceDto child)
				{
				return null;
				}

			bool? warning = child.CurrentTemperatureException is int temperatureWarning ? temperatureWarning != 0 : null;
			if (child.CurrentTemperature is null
				&& warning is null
				&& child.TemperatureUnit is null
				&& child.ComfortTemperatureConfig is null)
				{
				return null;
				}

			return new ChildTemperatureSensorState (
				child.CurrentTemperature,
				warning,
				child.TemperatureUnit,
				child.ComfortTemperatureConfig?.MinValue,
				child.ComfortTemperatureConfig?.MaxValue);
			}
		}

	internal ChildHumiditySensorState? HumidityState
		{
		get
			{
			if (RawState is not KasaResponseParser.SmartChildDeviceDto child)
				{
				return null;
				}

			bool? warning = child.CurrentHumidityException is int humidityWarning ? humidityWarning != 0 : null;
			if (child.CurrentHumidity is null && warning is null && child.ComfortHumidityConfig is null)
				{
				return null;
				}

			return new ChildHumiditySensorState (
				child.CurrentHumidity,
				warning,
				child.ComfortHumidityConfig?.MinValue,
				child.ComfortHumidityConfig?.MaxValue);
			}
		}

	internal ChildReportModeState? ReportModeState
		{
		get
			{
			if (RawState is not KasaResponseParser.SmartChildDeviceDto child || child.ReportInterval is null)
				{
				return null;
				}

			return new ChildReportModeState (child.ReportInterval);
			}
		}

	internal ChildDoubleClickState? DoubleClickState
		{
		get
			{
			if (RawState is not KasaResponseParser.SmartChildDeviceDto child || child.DoubleClickInfo?.Enable is not bool enabled)
				{
				return null;
				}

			return new ChildDoubleClickState (enabled);
			}
		}

	/// <summary>
	/// Gets trigger-log information for event-driven child devices.
	/// </summary>
	public ChildTriggerLogModule TriggerLogs => new (this);

	/// <summary>
	/// Gets battery information for child sensor devices.
	/// </summary>
	public ChildBatterySensorModule Battery => new (this);

	/// <summary>
	/// Gets contact/open information for child contact sensors.
	/// </summary>
	public ChildContactSensorModule Contact => new (this);

	/// <summary>
	/// Gets motion state information for child motion sensors.
	/// </summary>
	public ChildMotionSensorModule Motion => new (this);

	/// <summary>
	/// Gets water leak information for child water leak sensors.
	/// </summary>
	public ChildWaterLeakSensorModule WaterLeak => new (this);

	/// <summary>
	/// Gets temperature information for child environmental sensors.
	/// </summary>
	public ChildTemperatureSensorModule Temperature => new (this);

	/// <summary>
	/// Gets humidity information for child environmental sensors.
	/// </summary>
	public ChildHumiditySensorModule Humidity => new (this);

	/// <summary>
	/// Gets report-mode information for child environmental sensors.
	/// </summary>
	public ChildReportModeModule ReportMode => new (this);

	/// <summary>
	/// Gets double-click information for child button devices.
	/// </summary>
	public ChildDoubleClickModule DoubleClick => new (this);

	/// <summary>
	/// Gets frost-protection information for child thermostat devices.
	/// </summary>
	public ChildFrostProtectionModule FrostProtection => new (this);

	/// <summary>
	/// Gets child-protection information for child thermostat devices.
	/// </summary>
	public ChildProtectionModule ChildProtection => new (this);

	/// <summary>
	/// Gets temperature-control information for child thermostat devices.
	/// </summary>
	public ChildTemperatureControlModule TemperatureControl => new (this);

	/// <summary>
	/// Gets aggregated thermostat information for child thermostat devices.
	/// </summary>
	public ChildThermostatModule Thermostat => new (this);

	/// <summary>
	/// Refreshes the parent device state.
	/// </summary>
	/// <param name="cancellationToken">The cancellation token for the operation.</param>
	/// <returns>A task that completes when the parent device state has been refreshed.</returns>
	public Task UpdateAsync (CancellationToken cancellationToken = default) => _parent.UpdateAsync (cancellationToken);

	/// <summary>
	/// Turns the child device on.
	/// </summary>
	/// <param name="cancellationToken">The cancellation token for the operation.</param>
	/// <returns>A task that completes when the child device and parent state have been refreshed.</returns>
	public Task TurnOnAsync (CancellationToken cancellationToken = default) => _parent.TurnChildOnAsync (Id, cancellationToken);

	/// <summary>
	/// Turns the child device off.
	/// </summary>
	/// <param name="cancellationToken">The cancellation token for the operation.</param>
	/// <returns>A task that completes when the child device and parent state have been refreshed.</returns>
	public Task TurnOffAsync (CancellationToken cancellationToken = default) => _parent.TurnChildOffAsync (Id, cancellationToken);
	}

/// <summary>
/// Provides typed trigger-log information for a child device.
/// </summary>
public sealed class ChildTriggerLogModule
	{
	private readonly ChildDevice _child;

	internal ChildTriggerLogModule (ChildDevice child) => _child = child;

	private ChildTriggerLogState? StateOrNull => _child.TriggerLogState;

	/// <summary>
	/// Gets the latest typed trigger-log state for the child device.
	/// </summary>
	public ChildTriggerLogState? State => StateOrNull;

	/// <summary>
	/// Gets the latest trigger log entries reported for the child device.
	/// </summary>
	public IReadOnlyList<ChildTriggerLogEntry> Logs
		{
		get
			{
			ChildTriggerLogState? state = StateOrNull;
			return state is null ? Array.Empty<ChildTriggerLogEntry> () : state.Logs;
			}
		}

	/// <summary>
	/// Refreshes the parent device state and child module data.
	/// </summary>
	/// <param name="cancellationToken">The cancellation token for the operation.</param>
	/// <returns>A task that completes when the parent device state has been refreshed.</returns>
	public Task UpdateAsync (CancellationToken cancellationToken = default) => _child.UpdateAsync (cancellationToken);
	}

/// <summary>
/// Provides typed double-click information for a child button device.
/// </summary>
public sealed class ChildDoubleClickModule
	{
	private readonly ChildDevice _child;

	internal ChildDoubleClickModule (ChildDevice child) => _child = child;

	private ChildDoubleClickState? StateOrNull => _child.DoubleClickState;

	/// <summary>
	/// Gets the latest typed double-click state for the child device.
	/// </summary>
	public ChildDoubleClickState? State => StateOrNull;

	/// <summary>
	/// Gets whether double-click is enabled.
	/// </summary>
	public bool? Enabled => StateOrNull is ChildDoubleClickState state ? state.Enabled : null;

	/// <summary>
	/// Refreshes the parent device state and child module data.
	/// </summary>
	/// <param name="cancellationToken">The cancellation token for the operation.</param>
	/// <returns>A task that completes when the parent device state has been refreshed.</returns>
	public Task UpdateAsync (CancellationToken cancellationToken = default) => _child.UpdateAsync (cancellationToken);
	}

/// <summary>
/// Provides typed frost-protection information for a child thermostat device.
/// </summary>
public sealed class ChildFrostProtectionModule
	{
	private readonly ChildDevice _child;

	internal ChildFrostProtectionModule (ChildDevice child) => _child = child;

	private ChildFrostProtectionState? StateOrNull => _child.FrostProtectionState;

	/// <summary>
	/// Gets the latest typed frost-protection state for the child device.
	/// </summary>
	public ChildFrostProtectionState? State => StateOrNull;

	/// <summary>
	/// Gets whether frost protection is enabled.
	/// </summary>
	public bool? Enabled => StateOrNull is ChildFrostProtectionState state ? state.Enabled : null;

	/// <summary>
	/// Gets the minimum frost-protection temperature, when reported.
	/// </summary>
	public int? MinimumTemperature => StateOrNull is ChildFrostProtectionState state ? state.MinimumTemperature : null;

	/// <summary>
	/// Gets the reported temperature unit.
	/// </summary>
	public string? Unit => StateOrNull is ChildFrostProtectionState state ? state.Unit : null;

	/// <summary>
	/// Refreshes the parent device state and child module data.
	/// </summary>
	/// <param name="cancellationToken">The cancellation token for the operation.</param>
	/// <returns>A task that completes when the parent device state has been refreshed.</returns>
	public Task UpdateAsync (CancellationToken cancellationToken = default) => _child.UpdateAsync (cancellationToken);
	}

/// <summary>
/// Provides typed child-protection information for a child thermostat device.
/// </summary>
public sealed class ChildProtectionModule
	{
	private readonly ChildDevice _child;

	internal ChildProtectionModule (ChildDevice child) => _child = child;

	private ChildProtectionState? StateOrNull => _child.ChildProtectionState;

	/// <summary>
	/// Gets the latest typed child-protection state for the child device.
	/// </summary>
	public ChildProtectionState? State => StateOrNull;

	/// <summary>
	/// Gets whether child protection is enabled.
	/// </summary>
	public bool? Enabled => StateOrNull is ChildProtectionState state ? state.Enabled : null;

	/// <summary>
	/// Refreshes the parent device state and child module data.
	/// </summary>
	/// <param name="cancellationToken">The cancellation token for the operation.</param>
	/// <returns>A task that completes when the parent device state has been refreshed.</returns>
	public Task UpdateAsync (CancellationToken cancellationToken = default) => _child.UpdateAsync (cancellationToken);
	}

/// <summary>
/// Provides typed temperature-control information for a child thermostat device.
/// </summary>
public sealed class ChildTemperatureControlModule
	{
	private readonly ChildDevice _child;

	internal ChildTemperatureControlModule (ChildDevice child) => _child = child;

	private ChildTemperatureControlState? StateOrNull => _child.TemperatureControlState;

	/// <summary>
	/// Gets the latest typed temperature-control state for the child device.
	/// </summary>
	public ChildTemperatureControlState? State => StateOrNull;

	/// <summary>
	/// Gets whether temperature control is enabled.
	/// </summary>
	public bool? Enabled => StateOrNull is ChildTemperatureControlState state ? state.Enabled : null;

	/// <summary>
	/// Gets the target temperature, when reported.
	/// </summary>
	public double? TargetTemperature => StateOrNull is ChildTemperatureControlState state ? state.TargetTemperature : null;

	/// <summary>
	/// Gets the minimum supported target temperature, when reported.
	/// </summary>
	public int? MinimumTargetTemperature => StateOrNull is ChildTemperatureControlState state ? state.MinimumTargetTemperature : null;

	/// <summary>
	/// Gets the maximum supported target temperature, when reported.
	/// </summary>
	public int? MaximumTargetTemperature => StateOrNull is ChildTemperatureControlState state ? state.MaximumTargetTemperature : null;

	/// <summary>
	/// Gets the temperature offset, when reported.
	/// </summary>
	public int? TemperatureOffset => StateOrNull is ChildTemperatureControlState state ? state.TemperatureOffset : null;

	/// <summary>
	/// Gets the raw TRV state flags reported by the device.
	/// </summary>
	public IReadOnlyList<string> States
		{
		get
			{
			ChildTemperatureControlState? state = StateOrNull;
			return state is null ? Array.Empty<string> () : state.States;
			}
		}

	/// <summary>
	/// Refreshes the parent device state and child module data.
	/// </summary>
	/// <param name="cancellationToken">The cancellation token for the operation.</param>
	/// <returns>A task that completes when the parent device state has been refreshed.</returns>
	public Task UpdateAsync (CancellationToken cancellationToken = default) => _child.UpdateAsync (cancellationToken);
	}

/// <summary>
/// Provides aggregated thermostat information for a child thermostat device.
/// </summary>
public sealed class ChildThermostatModule
	{
	private readonly ChildDevice _child;

	internal ChildThermostatModule (ChildDevice child) => _child = child;

	private ChildThermostatState? StateOrNull => _child.ThermostatState;

	/// <summary>
	/// Gets the latest typed thermostat state for the child device.
	/// </summary>
	public ChildThermostatState? State => StateOrNull;

	/// <summary>
	/// Gets whether the thermostat is enabled.
	/// </summary>
	public bool? Enabled => StateOrNull is ChildThermostatState state ? state.Enabled : null;

	/// <summary>
	/// Gets the target temperature, when reported.
	/// </summary>
	public double? TargetTemperature => StateOrNull is ChildThermostatState state ? state.TargetTemperature : null;

	/// <summary>
	/// Gets the current measured temperature, when reported.
	/// </summary>
	public double? CurrentTemperature => StateOrNull is ChildThermostatState state ? state.CurrentTemperature : null;

	/// <summary>
	/// Gets the reported temperature unit.
	/// </summary>
	public string? Unit => StateOrNull is ChildThermostatState state ? state.Unit : null;

	/// <summary>
	/// Gets the raw TRV state flags reported by the device.
	/// </summary>
	public IReadOnlyList<string> States
		{
		get
			{
			ChildThermostatState? state = StateOrNull;
			return state is null ? Array.Empty<string> () : state.States;
			}
		}

	/// <summary>
	/// Refreshes the parent device state and child module data.
	/// </summary>
	/// <param name="cancellationToken">The cancellation token for the operation.</param>
	/// <returns>A task that completes when the parent device state has been refreshed.</returns>
	public Task UpdateAsync (CancellationToken cancellationToken = default) => _child.UpdateAsync (cancellationToken);
	}

/// <summary>
/// Provides typed battery information for a child sensor device.
/// </summary>
public sealed class ChildBatterySensorModule
	{
	private readonly ChildDevice _child;

	internal ChildBatterySensorModule (ChildDevice child) => _child = child;

	private ChildBatterySensorState? StateOrNull => _child.BatteryState;

	/// <summary>
	/// Gets the latest typed battery state for the child device.
	/// </summary>
	public ChildBatterySensorState? State => StateOrNull;

	/// <summary>
	/// Gets the latest battery level percentage, when reported.
	/// </summary>
	public int? BatteryLevel => StateOrNull is ChildBatterySensorState state ? state.BatteryLevel : null;

	/// <summary>
	/// Gets whether the child device reports a low battery condition.
	/// </summary>
	public bool? BatteryLow => StateOrNull is ChildBatterySensorState state ? state.BatteryLow : null;

	/// <summary>
	/// Refreshes the parent device state and child module data.
	/// </summary>
	/// <param name="cancellationToken">The cancellation token for the operation.</param>
	/// <returns>A task that completes when the parent device state has been refreshed.</returns>
	public Task UpdateAsync (CancellationToken cancellationToken = default) => _child.UpdateAsync (cancellationToken);
	}

/// <summary>
/// Provides typed contact sensor information for a child device.
/// </summary>
public sealed class ChildContactSensorModule
	{
	private readonly ChildDevice _child;

	internal ChildContactSensorModule (ChildDevice child) => _child = child;

	private ChildContactSensorState? StateOrNull => _child.ContactState;

	/// <summary>
	/// Gets the latest typed contact sensor state for the child device.
	/// </summary>
	public ChildContactSensorState? State => StateOrNull;

	/// <summary>
	/// Gets whether the contact sensor is currently open.
	/// </summary>
	public bool? IsOpen => StateOrNull is ChildContactSensorState state ? state.IsOpen : null;

	/// <summary>
	/// Refreshes the parent device state and child module data.
	/// </summary>
	/// <param name="cancellationToken">The cancellation token for the operation.</param>
	/// <returns>A task that completes when the parent device state has been refreshed.</returns>
	public Task UpdateAsync (CancellationToken cancellationToken = default) => _child.UpdateAsync (cancellationToken);
	}

/// <summary>
/// Provides typed motion sensor information for a child device.
/// </summary>
public sealed class ChildMotionSensorModule
	{
	private readonly ChildDevice _child;

	internal ChildMotionSensorModule (ChildDevice child) => _child = child;

	private ChildMotionSensorState? StateOrNull => _child.MotionState;

	/// <summary>
	/// Gets the latest typed motion sensor state for the child device.
	/// </summary>
	public ChildMotionSensorState? State => StateOrNull;

	/// <summary>
	/// Gets whether motion is currently detected.
	/// </summary>
	public bool? MotionDetected => StateOrNull is ChildMotionSensorState state ? state.MotionDetected : null;

	/// <summary>
	/// Refreshes the parent device state and child module data.
	/// </summary>
	/// <param name="cancellationToken">The cancellation token for the operation.</param>
	/// <returns>A task that completes when the parent device state has been refreshed.</returns>
	public Task UpdateAsync (CancellationToken cancellationToken = default) => _child.UpdateAsync (cancellationToken);
	}

/// <summary>
/// Provides typed water leak information for a child sensor device.
/// </summary>
public sealed class ChildWaterLeakSensorModule
	{
	private readonly ChildDevice _child;

	internal ChildWaterLeakSensorModule (ChildDevice child) => _child = child;

	private ChildWaterLeakSensorState? StateOrNull => _child.WaterLeakState;

	/// <summary>
	/// Gets the latest typed water leak state for the child device.
	/// </summary>
	public ChildWaterLeakSensorState? State => StateOrNull;

	/// <summary>
	/// Gets the latest reported water leak status string.
	/// </summary>
	public string? Status => StateOrNull is ChildWaterLeakSensorState state ? state.Status : null;

	/// <summary>
	/// Gets whether the child device currently reports an active alert.
	/// </summary>
	public bool? Alert => StateOrNull is ChildWaterLeakSensorState state ? state.Alert : null;

	/// <summary>
	/// Gets the latest reported alert timestamp, when available.
	/// </summary>
	public long? AlertTimestamp => StateOrNull is ChildWaterLeakSensorState state ? state.AlertTimestamp : null;

	/// <summary>
	/// Refreshes the parent device state and child module data.
	/// </summary>
	/// <param name="cancellationToken">The cancellation token for the operation.</param>
	/// <returns>A task that completes when the parent device state has been refreshed.</returns>
	public Task UpdateAsync (CancellationToken cancellationToken = default) => _child.UpdateAsync (cancellationToken);
	}

/// <summary>
/// Provides typed temperature information for a child sensor device.
/// </summary>
public sealed class ChildTemperatureSensorModule
	{
	private readonly ChildDevice _child;

	internal ChildTemperatureSensorModule (ChildDevice child) => _child = child;

	private ChildTemperatureSensorState? StateOrNull => _child.TemperatureState;

	/// <summary>
	/// Gets the latest typed temperature state for the child device.
	/// </summary>
	public ChildTemperatureSensorState? State => StateOrNull;

	/// <summary>
	/// Gets the latest reported temperature value.
	/// </summary>
	public double? Temperature => StateOrNull is ChildTemperatureSensorState state ? state.Temperature : null;

	/// <summary>
	/// Gets whether the child device reports a temperature warning.
	/// </summary>
	public bool? Warning => StateOrNull is ChildTemperatureSensorState state ? state.Warning : null;

	/// <summary>
	/// Gets the reported temperature unit.
	/// </summary>
	public string? Unit => StateOrNull is ChildTemperatureSensorState state ? state.Unit : null;

	/// <summary>
	/// Gets the minimum comfort temperature, when reported.
	/// </summary>
	public double? MinimumComfortTemperature => StateOrNull is ChildTemperatureSensorState state ? state.MinimumComfortTemperature : null;

	/// <summary>
	/// Gets the maximum comfort temperature, when reported.
	/// </summary>
	public double? MaximumComfortTemperature => StateOrNull is ChildTemperatureSensorState state ? state.MaximumComfortTemperature : null;

	/// <summary>
	/// Refreshes the parent device state and child module data.
	/// </summary>
	/// <param name="cancellationToken">The cancellation token for the operation.</param>
	/// <returns>A task that completes when the parent device state has been refreshed.</returns>
	public Task UpdateAsync (CancellationToken cancellationToken = default) => _child.UpdateAsync (cancellationToken);
	}

/// <summary>
/// Provides typed humidity information for a child sensor device.
/// </summary>
public sealed class ChildHumiditySensorModule
	{
	private readonly ChildDevice _child;

	internal ChildHumiditySensorModule (ChildDevice child) => _child = child;

	private ChildHumiditySensorState? StateOrNull => _child.HumidityState;

	/// <summary>
	/// Gets the latest typed humidity state for the child device.
	/// </summary>
	public ChildHumiditySensorState? State => StateOrNull;

	/// <summary>
	/// Gets the latest reported humidity percentage.
	/// </summary>
	public int? Humidity => StateOrNull is ChildHumiditySensorState state ? state.Humidity : null;

	/// <summary>
	/// Gets whether the child device reports a humidity warning.
	/// </summary>
	public bool? Warning => StateOrNull is ChildHumiditySensorState state ? state.Warning : null;

	/// <summary>
	/// Gets the minimum comfort humidity, when reported.
	/// </summary>
	public double? MinimumComfortHumidity => StateOrNull is ChildHumiditySensorState state ? state.MinimumComfortHumidity : null;

	/// <summary>
	/// Gets the maximum comfort humidity, when reported.
	/// </summary>
	public double? MaximumComfortHumidity => StateOrNull is ChildHumiditySensorState state ? state.MaximumComfortHumidity : null;

	/// <summary>
	/// Refreshes the parent device state and child module data.
	/// </summary>
	/// <param name="cancellationToken">The cancellation token for the operation.</param>
	/// <returns>A task that completes when the parent device state has been refreshed.</returns>
	public Task UpdateAsync (CancellationToken cancellationToken = default) => _child.UpdateAsync (cancellationToken);
	}

/// <summary>
/// Provides typed report-mode information for a child sensor device.
/// </summary>
public sealed class ChildReportModeModule
	{
	private readonly ChildDevice _child;

	internal ChildReportModeModule (ChildDevice child) => _child = child;

	private ChildReportModeState? StateOrNull => _child.ReportModeState;

	/// <summary>
	/// Gets the latest typed report-mode state for the child device.
	/// </summary>
	public ChildReportModeState? State => StateOrNull;

	/// <summary>
	/// Gets the latest reported sensor report interval in seconds.
	/// </summary>
	public int? ReportInterval => StateOrNull is ChildReportModeState state ? state.ReportInterval : null;

	/// <summary>
	/// Refreshes the parent device state and child module data.
	/// </summary>
	public Task UpdateAsync (CancellationToken cancellationToken = default) => _child.UpdateAsync (cancellationToken);
	}
