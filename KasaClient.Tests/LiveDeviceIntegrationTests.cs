// Copyright © 2026 Neil Colvin.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System;
using System.Threading.Tasks;

using KasaTapoClient;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace KasaClient.Tests;

[TestClass]
[TestCategory("Live")]
[DoNotParallelize]
public sealed class LiveDeviceIntegrationTests
	{
	[TestMethod]
	[DynamicData (nameof (LiveTestSupport.PlugDevices), typeof (LiveTestSupport), DynamicDataSourceType.Property)]
	public async Task Plug_TurnOffAndTurnOn_RefreshesObservedState (string host)
		{
		using KasaDevice device = await LiveTestSupport.ConnectAsync ("plug", host).ConfigureAwait (false);

		Assert.IsNotNull (device.SystemInfo);
		bool? originalState = device.IsOn;
		Assert.IsNotNull (originalState, "Plug should report an initial on/off state.");

		await device.TurnOnAsync ().ConfigureAwait (false);
		await LiveTestSupport.WaitForDevicePowerStateAsync (device, expectedState: true, $"Plug '{host}'").ConfigureAwait (false);

		if (originalState == false)
			{
			await device.TurnOffAsync ().ConfigureAwait (false);
			await LiveTestSupport.WaitForDevicePowerStateAsync (device, expectedState: false, $"Plug '{host}'").ConfigureAwait (false);
			}
		}

	[TestMethod]
	[DynamicData (nameof (LiveTestSupport.LightDevices), typeof (LiveTestSupport), DynamicDataSourceType.Property)]
	public async Task Light_TurnOnAndTurnOff_RefreshesObservedLightState (string host)
		{
		using KasaDevice device = await LiveTestSupport.ConnectAsync ("light", host).ConfigureAwait (false);

		Assert.IsTrue (device.Light.IsAvailable, "Configured light device does not expose light control.");
		bool? originalPower = device.Light.State?.IsOn;
		Assert.IsNotNull (originalPower, "Light should report an initial on/off state.");

		await device.TurnLightOnAsync ().ConfigureAwait (false);
		await LiveTestSupport.WaitForLightStateAsync (device, expectedPowerState: true, expectedBrightness: null, $"Light '{host}' on").ConfigureAwait (false);
		await LiveTestSupport.HoldObservableStateAsync ($"Light '{host}' on").ConfigureAwait (false);

		if (originalPower == false)
			{
			await device.TurnLightOffAsync ().ConfigureAwait (false);
			await LiveTestSupport.WaitForLightStateAsync (device, expectedPowerState: false, expectedBrightness: null, $"Light '{host}' off").ConfigureAwait (false);
			}
		}

	[TestMethod]
	[DynamicData (nameof (LiveTestSupport.LightDevices), typeof (LiveTestSupport), DynamicDataSourceType.Property)]
	public async Task Light_SetBrightness_RefreshesObservedLightState (string host)
		{
		using KasaDevice device = await LiveTestSupport.ConnectAsync ("light", host).ConfigureAwait (false);

		Assert.IsTrue (device.Light.IsAvailable, "Configured light device does not expose light control.");
		int? originalBrightness = device.Light.State?.Brightness;
		bool? originalPower = device.Light.State?.IsOn;
		Assert.IsNotNull (originalPower, "Light should report an initial on/off state.");
		const int targetBrightness = 25;

		await device.TurnLightOnAsync ().ConfigureAwait (false);
		await LiveTestSupport.WaitForLightStateAsync (device, expectedPowerState: true, expectedBrightness: null, $"Light '{host}' on before brightness").ConfigureAwait (false);
		await LiveTestSupport.HoldObservableStateAsync ($"Light '{host}' on before brightness").ConfigureAwait (false);
		await device.SetBrightnessAsync (targetBrightness).ConfigureAwait (false);
		await LiveTestSupport.WaitForLightStateAsync (device, expectedPowerState: true, expectedBrightness: targetBrightness, $"Light '{host}'").ConfigureAwait (false);
		await LiveTestSupport.HoldObservableStateAsync ($"Light '{host}' brightness {targetBrightness}").ConfigureAwait (false);

		if (originalBrightness is int brightnessToRestore)
			{
			await device.SetBrightnessAsync (brightnessToRestore).ConfigureAwait (false);
			await LiveTestSupport.WaitForLightStateAsync (device, expectedPowerState: true, expectedBrightness: brightnessToRestore, $"Light '{host}' brightness restore").ConfigureAwait (false);
			}

		if (originalPower == false)
			{
			await device.TurnLightOffAsync ().ConfigureAwait (false);
			await LiveTestSupport.WaitForLightStateAsync (device, expectedPowerState: false, expectedBrightness: null, $"Light '{host}' power restore").ConfigureAwait (false);
			}
		}

	[TestMethod]
	[DynamicData (nameof (LiveTestSupport.StripDevices), typeof (LiveTestSupport), DynamicDataSourceType.Property)]
	public async Task StripChild_InitiallyOff_TurnsOnThenOff_RefreshesObservedState (string host)
		{
		using KasaDevice device = await LiveTestSupport.ConnectAsync ("strip", host).ConfigureAwait (false);
		string childDeviceId = LiveTestSupport.GetRequiredSetting ("KASA_LIVE_STRIP_CHILD_ID", host);
		ChildDeviceInfo? child = device.GetChild (childDeviceId);
		Assert.IsNotNull (child, $"Strip child '{childDeviceId}' was not found after refresh.");
		Assert.AreEqual (false, child.IsOn, "Configured strip child must start off for the live on/off test.");

		await device.TurnChildOnAsync (childDeviceId).ConfigureAwait (false);
		await LiveTestSupport.WaitForConditionAsync (
			async () =>
				{
				await device.UpdateAsync ().ConfigureAwait (false);
				return device.GetChild (childDeviceId)?.IsOn == true;
				},
			$"Strip child '{childDeviceId}' did not report on within the live test timeout.").ConfigureAwait (false);
		await LiveTestSupport.HoldObservableStateAsync ($"Strip child '{childDeviceId}' on").ConfigureAwait (false);

		await device.TurnChildOffAsync (childDeviceId).ConfigureAwait (false);
		await LiveTestSupport.WaitForConditionAsync (
			async () =>
				{
				await device.UpdateAsync ().ConfigureAwait (false);
				return device.GetChild (childDeviceId)?.IsOn == false;
				},
			$"Strip child '{childDeviceId}' did not report off within the live test timeout.").ConfigureAwait (false);
		}
	}
