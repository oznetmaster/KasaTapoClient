// Copyright © 2026 Neil Colvin.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System;
using System.Threading.Tasks;

using KasaTapoClient;

using NUnit.Framework;

namespace KasaClient.Tests;

[TestFixture]
[Category ("Live")]
[NonParallelizable]
public sealed class LiveDeviceIntegrationTests
	{
	[Test]
	[TestCaseSource (typeof (LiveTestSupport), nameof (LiveTestSupport.PlugDevices))]
	public async Task Plug_TurnOffAndTurnOn_RefreshesObservedState (string host)
		{
		using KasaDevice device = await LiveTestSupport.ConnectAsync ("plug", host).ConfigureAwait (false);

		Assert.That (device.SystemInfo, Is.Not.Null);
		bool? originalState = device.IsOn;
		Assert.That (originalState, Is.Not.Null, "Plug should report an initial on/off state.");

		try
			{
			using IDisposable actionTiming = LiveTestSupport.Measure ("Test actions and state verification (including observation pauses)");
			await device.TurnOffAsync ().ConfigureAwait (false);
			await LiveTestSupport.WaitForDevicePowerStateAsync (device, expectedState: false, $"Plug '{host}' off").ConfigureAwait (false);
			await LiveTestSupport.HoldObservableStateAsync ($"Plug '{host}' off").ConfigureAwait (false);
			await device.TurnOnAsync ().ConfigureAwait (false);
			await LiveTestSupport.WaitForDevicePowerStateAsync (device, expectedState: true, $"Plug '{host}' on").ConfigureAwait (false);
			}
		finally
			{
			using IDisposable restorationTiming = LiveTestSupport.Measure ("Restore original state");
			if (originalState == true)
				{
				await device.TurnOnAsync ().ConfigureAwait (false);
				}
			else
				{
				await device.TurnOffAsync ().ConfigureAwait (false);
				}
			await LiveTestSupport.WaitForDevicePowerStateAsync (device, originalState!.Value, $"Plug '{host}' power restore").ConfigureAwait (false);
			}
		}

	[Test]
	[TestCaseSource (typeof (LiveTestSupport), nameof (LiveTestSupport.LightDevices))]
	public async Task Light_TurnOnAndTurnOff_RefreshesObservedLightState (string host)
		{
		using KasaDevice device = await LiveTestSupport.ConnectAsync ("light", host).ConfigureAwait (false);

		Assert.That (device.Light.IsAvailable, Is.True, "Configured light device does not expose light control.");
		bool? originalPower = device.Light.State?.IsOn;
		Assert.That (originalPower, Is.Not.Null, "Light should report an initial on/off state.");

		try
			{
			using IDisposable actionTiming = LiveTestSupport.Measure ("Test actions and state verification (including observation pauses)");
			await device.TurnLightOnAsync ().ConfigureAwait (false);
			await LiveTestSupport.WaitForLightStateAsync (device, expectedPowerState: true, expectedBrightness: null, $"Light '{host}' on").ConfigureAwait (false);
			await LiveTestSupport.HoldObservableStateAsync ($"Light '{host}' on").ConfigureAwait (false);
			await device.TurnLightOffAsync ().ConfigureAwait (false);
			await LiveTestSupport.WaitForLightStateAsync (device, expectedPowerState: false, expectedBrightness: null, $"Light '{host}' off").ConfigureAwait (false);
			}
		finally
			{
			using IDisposable restorationTiming = LiveTestSupport.Measure ("Restore original state");
			if (originalPower == true)
				{
				await device.TurnLightOnAsync ().ConfigureAwait (false);
				}
			else
				{
				await device.TurnLightOffAsync ().ConfigureAwait (false);
				}
			await LiveTestSupport.WaitForLightStateAsync (device, originalPower!.Value, expectedBrightness: null, $"Light '{host}' power restore").ConfigureAwait (false);
			}
		}

	[Test]
	[TestCaseSource (typeof (LiveTestSupport), nameof (LiveTestSupport.LightDevices))]
	public async Task Light_SetBrightness_RefreshesObservedLightState (string host)
		{
		using KasaDevice device = await LiveTestSupport.ConnectAsync ("light", host).ConfigureAwait (false);

		Assert.That (device.Light.IsAvailable, Is.True, "Configured light device does not expose light control.");
		int? originalBrightness = device.Light.State?.Brightness;
		bool? originalPower = device.Light.State?.IsOn;
		Assert.That (originalPower, Is.Not.Null, "Light should report an initial on/off state.");
		Assert.That (originalBrightness, Is.Not.Null, "Light should report an initial brightness so it can be restored.");
		const int TARGET_BRIGHTNESS = 25;

		try
			{
			using IDisposable actionTiming = LiveTestSupport.Measure ("Test actions and state verification (including observation pauses)");
			await device.TurnLightOnAsync ().ConfigureAwait (false);
			await LiveTestSupport.WaitForLightStateAsync (device, expectedPowerState: true, expectedBrightness: null, $"Light '{host}' on before brightness").ConfigureAwait (false);
			await LiveTestSupport.HoldObservableStateAsync ($"Light '{host}' on before brightness").ConfigureAwait (false);
			await device.SetBrightnessAsync (TARGET_BRIGHTNESS).ConfigureAwait (false);
			await LiveTestSupport.WaitForLightStateAsync (device, expectedPowerState: true, expectedBrightness: TARGET_BRIGHTNESS, $"Light '{host}'").ConfigureAwait (false);
			await LiveTestSupport.HoldObservableStateAsync ($"Light '{host}' brightness {TARGET_BRIGHTNESS}").ConfigureAwait (false);
			}
		finally
			{
			using IDisposable restorationTiming = LiveTestSupport.Measure ("Restore original state");
			try
				{
				await device.TurnLightOnAsync ().ConfigureAwait (false);
				await device.SetBrightnessAsync (originalBrightness!.Value).ConfigureAwait (false);
				await LiveTestSupport.WaitForLightStateAsync (device, expectedPowerState: true, originalBrightness.Value, $"Light '{host}' brightness restore").ConfigureAwait (false);
				}
			finally
				{
				if (originalPower == true)
					{
					await device.TurnLightOnAsync ().ConfigureAwait (false);
					}
				else
					{
					await device.TurnLightOffAsync ().ConfigureAwait (false);
					}
				await LiveTestSupport.WaitForLightStateAsync (device, originalPower!.Value, expectedBrightness: null, $"Light '{host}' power restore").ConfigureAwait (false);
				}
			}
		}

	[Test]
	[TestCaseSource (typeof (LiveTestSupport), nameof (LiveTestSupport.StripDevices))]
	public async Task StripChild_TurnsOnThenOff_RestoresOriginalState (string host)
		{
		using KasaDevice device = await LiveTestSupport.ConnectAsync ("strip", host).ConfigureAwait (false);
		string childDeviceId = LiveTestSupport.GetRequiredSetting ("KASA_LIVE_STRIP_CHILD_ID", host);
		ChildDeviceInfo? child = device.GetChild (childDeviceId);
		Assert.That (child, Is.Not.Null, $"Strip child '{childDeviceId}' was not found after refresh.");
		bool? originalState = child.IsOn;
		Assert.That (originalState, Is.Not.Null, "Strip child should report an initial on/off state.");

		async Task SetChildPowerAsync (bool isOn, string description)
			{
			if (isOn)
				{
				await device.TurnChildOnAsync (childDeviceId).ConfigureAwait (false);
				}
			else
				{
				await device.TurnChildOffAsync (childDeviceId).ConfigureAwait (false);
				}
			await LiveTestSupport.WaitForConditionAsync (
				async () =>
					{
						await device.UpdateAsync ().ConfigureAwait (false);
						return device.GetChild (childDeviceId)?.IsOn == isOn;
					},
				$"Strip child '{childDeviceId}' did not report {description} within the live test timeout.").ConfigureAwait (false);
			}

		try
			{
			using IDisposable actionTiming = LiveTestSupport.Measure ("Test actions and state verification (including observation pauses)");
			await SetChildPowerAsync (false, "off before the on/off test").ConfigureAwait (false);
			await SetChildPowerAsync (true, "on").ConfigureAwait (false);
			await LiveTestSupport.HoldObservableStateAsync ($"Strip child '{childDeviceId}' on").ConfigureAwait (false);
			await SetChildPowerAsync (false, "off").ConfigureAwait (false);
			}
		finally
			{
			using IDisposable restorationTiming = LiveTestSupport.Measure ("Restore original state");
			await SetChildPowerAsync (originalState!.Value, "its original power state").ConfigureAwait (false);
			}
		}
	}