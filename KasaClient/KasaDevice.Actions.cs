// Copyright © 2026 Neil Colvin.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// Behavior modeled after the independent python-kasa project (https://github.com/python-kasa/python-kasa)
// for protocol/compatibility reference only; no python-kasa source was copied. See ATTRIBUTIONS.md.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace KasaTapoClient;

public sealed partial class KasaDevice
	{
	private bool SupportsLightControl () => DeviceType is DeviceType.Bulb or DeviceType.LightStrip;

	private async Task SetRelayStateAsync (bool isOn, CancellationToken cancellationToken)
		{
		await RunDeviceOperationAsync (ct => SetRelayStateCoreAsync (isOn, ct), cancellationToken).ConfigureAwait (false);
		}

	private async Task SetRelayStateCoreAsync (bool isOn, CancellationToken cancellationToken)
		{
		if (UsesSmartProtocol ())
			{
			await ExecuteCommandCoreAsync (KasaTapoClient.Internal.KasaCommands.CreateSmartRequest ("set_device_info", new System.Text.Json.Nodes.JsonObject { ["device_on"] = isOn }), cancellationToken).ConfigureAwait (false);
			await UpdateCoreAsync (cancellationToken).ConfigureAwait (false);
			return;
			}

		if (SupportsLightControl ())
			{
			await SetLightStateCoreAsync (isOn: isOn, cancellationToken: cancellationToken).ConfigureAwait (false);
			return;
			}

		await ExecuteCommandCoreAsync (KasaTapoClient.Internal.KasaCommands.CreateSetRelayStateCommand (isOn), cancellationToken).ConfigureAwait (false);
		await UpdateCoreAsync (cancellationToken).ConfigureAwait (false);
		}

	private async Task SetChildRelayStateAsync (string childDeviceId, bool isOn, CancellationToken cancellationToken)
		{
		await RunDeviceOperationAsync (ct => SetChildRelayStateCoreAsync (childDeviceId, isOn, ct), cancellationToken).ConfigureAwait (false);
		}

	private async Task SetChildRelayStateCoreAsync (string childDeviceId, bool isOn, CancellationToken cancellationToken)
		{
		if (GetChild (childDeviceId) is null)
			{
			throw new InvalidOperationException ($"The child device '{childDeviceId}' was not found on '{Host}'.");
			}

		await ExecuteCommandCoreAsync (KasaTapoClient.Internal.KasaCommands.CreateSetChildRelayStateCommand (childDeviceId, isOn), cancellationToken).ConfigureAwait (false);
		await UpdateCoreAsync (cancellationToken).ConfigureAwait (false);
		}

	private async Task SetLightStateAsync (
		bool? isOn = null,
		int? brightness = null,
		int? colorTemperature = null,
		int? hue = null,
		int? saturation = null,
		int? transitionMilliseconds = null,
		CancellationToken cancellationToken = default)
		{
		await RunDeviceOperationAsync (ct => SetLightStateCoreAsync (isOn, brightness, colorTemperature, hue, saturation, transitionMilliseconds, ct), cancellationToken).ConfigureAwait (false);
		}

	private async Task SetLightStateCoreAsync (
		bool? isOn = null,
		int? brightness = null,
		int? colorTemperature = null,
		int? hue = null,
		int? saturation = null,
		int? transitionMilliseconds = null,
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
				await ExecuteCommandCoreAsync (KasaTapoClient.Internal.KasaCommands.CreateSmartRequest ("set_device_info", parameters), operationCancellationToken).ConfigureAwait (false);
				await UpdateCoreAsync (operationCancellationToken).ConfigureAwait (false);
				}
			catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
				{
				await UpdateCoreAsync (cancellationToken).ConfigureAwait (false);
				}
			finally
				{
				timeoutOverride?.Dispose ();
				}
			return;
			}

		await ExecuteCommandCoreAsync (KasaTapoClient.Internal.KasaCommands.CreateSetLightStateCommand (DeviceType, isOn, brightness, colorTemperature, hue, saturation, transitionMilliseconds), cancellationToken).ConfigureAwait (false);
		await UpdateCoreAsync (cancellationToken).ConfigureAwait (false);
		}

	private async Task SetLightEffectInternalAsync (string? effect, CancellationToken cancellationToken)
		{
		await RunDeviceOperationAsync (ct => SetLightEffectCoreAsync (effect, ct), cancellationToken).ConfigureAwait (false);
		}

	private async Task SetLightTransitionsEnabledInternalAsync (bool enabled, CancellationToken cancellationToken)
		{
		await RunDeviceOperationAsync (ct => SetLightTransitionsEnabledCoreAsync (enabled, ct), cancellationToken).ConfigureAwait (false);
		}

	private async Task SetLightTransitionsEnabledCoreAsync (bool enabled, CancellationToken cancellationToken)
		{
		EnsureSmartLightTransitionControlSupported ();
		bool supportsDirectionalStates = _smartComponentVersions.TryGetValue ("on_off_gradually", out int supportedVersion)
			&& supportedVersion >= 2;
		if (supportsDirectionalStates)
			{
			await ExecuteCommandCoreAsync (
				KasaTapoClient.Internal.KasaCommands.CreateSetSmartLightTransitionOnCommand (
					enabled,
					LightTransitionState?.TransitionOnDurationSeconds ?? 0),
				cancellationToken).ConfigureAwait (false);
			await ExecuteCommandCoreAsync (
				KasaTapoClient.Internal.KasaCommands.CreateSetSmartLightTransitionOffCommand (
					enabled,
					LightTransitionState?.TransitionOffDurationSeconds ?? 0),
				cancellationToken).ConfigureAwait (false);
			}
		else
			{
			await ExecuteCommandCoreAsync (KasaTapoClient.Internal.KasaCommands.CreateSetSmartLightTransitionEnabledCommand (enabled), cancellationToken).ConfigureAwait (false);
			}
		await UpdateCoreAsync (cancellationToken).ConfigureAwait (false);
		}

	private async Task SetLightTurnOnTransitionInternalAsync (int seconds, CancellationToken cancellationToken)
		{
		await RunDeviceOperationAsync (ct => SetLightTurnOnTransitionCoreAsync (seconds, ct), cancellationToken).ConfigureAwait (false);
		}

	private async Task SetLightTurnOnTransitionCoreAsync (int seconds, CancellationToken cancellationToken)
		{
		EnsureSmartLightTransitionControlSupported ();
		ValidateSmartLightTransitionSeconds (seconds, LightTransitionState?.TransitionOnMaximumDurationSeconds, LightTransitionState?.TransitionOnSeconds, nameof (seconds));
		int durationSeconds = seconds > 0 ? seconds : LightTransitionState?.TransitionOnDurationSeconds ?? 0;
		await ExecuteCommandCoreAsync (KasaTapoClient.Internal.KasaCommands.CreateSetSmartLightTransitionOnCommand (seconds > 0, durationSeconds), cancellationToken).ConfigureAwait (false);
		await UpdateCoreAsync (cancellationToken).ConfigureAwait (false);
		}

	private async Task SetLightTurnOffTransitionInternalAsync (int seconds, CancellationToken cancellationToken)
		{
		await RunDeviceOperationAsync (ct => SetLightTurnOffTransitionCoreAsync (seconds, ct), cancellationToken).ConfigureAwait (false);
		}

	private async Task SetLightTurnOffTransitionCoreAsync (int seconds, CancellationToken cancellationToken)
		{
		EnsureSmartLightTransitionControlSupported ();
		ValidateSmartLightTransitionSeconds (seconds, LightTransitionState?.TransitionOffMaximumDurationSeconds, LightTransitionState?.TransitionOffSeconds, nameof (seconds));
		int durationSeconds = seconds > 0 ? seconds : LightTransitionState?.TransitionOffDurationSeconds ?? 0;
		await ExecuteCommandCoreAsync (KasaTapoClient.Internal.KasaCommands.CreateSetSmartLightTransitionOffCommand (seconds > 0, durationSeconds), cancellationToken).ConfigureAwait (false);
		await UpdateCoreAsync (cancellationToken).ConfigureAwait (false);
		}

	private void EnsureSmartLightTransitionControlSupported ()
		{
		if (!UsesSmartProtocol () || LightTransitionState is null)
			{
			throw new InvalidOperationException ($"The device '{Host}' does not support smart light transition control.");
			}
		}

	private void ValidateSmartLightTransitionSeconds (int seconds, int? maximumDurationSeconds, int? currentSeconds, string paramName)
		{
		if (seconds < 0)
			{
			throw new ArgumentOutOfRangeException (paramName, seconds, "Transition duration must be zero or greater.");
			}

		if (LightTransitionState is null)
			{
			return;
			}

		const int defaultMaximumSeconds = 60;
		int maximumSeconds = maximumDurationSeconds ?? defaultMaximumSeconds;
		if (currentSeconds is int current && current > maximumSeconds)
			{
			maximumSeconds = current;
			}

		if (seconds > maximumSeconds)
			{
			throw new ArgumentOutOfRangeException (paramName, seconds, $"Transition duration must be between 0 and {maximumSeconds} seconds.");
			}
		}

	private async Task SetLightEffectCoreAsync (string? effect, CancellationToken cancellationToken)
		{
		if (!SupportsLightControl ())
			{
			throw new InvalidOperationException ($"The device '{Host}' does not support light-effect control.");
			}

		await UpdateCoreAsync (cancellationToken).ConfigureAwait (false);
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
				await ExecuteCommandCoreAsync (
					KasaTapoClient.Internal.KasaCommands.CreateSmartRequest (
						"set_lighting_effect",
						KasaTapoClient.Internal.KasaResponseParser.CreateSmartLightStripEffectPayload (effect)),
					operationCancellationToken).ConfigureAwait (false);
				await UpdateCoreAsync (operationCancellationToken).ConfigureAwait (false);
				}
			catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
				{
				await UpdateCoreAsync (cancellationToken).ConfigureAwait (false);
				}
			finally
				{
				timeoutOverride?.Dispose ();
				}
			return;
			}

		await ExecuteCommandCoreAsync (KasaTapoClient.Internal.KasaCommands.CreateSetLightEffectCommand (DeviceType, effect), cancellationToken).ConfigureAwait (false);
		await UpdateCoreAsync (cancellationToken).ConfigureAwait (false);
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
