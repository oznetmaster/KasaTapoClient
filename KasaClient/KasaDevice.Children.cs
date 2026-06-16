// Copyright © 2026 Neil Colvin.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// Adapted from python-kasa (https://github.com/python-kasa/python-kasa)
// Original work Copyright (c) python-kasa contributors, MIT License

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
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

				Dictionary<string, JsonObject?> childRequests = CreateSmartChildRefreshRequests (componentIds);
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

	private static Dictionary<string, JsonObject?> CreateSmartChildRefreshRequests (IReadOnlyList<string> componentIds)
		{
		var requests = new Dictionary<string, JsonObject?> (StringComparer.Ordinal);
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
		JsonObject childObject = JsonSupport.ParseObject (JsonSerializer.Serialize (child, JsonSupport.COMPACT_JSON));

		if (!TryParseSmartChildResponseData (childResponseJson, out JsonObject? responseData))
			{
			return false;
			}

		JsonSupport.MergeObjects (childObject, responseData!);
		mergedChild = JsonSerializer.Deserialize<KasaResponseParser.SmartChildDeviceDto> (childObject.ToJsonString (JsonSupport.COMPACT_JSON), JsonSupport.COMPACT_JSON);
		return mergedChild is not null;
		}

	private static bool TryParseSmartChildResponseData (string childResponseJson, out JsonObject? responseData)
		{
		responseData = null;
		JsonObject root = JsonSupport.ParseObject (childResponseJson);
		if (root["result"] is not JsonObject resultObject
			|| resultObject["responseData"] is not JsonObject responseDataObject)
			{
			return false;
			}

		JsonNode? responseResultNode = responseDataObject["result"];
		if (responseResultNode is not JsonObject responseResultObject)
			{
			return false;
			}

		responseData = new JsonObject ();
		if (responseResultObject["responses"] is JsonArray responses)
			{
				foreach (JsonNode? responseNode in responses)
					{
						if (responseNode is not JsonObject methodResponse)
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

		return JsonSerializer.Deserialize<KasaResponseParser.SmartChildDeviceDto> (childInfo.RawJson, JsonSupport.COMPACT_JSON);
		}

	internal IReadOnlyList<string> GetSupportedChildSetupCategories ()
		{
		if (SystemInfo?.RawJson is not string rawJson || string.IsNullOrWhiteSpace (rawJson))
			{
			return Array.Empty<string> ();
			}

		JsonObject root = JsonSupport.ParseObject (rawJson);
		if (root["result"] is not JsonObject resultObject
			|| resultObject["responses"] is not JsonArray responses)
			{
			return Array.Empty<string> ();
			}

		foreach (JsonNode? responseNode in responses)
			{
			if (responseNode is not JsonObject responseObject)
				{
				continue;
				}

			if (!string.Equals (responseObject["method"]?.GetValue<string> (), KasaCommands.SMART_GET_DEVICE_INFO_METHOD, StringComparison.Ordinal)
				|| responseObject["result"] is not JsonObject deviceInfoObject)
				{
				continue;
				}

			if (deviceInfoObject["device_category_list"] is not JsonArray categoryArray || categoryArray.Count == 0)
				{
				return Array.Empty<string> ();
				}

			var results = new List<string> (categoryArray.Count);
			foreach (JsonNode? categoryNode in categoryArray)
				{
				if (categoryNode is JsonObject categoryObject
					&& !string.IsNullOrWhiteSpace (categoryObject["category"]?.GetValue<string> ()))
					{
					results.Add (categoryObject["category"]!.GetValue<string> ());
					}
				}

			return results;
			}

		return Array.Empty<string> ();
		}

	private static ChildSetupScanResult ParseChildSetupScanResult (string responseJson, IReadOnlyList<string> supportedCategories)
		{
		JsonObject root = JsonSupport.ParseObject (responseJson);
		JsonObject resultObject = root["result"] as JsonObject ?? throw new InvalidOperationException ("The hub did not return a smart child setup result payload.");
		KasaResponseParser.SmartScannedChildDeviceListDto? result = JsonSerializer.Deserialize<KasaResponseParser.SmartScannedChildDeviceListDto> (resultObject.ToJsonString (JsonSupport.COMPACT_JSON), JsonSupport.COMPACT_JSON);

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
				JsonSerializer.Serialize (detectedDevice, JsonSupport.COMPACT_JSON)));
			}

		return new ChildSetupScanResult (supportedCategories, devices);
		}
	}
