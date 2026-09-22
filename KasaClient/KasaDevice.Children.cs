// Copyright © 2026 Neil Colvin.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// Behavior modeled after the independent python-kasa project (https://github.com/python-kasa/python-kasa)
// for protocol/compatibility reference only; no python-kasa source was copied. See ATTRIBUTIONS.md.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using KasaTapoClient.Internal;

namespace KasaTapoClient;

public sealed partial class KasaDevice
	{
	private async Task<KasaResponseParser.SmartParsedResponse> EnrichSmartChildResponseAsync (
		KasaResponseParser.SmartParsedResponse parsedResponse,
		CancellationToken cancellationToken)
		{
		if (parsedResponse.ChildDeviceList?.ChildDevices is not IReadOnlyList<KasaResponseParser.SmartChildDeviceDto> children
			|| children.Count == 0)
			{
			return parsedResponse;
			}

		Dictionary<string, KasaResponseParser.SmartChildDeviceDto>? childOverrides = null;
		foreach (KasaResponseParser.SmartChildDeviceDto child in children)
			{
				if (child.DeviceId is not string childDeviceId || string.IsNullOrWhiteSpace (childDeviceId))
					{
					continue;
					}

				if (!parsedResponse.ChildComponentIds.TryGetValue (childDeviceId, out IReadOnlyList<string>? componentIds))
					{
					continue;
					}

				Dictionary<string, object?> childRequests = CreateSmartChildRefreshRequests (componentIds);
				if (childRequests.Count == 0)
					{
					continue;
					}

				string childResponseJson = await _transport.SendAsync (KasaCommands.CreateSmartChildMultipleRequest (childDeviceId, childRequests), cancellationToken).ConfigureAwait (false);
				if (TryMergeSmartChildRefresh (child, childResponseJson, out KasaResponseParser.SmartChildDeviceDto? mergedChild)
					&& mergedChild is not null)
					{
					childOverrides ??= new Dictionary<string, KasaResponseParser.SmartChildDeviceDto> (StringComparer.OrdinalIgnoreCase);
					childOverrides[childDeviceId] = mergedChild;
					}
			}

		if (childOverrides is null || childOverrides.Count == 0)
			{
			return parsedResponse;
			}

		return new KasaResponseParser.SmartParsedResponse (
			parsedResponse.DeviceInfo,
			parsedResponse.ComponentIds,
			parsedResponse.ComponentVersions,
			parsedResponse.ChildDeviceList,
			parsedResponse.ChildComponentIds,
			childOverrides,
			parsedResponse.ModuleResults);
		}

	private static Dictionary<string, object?> CreateSmartChildRefreshRequests (IReadOnlyList<string> componentIds)
		{
		var requests = new Dictionary<string, object?> (StringComparer.Ordinal);
		foreach (string componentId in componentIds)
			{
				if (SMART_CHILD_REFRESH_DEFINITIONS.TryGetValue (componentId, out SmartChildRefreshDefinition? definition))
					{
					requests[definition.Method] = definition.CreateParameters ();
					}
			}

		return requests;
		}

	private static bool TryMergeSmartChildRefresh (
		KasaResponseParser.SmartChildDeviceDto child,
		string childResponseJson,
		out KasaResponseParser.SmartChildDeviceDto? mergedChild)
		{
		mergedChild = null;
		ResponseEnvelope<ChildControlResultDto> envelope = WireJson.Read<ResponseEnvelope<ChildControlResultDto>> (childResponseJson);
		ResponseEnvelope<KasaResponseParser.SmartChildDeviceDto>? response = envelope.Result?.ResponseData;
		if (envelope.ErrorCode is not (null or 0) || response?.ErrorCode is not (null or 0) || response?.Result is not KasaResponseParser.SmartChildDeviceDto update) return false;
		if (update.Responses is not null)
			{
			foreach (KasaResponseParser.SmartMethodResponseDto method in update.Responses)
				{
				if (method.ErrorCode is not (null or 0)) continue;
				switch (method.Method)
					{
					case KasaCommands.SMART_GET_DOUBLE_CLICK_INFO_METHOD: update.DoubleClickInfo = method.Result as KasaResponseParser.SmartDoubleClickInfoDto; break;
					case KasaCommands.SMART_GET_TRIGGER_LOGS_METHOD: update.TriggerLogs = method.Result as KasaResponseParser.SmartTriggerLogListDto; break;
					case KasaCommands.SMART_GET_COMFORT_HUMIDITY_CONFIG_METHOD: update.ComfortHumidityConfig = method.Result as KasaResponseParser.SmartComfortValueConfigDto; break;
					case KasaCommands.SMART_GET_FROST_PROTECTION_METHOD: update.FrostProtection = method.Result as KasaResponseParser.SmartFrostProtectionDto; break;
					}
				}
			}
		mergedChild = child.Overlay (update);
		return true;
		}

	private List<ChildDevice> CreateChildDevices ()
		{
		var children = new List<ChildDevice> ();
		foreach (ChildDeviceInfo child in Children)
			{
			children.Add (new ChildDevice (this, child));
			}

		return children;
		}

	internal KasaResponseParser.SmartChildDeviceDto? GetChildState (string childDeviceId)
		{
		if (_smartResponse is null) return null;
		if (_smartResponse.ChildOverrides.TryGetValue (childDeviceId, out KasaResponseParser.SmartChildDeviceDto? child)) return child;
		return _smartResponse.ChildDeviceList?.ChildDevices.FirstOrDefault (candidate => string.Equals (candidate.DeviceId, childDeviceId, StringComparison.OrdinalIgnoreCase));
		}

	internal IReadOnlyList<string> GetSupportedChildSetupCategories () =>
		_smartResponse?.DeviceInfo.DeviceCategoryList?.Where (item => !string.IsNullOrWhiteSpace (item.Category)).Select (item => item.Category!).ToArray () ?? Array.Empty<string> ();

	private static ChildSetupScanResult ParseChildSetupScanResult (string responseJson, IReadOnlyList<string> supportedCategories)
		{
		KasaResponseParser.SmartScannedChildDeviceListDto? result = WireJson.Read<ResponseEnvelope<KasaResponseParser.SmartScannedChildDeviceListDto>> (responseJson).Result
			?? throw new InvalidOperationException ("The hub did not return a smart child setup result payload.");

		if (result?.ChildDeviceList is not List<KasaResponseParser.SmartScannedChildDeviceDto> detected || detected.Count == 0)
			{
			return new ChildSetupScanResult (supportedCategories, Array.Empty<DetectedChildDevice> ());
			}

		var devices = new List<DetectedChildDevice> (detected.Count);
		foreach (KasaResponseParser.SmartScannedChildDeviceDto detectedDevice in detected)
			{
			if (string.IsNullOrWhiteSpace (detectedDevice.DeviceId))
				{
				continue;
				}

			devices.Add (new DetectedChildDevice (
				detectedDevice.DeviceId!,
				detectedDevice.DeviceModel,
				detectedDevice.Category));
			}

		return new ChildSetupScanResult (supportedCategories, devices);
		}
	}
