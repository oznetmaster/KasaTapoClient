// Copyright © 2026 Neil Colvin.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// Behavior modeled after the independent python-kasa project (https://github.com/python-kasa/python-kasa)
// for protocol/compatibility reference only; no python-kasa source was copied. See ATTRIBUTIONS.md.

using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using System.Threading;
using System.Threading.Tasks;

using KasaTapoClient.Internal;

namespace KasaTapoClient;

public sealed partial class KasaDevice
	{
	private async Task UpdateLegacyAsync (CancellationToken cancellationToken)
		{
		DateTime now = DateTime.Now;

		string response = await _transport.SendAsync (
			KasaCommands.GET_SYSTEM_INFO,
			cancellationToken).ConfigureAwait (false);
		KasaResponseParser.ParsedResponse parsedResponse = KasaResponseParser.ParseResponse (response);

		if (DeviceType == DeviceType.Bulb || parsedResponse.SystemInfo.Model?.StartsWith ("KL", StringComparison.OrdinalIgnoreCase) == true || parsedResponse.SystemInfo.Model?.StartsWith ("LB", StringComparison.OrdinalIgnoreCase) == true || parsedResponse.SystemInfo.Model?.StartsWith ("KB", StringComparison.OrdinalIgnoreCase) == true)
			{
			string bulbModuleResponse = await _transport.SendManyAsync (
				[
				KasaCommands.GET_BULB_EMETER_REALTIME,
				KasaCommands.CreateGetBulbEmeterDayStatCommand (now.Year, now.Month),
				KasaCommands.CreateGetBulbEmeterMonthStatCommand (now.Year),
				KasaCommands.GET_BULB_TIME,
				KasaCommands.GET_BULB_TIMEZONE,
				KasaCommands.GET_BULB_CLOUD_INFO,
				KasaCommands.GET_BULB_COUNTDOWN_RULE,
				KasaCommands.GET_BULB_SCHEDULE_RULES,
				KasaCommands.GET_BULB_ANTITHEFT_RULES,
				],
				cancellationToken).ConfigureAwait (false);
			parsedResponse = KasaResponseParser.MergeParsedResponse (parsedResponse, KasaResponseParser.ParseModuleResponse (bulbModuleResponse, parsedResponse.SystemInfo));
			}
		else
			{
			string legacyModuleResponse = await _transport.SendManyAsync (
				[
				KasaCommands.GET_EMETER_REALTIME,
				KasaCommands.CreateGetEmeterDayStatCommand (now.Year, now.Month),
				KasaCommands.CreateGetEmeterMonthStatCommand (now.Year),
				KasaCommands.GET_TIME,
				KasaCommands.GET_TIMEZONE,
				KasaCommands.GET_CLOUD_INFO,
				KasaCommands.GET_COUNTDOWN_RULE,
				KasaCommands.GET_SCHEDULE_RULES,
				KasaCommands.GET_ANTITHEFT_RULES,
				],
				cancellationToken).ConfigureAwait (false);
			parsedResponse = KasaResponseParser.MergeParsedResponse (parsedResponse, KasaResponseParser.ParseModuleResponse (legacyModuleResponse, parsedResponse.SystemInfo));
			}

		ApplyParsedState (KasaResponseParser.ParseDeviceState (parsedResponse));
		}

	private async Task UpdateSmartAsync (CancellationToken cancellationToken)
		{
		DeviceConnectionParameters? connectionParameters = Configuration.ConnectionOptions.ConnectionParameters;
		bool shouldRequestChildDeviceList = DeviceType == DeviceType.Hub
			|| Children.Count > 0
			|| connectionParameters?.DeviceFamily == DeviceFamilyKind.SmartTapoHub
			|| connectionParameters?.DeviceFamily == DeviceFamilyKind.SmartKasaHub;
		bool shouldRequestChildDeviceComponentList = DeviceType == DeviceType.Hub
			|| connectionParameters?.DeviceFamily == DeviceFamilyKind.SmartTapoHub
			|| connectionParameters?.DeviceFamily == DeviceFamilyKind.SmartKasaHub;

		var coreRequests = new Dictionary<string, JObject?>
			{
			[KasaCommands.SMART_GET_DEVICE_INFO_METHOD] = null,
			[KasaCommands.SMART_COMPONENT_NEGO_METHOD] = null,
			};
		if (shouldRequestChildDeviceList)
			{
			coreRequests[KasaCommands.SMART_GET_CHILD_DEVICE_LIST_METHOD] = null;
			}
		if (shouldRequestChildDeviceComponentList)
			{
			coreRequests[KasaCommands.SMART_GET_CHILD_DEVICE_COMPONENT_LIST_METHOD] = null;
			}

		string response = await _transport.SendAsync (KasaCommands.CreateSmartMultipleRequest (coreRequests), cancellationToken).ConfigureAwait (false);
		KasaResponseParser.SmartParsedResponse parsedResponse = KasaResponseParser.ParseSmartResponse (response);
		parsedResponse = await EnrichSmartModuleResultsAsync (parsedResponse, cancellationToken).ConfigureAwait (false);
		parsedResponse = await EnrichSmartChildResponseAsync (parsedResponse, cancellationToken).ConfigureAwait (false);
		_smartComponentVersions = parsedResponse.ComponentVersions;
		ApplyParsedState (KasaResponseParser.ParseSmartDeviceState (parsedResponse));
		}

	private void ApplyParsedState (KasaResponseParser.ParsedDeviceState parsed)
		{
		SystemInfo = parsed.SystemInfo;
		EnergyUsage = parsed.EnergyUsage;
		LightState = parsed.LightState;
		LightPresetState = parsed.LightPresetState;
		LightTransitionState = parsed.LightTransitionState;
		LightStripEffectState = parsed.LightStripEffectState;
		AlarmState = parsed.AlarmState;
		OverheatProtectionState = parsed.OverheatProtectionState;
		PowerProtectionState = parsed.PowerProtectionState;
		FanState = parsed.FanState;
		SpeakerState = parsed.SpeakerState;
		RuleState = parsed.RuleState;
		FirmwareState = parsed.FirmwareState;
		CloudState = parsed.CloudState;
		TimeState = parsed.TimeState;
		MatterSetup = parsed.MatterSetup;
		HomeKitSetup = parsed.HomeKitSetup;
		AutoOffState = parsed.AutoOffState;
		LedState = parsed.LedState;
		ChildLockState = parsed.ChildLockState;
		Rssi = parsed.Rssi;
		_features = CreateFeatures ();
		}

	private async Task<KasaResponseParser.SmartParsedResponse> EnrichSmartModuleResultsAsync (
		KasaResponseParser.SmartParsedResponse parsedResponse,
		CancellationToken cancellationToken)
		{
		Dictionary<string, JObject?> parentRequests = CreateSmartParentRefreshRequests (parsedResponse.ComponentVersions);
		if (parentRequests.Count == 0)
			{
			return parsedResponse;
			}

		try
			{
			string moduleResponseJson = await _transport.SendAsync (KasaCommands.CreateSmartMultipleRequest (parentRequests), cancellationToken).ConfigureAwait (false);
			IReadOnlyDictionary<string, JObject> moduleResults = KasaResponseParser.ParseSmartModuleResults (moduleResponseJson);
			if (moduleResults.Count == 0)
				{
				return parsedResponse;
				}

			var mergedModuleResults = new Dictionary<string, JObject> (0, StringComparer.Ordinal);
			foreach (KeyValuePair<string, JObject> item in parsedResponse.ModuleResults)
				{
				mergedModuleResults[item.Key] = item.Value;
				}
			foreach (KeyValuePair<string, JObject> item in moduleResults)
				{
				mergedModuleResults[item.Key] = item.Value;
				}

			return new KasaResponseParser.SmartParsedResponse (
				parsedResponse.RawJson,
				parsedResponse.DeviceInfo,
				parsedResponse.ComponentIds,
				parsedResponse.ComponentVersions,
				parsedResponse.ChildDeviceList,
				parsedResponse.ChildComponentIds,
				parsedResponse.ChildOverrides,
				mergedModuleResults);
			}
		catch
			{
			return parsedResponse;
			}
		}

	private static Dictionary<string, JObject?> CreateSmartParentRefreshRequests (IReadOnlyDictionary<string, int> componentVersions)
		{
		var requests = new Dictionary<string, JObject?> (StringComparer.Ordinal);
		foreach (SmartRefreshContribution contribution in SMART_PARENT_REFRESH_DEFINITIONS)
			{
			if (!TryGetSmartComponentVersion (componentVersions, contribution.RequiredComponent, out int supportedVersion))
				{
				continue;
				}

			if (contribution.MinimumSupportedVersion is int minimumSupportedVersion
				&& supportedVersion < minimumSupportedVersion)
				{
				continue;
				}

			requests[contribution.Method] = contribution.CreateParameters ();
			}

		return requests;
		}

	private static bool TryGetSmartComponentVersion (IReadOnlyDictionary<string, int> componentVersions, string componentId, out int version)
		{
		return componentVersions.TryGetValue (componentId, out version);
		}
	}
