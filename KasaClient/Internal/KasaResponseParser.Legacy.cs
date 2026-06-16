// Copyright © 2026 Neil Colvin.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// Adapted from python-kasa (https://github.com/python-kasa/python-kasa)
// Original work Copyright (c) python-kasa contributors, MIT License

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;

namespace KasaTapoClient.Internal;

internal static partial class KasaResponseParser
	{
	private static DeviceFamilyKind DetermineLegacyDiscoveryFamilyKind (string? model)
		{
		string modelText = (model ?? string.Empty).Trim ().ToUpperInvariant ();
		if (modelText.StartsWith ("LB", StringComparison.Ordinal)
			|| modelText.StartsWith ("KL", StringComparison.Ordinal)
			|| modelText.StartsWith ("L5", StringComparison.Ordinal)
			|| modelText.StartsWith ("L6", StringComparison.Ordinal)
			|| modelText.StartsWith ("L9", StringComparison.Ordinal))
			{
			return DeviceFamilyKind.IotSmartBulb;
			}

		if (modelText.StartsWith ("KC", StringComparison.Ordinal))
			{
			return DeviceFamilyKind.IotIpCamera;
			}

		return DeviceFamilyKind.IotSmartPlugSwitch;
		}

	private static DeviceSystemInfo CreateSystemInfo (LegacySystemInfoDto systemInfo, string responseJson)
		{
		string alias = FirstNonEmpty (systemInfo.Alias, systemInfo.Nickname) ?? string.Empty;
		string? model = FirstNonEmpty (systemInfo.Model, systemInfo.DeviceModel);
		string? deviceId = FirstNonEmpty (systemInfo.DeviceId, systemInfo.DeviceIdUnderscore);
		string? macAddress = FirstNonEmpty (systemInfo.Mac, systemInfo.MicMac);
		string? hardwareVersion = FirstNonEmpty (systemInfo.HardwareVersion, systemInfo.HardwareVersionAlt);
		string? softwareVersion = FirstNonEmpty (systemInfo.SoftwareVersion, systemInfo.SoftwareVersionAlt);
		string? rawType = FirstNonEmpty (systemInfo.Type, systemInfo.MicType, systemInfo.DeviceType);
		DeviceType deviceType = DetermineDeviceType (rawType, model);
		bool? isOn = ReadPowerState (systemInfo, deviceType);

		return new DeviceSystemInfo (
			alias,
			model,
			deviceId,
			macAddress,
			hardwareVersion,
			softwareVersion,
			signalLevel: null,
			rssi: systemInfo.Rssi,
			ssid: null,
			deviceType,
			isOn,
			systemInfo.OnTimeSeconds is int onTimeSeconds ? TimeSpan.FromSeconds (onTimeSeconds) : null,
			CreateChildren (systemInfo),
			responseJson);
		}

	private static LightState CreateLightState (LegacySystemInfoDto systemInfo)
		{
		if (systemInfo.LightState is not LegacyLightStateDto lightState)
			{
			throw new InvalidDataException ("The device response did not contain light_state data.");
			}

		LegacyLightStateDto effectiveLightState = CreateEffectiveLightState (lightState);
		int? brightness = effectiveLightState.Brightness;
		int? hue = effectiveLightState.Hue;
		int? saturation = effectiveLightState.Saturation;
		bool supportsEffects = SupportsLightEffects (lightState);
		LightEffectState? effect = CreateLightEffectState (effectiveLightState);
		HsvColor? hsv = brightness is int value && hue is int hueValue && saturation is int saturationValue
			? new HsvColor (hueValue, saturationValue, value)
			: null;
		List<LightPresetDefinition> availablePresets = CreateAvailableLightPresets (systemInfo.PreferredState);
		string? activePreset = ResolveActiveLightPreset (availablePresets, brightness, effectiveLightState.ColorTemperature, hue, saturation);

		return new LightState (
			ReadLightPowerState (lightState),
			brightness,
			effectiveLightState.ColorTemperature,
			hue,
			saturation,
			supportsEffects,
			effect,
			hsv,
			availablePresets,
			activePreset,
			JsonSerializer.Serialize (lightState, JsonSupport.COMPACT_JSON));
		}

	private static LegacyLightStateDto CreateEffectiveLightState (LegacyLightStateDto lightState)
		{
		if (ReadLightPowerState (lightState) != false || lightState.DefaultOnState is not LegacyLightStateDto defaultOnState)
			{
			return lightState;
			}
		return new LegacyLightStateDto
			{
			OnOff = lightState.OnOff,
			Brightness = defaultOnState.Brightness ?? lightState.Brightness,
			ColorTemperature = defaultOnState.ColorTemperature ?? lightState.ColorTemperature,
			Hue = defaultOnState.Hue ?? lightState.Hue,
			Saturation = defaultOnState.Saturation ?? lightState.Saturation,
			DynamicLightEffectEnable = defaultOnState.DynamicLightEffectEnable ?? lightState.DynamicLightEffectEnable,
			DynamicLightEffectId = defaultOnState.DynamicLightEffectId ?? lightState.DynamicLightEffectId,
			DynamicLightEffectRuleList = defaultOnState.DynamicLightEffectRuleList ?? lightState.DynamicLightEffectRuleList,
			LightingEffect = defaultOnState.LightingEffect ?? lightState.LightingEffect,
			DefaultOnState = defaultOnState.DefaultOnState ?? lightState.DefaultOnState,
			};
		}

	private static bool SupportsLightEffects (LegacyLightStateDto lightState)
		{
		return lightState.LightingEffect is not null
			|| lightState.DynamicLightEffectEnable is not null
			|| !string.IsNullOrWhiteSpace (lightState.DynamicLightEffectId)
			|| (lightState.DynamicLightEffectRuleList is { Count: > 0 });
		}

	private static LightEffectState? CreateLightEffectState (LegacyLightStateDto lightState)
		{
		List<LightEffectDefinition> availableEffects = CreateAvailableEffects (lightState);
		if (lightState.LightingEffect is LegacyLightingEffectDto lightingEffect)
			{
			return new LightEffectState (
				FirstNonEmpty (lightingEffect.Id, lightingEffect.Name),
				lightingEffect.Name,
				lightingEffect.Enable is int enabled ? enabled != 0 : null,
				lightingEffect.Brightness,
				availableEffects,
				JsonSerializer.Serialize (lightingEffect, JsonSupport.COMPACT_JSON));
			}

		if (lightState.DynamicLightEffectEnable is int || !string.IsNullOrWhiteSpace (lightState.DynamicLightEffectId))
			{
			string? effectId = lightState.DynamicLightEffectId;
			string? effectName = null;
			bool? isEnabled = lightState.DynamicLightEffectEnable is int enableValue ? enableValue != 0 : null;
			if (lightState.DynamicLightEffectRuleList is List<LegacyDynamicLightEffectRuleDto> rules)
				{
				foreach (LegacyDynamicLightEffectRuleDto rule in rules)
					{
					if (string.Equals (rule.Id, effectId, StringComparison.OrdinalIgnoreCase))
						{
						effectName = rule.Name;
						break;
						}
					}
				}

			return new LightEffectState (
				effectId,
				effectName,
				isEnabled,
				lightState.Brightness,
				availableEffects,
				JsonSerializer.Serialize (new
					{
					lightState.DynamicLightEffectEnable,
					lightState.DynamicLightEffectId,
					lightState.DynamicLightEffectRuleList,
					}, JsonSupport.COMPACT_JSON));
			}

		return null;
		}

	private static List<LightEffectDefinition> CreateAvailableEffects (LegacyLightStateDto lightState)
		{
		var availableEffects = new List<LightEffectDefinition> ();
		if (lightState.DynamicLightEffectRuleList is List<LegacyDynamicLightEffectRuleDto> rules)
			{
			foreach (LegacyDynamicLightEffectRuleDto rule in rules)
				{
				string? identifier = FirstNonEmpty (rule.Id, rule.Name);
				if (string.IsNullOrWhiteSpace (identifier))
					{
					continue;
					}

				availableEffects.Add (new LightEffectDefinition (identifier!, rule.Name));
				}
			}

		if (lightState.LightingEffect is LegacyLightingEffectDto lightingEffect)
			{
			string? identifier = FirstNonEmpty (lightingEffect.Id, lightingEffect.Name);
			if (!string.IsNullOrWhiteSpace (identifier) && !ContainsEffectDefinition (availableEffects, identifier!))
				{
				availableEffects.Add (new LightEffectDefinition (identifier!, lightingEffect.Name));
				}
			}

		return availableEffects;
		}

	private static List<LightPresetDefinition> CreateAvailableLightPresets (List<LegacyLightPresetDto>? preferredState)
		{
		var presets = new List<LightPresetDefinition> ();
		if (preferredState is null)
			{
			return presets;
			}

		for (int index = 0; index < preferredState.Count; index++)
			{
			LegacyLightPresetDto preset = preferredState[index];
			if (!string.IsNullOrWhiteSpace (preset.Id))
				{
				continue;
				}

			presets.Add (new LightPresetDefinition (
				$"Light preset {index + 1}",
				preset.Brightness,
				preset.ColorTemperature,
				preset.Hue,
				preset.Saturation,
				JsonSerializer.Serialize (preset, JsonSupport.COMPACT_JSON)));
			}

		return presets;
		}

	private static LightPresetState? CreateLightPresetState (LightState? lightState)
		{
		if (lightState is null || lightState.AvailablePresets.Count == 0)
			{
			return null;
			}

		return new LightPresetState (lightState.AvailablePresets, lightState.ActivePreset);
		}

	private static LightTransitionState? CreateLegacyLightTransitionState (LegacyLightStateDto? lightState, string rawJson)
		{
		if (lightState is null)
			{
			return null;
			}

		using JsonDocument document = JsonDocument.Parse (rawJson);
		if (!TryGetTransitionMilliseconds (document.RootElement, out int? transitionMilliseconds))
			{
			return null;
			}

		return new LightTransitionState (transitionMilliseconds, transitionMilliseconds, rawJson);
		}

	private static DeviceType DetermineLegacyDeviceType (LegacySystemInfoDto systemInfo)
		{
		return CreateSystemInfo (systemInfo, string.Empty).DeviceType;
		}

	private static string? ResolveActiveLightPreset (IReadOnlyList<LightPresetDefinition> availablePresets, int? brightness, int? colorTemperature, int? hue, int? saturation)
		{
		if (brightness is null)
			{
			return null;
			}

		bool hasColorTemperature = colorTemperature is not null && colorTemperature != 0;
		bool hasColor = (hue is not null && hue != 0) || (saturation is not null && saturation != 0);
		foreach (LightPresetDefinition preset in availablePresets)
			{
			if (preset.Brightness != brightness)
				{
				continue;
				}

			if (hasColorTemperature && preset.ColorTemperature != colorTemperature)
				{
				continue;
				}

			if (hasColor && (preset.Hue != hue || preset.Saturation != saturation))
				{
				continue;
				}

			return preset.Name;
			}

		return null;
		}

	private static bool ContainsEffectDefinition (IReadOnlyList<LightEffectDefinition> availableEffects, string identifier)
		{
		foreach (LightEffectDefinition effect in availableEffects)
			{
			if (string.Equals (effect.Identifier, identifier, StringComparison.OrdinalIgnoreCase))
				{
				return true;
				}
			}

		return false;
		}

	private static EnergyUsage CreateEnergyUsage (
		LegacyEmeterRealtimeDto emeterInfo,
		LegacyEmeterDailyStatDto? dayStat,
		LegacyEmeterMonthlyStatDto? monthStat,
		string responseJson)
		{
		double? currentPowerWatts = ReadScaledDouble (emeterInfo.Power, emeterInfo.PowerMilliwatts, 1000d);
		double? voltageVolts = ReadScaledDouble (emeterInfo.Voltage, emeterInfo.VoltageMillivolts, 1000d);
		double? currentAmps = ReadScaledDouble (emeterInfo.Current, emeterInfo.CurrentMilliamps, 1000d);
		double? totalKilowattHours = ReadScaledDouble (emeterInfo.Total, emeterInfo.TotalWattHours, 1000d) ?? ReadScaledDouble (emeterInfo.Energy, emeterInfo.EnergyWattHours, 1000d);
		double? todayKilowattHours = GetCurrentDayEnergyKilowattHours (dayStat);
		double? monthKilowattHours = GetCurrentMonthEnergyKilowattHours (monthStat);
		return new EnergyUsage (currentPowerWatts, voltageVolts, currentAmps, totalKilowattHours, todayKilowattHours, monthKilowattHours, responseJson);
		}

	private static FirmwareState? CreateLegacyFirmwareState (ParsedResponse response)
		{
		bool? autoUpdateEnabled = null;
		string? currentFirmware = response.SystemInfo.SoftwareVersion ?? response.SystemInfo.SoftwareVersionAlt;
		string? currentHardware = response.SystemInfo.HardwareVersion ?? response.SystemInfo.HardwareVersionAlt;
		if (currentFirmware is null && currentHardware is null)
			{
			return null;
			}

		return new FirmwareState (currentFirmware, currentHardware, autoUpdateEnabled, availableFirmwareVersion: null, updateAvailable: null, response.RawJson);
		}

	private static CloudConnectionState? CreateLegacyCloudConnectionState (ParsedResponse response)
		{
		LegacyCloudInfoDto? cloudInfo = response.Cloud?.GetInfo;
		if (cloudInfo is null)
			{
			return null;
			}

		return new CloudConnectionState (
			cloudInfo.CloudConnection is int cloudConnection ? cloudConnection != 0 : null,
			cloudInfo.Binded is int provisioned ? provisioned != 0 : null,
			cloudInfo.Server,
			cloudInfo.UserName,
			JsonSerializer.Serialize (cloudInfo, JsonSupport.COMPACT_JSON));
		}

	private static DeviceTimeState? CreateLegacyDeviceTimeState (ParsedResponse response)
		{
		LegacyTimeInfoDto? time = response.Time?.GetTime;
		if (time is null)
			{
			return null;
			}

		DateTime? localTime = TryCreateLegacyDateTime (time);
		return new DeviceTimeState (localTime, region: null, timeDifferenceMinutes: null, JsonSerializer.Serialize (response.Time, JsonSupport.COMPACT_JSON));
		}

	private static HomeKitSetupInfo? CreateLegacyHomeKitSetupInfo (ParsedResponse response)
		{
		LegacyHomeKitInfoDto? homeKit = response.HomeKit?.SetupInfoGet;
		if (homeKit?.SetupCode is null && homeKit?.SetupPayload is null)
			{
			return null;
			}

		return new HomeKitSetupInfo (homeKit.SetupCode, homeKit.SetupPayload, JsonSerializer.Serialize (homeKit, JsonSupport.COMPACT_JSON));
		}

	private static AutoOffState? CreateLegacyAutoOffState (ParsedResponse response)
		{
		if (response.SystemInfo.AutoOffStatus is null && response.SystemInfo.AutoOffRemainTimeSeconds is null)
			{
			return null;
			}

		bool? timerActive = response.SystemInfo.AutoOffStatus is string status
			? string.Equals (status, "on", StringComparison.OrdinalIgnoreCase)
			: null;
		DateTime? autoOffAt = timerActive == true && response.SystemInfo.AutoOffRemainTimeSeconds is int remainingSeconds
			? DateTime.Now.AddSeconds (remainingSeconds)
			: null;
		return new AutoOffState (enabled: null, delayMinutes: null, timerActive, autoOffAt, response.RawJson);
		}

	private static LedState? CreateLegacyLedState (ParsedResponse response)
		{
		if (response.SystemInfo.LedOff is not int ledOff)
			{
			return null;
			}

		return new LedState (ledOff == 0, ledOff == 0 ? "always" : "never", nightModeSettings: null, response.RawJson);
		}

	private static double? GetCurrentDayEnergyKilowattHours (LegacyEmeterDailyStatDto? dayStat)
		{
		if (dayStat?.DayList is not List<LegacyEmeterDayStatEntryDto> dayEntries)
			{
			return null;
			}

		int today = DateTime.Now.Day;
		for (int index = dayEntries.Count - 1; index >= 0; index--)
			{
			LegacyEmeterDayStatEntryDto entry = dayEntries[index];
			if (entry.Day == today)
				{
				return ReadScaledDouble (entry.EnergyKilowattHours, entry.EnergyWattHours, 1000d) ?? 0d;
				}
			}

		return 0d;
		}

	private static double? GetCurrentMonthEnergyKilowattHours (LegacyEmeterMonthlyStatDto? monthStat)
		{
		if (monthStat?.MonthList is not List<LegacyEmeterMonthStatEntryDto> monthEntries)
			{
			return null;
			}

		int currentMonth = DateTime.Now.Month;
		for (int index = monthEntries.Count - 1; index >= 0; index--)
			{
			LegacyEmeterMonthStatEntryDto entry = monthEntries[index];
			if (entry.Month == currentMonth)
				{
				return ReadScaledDouble (entry.EnergyKilowattHours, entry.EnergyWattHours, 1000d) ?? 0d;
				}
			}

		return 0d;
		}

	private static DateTime? TryCreateLegacyDateTime (LegacyTimeInfoDto time)
		{
		if (time.Year is not int year
			|| time.Month is not int month
			|| time.Day is not int day
			|| time.Hour is not int hour
			|| time.Minute is not int minute
			|| time.Second is not int second)
			{
			return null;
			}

		try
			{
			return new DateTime (year, month, day, hour, minute, second, DateTimeKind.Unspecified);
			}
		catch (ArgumentOutOfRangeException)
			{
			return null;
			}
		}

	private static RuleModuleState? CreateRuleModuleState (ParsedResponse response)
		{
		CountdownRuleState? countdown = CreateCountdownRuleState (response.CountdownRules);
		IReadOnlyList<ScheduledRule> schedules = CreateScheduledRules (response.ScheduleRules);
		IReadOnlyList<ScheduledRule> antitheftRules = CreateScheduledRules (response.AntitheftRules);
		if (countdown is null && schedules.Count == 0 && antitheftRules.Count == 0)
			{
			return null;
			}

		return new RuleModuleState (countdown, schedules, antitheftRules, response.RawJson);
		}

	private static CountdownRuleState? CreateCountdownRuleState (LegacyRuleListDto? rules)
		{
		if (rules?.RuleList is not List<LegacyRuleDto> countdownRules || countdownRules.Count == 0)
			{
			return null;
			}

		LegacyRuleDto countdown = countdownRules[0];
		bool? isEnabled = rules.Enable is int enabled ? enabled != 0 : null;
		bool? isActive = countdown.RemainingSeconds is int remaining ? remaining > 0 : null;
		bool? actionTurnsOn = countdown.Action is int action ? action != 0 : null;
		return new CountdownRuleState (isEnabled, isActive, countdown.DelaySeconds, actionTurnsOn, JsonSerializer.Serialize (countdown, JsonSupport.COMPACT_JSON));
		}

	private static IReadOnlyList<ScheduledRule> CreateScheduledRules (LegacyRuleListDto? rules)
		{
		if (rules?.RuleList is not List<LegacyRuleDto> ruleList)
			{
			return Array.Empty<ScheduledRule> ();
			}

		var schedules = new List<ScheduledRule> (ruleList.Count);
		foreach (LegacyRuleDto rule in ruleList)
			{
			string? id = FirstNonEmpty (rule.Id, rule.Name);
			if (string.IsNullOrWhiteSpace (id))
				{
				continue;
				}

			bool? isEnabled = rule.Enable is int enabled ? enabled != 0 : null;
			bool? actionTurnsOn = rule.Action is int action ? action != 0 : null;
			schedules.Add (new ScheduledRule (id!, rule.Name, isEnabled, actionTurnsOn, rule.StartMinute, rule.EndMinute, JsonSerializer.Serialize (rule, JsonSupport.COMPACT_JSON)));
			}

		return schedules;
		}

	private static string? FirstNonEmpty (params string?[] values)
		{
		foreach (string? value in values)
			{
			if (!string.IsNullOrWhiteSpace (value))
				{
				return value;
				}
			}

		return null;
		}

	private static bool? ReadLightPowerState (LegacyLightStateDto lightState)
		{
		if (lightState.OnOff is int onOff)
			{
			return onOff != 0;
			}

		return null;
		}

	private static IReadOnlyList<ChildDeviceInfo> CreateChildren (LegacySystemInfoDto systemInfo)
		{
		if (systemInfo.Children is not List<LegacyChildDeviceDto> childrenArray)
			{
			return Array.Empty<ChildDeviceInfo> ();
			}

		var children = new List<ChildDeviceInfo> ();
		foreach (LegacyChildDeviceDto child in childrenArray)
			{
			string? childId = FirstNonEmpty (child.Id, child.DeviceId);
			if (string.IsNullOrWhiteSpace (childId))
				{
				continue;
				}

			children.Add (
				new ChildDeviceInfo (
					childId!,
					FirstNonEmpty (child.Alias, child.Nickname),
					FirstNonEmpty (child.Model, child.DeviceModel),
					DetermineChildDeviceType (FirstNonEmpty (child.Model, child.DeviceModel)),
					ReadPowerState (child),
					JsonSerializer.Serialize (child, JsonSupport.COMPACT_JSON)));
			}

		return children;
		}

	private static double? ReadScaledDouble (double? directValue, double? scaledValue, double scale)
		{
		if (directValue is double value)
			{
			return value;
			}

		if (scaledValue is double scaled)
			{
			return scaled / scale;
			}

		return null;
		}

	private static DeviceType DetermineChildDeviceType (string? model)
		{
		string modelText = (model ?? string.Empty).Trim ().ToUpperInvariant ();
		if (modelText.StartsWith ("T100", StringComparison.Ordinal)
			|| modelText.StartsWith ("T110", StringComparison.Ordinal)
			|| modelText.StartsWith ("T300", StringComparison.Ordinal)
			|| modelText.StartsWith ("T310", StringComparison.Ordinal)
			|| modelText.StartsWith ("T315", StringComparison.Ordinal)
			|| modelText.StartsWith ("T31", StringComparison.Ordinal)
			|| modelText.StartsWith ("T30", StringComparison.Ordinal))
			{
			return DeviceType.Sensor;
			}

		if (modelText.StartsWith ("KE100", StringComparison.Ordinal))
			{
			return DeviceType.Thermostat;
			}

		return DeviceType.Plug;
		}

	private static bool? ReadPowerState (LegacySystemInfoDto systemInfo, DeviceType deviceType)
		{
		if (deviceType == DeviceType.Strip && systemInfo.Children is List<LegacyChildDeviceDto> children && children.Count > 0)
			{
			bool hasKnownChildState = false;
			foreach (LegacyChildDeviceDto child in children)
				{
				bool? childIsOn = ReadPowerState (child);
				if (childIsOn == true)
					{
					return true;
					}

				if (childIsOn == false)
					{
					hasKnownChildState = true;
					}
				}

			if (hasKnownChildState)
				{
				return false;
				}
			}

		if (systemInfo.RelayState is int relayState)
			{
			return relayState != 0;
			}

		if (systemInfo.DeviceOn is bool deviceOn)
			{
			return deviceOn;
			}

		if (systemInfo.LightState?.OnOff is int lightOnOff)
			{
			return lightOnOff != 0;
			}

		return null;
		}

	private static bool? ReadPowerState (LegacyChildDeviceDto child)
		{
		if (child.RelayState is int relayState)
			{
			return relayState != 0;
			}

		if (child.State is int state)
			{
			return state != 0;
			}

		if (child.DeviceOn is bool deviceOn)
			{
			return deviceOn;
			}

		return null;
		}

	private static LegacyResponseDto DeserializeResponse (string responseJson)
		{
		LegacyResponseDto? response = JsonSerializer.Deserialize<LegacyResponseDto> (responseJson, JsonSupport.COMPACT_JSON);
		return response ?? throw new InvalidDataException ("The device response could not be deserialized.");
		}

	private static DeviceType DetermineDeviceType (string? rawType, string? model)
		{
		string typeText = (rawType ?? string.Empty).Trim ().ToLowerInvariant ();
		string modelText = (model ?? string.Empty).Trim ().ToUpperInvariant ();
		if (modelText.StartsWith ("KP303", StringComparison.Ordinal)
			|| modelText.StartsWith ("HS300", StringComparison.Ordinal)
			|| modelText.StartsWith ("P300", StringComparison.Ordinal))
			{
			return DeviceType.Strip;
			}

		if (typeText.Contains ("light strip") || typeText.Contains ("lightstrip"))
			{
			return DeviceType.LightStrip;
			}

		if (typeText.Contains ("bulb") || typeText.Contains ("lamp"))
			{
			return DeviceType.Bulb;
			}

		if (typeText.Contains ("strip"))
			{
			return DeviceType.Strip;
			}

		if (typeText.Contains ("plug") || typeText.Contains ("socket"))
			{
			return DeviceType.Plug;
			}

		if (typeText.Contains ("dimmer"))
			{
			return DeviceType.Dimmer;
			}

		if (typeText.Contains ("switch"))
			{
			return DeviceType.WallSwitch;
			}

		if (typeText.Contains ("camera"))
			{
			return DeviceType.Camera;
			}

		if (typeText.Contains ("hub"))
			{
			return DeviceType.Hub;
			}

		if (typeText.Contains ("sensor"))
			{
			return DeviceType.Sensor;
			}

		if (typeText.Contains ("vacuum") || typeText.Contains ("robot"))
			{
			return DeviceType.Vacuum;
			}

		if (modelText.StartsWith ("KL", StringComparison.Ordinal)
			|| modelText.StartsWith ("LB", StringComparison.Ordinal)
			|| modelText.StartsWith ("L5", StringComparison.Ordinal)
			|| modelText.StartsWith ("L6", StringComparison.Ordinal)
			|| modelText.StartsWith ("L9", StringComparison.Ordinal))
			{
			return modelText.Contains ("900") || modelText.Contains ("920") || modelText.Contains ("930")
				? DeviceType.LightStrip
				: DeviceType.Bulb;
			}

		if (modelText.StartsWith ("KP303", StringComparison.Ordinal)
			|| modelText.StartsWith ("HS300", StringComparison.Ordinal)
			|| modelText.StartsWith ("P300", StringComparison.Ordinal))
			{
			return DeviceType.Strip;
			}

		if (modelText.StartsWith ("HS", StringComparison.Ordinal)
			|| modelText.StartsWith ("KP", StringComparison.Ordinal)
			|| modelText.StartsWith ("EP", StringComparison.Ordinal)
			|| modelText.StartsWith ("P1", StringComparison.Ordinal)
			|| modelText.StartsWith ("TP", StringComparison.Ordinal))
			{
			return DeviceType.Plug;
			}

		return DeviceType.Unknown;
		}
	}
