// Copyright © 2026 Neil Colvin.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// Behavior modeled after the independent python-kasa project (https://github.com/python-kasa/python-kasa)
// for protocol/compatibility reference only; no python-kasa source was copied. See ATTRIBUTIONS.md.

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

using KasaTapoClient.Internal;

namespace KasaTapoClient;

public sealed partial class KasaDevice
	{
	// Accessed only inside the serialized device operation. A successful fallback
	// avoids paying for the same core batch timeout on every poll of this device.
	private bool _useSingleSmartCoreRequests;

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
			|| DeviceType == DeviceType.Strip
			|| Children.Count > 0
			|| connectionParameters?.DeviceFamily == DeviceFamilyKind.SmartTapoHub
			|| connectionParameters?.DeviceFamily == DeviceFamilyKind.SmartKasaHub;
		bool shouldRequestChildDeviceComponentList = DeviceType == DeviceType.Hub
			|| DeviceType == DeviceType.Strip
			|| connectionParameters?.DeviceFamily == DeviceFamilyKind.SmartTapoHub
			|| connectionParameters?.DeviceFamily == DeviceFamilyKind.SmartKasaHub;

		var coreRequests = new Dictionary<string, object?>
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

		List<KasaResponseParser.SmartMethodResponseDto> coreResults = await ReadSmartCoreAsync (coreRequests, cancellationToken).ConfigureAwait (false);
		KasaResponseParser.SmartParsedResponse parsedResponse = KasaResponseParser.ParseSmartCoreResponses (coreResults);
		if (!shouldRequestChildDeviceList && KasaResponseParser.DetermineSmartDeviceType (parsedResponse) == DeviceType.Strip)
			{
			// SMART strips identify themselves as plugs until component negotiation.
			if (parsedResponse.ChildDeviceList is null)
				{
				var childRequests = new Dictionary<string, object?>
					{
					[KasaCommands.SMART_GET_CHILD_DEVICE_LIST_METHOD] = null,
					[KasaCommands.SMART_GET_CHILD_DEVICE_COMPONENT_LIST_METHOD] = null,
					};
				coreResults.AddRange (await ReadSmartCoreAsync (childRequests, cancellationToken).ConfigureAwait (false));
				parsedResponse = KasaResponseParser.ParseSmartCoreResponses (coreResults);
				}
			shouldRequestChildDeviceList = true;
			}
		if (shouldRequestChildDeviceList)
			{
			parsedResponse = await EnsureFullChildDeviceListAsync (parsedResponse, cancellationToken).ConfigureAwait (false);
			}
		parsedResponse = await EnrichSmartModuleResultsAsync (parsedResponse, cancellationToken).ConfigureAwait (false);
		parsedResponse = await EnrichSmartChildResponseAsync (parsedResponse, cancellationToken).ConfigureAwait (false);
		KasaResponseParser.ParsedDeviceState state = KasaResponseParser.ParseSmartDeviceState (parsedResponse);
		_operations.Commit (() =>
			{
				_smartComponentVersions = parsedResponse.ComponentVersions;
				ApplyParsedStateCore (state);
			});
		_smartResponse = parsedResponse;
		}

	private async Task<List<KasaResponseParser.SmartMethodResponseDto>> ReadSmartCoreAsync (
		Dictionary<string, object?> requests,
		CancellationToken cancellationToken)
		{
		if (!_useSingleSmartCoreRequests)
			{
			try
				{
				string batchResponse = await SendSmartCoreReadAsync (KasaCommands.CreateSmartMultipleRequest (requests), cancellationToken).ConfigureAwait (false);
				return KasaResponseParser.ReadSmartBatchResults (batchResponse);
				}
			catch (TimeoutException) when (!cancellationToken.IsCancellationRequested)
				{
				// Some devices never answer a batch containing both child-list reads.
				}
			}

		var results = new List<KasaResponseParser.SmartMethodResponseDto> (requests.Count);
		foreach (KeyValuePair<string, object?> request in requests)
			{
			cancellationToken.ThrowIfCancellationRequested ();
			string response = await SendSmartCoreReadAsync (KasaCommands.CreateSmartRequest (request.Key, request.Value), cancellationToken).ConfigureAwait (false);
			KasaResponseParser.SmartMethodResponseDto result = KasaResponseParser.ParseSmartCoreResult (request.Key, response);
			if (request.Key == KasaCommands.SMART_GET_CHILD_DEVICE_LIST_METHOD && result.Result is not KasaResponseParser.SmartEnvelopeResultDto { ChildDeviceList: not null })
				{
				throw new InvalidDataException ("The individual smart response did not contain the child device list.");
				}
			results.Add (result);
			}
		_useSingleSmartCoreRequests = true;
		return results;
		}

	private async Task<string> SendSmartCoreReadAsync (string request, CancellationToken cancellationToken)
		{
		// Use our own deadline for transports whose HTTP API does not enforce its
		// setting for asynchronous requests (including HttpWebRequest on Mono).
		using var deadline = CancellationTokenSource.CreateLinkedTokenSource (cancellationToken);
		if (Configuration.Timeout > TimeSpan.Zero)
			{
			deadline.CancelAfter (Configuration.Timeout);
			}
		try
			{
			return await _transport.SendAsync (request, deadline.Token).ConfigureAwait (false);
			}
		catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested
			&& (deadline.IsCancellationRequested || exception is TaskCanceledException))
			{
			throw new TimeoutException ("The smart refresh request timed out.", exception);
			}
		catch (WebException exception) when (!cancellationToken.IsCancellationRequested && exception.Status == WebExceptionStatus.Timeout)
			{
			throw new TimeoutException ("The smart refresh request timed out.", exception);
			}
		}

	private async Task<KasaResponseParser.SmartParsedResponse> EnsureFullChildDeviceListAsync (
		KasaResponseParser.SmartParsedResponse parsedResponse,
		CancellationToken cancellationToken)
		{
		KasaResponseParser.SmartChildDeviceListDto? childDeviceList = parsedResponse.ChildDeviceList;
		if (childDeviceList is null
			|| childDeviceList.Sum is not int sum
			|| sum <= childDeviceList.ChildDevices.Count)
			{
			return parsedResponse;
			}

		var mergedChildren = new List<KasaResponseParser.SmartChildDeviceDto> (childDeviceList.ChildDevices);
		while (mergedChildren.Count < sum)
			{
			int startIndex = mergedChildren.Count;
			string pageResponseJson = await SendSmartCoreReadAsync (
				KasaCommands.CreateSmartRequest (
					KasaCommands.SMART_GET_CHILD_DEVICE_LIST_METHOD,
					new PageParametersDto { StartIndex = startIndex }),
				cancellationToken).ConfigureAwait (false);
			KasaResponseParser.SmartChildDeviceListDto? page = KasaResponseParser.ParseSmartChildDeviceListPage (pageResponseJson);
			if (page is null || page.ChildDevices.Count == 0 || (page.StartIndex.HasValue && page.StartIndex != startIndex))
				{
				throw new InvalidDataException ("The smart device did not return the remaining child device list.");
				}

			mergedChildren.AddRange (page.ChildDevices);
			}

		var mergedChildDeviceList = new KasaResponseParser.SmartChildDeviceListDto (mergedChildren, sum, 0);
		return new KasaResponseParser.SmartParsedResponse (
			parsedResponse.DeviceInfo,
			parsedResponse.ComponentIds,
			parsedResponse.ComponentVersions,
			mergedChildDeviceList,
			parsedResponse.ChildComponentIds,
			parsedResponse.ChildOverrides,
			parsedResponse.ModuleResults);
		}

	private void ApplyParsedState (KasaResponseParser.ParsedDeviceState parsed)
		=> _operations.Commit (() => ApplyParsedStateCore (parsed));

	private void ApplyParsedStateCore (KasaResponseParser.ParsedDeviceState parsed)
		{
		SystemInfo = parsed.SystemInfo;
		EnergyUsage = parsed.EnergyUsage;
		LightState = parsed.LightState;
		LightPresetState = parsed.LightPresetState;
		LightTransitionState = parsed.LightTransitionState;
		LightStripEffectState = parsed.LightStripEffectState;
		LightStripSegmentState = parsed.LightStripSegmentState;
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
		Dictionary<string, object?> parentRequests = CreateSmartParentRefreshRequests (parsedResponse.ComponentVersions);
		if (parentRequests.Count == 0)
			{
			return parsedResponse;
			}

		try
			{
			string moduleResponseJson = await _transport.SendAsync (KasaCommands.CreateSmartMultipleRequest (parentRequests), cancellationToken).ConfigureAwait (false);
			IReadOnlyDictionary<string, object> moduleResults = KasaResponseParser.ParseSmartModuleResults (moduleResponseJson);
			if (moduleResults.Count == 0)
				{
				return parsedResponse;
				}

			var mergedModuleResults = new Dictionary<string, object> (0, StringComparer.Ordinal);
			foreach (KeyValuePair<string, object> item in parsedResponse.ModuleResults)
				{
				mergedModuleResults[item.Key] = item.Value;
				}
			foreach (KeyValuePair<string, object> item in moduleResults)
				{
				mergedModuleResults[item.Key] = item.Value;
				}

			return new KasaResponseParser.SmartParsedResponse (
				parsedResponse.DeviceInfo,
				parsedResponse.ComponentIds,
				parsedResponse.ComponentVersions,
				parsedResponse.ChildDeviceList,
				parsedResponse.ChildComponentIds,
				parsedResponse.ChildOverrides,
				mergedModuleResults);
			}
		catch (OperationCanceledException)
			{
			throw;
			}
		catch
			{
			return parsedResponse;
			}
		}

	private static Dictionary<string, object?> CreateSmartParentRefreshRequests (IReadOnlyDictionary<string, int> componentVersions)
		{
		var requests = new Dictionary<string, object?> (StringComparer.Ordinal);
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
