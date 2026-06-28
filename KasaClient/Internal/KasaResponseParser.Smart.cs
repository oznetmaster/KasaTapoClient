// Copyright © 2026 Neil Colvin.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// Adapted from python-kasa (https://github.com/python-kasa/python-kasa)
// Original work Copyright (c) python-kasa contributors, MIT License

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace KasaTapoClient.Internal;

internal static partial class KasaResponseParser
	{
	private static LightPresetState? CreateSmartLightPresetState (SmartParsedResponse response, LightState? lightState)
		{
		if (lightState is null)
			{
			return null;
			}

		SmartPresetRulesDto? presetRules = DeserializeModuleResult<SmartPresetRulesDto> (response, KasaCommands.SMART_GET_PRESET_RULES_METHOD);
		List<LightPresetDefinition> presets = CreateSmartLightPresetDefinitions (presetRules);
		if (presets.Count == 0)
			{
			return null;
			}

		string? activePreset = ResolveActiveLightPreset (presets, lightState.Brightness, lightState.ColorTemperature, lightState.Hue, lightState.Saturation);
		return new LightPresetState (presets, activePreset ?? "Not set");
		}

	private static List<LightPresetDefinition> CreateSmartLightPresetDefinitions (SmartPresetRulesDto? presetRules)
		{
		var presets = new List<LightPresetDefinition> ();
		if (presetRules?.States is List<LegacyLightPresetDto> states)
			{
			for (int index = 0; index < states.Count; index++)
				{
				LegacyLightPresetDto preset = states[index];
				if (preset.Brightness is null)
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
			}
		else if (presetRules?.BrightnessLevels is List<int> brightnessLevels)
			{
			for (int index = 0; index < brightnessLevels.Count; index++)
				{
				presets.Add (new LightPresetDefinition (
					$"Brightness preset {index + 1}",
					brightnessLevels[index],
					colorTemperature: null,
					hue: null,
					saturation: null,
					JsonSerializer.Serialize (brightnessLevels[index], JsonSupport.COMPACT_JSON)));
				}
			}

		return presets;
		}

	private static LightTransitionState? CreateSmartLightTransitionState (SmartParsedResponse response)
		{
		SmartOnOffGraduallyInfoDto? graduallyInfo = DeserializeModuleResult<SmartOnOffGraduallyInfoDto> (response, KasaCommands.SMART_GET_ON_OFF_GRADUALLY_INFO_METHOD);
		SmartOnOffGraduallyStateDto? onGraduallyState = graduallyInfo?.OnState;
		SmartOnOffGraduallyStateDto? offGraduallyState = graduallyInfo?.OffState;
		if (onGraduallyState is not null || offGraduallyState is not null)
			{
			bool? isEnabled = graduallyInfo?.Enable;
			if (isEnabled is null)
				{
				bool? onEnabled = onGraduallyState?.Enable;
				bool? offEnabled = offGraduallyState?.Enable;
				if (onEnabled == true || offEnabled == true)
					{
					isEnabled = true;
					}
				else if (onEnabled == false && offEnabled == false)
					{
					isEnabled = false;
					}
				}

			return new LightTransitionState (
				isEnabled,
				onGraduallyState?.Enable,
				onGraduallyState?.Duration,
				onGraduallyState?.MaximumDuration,
				offGraduallyState?.Enable,
				offGraduallyState?.Duration,
				offGraduallyState?.MaximumDuration,
				JsonSerializer.Serialize (graduallyInfo, JsonSupport.COMPACT_JSON));
			}

		if (graduallyInfo?.Enable is bool enabled)
			{
			int transitionSeconds = enabled ? SMART_LIGHT_TRANSITION_DEFAULT_MAXIMUM_SECONDS : 0;
			return new LightTransitionState (enabled, enabled, transitionSeconds, SMART_LIGHT_TRANSITION_DEFAULT_MAXIMUM_SECONDS, enabled, transitionSeconds, SMART_LIGHT_TRANSITION_DEFAULT_MAXIMUM_SECONDS, JsonSerializer.Serialize (graduallyInfo, JsonSupport.COMPACT_JSON));
			}

		using JsonDocument document = JsonDocument.Parse (response.RawJson);
		JsonElement root = document.RootElement;
		int? onTransition = TryGetNamedTransitionMilliseconds (root, "smooth_transition_on");
		int? offTransition = TryGetNamedTransitionMilliseconds (root, "smooth_transition_off");
		if (onTransition is null && offTransition is null && TryGetTransitionMilliseconds (root, out int? transitionMilliseconds))
			{
			onTransition = transitionMilliseconds;
			offTransition = transitionMilliseconds;
			}

		return onTransition is null && offTransition is null
			? null
			: new LightTransitionState (null, null, onTransition, null, null, offTransition, null, response.RawJson);
		}

	private static LightEffectState? CreateSmartBulbLightEffectState (SmartParsedResponse response)
		{
		SmartDynamicLightEffectRulesDto? effectRules = DeserializeModuleResult<SmartDynamicLightEffectRulesDto> (response, KasaCommands.SMART_GET_DYNAMIC_LIGHT_EFFECT_RULES_METHOD);
		if (effectRules?.RuleList is not List<SmartDynamicLightEffectRuleDto> ruleList || ruleList.Count == 0)
			{
			return null;
			}

		var availableEffects = new List<LightEffectDefinition> (ruleList.Count);
		var effectNamesById = new Dictionary<string, string> (StringComparer.Ordinal);
		var brightnessById = new Dictionary<string, int?> (StringComparer.Ordinal);
		foreach (SmartDynamicLightEffectRuleDto rule in ruleList)
			{
			string? effectId = rule.Id;
			if (string.IsNullOrWhiteSpace (effectId))
				{
				continue;
				}

			string effectName = DecodeSmartBulbEffectName (effectId!, rule.SceneName);
			availableEffects.Add (new LightEffectDefinition (effectId!, effectName));
			effectNamesById[effectId!] = effectName;
			brightnessById[effectId!] = GetSmartBulbEffectBrightness (rule);
			}

		bool isEnabled = response.DeviceInfo.LightingEffect?.Enable is int infoEnabled
			? infoEnabled != 0
			: response.DeviceInfo.LightingEffect is not null
				? true
				: effectRules.Enable == true;
		string? activeId = FirstNonEmpty (response.DeviceInfo.LightingEffect?.Id, response.DeviceInfo.LightingEffect?.Name, response.DeviceInfo.LightingEffect?.Enable == 0 ? null : effectRules.CurrentRuleId);
		string? activeName = FirstNonEmpty (
			response.DeviceInfo.LightingEffect?.Name,
			activeId is not null && effectNamesById.TryGetValue (activeId, out string? resolvedName) ? resolvedName : null);
		int? brightness = response.DeviceInfo.LightingEffect?.Brightness;
		if (brightness is null && activeId is not null && brightnessById.TryGetValue (activeId, out int? activeBrightness))
			{
			brightness = activeBrightness;
			}

		if (!isEnabled)
			{
			activeId = "Off";
			activeName = "Off";
			}

		return new LightEffectState (
			activeId,
			activeName,
			isEnabled,
			brightness,
			availableEffects,
			JsonSerializer.Serialize (effectRules, JsonSupport.COMPACT_JSON));
		}

	private static int? GetSmartBulbEffectBrightness (SmartDynamicLightEffectRuleDto rule)
		{
		if (rule.ColorStatusList is not List<List<int>> colorStatusList || colorStatusList.Count == 0)
			{
			return null;
			}

		List<int>? firstColorStatus = colorStatusList[0];
		return firstColorStatus is { Count: > 0 }
			? firstColorStatus[0]
			: null;
		}

	private static string DecodeSmartBulbEffectName (string effectId, string? rawSceneName)
		{
		if (string.IsNullOrWhiteSpace (rawSceneName))
			{
			return effectId switch
				{
					"L1" => "Party",
					"L2" => "Relax",
					_ => effectId,
				};
			}

		if (IsLikelyBase64Text (rawSceneName!))
			{
			try
				{
				return DecodeBase64Text (rawSceneName!);
				}
			catch
				{
				}
			}

		return rawSceneName!;
		}

	private static bool IsLikelyBase64Text (string value)
		{
		if (string.IsNullOrWhiteSpace (value) || value.Length % 4 != 0)
			{
			return false;
			}

		foreach (char character in value)
			{
			if ((character >= 'A' && character <= 'Z')
				|| (character >= 'a' && character <= 'z')
				|| (character >= '0' && character <= '9')
				|| character == '+'
				|| character == '/'
				|| character == '=')
				{
				continue;
				}

			return false;
			}

		return true;
		}

	private static string DecodeBase64Text (string value) => Encoding.UTF8.GetString (Convert.FromBase64String (value));

	private static LightStripEffectState? CreateLightStripEffectState (LightState? lightState, DeviceType deviceType)
		{
		if (deviceType is not DeviceType.LightStrip || lightState?.Effect is null)
			{
			return null;
			}

		return new LightStripEffectState (lightState.Effect, lightState.Effect.AvailableEffects);
		}

	private static AlarmState? CreateSmartAlarmState (SmartParsedResponse response)
		{
		if (!response.ComponentVersions.ContainsKey ("alarm"))
			{
			return null;
			}

		JsonObject? alarmResult = GetModuleResult (response.ModuleResults, "get_alarm_configure", "get_alarm_config", "get_alarm_info", "get_guard_mode");
		if (alarmResult is null)
			{
			return null;
			}

		using JsonDocument document = JsonDocument.Parse (alarmResult.ToJsonString ());
		JsonElement root = document.RootElement;
		bool? isActive = TryGetNamedBoolean (root, "in_alarm") ?? TryGetNamedBoolean (root, "alarm") ?? TryGetNamedBoolean (root, "guard_on");
		string? source = TryGetNamedString (root, "alarm_source") ?? TryGetNamedString (root, "guard_mode");
		string? sound = TryGetNamedString (root, "alarm_type") ?? TryGetNamedString (root, "alarm_sound") ?? TryGetNamedString (root, "type");
		string? volume = TryGetNamedString (root, "alarm_volume") ?? TryGetNamedString (root, "volume");
		int? volumeLevel = TryGetNamedInt32 (root, "alarm_volume_level");
		int? durationSeconds = TryGetNamedInt32 (root, "alarm_duration") ?? TryGetNamedInt32 (root, "duration");
		if (isActive is null && source is null && sound is null && volume is null && volumeLevel is null && durationSeconds is null)
			{
			return null;
			}

		return new AlarmState (isActive, source, sound, volume, volumeLevel, durationSeconds, alarmResult.ToJsonString ());
		}

	private static OverheatProtectionState? CreateSmartOverheatProtectionState (SmartParsedResponse response)
		{
		using JsonDocument document = JsonDocument.Parse (response.RawJson);
		bool? overheated = TryGetNamedBoolean (document.RootElement, "overheated");
		return overheated is null ? null : new OverheatProtectionState (overheated, response.RawJson);
		}

	private static PowerProtectionState? CreateSmartPowerProtectionState (SmartParsedResponse response)
		{
		using JsonDocument document = JsonDocument.Parse (response.RawJson);
		bool? protectionActive = TryGetNamedBoolean (document.RootElement, "power_protection")
			?? TryGetNamedBoolean (document.RootElement, "power_protect");
		return protectionActive is null ? null : new PowerProtectionState (protectionActive, response.RawJson);
		}

	private static FanState? CreateSmartFanState (SmartParsedResponse response, DeviceSystemInfo systemInfo)
		{
		if (systemInfo.DeviceType is not DeviceType.Fan)
			{
			return null;
			}

		return new FanState (systemInfo.IsOn, response.RawJson);
		}

	private static SpeakerState? CreateSmartSpeakerState (SmartParsedResponse response)
		{
		using JsonDocument document = JsonDocument.Parse (response.RawJson);
		bool? isAvailable = TryGetNamedBoolean (document.RootElement, "speaker")
			?? (TryGetNamedString (document.RootElement, "alarm_sound") is not null ? true : null);
		return isAvailable is null ? null : new SpeakerState (isAvailable, response.RawJson);
		}

	private static JsonObject? GetModuleResult (IReadOnlyDictionary<string, JsonObject> moduleResults, params string[] methodNames)
		{
		foreach (string methodName in methodNames)
			{
			if (moduleResults.TryGetValue (methodName, out JsonObject? result))
				{
				return result;
				}
			}

		return null;
		}

	private static bool? TryGetNamedBoolean (JsonElement element, string propertyName)
		{
		JsonElement? value = FindNamedElement (element, propertyName);
		if (value is null)
			{
			return null;
			}

		return value.Value.ValueKind switch
			{
				JsonValueKind.True => true,
				JsonValueKind.False => false,
				JsonValueKind.Number when value.Value.TryGetInt32 (out int intValue) => intValue != 0,
				JsonValueKind.String when bool.TryParse (value.Value.GetString (), out bool boolValue) => boolValue,
				JsonValueKind.String when int.TryParse (value.Value.GetString (), NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedValue) => parsedValue != 0,
				_ => null,
			};
		}

	private static string? TryGetNamedString (JsonElement element, string propertyName)
		{
		JsonElement? value = FindNamedElement (element, propertyName);
		if (value is null)
			{
			return null;
			}

		return value.Value.ValueKind switch
			{
				JsonValueKind.String => value.Value.GetString (),
				JsonValueKind.Number => value.Value.ToString (),
				JsonValueKind.True => bool.TrueString,
				JsonValueKind.False => bool.FalseString,
				_ => null,
			};
		}

	private static int? TryGetNamedInt32 (JsonElement element, string propertyName)
		{
		JsonElement? value = FindNamedElement (element, propertyName);
		if (value is null)
			{
			return null;
			}

		if (value.Value.ValueKind == JsonValueKind.Number && value.Value.TryGetInt32 (out int intValue))
			{
			return intValue;
			}

		return value.Value.ValueKind == JsonValueKind.String
			&& int.TryParse (value.Value.GetString (), NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedValue)
			? parsedValue
			: null;
		}

	private static JsonElement? FindNamedElement (JsonElement element, string propertyName)
		{
		if (element.ValueKind == JsonValueKind.Object)
			{
			foreach (JsonProperty property in element.EnumerateObject ())
				{
				if (string.Equals (property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
					{
					return property.Value;
					}

				JsonElement? nested = FindNamedElement (property.Value, propertyName);
				if (nested is not null)
					{
					return nested;
					}
				}
			}
		else if (element.ValueKind == JsonValueKind.Array)
			{
			foreach (JsonElement child in element.EnumerateArray ())
				{
				JsonElement? nested = FindNamedElement (child, propertyName);
				if (nested is not null)
					{
					return nested;
					}
				}
			}

		return null;
		}

	private static bool TryGetTransitionMilliseconds (JsonElement element, out int? transitionMilliseconds)
		{
		transitionMilliseconds = TryGetNamedTransitionMilliseconds (element, "transition_period");
		return transitionMilliseconds is not null;
		}

	private static int? TryGetNamedTransitionMilliseconds (JsonElement element, string propertyName)
		{
		if (element.ValueKind == JsonValueKind.Object)
			{
			foreach (JsonProperty property in element.EnumerateObject ())
				{
				if (string.Equals (property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
					{
					if (property.Value.ValueKind == JsonValueKind.Number && property.Value.TryGetInt32 (out int intValue))
						{
						return intValue;
						}

					if (property.Value.ValueKind == JsonValueKind.String
						&& int.TryParse (property.Value.GetString (), NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedValue))
						{
						return parsedValue;
						}
					}

				int? nested = TryGetNamedTransitionMilliseconds (property.Value, propertyName);
				if (nested is not null)
					{
					return nested;
					}
				}
			}
		else if (element.ValueKind == JsonValueKind.Array)
			{
			foreach (JsonElement child in element.EnumerateArray ())
				{
				int? nested = TryGetNamedTransitionMilliseconds (child, propertyName);
				if (nested is not null)
					{
					return nested;
					}
				}
			}

		return null;
		}

	private static DeviceSystemInfo CreateSmartSystemInfo (SmartParsedResponse response)
		{
		SmartDeviceInfoDto info = response.DeviceInfo;
		IReadOnlyList<ChildDeviceInfo> children = CreateSmartChildren (response.ChildDeviceList, response.ChildComponentIds, response.ChildOverrides, response.RawJson);
		DeviceType deviceType = DetermineSmartDeviceType (response.ComponentIds, info.Type);
		bool? isOn = info.DeviceOn;
		if (isOn is null && deviceType == DeviceType.Hub)
			{
			isOn = false;
			}
		return new DeviceSystemInfo (
			DecodeSmartAlias (info.Nickname),
			info.Model,
			info.DeviceId,
			info.Mac,
			info.HardwareVersion,
			info.FirmwareVersion,
			info.SignalLevel,
			info.Rssi,
			info.Ssid,
			deviceType,
			isOn,
			info.OnTimeSeconds is int onTimeSeconds ? TimeSpan.FromSeconds (onTimeSeconds) : null,
			children,
			response.RawJson);
		}

	private static EnergyUsage? CreateSmartEnergyUsage (SmartParsedResponse response)
		{
		SmartEnergyUsageDto? energyUsage = DeserializeModuleResult<SmartEnergyUsageDto> (response, KasaCommands.SMART_GET_ENERGY_USAGE_METHOD);
		SmartCurrentPowerDto? currentPower = DeserializeModuleResult<SmartCurrentPowerDto> (response, KasaCommands.SMART_GET_CURRENT_POWER_METHOD);
		SmartEmeterDataDto? emeterData = DeserializeModuleResult<SmartEmeterDataDto> (response, KasaCommands.SMART_GET_EMETER_DATA_METHOD);

		double? currentPowerWatts = ReadScaledDouble (currentPower?.CurrentPowerWatts, emeterData?.PowerMilliwatts, 1000d)
			?? ReadScaledDouble (null, energyUsage?.CurrentPower, 1000d);
		double? voltageVolts = ReadScaledDouble (null, emeterData?.VoltageMillivolts, 1000d);
		double? currentAmps = ReadScaledDouble (null, emeterData?.CurrentMilliamps, 1000d);
		double? totalKilowattHours = ReadScaledDouble (null, energyUsage?.TodayEnergyWattHours, 1000d);

		if (currentPowerWatts is null
			&& voltageVolts is null
			&& currentAmps is null
			&& totalKilowattHours is null)
			{
			return null;
			}

		JsonObject rawEnergy = new ()
			{
			["get_energy_usage"] = SerializeModuleResultNode (energyUsage),
			["get_current_power"] = SerializeModuleResultNode (currentPower),
			["get_emeter_data"] = SerializeModuleResultNode (emeterData),
			};
		return new EnergyUsage (currentPowerWatts, voltageVolts, currentAmps, totalKilowattHours, todayKilowattHours: null, monthKilowattHours: null, rawEnergy.ToJsonString (JsonSupport.COMPACT_JSON));
		}

	private static FirmwareState? CreateSmartFirmwareState (SmartParsedResponse response)
		{
		SmartAutoUpdateInfoDto? autoUpdate = DeserializeModuleResult<SmartAutoUpdateInfoDto> (response, KasaCommands.SMART_GET_AUTO_UPDATE_INFO_METHOD);
		if (response.DeviceInfo.FirmwareVersion is null
			&& response.DeviceInfo.HardwareVersion is null
			&& autoUpdate?.Enable is null
			&& !response.ModuleResults.ContainsKey (KasaCommands.SMART_GET_LATEST_FW_METHOD))
			{
			return null;
			}

		SmartLatestFirmwareDto? latestFirmware = DeserializeModuleResult<SmartLatestFirmwareDto> (response, KasaCommands.SMART_GET_LATEST_FW_METHOD);
		bool? updateAvailable = latestFirmware?.Type is int type ? type != 0 : null;

		return new FirmwareState (
			response.DeviceInfo.FirmwareVersion,
			response.DeviceInfo.HardwareVersion,
			autoUpdate?.Enable,
			latestFirmware?.FirmwareVersion,
			updateAvailable,
			response.RawJson);
		}

	private static CloudConnectionState? CreateSmartCloudConnectionState (SmartParsedResponse response)
		{
		SmartCloudConnectStateDto? cloudState = DeserializeModuleResult<SmartCloudConnectStateDto> (response, KasaCommands.SMART_GET_CONNECT_CLOUD_STATE_METHOD);
		if (cloudState?.Status is not int status)
			{
			return null;
			}

		return new CloudConnectionState (status == 0, isProvisioned: null, server: null, userName: null, JsonSerializer.Serialize (cloudState, JsonSupport.COMPACT_JSON));
		}

	private static DeviceTimeState? CreateSmartDeviceTimeState (SmartParsedResponse response)
		{
		SmartDeviceTimeDto? time = DeserializeModuleResult<SmartDeviceTimeDto> (response, KasaCommands.SMART_GET_DEVICE_TIME_METHOD);
		if (time?.Timestamp is not long timestamp)
			{
			return null;
			}

		DateTime localTime = DateTimeOffset.FromUnixTimeSeconds (timestamp).LocalDateTime;
		return new DeviceTimeState (localTime, time.Region, time.TimeDifferenceMinutes, JsonSerializer.Serialize (time, JsonSupport.COMPACT_JSON));
		}

	private static MatterSetupInfo? CreateSmartMatterSetupInfo (SmartParsedResponse response)
		{
		SmartMatterSetupDto? matter = DeserializeModuleResult<SmartMatterSetupDto> (response, KasaCommands.SMART_GET_MATTER_SETUP_INFO_METHOD);
		if (matter?.SetupCode is null && matter?.SetupPayload is null)
			{
			return null;
			}

		return new MatterSetupInfo (matter.SetupCode, matter.SetupPayload, JsonSerializer.Serialize (matter, JsonSupport.COMPACT_JSON));
		}

	private static HomeKitSetupInfo? CreateSmartHomeKitSetupInfo (SmartParsedResponse response)
		{
		SmartHomeKitInfoDto? homeKit = DeserializeModuleResult<SmartHomeKitInfoDto> (response, KasaCommands.SMART_GET_HOMEKIT_INFO_METHOD);
		if (homeKit?.SetupCode is null)
			{
			return null;
			}

		return new HomeKitSetupInfo (homeKit.SetupCode, setupPayload: null, JsonSerializer.Serialize (homeKit, JsonSupport.COMPACT_JSON));
		}

	private static AutoOffState? CreateSmartAutoOffState (SmartParsedResponse response)
		{
		SmartAutoOffConfigDto? autoOff = DeserializeModuleResult<SmartAutoOffConfigDto> (response, KasaCommands.SMART_GET_AUTO_OFF_CONFIG_METHOD);
		if (autoOff?.Enable is null && autoOff?.DelayMinutes is null)
			{
			return null;
			}

		return new AutoOffState (autoOff.Enable, autoOff.DelayMinutes, timerActive: null, autoOffAt: null, JsonSerializer.Serialize (autoOff, JsonSupport.COMPACT_JSON));
		}

	private static LedState? CreateSmartLedState (SmartParsedResponse response)
		{
		SmartLedInfoDto? led = DeserializeModuleResult<SmartLedInfoDto> (response, KasaCommands.SMART_GET_LED_INFO_METHOD);
		if (led?.LedRule is null)
			{
			return null;
			}

		LedNightModeSettings? nightMode = led.StartTime is null
			&& led.EndTime is null
			&& led.NightModeType is null
			&& led.SunriseOffset is null
			&& led.SunsetOffset is null
			? null
			: new LedNightModeSettings (led.StartTime, led.EndTime, led.NightModeType, led.SunriseOffset, led.SunsetOffset);
		return new LedState (led.LedRule != "never", led.LedRule, nightMode, JsonSerializer.Serialize (led, JsonSupport.COMPACT_JSON));
		}

	private static ChildLockState? CreateSmartChildLockState (SmartParsedResponse response)
		{
		SmartChildLockInfoDto? childLock = DeserializeModuleResult<SmartChildLockInfoDto> (response, KasaCommands.SMART_GET_CHILD_LOCK_INFO_METHOD);
		if (childLock?.ChildLockStatus is null)
			{
			return null;
			}

		return new ChildLockState (childLock.ChildLockStatus, JsonSerializer.Serialize (childLock, JsonSupport.COMPACT_JSON));
		}

	private static TDto? DeserializeModuleResult<TDto> (SmartParsedResponse response, string method)
		where TDto : class
		{
		if (!response.ModuleResults.TryGetValue (method, out JsonObject? result))
			{
			return null;
			}

		return JsonSerializer.Deserialize<TDto> (result.ToJsonString (JsonSupport.COMPACT_JSON), JsonSupport.COMPACT_JSON);
		}

	private static JsonNode? SerializeModuleResultNode<TDto> (TDto? value)
		where TDto : class
		{
		return value is null
			? null
			: JsonSerializer.SerializeToNode (value, JsonSupport.COMPACT_JSON);
		}

	private static SmartEnvelopeResultDto? DeserializeSmartEnvelopeResult (JsonObject? resultObject)
		{
		if (resultObject is null)
			{
			return null;
			}

		return JsonSerializer.Deserialize<SmartEnvelopeResultDto> (resultObject.ToJsonString (JsonSupport.COMPACT_JSON), JsonSupport.COMPACT_JSON);
		}

	private static IReadOnlyList<ChildDeviceInfo> CreateSmartChildren (
		SmartChildDeviceListDto? childDeviceList,
		IReadOnlyDictionary<string, IReadOnlyList<string>> childComponentIds,
		IReadOnlyDictionary<string, SmartChildDeviceDto> childOverrides,
		string rawJson)
		{
		if (childDeviceList?.ChildDevices is not IReadOnlyList<SmartChildDeviceDto> children || children.Count == 0)
			{
			return Array.Empty<ChildDeviceInfo> ();
			}

		var result = new List<ChildDeviceInfo> (children.Count);
		foreach (SmartChildDeviceDto child in children)
			{
			string? id = child.DeviceId;
			if (string.IsNullOrWhiteSpace (id))
				{
				continue;
				}

			IReadOnlyList<string> componentIds = childComponentIds.TryGetValue (id!, out IReadOnlyList<string>? parsedComponentIds)
				? parsedComponentIds
				: Array.Empty<string> ();
			SmartChildDeviceDto effectiveChild = childOverrides.TryGetValue (id!, out SmartChildDeviceDto? childOverride)
				? childOverride
				: child;
			string childRawJson = JsonSerializer.Serialize (effectiveChild, JsonSupport.COMPACT_JSON);

			result.Add (new ChildDeviceInfo (
				id!,
				DecodeSmartAlias (effectiveChild.Nickname),
				effectiveChild.Model,
				DetermineSmartChildDeviceType (effectiveChild.Category, effectiveChild.Model),
				effectiveChild.DeviceOn,
				childRawJson,
				effectiveChild.Category,
				componentIds,
				CreateSmartChildFeatures (effectiveChild, componentIds)));
			}

		return result;
		}

	internal static string DecodeSmartAlias (string? alias)
		{
		if (string.IsNullOrWhiteSpace (alias))
			{
			return string.Empty;
			}

		string aliasText = alias!;

		if (!LooksLikeBase64 (aliasText))
			{
			return aliasText;
			}

		try
			{
			byte[] decodedBytes = Convert.FromBase64String (aliasText);
			string decodedAlias = Encoding.UTF8.GetString (decodedBytes);
			return string.IsNullOrWhiteSpace (decodedAlias)
				? aliasText
				: decodedAlias;
			}
		catch (FormatException)
			{
			return aliasText;
			}
		catch (ArgumentException)
			{
			return aliasText;
			}
		}

	private static bool LooksLikeBase64 (string value)
		{
		if (value.Length == 0 || value.Length % 4 != 0)
			{
			return false;
			}

		foreach (char character in value)
			{
			if ((character >= 'A' && character <= 'Z')
				|| (character >= 'a' && character <= 'z')
				|| (character >= '0' && character <= '9')
				|| character == '+'
				|| character == '/'
				|| character == '=')
				{
				continue;
				}

			return false;
			}

		return true;
		}

	private static LightState? CreateSmartLightState (SmartParsedResponse response, LightEffectState? smartBulbEffect)
		{
		if (!HasSmartLightComponent (response.ComponentIds, response.DeviceInfo.Type))
			{
			return null;
			}

		SmartDeviceInfoDto info = response.DeviceInfo;
		HsvColor? hsv = info.Brightness is int brightness && info.Hue is int hue && info.Saturation is int saturation
			? new HsvColor (hue, saturation, brightness)
			: null;
		bool supportsEffects = SupportsSmartLightEffects (response.ComponentIds);
		IReadOnlyList<LightEffectDefinition> availableEffects = supportsEffects && response.ComponentIds.Contains ("light_strip")
			? SMART_LIGHT_STRIP_EFFECTS
			: Array.Empty<LightEffectDefinition> ();
		LightEffectState? effect = null;
		if (response.ComponentIds.Contains ("light_effect"))
			{
			effect = smartBulbEffect;
			}
		else if (supportsEffects)
			{
			LegacyLightingEffectDto? lightingEffect = info.LightingEffect;
			effect = new LightEffectState (
				FirstNonEmpty (lightingEffect?.Id, lightingEffect?.Name),
				lightingEffect?.Name,
				lightingEffect?.Enable is int enabled ? enabled != 0 : null,
				lightingEffect?.Brightness ?? info.Brightness,
				availableEffects,
				lightingEffect is null ? response.RawJson : JsonSerializer.Serialize (lightingEffect, JsonSupport.COMPACT_JSON));
			}

		return new LightState (
			info.DeviceOn,
			effect?.IsEnabled == true && effect.Brightness is int effectBrightness ? effectBrightness : info.Brightness,
			info.ColorTemperature,
			info.Hue,
			info.Saturation,
			supportsEffects,
			effect,
			hsv,
			Array.Empty<LightPresetDefinition> (),
			null,
			response.RawJson);
		}

	private static bool SupportsSmartLightEffects (IReadOnlyList<string> componentIds)
		{
		return componentIds.Contains ("light_strip")
			|| componentIds.Contains ("light_effect")
			|| componentIds.Contains ("dynamic_light_effect");
		}

	private static DeviceType DetermineSmartDeviceType (IReadOnlyList<string> componentIds, string? rawDeviceType)
		{
		string deviceType = (rawDeviceType ?? string.Empty).ToUpperInvariant ();
		if (deviceType.Contains ("HUB"))
			{
			return DeviceType.Hub;
			}

		if (deviceType.Contains ("PLUG"))
			{
			return componentIds.Contains ("child_device")
				? DeviceType.Strip
				: DeviceType.Plug;
			}

		if (componentIds.Contains ("light_strip"))
			{
			return DeviceType.LightStrip;
			}

		if (deviceType.Contains ("SWITCH") && componentIds.Contains ("child_device"))
			{
			return DeviceType.WallSwitch;
			}

		if (componentIds.Contains ("dimmer_calibration"))
			{
			return DeviceType.Dimmer;
			}

		if (componentIds.Contains ("brightness"))
			{
			return DeviceType.Bulb;
			}

		if (deviceType.Contains ("SWITCH"))
			{
			return DeviceType.WallSwitch;
			}

		if (deviceType.Contains ("SENSOR"))
			{
			return DeviceType.Sensor;
			}

		if (deviceType.Contains ("ENERGY"))
			{
			return DeviceType.Thermostat;
			}

		if (deviceType.Contains ("ROBOVAC"))
			{
			return DeviceType.Vacuum;
			}

		if (deviceType.Contains ("TAPOCHIME"))
			{
			return DeviceType.Chime;
			}

		return DeviceType.Plug;
		}

	private static DeviceType DetermineSmartChildDeviceType (string? category, string? model)
		{
		string categoryText = (category ?? string.Empty).Trim ();
		return categoryText switch
			{
				"subg.plugswitch.switch" => DeviceType.WallSwitch,
				"subg.trigger.contact-sensor" => DeviceType.Sensor,
				"subg.trigger.temp-hmdt-sensor" => DeviceType.Sensor,
				"subg.trigger.water-leak-sensor" => DeviceType.Sensor,
				"subg.trigger.motion-sensor" => DeviceType.Sensor,
				"kasa.switch.outlet.sub-fan" => DeviceType.Fan,
				"kasa.switch.outlet.sub-dimmer" => DeviceType.Dimmer,
				"subg.trv" => DeviceType.Thermostat,
				"subg.trigger.button" => DeviceType.Sensor,
				_ => DetermineChildDeviceType (model),
			};
		}

	private static List<DeviceFeature> CreateSmartChildFeatures (SmartChildDeviceDto child, IReadOnlyList<string> componentIds)
		{
		var features = new List<DeviceFeature> ();
		HashSet<string> components = [.. componentIds];

		if (child.SignalLevel is int signalLevel)
			{
			features.Add (new DeviceFeature ("signal_level", "Signal Level", FeatureKind.Number, signalLevel.ToString (CultureInfo.InvariantCulture)));
			}

		if (!string.IsNullOrWhiteSpace (child.Status))
			{
			features.Add (new DeviceFeature (
				"cloud_connection",
				"Cloud connection",
				FeatureKind.Switch,
				string.Equals (child.Status, "online", StringComparison.OrdinalIgnoreCase)));
			}

		if (child.BatteryPercentage is int batteryLevel)
			{
			features.Add (new DeviceFeature ("battery_level", "Battery level", FeatureKind.Number, batteryLevel.ToString (CultureInfo.InvariantCulture), "%"));
			}

		if (child.AtLowBattery is bool atLowBattery)
			{
			features.Add (new DeviceFeature ("battery_low", "Battery low", FeatureKind.Switch, atLowBattery));
			}
		else if (child.IsLowBattery is bool isLowBattery)
			{
			features.Add (new DeviceFeature ("battery_low", "Battery low", FeatureKind.Switch, isLowBattery));
			}

		if (child.CurrentHumidity is int humidity)
			{
			features.Add (new DeviceFeature ("humidity", "Humidity", FeatureKind.Number, humidity.ToString (CultureInfo.InvariantCulture), "%"));
			}

		if (child.CurrentHumidityException is int humidityWarning)
			{
			features.Add (new DeviceFeature ("humidity_warning", "Humidity warning", FeatureKind.Switch, humidityWarning != 0));
			}

		if (child.CurrentTemperature is double temperature)
			{
			features.Add (new DeviceFeature (
				"temperature",
				"Temperature",
				FeatureKind.Number,
				temperature.ToString (CultureInfo.InvariantCulture),
				child.TemperatureUnit ?? "celsius"));
			}

		if (child.CurrentTemperatureException is int temperatureWarning)
			{
			features.Add (new DeviceFeature ("temperature_warning", "Temperature warning", FeatureKind.Switch, temperatureWarning != 0));
			}

		if (!string.IsNullOrWhiteSpace (child.TemperatureUnit))
			{
			features.Add (new DeviceFeature (
				"temperature_unit",
				"Temperature unit",
				FeatureKind.Choice,
				child.TemperatureUnit,
				choices: ["celsius", "fahrenheit"]));
			}

		if (child.ReportInterval is int reportInterval)
			{
			features.Add (new DeviceFeature ("report_interval", "Report interval", FeatureKind.Number, reportInterval.ToString (CultureInfo.InvariantCulture), "s"));
			}

		if (child.DoubleClickInfo?.Enable is bool doubleClickEnabled)
			{
			features.Add (new DeviceFeature ("double_click_enabled", "Double click enabled", FeatureKind.Switch, doubleClickEnabled));
			}

		if (child.Detected is bool detected)
			{
			features.Add (new DeviceFeature ("motion_detected", "Motion detected", FeatureKind.Switch, detected));
			}

		if (child.Open is bool isOpen)
			{
			features.Add (new DeviceFeature ("is_open", "Open", FeatureKind.Switch, isOpen));
			}

		if (child.InAlarm is bool inAlarm)
			{
			features.Add (new DeviceFeature ("water_alert", "Water alert", FeatureKind.Switch, inAlarm));
			}

		if (!string.IsNullOrWhiteSpace (child.WaterLeakStatus))
			{
			features.Add (new DeviceFeature ("water_leak", "Water leak", FeatureKind.Info, child.WaterLeakStatus));
			}

		if (child.TriggerTimestamp is long triggerTimestamp)
			{
			features.Add (new DeviceFeature ("water_alert_timestamp", "Last alert timestamp", FeatureKind.Info, triggerTimestamp.ToString (CultureInfo.InvariantCulture)));
			}

		if (child.FrostProtection?.MinimumTemperature is int frostProtectionMinimumTemperature)
			{
			features.Add (new DeviceFeature (
				"frost_protection_minimum_temperature",
				"Frost protection minimum temperature",
				FeatureKind.Number,
				frostProtectionMinimumTemperature.ToString (CultureInfo.InvariantCulture),
				child.FrostProtection.TemperatureUnit ?? child.TemperatureUnit ?? "celsius"));
			}

		if (child.FrostProtectionOn is bool frostProtectionOn)
			{
			features.Add (new DeviceFeature ("frost_protection_on", "Frost protection", FeatureKind.Switch, frostProtectionOn));
			}

		if (child.TargetTemperature is double targetTemperature)
			{
			features.Add (new DeviceFeature (
				"target_temperature",
				"Target temperature",
				FeatureKind.Number,
				targetTemperature.ToString (CultureInfo.InvariantCulture),
				child.TemperatureUnit ?? "celsius"));
			}

		if (child.MinimumControlTemperature is int minimumControlTemperature)
			{
			features.Add (new DeviceFeature (
				"minimum_target_temperature",
				"Minimum target temperature",
				FeatureKind.Number,
				minimumControlTemperature.ToString (CultureInfo.InvariantCulture),
				child.TemperatureUnit ?? "celsius"));
			}

		if (child.MaximumControlTemperature is int maximumControlTemperature)
			{
			features.Add (new DeviceFeature (
				"maximum_target_temperature",
				"Maximum target temperature",
				FeatureKind.Number,
				maximumControlTemperature.ToString (CultureInfo.InvariantCulture),
				child.TemperatureUnit ?? "celsius"));
			}

		if (child.TemperatureOffset is int temperatureOffset)
			{
			features.Add (new DeviceFeature (
				"temperature_offset",
				"Temperature offset",
				FeatureKind.Number,
				temperatureOffset.ToString (CultureInfo.InvariantCulture),
				child.TemperatureUnit ?? "celsius"));
			}

		if (child.ChildProtection is bool childProtection)
			{
			features.Add (new DeviceFeature ("child_protection", "Child protection", FeatureKind.Switch, childProtection));
			}

		if (child.ComfortTemperatureConfig?.MinValue is double minimumComfortTemperature)
			{
			features.Add (new DeviceFeature ("minimum_comfort_temperature", "Minimum comfort temperature", FeatureKind.Number, minimumComfortTemperature.ToString (CultureInfo.InvariantCulture), child.TemperatureUnit ?? "celsius"));
			}

		if (child.ComfortTemperatureConfig?.MaxValue is double maximumComfortTemperature)
			{
			features.Add (new DeviceFeature ("maximum_comfort_temperature", "Maximum comfort temperature", FeatureKind.Number, maximumComfortTemperature.ToString (CultureInfo.InvariantCulture), child.TemperatureUnit ?? "celsius"));
			}

		if (child.ComfortHumidityConfig?.MinValue is double minimumComfortHumidity)
			{
			features.Add (new DeviceFeature ("minimum_comfort_humidity", "Minimum comfort humidity", FeatureKind.Number, minimumComfortHumidity.ToString (CultureInfo.InvariantCulture), "%"));
			}

		if (child.ComfortHumidityConfig?.MaxValue is double maximumComfortHumidity)
			{
			features.Add (new DeviceFeature ("maximum_comfort_humidity", "Maximum comfort humidity", FeatureKind.Number, maximumComfortHumidity.ToString (CultureInfo.InvariantCulture), "%"));
			}

		if (child.TrvStates is IReadOnlyList<string> trvStates && trvStates.Count > 0)
			{
			features.Add (new DeviceFeature ("thermostat_mode", "Thermostat mode", FeatureKind.Info, string.Join (", ", trvStates)));
			}

		if (!string.IsNullOrWhiteSpace (child.FirmwareVersion))
			{
			features.Add (new DeviceFeature ("current_firmware_version", "Current firmware version", FeatureKind.Info, child.FirmwareVersion));
			features.Add (new DeviceFeature ("available_firmware_version", "Available firmware version", FeatureKind.Info, value: null));
			features.Add (new DeviceFeature ("update_available", "Update available", FeatureKind.Switch, value: null));
			features.Add (new DeviceFeature ("check_latest_firmware", "Check latest firmware", FeatureKind.Action, value: null));
			}

		if (child.Rssi is int rssi)
			{
			features.Add (new DeviceFeature ("rssi", "RSSI", FeatureKind.Number, rssi.ToString (CultureInfo.InvariantCulture), "dBm"));
			}

		features.Add (new DeviceFeature ("reboot", "Reboot", FeatureKind.Action, value: null));
		features.Add (new DeviceFeature ("unpair", "Unpair device", FeatureKind.Action, value: null));

		return features;
		}

	private static bool HasSmartLightComponent (IReadOnlyList<string> componentIds, string? rawDeviceType)
		{
		return componentIds.Contains ("brightness")
			|| componentIds.Contains ("light_strip")
			|| HasOrdinalIgnoreCaseText (rawDeviceType, "BULB");
		}

	private static bool HasOrdinalIgnoreCaseText (string? value, string text)
		{
		string source = value ?? string.Empty;
#if NET10_0_OR_GREATER
		return source.Contains (text, StringComparison.OrdinalIgnoreCase);
#else
		return source.ToUpperInvariant ().Contains (text.ToUpperInvariant ());
#endif
		}
	}
