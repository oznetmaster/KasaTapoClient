// Copyright © 2026 Neil Colvin.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// Behavior modeled after the independent python-kasa project (https://github.com/python-kasa/python-kasa)
// for protocol/compatibility reference only; no python-kasa source was copied. See ATTRIBUTIONS.md.

using System;
using System.Collections.Generic;
using System.Globalization;

namespace KasaTapoClient;

public sealed partial class KasaDevice
	{
	private List<DeviceFeature> CreateFeatures ()
		{
		var features = new List<DeviceFeature> ();

		if (SystemInfo is not null)
			{
			features.Add (new DeviceFeature ("state", "State", FeatureKind.Switch, IsOn, isReadOnly: false));
			features.Add (new DeviceFeature ("device_id", "Device ID", FeatureKind.Info, SystemInfo.DeviceId));
			if (SystemInfo.SignalLevel is int signalLevel)
				{
				features.Add (new DeviceFeature ("signal_level", "Signal Level", FeatureKind.Number, signalLevel.ToString (CultureInfo.InvariantCulture)));
				}

			if (SystemInfo.Rssi is int systemRssi)
				{
				features.Add (new DeviceFeature ("rssi", "RSSI", FeatureKind.Number, systemRssi.ToString (CultureInfo.InvariantCulture), "dBm"));
				}

			if (!string.IsNullOrWhiteSpace (SystemInfo.Ssid))
				{
				features.Add (new DeviceFeature ("ssid", "SSID", FeatureKind.Info, SystemInfo.Ssid));
				}
			}

		if (EnergyUsage is not null)
			{
			features.Add (CreateNumericFeature ("current_consumption", "Current consumption", EnergyUsage.CurrentPowerWatts, "W"));
			features.Add (CreateNumericFeature ("consumption_today", "Today's consumption", EnergyUsage.TodayKilowattHours, "kWh"));
			features.Add (CreateNumericFeature ("consumption_this_month", "This month's consumption", EnergyUsage.MonthKilowattHours, "kWh"));
			features.Add (CreateNumericFeature ("consumption_total", "Total consumption since reboot", EnergyUsage.TotalKilowattHours, "kWh"));
			}

		if (FirmwareState is not null)
			{
			features.Add (new DeviceFeature ("current_firmware_version", "Current firmware version", FeatureKind.Info, FirmwareState.CurrentFirmwareVersion));
			features.Add (new DeviceFeature ("available_firmware_version", "Available firmware version", FeatureKind.Info, FirmwareState.AvailableFirmwareVersion));
			if (FirmwareState.AutoUpdateEnabled is bool autoUpdateEnabled)
				{
				features.Add (new DeviceFeature ("auto_update_enabled", "Auto update enabled", FeatureKind.Switch, autoUpdateEnabled));
				}

			features.Add (new DeviceFeature ("update_available", "Update available", FeatureKind.Switch, FirmwareState.UpdateAvailable));
			features.Add (new DeviceFeature ("check_latest_firmware", "Check latest firmware", FeatureKind.Action, value: null));
			}

		if (CloudState is not null)
			{
			if (CloudState.IsConnected is bool isConnected)
				{
				features.Add (new DeviceFeature ("cloud_connection", "Cloud connection", FeatureKind.Switch, isConnected));
				}
			}

		if (TimeState is not null)
			{
			features.Add (new DeviceFeature ("device_time", "Device time", FeatureKind.Info, TimeState.LocalTime?.ToString ("O", CultureInfo.InvariantCulture)));
			}

		features.Add (new DeviceFeature ("reboot", "Reboot", FeatureKind.Action, value: null));

		if (MatterSetup is not null)
			{
			features.Add (new DeviceFeature ("matter_setup_code", "Matter setup code", FeatureKind.Info, MatterSetup.SetupCode));
			features.Add (new DeviceFeature ("matter_setup_payload", "Matter setup payload", FeatureKind.Info, MatterSetup.SetupPayload));
			}

		if (HomeKitSetup is not null)
			{
			features.Add (new DeviceFeature ("homekit_setup_code", "HomeKit setup code", FeatureKind.Info, HomeKitSetup.SetupCode));
			features.Add (new DeviceFeature ("homekit_setup_payload", "HomeKit setup payload", FeatureKind.Info, HomeKitSetup.SetupPayload));
			}

		if (AutoOffState is not null)
			{
			if (AutoOffState.Enabled is bool autoOffEnabled)
				{
				features.Add (new DeviceFeature ("auto_off_enabled", "Auto off enabled", FeatureKind.Switch, autoOffEnabled));
				}

			features.Add (new DeviceFeature ("auto_off_minutes", "Auto off in", FeatureKind.Number, AutoOffState.DelayMinutes?.ToString (CultureInfo.InvariantCulture), "min"));
			features.Add (new DeviceFeature ("auto_off_at", "Auto off at", FeatureKind.Info, AutoOffState.AutoOffAt?.ToString ("O", CultureInfo.InvariantCulture)));
			}

		if (LedState is not null)
			{
			if (LedState.Enabled is bool ledEnabled)
				{
				features.Add (new DeviceFeature ("led", "LED", FeatureKind.Switch, ledEnabled));
				}

			features.Add (new DeviceFeature ("led_mode", "LED mode", FeatureKind.Info, LedState.Mode));
			}

		if (ChildLockState?.Enabled is bool childLockEnabled)
			{
			features.Add (new DeviceFeature ("child_lock", "Child lock", FeatureKind.Switch, childLockEnabled));
			}

		if (AlarmState is not null)
			{
			if (AlarmState.IsActive is bool alarmActive)
				{
				features.Add (new DeviceFeature ("alarm", "Alarm", FeatureKind.Switch, alarmActive));
				}

			features.Add (new DeviceFeature ("alarm_source", "Alarm source", FeatureKind.Info, AlarmState.Source));
			features.Add (new DeviceFeature ("alarm_sound", "Alarm sound", FeatureKind.Info, AlarmState.Sound));
			features.Add (new DeviceFeature ("alarm_volume", "Alarm volume", FeatureKind.Info, AlarmState.Volume));
			features.Add (new DeviceFeature ("alarm_volume_level", "Alarm volume level", FeatureKind.Number, AlarmState.VolumeLevel?.ToString (CultureInfo.InvariantCulture)));
			features.Add (new DeviceFeature ("alarm_duration", "Alarm duration", FeatureKind.Number, AlarmState.DurationSeconds?.ToString (CultureInfo.InvariantCulture), "s"));
			features.Add (new DeviceFeature ("test_alarm", "Test alarm", FeatureKind.Action, value: null));
			features.Add (new DeviceFeature ("stop_alarm", "Stop alarm", FeatureKind.Action, value: null));
			}

		if (OverheatProtectionState?.Overheated is bool overheated)
			{
			features.Add (new DeviceFeature ("overheated", "Overheated", FeatureKind.Switch, overheated));
			}

		if (PowerProtectionState?.ProtectionActive is bool protectionActive)
			{
			features.Add (new DeviceFeature ("power_protection", "Power protection", FeatureKind.Switch, protectionActive));
			}

		if (FanState?.IsOn is bool fanOn)
			{
			features.Add (new DeviceFeature ("fan", "Fan", FeatureKind.Switch, fanOn));
			}

		if (SpeakerState?.IsAvailable is bool speakerAvailable)
			{
			features.Add (new DeviceFeature ("speaker", "Speaker", FeatureKind.Switch, speakerAvailable));
			}

		if (DeviceType == DeviceType.Hub)
			{
			features.Add (new DeviceFeature ("pair", "Pair", FeatureKind.Action, value: null));
			}

		if (LightState is not null)
			{
			(int? minimumColorTemperature, int? maximumColorTemperature) = GetColorTemperatureRange (SystemInfo?.Model);
			features.Add (new DeviceFeature ("brightness", "Brightness", FeatureKind.Number, LightState.Brightness?.ToString (CultureInfo.InvariantCulture), "%", minimumValue: 0, maximumValue: 100));
			if (LightState.ColorTemperature is not null || minimumColorTemperature is not null || maximumColorTemperature is not null)
				{
				features.Add (new DeviceFeature ("color_temperature", "Color temperature", FeatureKind.Number, LightState.ColorTemperature?.ToString (CultureInfo.InvariantCulture), null, minimumValue: minimumColorTemperature, maximumValue: maximumColorTemperature));
				}
			features.Add (new DeviceFeature ("hsv", "HSV", FeatureKind.Info, FormatHsv (LightState.Hsv)));
			features.Add (new DeviceFeature ("light_preset", "Light preset", FeatureKind.Info, LightPresetState?.ActivePreset ?? "Not set", choices: CreateLightPresetChoices (LightPresetState)));
			if (LightEffect is not null)
				{
				features.Add (new DeviceFeature ("light_effect", "Light effect", FeatureKind.Info, LightEffect.Name ?? LightEffect.Identifier ?? "Off", choices: CreateLightEffectChoices (LightEffect)));
				}

			if (LightTransitionState is not null)
				{
				if (_smartComponentVersions.TryGetValue ("on_off_gradually", out int supportedVersion) && supportedVersion >= 2)
					{
					features.Add (new DeviceFeature ("smooth_transition_on", "Smooth transition on", FeatureKind.Number, LightTransitionState.TransitionOnSeconds?.ToString (CultureInfo.InvariantCulture), minimumValue: 0, maximumValue: LightTransitionState.TransitionOnMaximumDurationSeconds ?? 60));
					features.Add (new DeviceFeature ("smooth_transition_off", "Smooth transition off", FeatureKind.Number, LightTransitionState.TransitionOffSeconds?.ToString (CultureInfo.InvariantCulture), minimumValue: 0, maximumValue: LightTransitionState.TransitionOffMaximumDurationSeconds ?? 60));
					}
				else
					{
					features.Add (new DeviceFeature ("smooth_transitions", "Smooth transitions", FeatureKind.Switch, LightTransitionState.IsEnabled, isReadOnly: false));
					}
				}
			}

		return features;
		}

	private static double? ConvertToDouble (int? value) => value;

	private static string? FormatHsv (HsvColor? hsv) => hsv is null
		? null
		: $"HSV(hue={hsv.Hue}, saturation={hsv.Saturation}, value={hsv.Value})";

	private static List<string> CreateLightPresetChoices (LightPresetState? lightPresetState)
		{
		if (lightPresetState?.Presets is not IReadOnlyList<LightPresetDefinition> presets || presets.Count == 0)
			{
			return ["Not set"];
			}

		var choices = new List<string> (presets.Count + 1)
			{
			"Not set",
			};
		foreach (LightPresetDefinition preset in presets)
			{
			choices.Add (preset.Name);
			}

		return choices;
		}

	private static List<string> CreateLightEffectChoices (LightEffectState lightEffect)
		{
		var choices = new List<string> (lightEffect.AvailableEffects.Count + 1)
			{
			"Off",
			};

		foreach (LightEffectDefinition effect in lightEffect.AvailableEffects)
			{
			choices.Add (effect.Name ?? effect.Identifier);
			}

		return choices;
		}

	private static (int? Minimum, int? Maximum) GetColorTemperatureRange (string? model)
		{
		string modelText = (model ?? string.Empty).Trim ().ToUpperInvariant ();
		if (modelText.StartsWith ("LB130", StringComparison.Ordinal)
			|| modelText.StartsWith ("LB230", StringComparison.Ordinal)
			|| modelText.StartsWith ("KB130", StringComparison.Ordinal)
			|| modelText.StartsWith ("KL130", StringComparison.Ordinal)
			|| modelText.StartsWith ("KL135", StringComparison.Ordinal)
			|| modelText.StartsWith ("KL430", StringComparison.Ordinal)
			|| modelText.StartsWith ("L900", StringComparison.Ordinal))
			{
			return (2500, 9000);
			}

		if (modelText.StartsWith ("LB120", StringComparison.Ordinal)
			|| modelText.StartsWith ("KL120(EU)", StringComparison.Ordinal)
			|| modelText.StartsWith ("KL125", StringComparison.Ordinal)
			|| modelText.StartsWith ("L530", StringComparison.Ordinal))
			{
			return (2500, 6500);
			}

		if (modelText.StartsWith ("KL120(US)", StringComparison.Ordinal))
			{
			return (2700, 5000);
			}

		return (null, null);
		}

	private static DeviceFeature CreateNumericFeature (string id, string name, double? value, string unit) =>
		new (
			id,
			name,
			FeatureKind.Number,
			value?.ToString (CultureInfo.InvariantCulture),
			unit);
	}
