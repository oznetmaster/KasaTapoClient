// Copyright © 2026 Neil Colvin.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;

using KasaTapoClient;

namespace KasaClient.Tests;

internal sealed class LiveDeviceTarget
	{
	private readonly string? _deviceId;
	private readonly string? _alias;
	internal string? Key
		{
		get;
		}
	internal bool UsesDiscovery => _deviceId != null || _alias != null;

	internal LiveDeviceTarget (string? deviceId, string? alias, string? host)
		{
		_deviceId = Normalize (deviceId);
		_alias = Normalize (alias);
		Key = _deviceId != null ? "device-id:" + _deviceId : _alias != null ? "alias:" + _alias : Normalize (host);
		}

	internal DiscoveryResult Select (IEnumerable<DiscoveryResult> results)
		{
		if (!UsesDiscovery)
			throw new InvalidOperationException ("A device ID or discovery alias is required for device selection.");

		DiscoveryResult[] matches = results.Where (result =>
			 _deviceId != null
				  ? string.Equals (result.DeviceId, _deviceId, StringComparison.OrdinalIgnoreCase)
				  : string.Equals (result.Alias, _alias, StringComparison.OrdinalIgnoreCase)).ToArray ();

		if (matches.Length == 0)
			throw new TimeoutException ($"Discovery did not find live target '{Key}'. Check that the selected device is online and reachable.");

		if (matches.Select (result => result.Host).Distinct (StringComparer.OrdinalIgnoreCase).Count () != 1)
			throw new InvalidOperationException ($"Discovery found multiple devices for live target '{Key}'. Configure a unique device ID; no device was selected.");

		// One device may answer through more than one discovery protocol.
		return matches.OrderByDescending (result => result.TpapPreferred == true || result.TpapMetadata != null).First ();
		}

	private static string? Normalize (string? value) => string.IsNullOrWhiteSpace (value) ? null : value!.Trim ();
	}