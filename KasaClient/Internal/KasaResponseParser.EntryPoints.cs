// Copyright © 2026 Neil Colvin.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// Behavior modeled after the independent python-kasa project (https://github.com/python-kasa/python-kasa)
// for protocol/compatibility reference only; no python-kasa source was copied. See ATTRIBUTIONS.md.

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;

namespace KasaTapoClient.Internal;

internal static partial class KasaResponseParser
	{
	public static ParsedResponse ParseResponse (string responseJson)
		{
		LegacyResponseDto response = DeserializeResponse (responseJson);
		LegacySystemInfoDto systemInfo = response.System?.GetSystemInfo
			?? throw new InvalidDataException ("The device response did not contain system.get_sysinfo data.");
		return new ParsedResponse (
			systemInfo,
			response.Emeter ?? response.SmartEmeter,
			(response.Emeter ?? response.SmartEmeter)?.GetRealtime,
			(response.CountDown ?? response.BulbCountDown)?.GetRules,
			(response.Schedule ?? response.SmartSchedule)?.GetRules,
			(response.AntiTheft ?? response.SmartAntiTheft)?.GetRules,
			response.Time ?? response.SmartTime,
			response.Cloud ?? response.SmartCloud,
			response.HomeKit);
		}

	internal static ParsedResponse ParseModuleResponse (string responseJson, LegacySystemInfoDto systemInfo)
		{
		LegacyResponseDto response = DeserializeResponse (responseJson);
		return new ParsedResponse (
			systemInfo,
			response.Emeter ?? response.SmartEmeter,
			(response.Emeter ?? response.SmartEmeter)?.GetRealtime,
			(response.CountDown ?? response.BulbCountDown)?.GetRules,
			(response.Schedule ?? response.SmartSchedule)?.GetRules,
			(response.AntiTheft ?? response.SmartAntiTheft)?.GetRules,
			response.Time ?? response.SmartTime,
			response.Cloud ?? response.SmartCloud,
			response.HomeKit);
		}

	internal static SmartParsedResponse ParseSmartResponse (string responseJson)
		{
		SmartEnvelopeDto root = WireJson.Read<SmartEnvelopeDto> (responseJson);
		List<SmartMethodResponseDto> responses = root.Result?.Responses
			?? throw new InvalidDataException ("The smart device response did not contain result.responses.");

		SmartEnvelopeResultDto? deviceInfoResult = null;
		SmartEnvelopeResultDto? componentResult = null;
		SmartEnvelopeResultDto? childDeviceListResult = null;
		SmartEnvelopeResultDto? childComponentListResult = null;
		Dictionary<string, object> moduleResults = new (StringComparer.Ordinal);
		foreach (SmartMethodResponseDto responseObject in responses)
			{
			string? method = responseObject.Method;
			object? resultObject = responseObject.Result;
			if (!string.IsNullOrWhiteSpace (method) && resultObject is not null)
				{
				moduleResults[method!] = resultObject;
				}

			if (string.Equals (method, KasaCommands.SMART_GET_DEVICE_INFO_METHOD, StringComparison.Ordinal))
				{
				deviceInfoResult = resultObject as SmartEnvelopeResultDto;
				}
			else if (string.Equals (method, KasaCommands.SMART_COMPONENT_NEGO_METHOD, StringComparison.Ordinal))
				{
				componentResult = resultObject as SmartEnvelopeResultDto;
				}
			else if (string.Equals (method, KasaCommands.SMART_GET_CHILD_DEVICE_LIST_METHOD, StringComparison.Ordinal))
				{
				childDeviceListResult = resultObject as SmartEnvelopeResultDto;
				}
			else if (string.Equals (method, KasaCommands.SMART_GET_CHILD_DEVICE_COMPONENT_LIST_METHOD, StringComparison.Ordinal))
				{
				childComponentListResult = resultObject as SmartEnvelopeResultDto;
				}
			}

		SmartEnvelopeResultDto deviceInfo = deviceInfoResult
			?? throw new InvalidDataException ("The smart device response did not contain get_device_info data.");
		SmartEnvelopeResultDto componentInfo = componentResult
			?? throw new InvalidDataException ("The smart device response did not contain component_nego data.");

		var componentIds = new List<string> ();
		Dictionary<string, int> componentVersions = new (StringComparer.Ordinal);
		if (componentInfo.ComponentList is List<SmartComponentDto> componentList)
			{
			foreach (SmartComponentDto component in componentList)
				{
				if (!string.IsNullOrWhiteSpace (component.Id))
					{
					componentIds.Add (component.Id!);
					componentVersions[component.Id!] = component.VersionCode ?? 0;
					}
				}
			}

		SmartChildDeviceListDto? childDeviceList = null;
		if (childDeviceListResult?.ChildDeviceList is List<SmartChildDeviceDto> children)
			{
			childDeviceList = new SmartChildDeviceListDto (children, childDeviceListResult.Sum, childDeviceListResult.StartIndex);
			}

		Dictionary<string, IReadOnlyList<string>> childComponentIds = new (StringComparer.OrdinalIgnoreCase);
		if (childComponentListResult?.ChildComponentList is List<SmartChildComponentDto> childComponents)
			{
			foreach (SmartChildComponentDto childComponent in childComponents)
				{
				if (string.IsNullOrWhiteSpace (childComponent.DeviceId))
					{
					continue;
					}

				var componentListForChild = new List<string> ();
				if (childComponent.ComponentList is List<SmartComponentDto> childComponentEntries)
					{
					foreach (SmartComponentDto component in childComponentEntries)
						{
						if (!string.IsNullOrWhiteSpace (component.Id))
							{
							componentListForChild.Add (component.Id!);
							}
						}
					}

				childComponentIds[childComponent.DeviceId!] = componentListForChild;
				}
			}

		return new SmartParsedResponse (
			new SmartDeviceInfoDto (
				deviceInfo.Model,
				deviceInfo.Type,
				deviceInfo.DeviceId,
				deviceInfo.Nickname,
				deviceInfo.DeviceOn,
				deviceInfo.FirmwareVersion,
				deviceInfo.HardwareVersion,
				deviceInfo.Mac,
				deviceInfo.SignalLevel,
				deviceInfo.Rssi,
				deviceInfo.Ssid,
				deviceInfo.Specs,
				deviceInfo.DeviceCategoryList,
				deviceInfo.Brightness,
				deviceInfo.Hue,
				deviceInfo.Saturation,
				deviceInfo.ColorTemperature,
				deviceInfo.LightingEffect,
				deviceInfo.Overheated,
				deviceInfo.PowerProtection,
				deviceInfo.PowerProtect,
				deviceInfo.Speaker,
				deviceInfo.SmoothTransitionOn,
				deviceInfo.SmoothTransitionOff,
				deviceInfo.TransitionPeriod),
			componentIds,
			componentVersions,
			childDeviceList,
			childComponentIds,
			new Dictionary<string, SmartChildDeviceDto> (StringComparer.OrdinalIgnoreCase),
			moduleResults);
		}

	internal static SmartChildDeviceListDto? ParseSmartChildDeviceListPage (string responseJson)
		{
		SmartEnvelopeResultDto? page = WireJson.Read<SmartEnvelopeDto> (responseJson).Result;
		return page?.ChildDeviceList is List<SmartChildDeviceDto> children ? new SmartChildDeviceListDto (children, page.Sum, page.StartIndex) : null;
		}

	internal static IReadOnlyDictionary<string, object> ParseSmartModuleResults (string responseJson)
		{
		SmartEnvelopeDto root = WireJson.Read<SmartEnvelopeDto> (responseJson);
		List<SmartMethodResponseDto> responses = root.Result?.Responses
			?? throw new InvalidDataException ("The smart device response did not contain result.responses.");

		Dictionary<string, object> moduleResults = new (StringComparer.Ordinal);
		foreach (SmartMethodResponseDto responseObject in responses)
			{
			string? method = responseObject.Method;
			object? resultObject = responseObject.Result;
			if (!string.IsNullOrWhiteSpace (method) && resultObject is not null)
				{
				moduleResults[method!] = resultObject;
				}
			}

		return moduleResults;
		}

	internal static object? ParseSmartModuleResult (string responseJson, string expectedMethod, out int? errorCode)
		{
		List<SmartMethodResponseDto> responses = WireJson.Read<SmartEnvelopeDto> (responseJson).Result?.Responses
			?? throw new InvalidDataException ("The smart device response did not contain result.responses.");
		foreach (SmartMethodResponseDto response in responses)
			{
			if (!string.Equals (response.Method, expectedMethod, StringComparison.Ordinal)) continue;
			errorCode = response.ErrorCode;
			return response.Result;
			}
		throw new InvalidDataException ($"The smart device response did not contain {expectedMethod} data.");
		}

	internal static EnergyUsage? ParseSmartEnergyUsage (IReadOnlyDictionary<string, object> moduleResults) => CreateSmartEnergyUsage (moduleResults);

	internal static DeviceSystemInfo ParseSystemInfo (ParsedResponse response)
		{
		return CreateSystemInfo (response.SystemInfo);
		}

	internal static ParsedDeviceState ParseSmartDeviceState (SmartParsedResponse response)
		{
		DeviceSystemInfo systemInfo = CreateSmartSystemInfo (response);
		LightEffectState? smartBulbEffect = CreateSmartBulbLightEffectState (response);
		LightState? lightState = CreateSmartLightState (response, smartBulbEffect);
		LightPresetState? lightPresetState = CreateSmartLightPresetState (response, lightState);
		LightTransitionState? lightTransitionState = CreateSmartLightTransitionState (response);
		return new ParsedDeviceState (
			systemInfo,
			CreateSmartEnergyUsage (response),
			lightState,
			lightPresetState,
			lightTransitionState,
			CreateLightStripEffectState (lightState, systemInfo.DeviceType),
			CreateSmartAlarmState (response),
			CreateSmartOverheatProtectionState (response),
			CreateSmartPowerProtectionState (response),
			CreateSmartFanState (response, systemInfo),
			CreateSmartSpeakerState (response),
			ruleState: null,
			CreateSmartFirmwareState (response),
			CreateSmartCloudConnectionState (response),
			CreateSmartDeviceTimeState (response),
			CreateSmartMatterSetupInfo (response),
			CreateSmartHomeKitSetupInfo (response),
			CreateSmartAutoOffState (response),
			CreateSmartLedState (response),
			CreateSmartChildLockState (response),
			response.DeviceInfo.Rssi);
		}

	internal static ParsedDeviceState ParseDeviceState (ParsedResponse response)
		{
		EnergyUsage? energyUsage = response.EmeterInfo is LegacyEmeterRealtimeDto emeterInfo
			? CreateEnergyUsage (emeterInfo, response.Emeter?.GetDayStat, response.Emeter?.GetMonthStat)
			: null;
		LightState? lightState = response.SystemInfo.LightState is null
			? null
			: CreateLightState (response.SystemInfo);
		RuleModuleState? ruleState = CreateRuleModuleState (response);

		return new ParsedDeviceState (
			CreateSystemInfo (response.SystemInfo),
			energyUsage,
			lightState,
			CreateLightPresetState (lightState),
			CreateLegacyLightTransitionState (response.SystemInfo.LightState),
			CreateLightStripEffectState (lightState, DetermineLegacyDeviceType (response.SystemInfo)),
			alarmState: null,
			overheatProtectionState: null,
			powerProtectionState: null,
			fanState: null,
			speakerState: null,
			ruleState,
			CreateLegacyFirmwareState (response),
			CreateLegacyCloudConnectionState (response),
			CreateLegacyDeviceTimeState (response),
			matterSetup: null,
			CreateLegacyHomeKitSetupInfo (response),
			CreateLegacyAutoOffState (response),
			CreateLegacyLedState (response),
			childLockState: null,
			response.SystemInfo.Rssi);
		}

	internal static EnergyUsage ParseEnergyUsage (ParsedResponse response)
		{
		if (response.EmeterInfo is not LegacyEmeterRealtimeDto emeterInfo)
			{
			throw new InvalidDataException ("The device response did not contain emeter.get_realtime data.");
			}

		return CreateEnergyUsage (emeterInfo, response.Emeter?.GetDayStat, response.Emeter?.GetMonthStat);
		}

	internal static ParsedResponse MergeParsedResponse (ParsedResponse primary, ParsedResponse overlay)
		{
		return new ParsedResponse (
			overlay.SystemInfo ?? primary.SystemInfo,
			overlay.Emeter ?? primary.Emeter,
			overlay.EmeterInfo ?? primary.EmeterInfo,
			overlay.CountdownRules ?? primary.CountdownRules,
			overlay.ScheduleRules ?? primary.ScheduleRules,
			overlay.AntitheftRules ?? primary.AntitheftRules,
			overlay.Time ?? primary.Time,
			overlay.Cloud ?? primary.Cloud,
			overlay.HomeKit ?? primary.HomeKit);
		}

	internal static LightState ParseLightState (ParsedResponse response)
		{
		return response.SystemInfo.LightState is null
			? throw new InvalidDataException ("The device response did not contain light_state data.")
			: CreateLightState (response.SystemInfo);
		}

	internal static bool TryParseDiscoveryResult (ParsedResponse response, IPEndPoint endpoint, out DiscoveryResult? discoveryResult)
		{
		try
			{
			DeviceSystemInfo parsed = CreateSystemInfo (response.SystemInfo);
			discoveryResult = new DiscoveryResult (
				endpoint.Address.ToString (),
				parsed.DeviceType,
				parsed.Alias,
				parsed.Model,
				parsed.DeviceId,
				DeviceTransportKind.LegacyXor,
				supportsHttps: false,
				port: endpoint.Port,
				new DeviceConnectionParameters (DetermineLegacyDiscoveryFamilyKind (parsed.Model), DeviceEncryptionKind.Xor, useHttps: false, httpPort: null));
			return true;
			}
		catch
			{
			discoveryResult = null;
			return false;
			}
		}
	}
