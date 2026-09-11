// Copyright © 2026 Neil Colvin.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Threading.Tasks;

using KasaTapoClient;

using NUnit.Framework;

namespace KasaClient.Tests;

/// <summary>
/// Guards on dimmer behaviour that works today and must keep working.
///
/// Both tests pass, and both would fail if light-state control were extended to
/// DeviceType.Dimmer by widening KasaDevice.SupportsLightControl (), because
/// KasaCommands.CreateSetLightStateCommand throws for any type that is neither Bulb nor
/// LightStrip and the SMART path would start emitting colour payloads to a device with no
/// colour capability.
///
/// Specs for dimmer behaviour that is not implemented yet live in DimmerSupportTests.cs
/// on the dimmer-support branch.
/// </summary>
[TestFixture]
public sealed class DimmerRegressionTests
	{
	/// <summary>
	/// A legacy (IOT) dimmer turns on through system.set_relay_state, exactly as a plug does.
	/// python-kasa models IotDimmer as a subclass of IotPlug for the same reason: the smartbulb
	/// lighting service is not implemented on these devices.
	/// </summary>
	[Test]
	public async Task TurnOnAsync_WithLegacyDimmer_SendsRelayStateNotLightingService ()
		{
		var transport = new FakeDeviceTransport (
			sendResponses:
			[
				DimmerTestSupport.LegacyDimmerSysInfo (relayState: 0, brightness: 25),
				"{\"system\":{\"set_relay_state\":{\"err_code\":0}}}",
				DimmerTestSupport.LegacyDimmerSysInfo (relayState: 1, brightness: 25)
			],
			sendManyResponses: [DimmerTestSupport.LEGACY_MODULE_RESPONSE, DimmerTestSupport.LEGACY_MODULE_RESPONSE]);
		DeviceConfiguration configuration = new ("127.0.0.1");
		var device = new KasaDevice (configuration, transport);

		await device.UpdateAsync ().ConfigureAwait (false);

		// Guard: the fixture must actually classify as a dimmer, otherwise the assertions
		// below would pass for the wrong reason.
		Assert.That (device.DeviceType, Is.EqualTo (DeviceType.Dimmer));

		await device.TurnOnAsync ().ConfigureAwait (false);

		Assert.That (transport.SentCommands.Count, Is.EqualTo (3));
		Assert.That (transport.SentCommands[1], Is.EqualTo ("{\"system\":{\"set_relay_state\":{\"state\":1}}}"));
		Assert.That (transport.SentCommands[1].Contains ("lightingservice"), Is.False);
		Assert.That (device.IsOn, Is.EqualTo (true));
		}

	/// <summary>
	/// A dimmer has no colour temperature. The device should refuse locally rather than emit a
	/// set_device_info the hardware will reject with an opaque protocol error.
	/// </summary>
	[Test]
	public async Task SetColorTemperatureAsync_WithSmartDimmer_Throws ()
		{
		KasaDevice device = DimmerTestSupport.CreateSmartDimmer (out FakeDeviceTransport transport);

		await device.UpdateAsync ().ConfigureAwait (false);
		Assert.That (device.DeviceType, Is.EqualTo (DeviceType.Dimmer));

		await Assert.ThatAsync (() => device.SetColorTemperatureAsync (2700), Throws.TypeOf<InvalidOperationException> ()).ConfigureAwait (false);

		foreach (string command in transport.SentCommands)
			{
			Assert.That (command.Contains ("color_temp"), Is.False, $"No colour-temperature command should reach a dimmer, but got: {command}");
			}
		}
	}