// Copyright © 2026 Neil Colvin.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// Adapted from python-kasa (https://github.com/python-kasa/python-kasa)
// Original work Copyright (c) python-kasa contributors, MIT License

using System;
using System.Threading;
using System.Threading.Tasks;

namespace KasaTapoClient;

public sealed partial class KasaDevice
	{
	private bool SupportsLightControl () => DeviceType is DeviceType.Bulb or DeviceType.LightStrip;

	private async Task SetRelayStateAsync (bool isOn, CancellationToken cancellationToken)
		{
		if (UsesSmartProtocol ())
			{
			await ExecuteCommandAsync (KasaTapoClient.Internal.KasaCommands.CreateSmartRequest ("set_device_info", new System.Text.Json.Nodes.JsonObject { ["device_on"] = isOn }), cancellationToken).ConfigureAwait (false);
			await UpdateAsync (cancellationToken).ConfigureAwait (false);
			return;
			}

		if (SupportsLightControl ())
			{
			await SetLightStateAsync (isOn: isOn, cancellationToken: cancellationToken).ConfigureAwait (false);
			return;
			}

		await ExecuteCommandAsync (KasaTapoClient.Internal.KasaCommands.CreateSetRelayStateCommand (isOn), cancellationToken).ConfigureAwait (false);
		await UpdateAsync (cancellationToken).ConfigureAwait (false);
		}

	private async Task SetChildRelayStateAsync (string childDeviceId, bool isOn, CancellationToken cancellationToken)
		{
		if (GetChild (childDeviceId) is null)
			{
			throw new InvalidOperationException ($"The child device '{childDeviceId}' was not found on '{Host}'.");
			}

		await ExecuteCommandAsync (KasaTapoClient.Internal.KasaCommands.CreateSetChildRelayStateCommand (childDeviceId, isOn), cancellationToken).ConfigureAwait (false);
		await UpdateAsync (cancellationToken).ConfigureAwait (false);
		}

	private async Task SetLightStateAsync (
		bool? isOn = null,
		int? brightness = null,
		int? colorTemperature = null,
		int? hue = null,
		int? saturation = null,
		CancellationToken cancellationToken = default)
		{
		if (!SupportsLightControl ())
			{
			throw new InvalidOperationException ($"The device '{Host}' does not support light-state control.");
			}

		if (UsesSmartProtocol ())
			{
			var parameters = new System.Text.Json.Nodes.JsonObject ();
			bool isColorUpdate = hue is int || saturation is int;
			if (isOn is bool smartPowerState)
				{
				parameters["device_on"] = smartPowerState;
				}
			else if (isColorUpdate)
				{
				parameters["device_on"] = true;
				}
			if (brightness is int brightnessValue)
				{
				parameters["brightness"] = brightnessValue;
				}
			if (colorTemperature is int colorTemperatureValue)
				{
				parameters["color_temp"] = colorTemperatureValue;
				}
			if (hue is int hueValue)
				{
				parameters["hue"] = hueValue;
				}
			if (saturation is int saturationValue)
				{
				parameters["saturation"] = saturationValue;
				}
			if (isColorUpdate)
				{
				parameters["color_temp"] = 0;
				}

			await ExecuteCommandAsync (KasaTapoClient.Internal.KasaCommands.CreateSmartRequest ("set_device_info", parameters), cancellationToken).ConfigureAwait (false);
			await UpdateAsync (cancellationToken).ConfigureAwait (false);
			return;
			}

		await ExecuteCommandAsync (KasaTapoClient.Internal.KasaCommands.CreateSetLightStateCommand (DeviceType, isOn, brightness, colorTemperature, hue, saturation), cancellationToken).ConfigureAwait (false);
		await UpdateAsync (cancellationToken).ConfigureAwait (false);
		}

	private async Task SetLightEffectInternalAsync (string? effect, CancellationToken cancellationToken)
		{
		if (!SupportsLightControl ())
			{
			throw new InvalidOperationException ($"The device '{Host}' does not support light-effect control.");
			}

		await UpdateAsync (cancellationToken).ConfigureAwait (false);
		if (!SupportsLightEffects)
			{
			throw new InvalidOperationException ($"The device '{Host}' does not report light-effect support.");
			}

		if (UsesSmartProtocol () && DeviceType == DeviceType.LightStrip)
			{
			CancellationToken operationCancellationToken = cancellationToken;
			CancellationTokenSource? timeoutOverride = null;
			if (!cancellationToken.IsCancellationRequested && Configuration.Timeout < TimeSpan.FromSeconds (45))
				{
				timeoutOverride = CancellationTokenSource.CreateLinkedTokenSource (cancellationToken);
				timeoutOverride.CancelAfter (TimeSpan.FromSeconds (45));
				operationCancellationToken = timeoutOverride.Token;
				}

			try
				{
				await ExecuteCommandAsync (
					KasaTapoClient.Internal.KasaCommands.CreateSmartRequest (
						"set_lighting_effect",
						KasaTapoClient.Internal.KasaResponseParser.CreateSmartLightStripEffectPayload (effect)),
					operationCancellationToken).ConfigureAwait (false);
				await UpdateAsync (operationCancellationToken).ConfigureAwait (false);
				}
			catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
				{
				await UpdateAsync (cancellationToken).ConfigureAwait (false);
				}
			finally
				{
				timeoutOverride?.Dispose ();
				}
			return;
			}

		await ExecuteCommandAsync (KasaTapoClient.Internal.KasaCommands.CreateSetLightEffectCommand (DeviceType, effect), cancellationToken).ConfigureAwait (false);
		await UpdateAsync (cancellationToken).ConfigureAwait (false);
		}

	private bool UsesSmartProtocol ()
		{
		DeviceConnectionParameters? connectionParameters = Configuration.ConnectionOptions.ConnectionParameters;
		if (connectionParameters is null)
			{
			return false;
			}

		return connectionParameters.DeviceFamily is not DeviceFamilyKind.IotSmartPlugSwitch
			&& connectionParameters.DeviceFamily is not DeviceFamilyKind.IotSmartBulb
			&& connectionParameters.DeviceFamily is not DeviceFamilyKind.IotIpCamera;
		}
	}
