// Copyright (c) 2026 Neil Colvin. See LICENSE in the repository root.
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using KasaTapoClient.Internal;

namespace KasaTapoClient;

public sealed partial class KasaDevice
	{
	/// <summary>Reads the root outlet's current relay state without refreshing other modules.</summary>
	/// <param name="cancellationToken">The cancellation token for the state read.</param>
	/// <returns>The reported state, or <see langword="null"/> if the device omitted it.</returns>
	/// <remarks>This does not replace the full state and energy snapshots from <see cref="UpdateAsync"/>.</remarks>
	public Task<bool?> ReadOutletStateAsync (CancellationToken cancellationToken = default) =>
		RunDeviceOperationAsync (ReadOutletStateCoreAsync, cancellationToken);

	/// <summary>Refreshes the relay states of the physical strip's sockets.</summary>
	/// <param name="cancellationToken">The cancellation token for the refresh.</param>
	/// <returns>A task that completes when the socket states have been refreshed.</returns>
	/// <remarks>Legacy strips need only their system-info request. SMART strips retain their full child-list refresh.</remarks>
	public Task RefreshStripOutletStatesAsync (CancellationToken cancellationToken = default) =>
		RunDeviceOperationAsync (async ct =>
			{
			if (UsesSmartProtocol ())
				{
				await UpdateCoreAsync (ct).ConfigureAwait (false);
				return;
				}
			string response = await _transport.SendAsync (KasaCommands.GET_SYSTEM_INFO, ct).ConfigureAwait (false);
			SystemInfo = KasaResponseParser.ParseSystemInfo (KasaResponseParser.ParseResponse (response));
			}, cancellationToken);

	/// <summary>Sets a standalone outlet relay and reads back its actual state without refreshing other modules.</summary>
	/// <param name="isOn">The requested relay state.</param>
	/// <param name="cancellationToken">The cancellation token for the write and readback.</param>
	/// <returns>The confirmed state, or <see langword="null"/> if the device omitted it.</returns>
	/// <remarks>For standalone plugs, not bulbs or individual sockets on a strip.</remarks>
	public Task<bool?> SetOutletStateAndReadBackAsync (bool isOn, CancellationToken cancellationToken = default) =>
		RunDeviceOperationAsync (async ct =>
			{
			string request = UsesSmartProtocol ()
				? KasaCommands.CreateSmartRequest ("set_device_info", new SmartDeviceSettingsDto { DeviceOn = isOn })
				: KasaCommands.CreateSetRelayStateCommand (isOn);
			await ExecuteCommandCoreAsync (request, ct).ConfigureAwait (false);
			return await ReadOutletStateCoreAsync (ct).ConfigureAwait (false);
			}, cancellationToken);

	private async Task<bool?> ReadOutletStateCoreAsync (CancellationToken cancellationToken)
		{
		if (!UsesSmartProtocol ())
			{
			string legacy = await _transport.SendAsync (KasaCommands.GET_SYSTEM_INFO, cancellationToken).ConfigureAwait (false);
			return KasaResponseParser.ParseSystemInfo (KasaResponseParser.ParseResponse (legacy)).IsOn;
			}
		const string method = KasaCommands.SMART_GET_DEVICE_INFO_METHOD;
		string response = await _transport.SendAsync (KasaCommands.CreateSmartMultipleRequest (
			new Dictionary<string, object?> { [method] = null }), cancellationToken).ConfigureAwait (false);
		object? result = KasaResponseParser.ParseSmartModuleResult (response, method, out int? errorCode);
		if (errorCode.GetValueOrDefault () != 0 || result is not KasaResponseParser.SmartEnvelopeResultDto state)
			throw new InvalidDataException ("The smart device did not return valid outlet state.");
		return state.DeviceOn;
		}
	}
