// Copyright © 2026 Neil Colvin.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// Behavior modeled after the independent python-kasa project (https://github.com/python-kasa/python-kasa)
// for protocol/compatibility reference only; no python-kasa source was copied. See ATTRIBUTIONS.md.

using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
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

				Dictionary<string, JObject?> childRequests = CreateSmartChildRefreshRequests (componentIds);
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
			parsedResponse.RawJson,
			parsedResponse.DeviceInfo,
			parsedResponse.ComponentIds,
			parsedResponse.ComponentVersions,
			parsedResponse.ChildDeviceList,
			parsedResponse.ChildComponentIds,
			childOverrides,
			parsedResponse.ModuleResults);
		}

	private static Dictionary<string, JObject?> CreateSmartChildRefreshRequests (IReadOnlyList<string> componentIds)
		{
		var requests = new Dictionary<string, JObject?> (StringComparer.Ordinal);
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
		JObject childObject = JsonSupport.ParseObject (JsonConvert.SerializeObject (child, JsonSupport.COMPACT_JSON));

		if (!TryParseSmartChildResponseData (childResponseJson, out JObject? responseData))
			{
			return false;
			}

		JsonSupport.MergeObjects (childObject, responseData!);
		mergedChild = JsonConvert.DeserializeObject<KasaResponseParser.SmartChildDeviceDto> (childObject.ToJsonString (JsonSupport.COMPACT_JSON), JsonSupport.COMPACT_JSON);
		return mergedChild is not null;
		}

	private static bool TryParseSmartChildResponseData (string childResponseJson, out JObject? responseData)
		{
		responseData = null;
		JObject root = JsonSupport.ParseObject (childResponseJson);
		if (root["result"] is not JObject resultObject
			|| resultObject["responseData"] is not JObject responseDataObject)
			{
			return false;
			}

		JToken? responseResultNode = responseDataObject["result"];
		if (responseResultNode is not JObject responseResultObject)
			{
			return false;
			}

		responseData = new JObject ();
		if (responseResultObject["responses"] is JArray responses)
			{
				foreach (JToken? responseNode in responses)
					{
						if (responseNode is not JObject methodResponse)
							{
							continue;
							}

						string? method = methodResponse["method"]?.GetValue<string> ();
						if (string.IsNullOrWhiteSpace (method))
							{
							continue;
							}

						responseData[MapSmartChildResponseMethodToPropertyName (method!)] = methodResponse["result"]?.DeepClone ();
						}
			}
		else
			{
			JsonSupport.MergeObjects (responseData, responseResultObject);
			}

		return true;
		}

	private static string MapSmartChildResponseMethodToPropertyName (string method)
		{
		foreach (SmartChildRefreshDefinition definition in SMART_CHILD_REFRESH_DEFINITIONS.Values)
			{
			if (string.Equals (definition.Method, method, StringComparison.Ordinal))
				{
				return definition.ResponsePropertyName;
				}
			}

		return method;
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

	internal KasaResponseParser.SmartChildDeviceDto? GetChildRawState (string childDeviceId)
		{
		ChildDeviceInfo? childInfo = GetChild (childDeviceId);
		if (childInfo is null)
			{
			return null;
			}

		return JsonConvert.DeserializeObject<KasaResponseParser.SmartChildDeviceDto> (childInfo.RawJson, JsonSupport.COMPACT_JSON);
		}

	internal IReadOnlyList<string> GetSupportedChildSetupCategories ()
		{
		if (SystemInfo?.RawJson is not string rawJson || string.IsNullOrWhiteSpace (rawJson))
			{
			return Array.Empty<string> ();
			}

		JObject root = JsonSupport.ParseObject (rawJson);
		if (root["result"] is not JObject resultObject
			|| resultObject["responses"] is not JArray responses)
			{
			return Array.Empty<string> ();
			}

		foreach (JToken? responseNode in responses)
			{
			if (responseNode is not JObject responseObject)
				{
				continue;
				}

			if (!string.Equals (responseObject["method"]?.GetValue<string> (), KasaCommands.SMART_GET_DEVICE_INFO_METHOD, StringComparison.Ordinal)
				|| responseObject["result"] is not JObject deviceInfoObject)
				{
				continue;
				}

			if (deviceInfoObject["device_category_list"] is not JArray categoryArray || categoryArray.Count == 0)
				{
				return Array.Empty<string> ();
				}

			var results = new List<string> (categoryArray.Count);
			foreach (JToken? categoryNode in categoryArray)
				{
				if (categoryNode is JObject categoryObject
					&& !string.IsNullOrWhiteSpace (categoryObject["category"]?.GetValue<string> ()))
					{
					results.Add (categoryObject["category"]!.GetValue<string> ()!);
					}
				}

			return results;
			}

		return Array.Empty<string> ();
		}

	private static ChildSetupScanResult ParseChildSetupScanResult (string responseJson, IReadOnlyList<string> supportedCategories)
		{
		JObject root = JsonSupport.ParseObject (responseJson);
		JObject resultObject = root["result"] as JObject ?? throw new InvalidOperationException ("The hub did not return a smart child setup result payload.");
		KasaResponseParser.SmartScannedChildDeviceListDto? result = JsonConvert.DeserializeObject<KasaResponseParser.SmartScannedChildDeviceListDto> (resultObject.ToJsonString (JsonSupport.COMPACT_JSON), JsonSupport.COMPACT_JSON);

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
				detectedDevice.Category,
				JsonConvert.SerializeObject (detectedDevice, JsonSupport.COMPACT_JSON)));
			}

		return new ChildSetupScanResult (supportedCategories, devices);
		}
	}
