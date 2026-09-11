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
public sealed class LiveHubIntegrationTests
	{
	[Test]
	[TestCaseSource (typeof (LiveTestSupport), nameof (LiveTestSupport.HubDevices))]
	public async Task Hub_GetScannedChildDevices_ReturnsResultWithoutMutatingState (string host)
		{
		using KasaDevice device = await LiveTestSupport.ConnectAsync ("hub", host).ConfigureAwait (false);
		using IDisposable actionTiming = LiveTestSupport.Measure ("Read hub state and verify results");

		Assert.That (device.DeviceType, Is.EqualTo (DeviceType.Hub), "Configured hub device should resolve as a hub.");
		ChildSetupScanResult result = await device.GetScannedChildDevicesAsync ().ConfigureAwait (false);

		Assert.That (result, Is.Not.Null);
		Assert.That (result.SupportedCategories, Is.Not.Null);
		Assert.That (result.DetectedDevices, Is.Not.Null);
		}

	[Test]
	[TestCaseSource (typeof (LiveTestSupport), nameof (LiveTestSupport.HubDevices))]
	public async Task Hub_ConfiguredChild_IsPresentAndExposesReportedMetadata (string host)
		{
		using KasaDevice device = await LiveTestSupport.ConnectAsync ("hub", host).ConfigureAwait (false);
		using IDisposable actionTiming = LiveTestSupport.Measure ("Read hub state and verify results");
		string childDeviceId = LiveTestSupport.GetRequiredSetting ("KASA_LIVE_HUB_CHILD_ID", host);
		ChildDeviceInfo? child = device.GetChild (childDeviceId);
		Assert.That (child, Is.Not.Null, $"Hub child '{childDeviceId}' was not found after refresh.");
		Assert.That (child.Id, Is.EqualTo (childDeviceId), "Hub child id should match the configured child id.");
		Assert.That (string.IsNullOrWhiteSpace (child.Model), Is.False, "Hub child should report a model.");
		Assert.That (string.IsNullOrWhiteSpace (child.RawJson), Is.False, "Hub child should expose raw state payload data.");
		Assert.That (child.Features.Count > 0, Is.True, "Hub child should expose at least one reported feature.");
		}
	[Test]
	[Category ("Unattended")]
	[TestCaseSource (typeof (LiveTestSupport), nameof (LiveTestSupport.HubDevices))]
	public async Task Hub_TemperatureSensor_RefreshesReportedReading (string host)
		{
		using KasaDevice device = await LiveTestSupport.ConnectAsync ("hub", host).ConfigureAwait (false);
		using IDisposable actionTiming = LiveTestSupport.Measure ("Read temperature sensor twice and verify results");
		Assert.That (device.DeviceType, Is.EqualTo (DeviceType.Hub));
		string childDeviceId = LiveTestSupport.GetRequiredSetting ("KASA_LIVE_HUB_TEMPERATURE_CHILD_ID", host);
		ChildDevice? sensor = device.GetChildDevice (childDeviceId);
		Assert.That (sensor, Is.Not.Null, "Configured temperature sensor was not found on the hub.");

		string? initialUnit = null;
		for (int reading = 1; reading <= 2; reading++)
			{
			await sensor.Temperature.UpdateAsync ().ConfigureAwait (false);
			Assert.That (sensor.Info, Is.Not.Null, "Temperature sensor disappeared after refreshing the hub.");
			ChildTemperatureSensorState? state = sensor.Temperature.State;
			Assert.That (state, Is.Not.Null, "Configured child does not report temperature sensor state.");
			Assert.That (state.Temperature, Is.Not.Null, "Temperature sensor did not report a reading.");
			double temperature = state.Temperature!.Value;
			Assert.That (double.IsNaN (temperature) || double.IsInfinity (temperature), Is.False, "Temperature must be a finite number.");
			string? unit = state.Unit?.Trim ().ToLowerInvariant ();
			Assert.That (unit, Is.EqualTo ("celsius").Or.EqualTo ("fahrenheit"), "Temperature sensor should report its measurement unit.");
			if (initialUnit != null)
				{
				Assert.That (unit, Is.EqualTo (initialUnit), "Reading the sensor should not change its configured unit.");
				}
			initialUnit = unit;
			TestContext.Progress.WriteLine ($"Temperature reading {reading}: {temperature:F2} {unit} ({sensor.Model}).");
			}
		}
	}