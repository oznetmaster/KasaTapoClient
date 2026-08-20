// Copyright © 2026 Neil Colvin.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System;
using System.Threading.Tasks;

using KasaTapoClient;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace KasaClient.Tests;

/// <summary>
/// Coverage for dimmer/brightness capability gating, following review feedback on PR #3
/// ("dimmer support (SupportsLightControl DeviceType.Dimmer)"): brightness must be keyed to the
/// capability a device actually advertises (the "brightness" component on SMART) rather than to
/// DeviceType, since KS240 and P135 both dim but never classify as DeviceType.Dimmer. Color
/// temperature and HSV stay keyed to Bulb/LightStrip, and legacy (IOT) dimmer on/off keeps using
/// system.set_relay_state - the legacy IOT dimmer command surface (smartlife.iot.dimmer.*) is
/// intentionally out of scope here.
/// </summary>
[TestClass]
public sealed class DimmerSupportTests
	{
	// Every SMART SendAsync is answered with one envelope. ParseSmartResponse requires
	// get_device_info and component_nego in each response it parses and ignores any method it
	// did not ask for, so a single envelope satisfies the initial update, the module refresh, and
	// the post-command refresh alike.
	private static string SmartEnvelope (string model, string type, string nicknameBase64, string components) => $$"""
		{
		  "result": {
			 "responses": [
				{
				  "method": "get_device_info",
				  "result": {
					 "model": "{{model}}",
					 "type": "{{type}}",
					 "device_id": "device-1",
					 "nickname": "{{nicknameBase64}}",
					 "device_on": true,
					 "brightness": 25,
					 "fw_ver": "1.0.0",
					 "hw_ver": "1.0"
				  }
				},
				{
				  "method": "component_nego",
				  "result": { "component_list": [{{components}}] }
				},
				{ "method": "get_child_device_list", "result": { "child_device_list": [] } },
				{ "method": "set_device_info", "result": { } }
			 ]
		  }
		}
		""";

	private const string DIMMER_COMPONENTS =
		"""{ "id": "device", "ver_code": 2 }, { "id": "brightness", "ver_code": 1 }, { "id": "dimmer_calibration", "ver_code": 1 }""";

	private const string DIMMER_WITH_CHILDREN_COMPONENTS =
		"""{ "id": "device", "ver_code": 2 }, { "id": "brightness", "ver_code": 1 }, { "id": "dimmer_calibration", "ver_code": 1 }, { "id": "child_device", "ver_code": 1 }""";

	private const string LEGACY_MODULE_RESPONSE =
		"{\"emeter\":{\"err_code\":-1},\"time\":{\"get_time\":{\"year\":2025,\"month\":1,\"mday\":2,\"hour\":3,\"min\":4,\"sec\":5}},\"cnCloud\":{\"get_info\":{\"binded\":1,\"cld_connection\":1}},\"count_down\":{\"get_rules\":{\"rule_list\":[]}},\"schedule\":{\"get_rules\":{\"rule_list\":[]}},\"anti_theft\":{\"get_rules\":{\"rule_list\":[]}}}";

	private static string LegacyDimmerSysInfo (int relayState, int brightness) =>
		"{\"system\":{\"get_sysinfo\":{\"alias\":\"Hall Dimmer\",\"model\":\"HS220\",\"deviceId\":\"dimmer-1\",\"type\":\"IOT.DIMMER\",\"relay_state\":"
		+ relayState + ",\"brightness\":" + brightness + "}}}";

	private static KasaDevice CreateSmartDevice (string model, string type, string nicknameBase64, string components, out FakeDeviceTransport transport)
		{
		string envelope = SmartEnvelope (model, type, nicknameBase64, components);
		transport = new FakeDeviceTransport (sendHandler: (_, _) => Task.FromResult (envelope));
		DeviceConfiguration configuration = new (
			"127.0.0.1",
			connectionOptions: new DeviceConnectionOptions (
				connectionParameters: new DeviceConnectionParameters (
					type.Contains ("PLUG") ? DeviceFamilyKind.SmartTapoPlug : DeviceFamilyKind.SmartKasaSwitch,
					DeviceEncryptionKind.Aes)));
		return new KasaDevice (configuration, transport);
		}

	private static KasaDevice CreateSmartDimmer (out FakeDeviceTransport transport) =>
		CreateSmartDevice ("KS225", "SMART.KASASWITCH", "SGFsbCBEaW1tZXI=", DIMMER_COMPONENTS, out transport);

	private static string? FindCommand (FakeDeviceTransport transport, int startIndex, string needle)
		{
		for (int index = startIndex; index < transport.SentCommands.Count; index++)
			{
			if (transport.SentCommands[index].Contains (needle))
				{
				return transport.SentCommands[index];
				}
			}

		return null;
		}

	/// <summary>
	/// A legacy (IOT) dimmer turns on through system.set_relay_state, exactly as a plug does. The
	/// lighting service is not implemented on these devices, so SetRelayStateCoreAsync must never
	/// route a legacy dimmer into SetLightStateCoreAsync.
	/// </summary>
	[TestMethod]
	public async Task TurnOnAsync_WithLegacyDimmer_SendsRelayStateNotLightingService ()
		{
		var transport = new FakeDeviceTransport (
			sendResponses:
			[
				LegacyDimmerSysInfo (relayState: 0, brightness: 25),
				"{\"system\":{\"set_relay_state\":{\"err_code\":0}}}",
				LegacyDimmerSysInfo (relayState: 1, brightness: 25)
			],
			sendManyResponses: [LEGACY_MODULE_RESPONSE, LEGACY_MODULE_RESPONSE]);
		DeviceConfiguration configuration = new ("127.0.0.1");
		var device = new KasaDevice (configuration, transport);
		await device.UpdateAsync ().ConfigureAwait (false);

		// Guard: the fixture must actually classify as a dimmer, otherwise the assertions below
		// would pass for the wrong reason.
		Assert.AreEqual (DeviceType.Dimmer, device.DeviceType);

		await device.TurnOnAsync ().ConfigureAwait (false);

		Assert.AreEqual (3, transport.SentCommands.Count);
		Assert.AreEqual ("{\"system\":{\"set_relay_state\":{\"state\":1}}}", transport.SentCommands[1]);
		Assert.IsFalse (transport.SentCommands[1].Contains ("lightingservice"));
		Assert.AreEqual (true, device.IsOn);
		}

	/// <summary>
	/// A dimmer has no colour temperature. The device should refuse locally rather than emit a
	/// set_device_info the hardware will reject with an opaque protocol error.
	/// </summary>
	[TestMethod]
	public async Task SetColorTemperatureAsync_WithSmartDimmer_Throws ()
		{
		KasaDevice device = CreateSmartDimmer (out FakeDeviceTransport transport);
		await device.UpdateAsync ().ConfigureAwait (false);
		Assert.AreEqual (DeviceType.Dimmer, device.DeviceType);

		await Assert.ThrowsExactlyAsync<InvalidOperationException> (
			() => device.SetColorTemperatureAsync (2700)).ConfigureAwait (false);

		foreach (string command in transport.SentCommands)
			{
			Assert.IsFalse (command.Contains ("color_temp"), $"No colour-temperature command should reach a dimmer, but got: {command}");
			}
		}

	/// <summary>
	/// A dimmer has no color control. Setting HSV should refuse locally rather than send hue/saturation
	/// to hardware that cannot act on them.
	/// </summary>
	[TestMethod]
	public async Task SetHsvAsync_WithSmartDimmer_Throws ()
		{
		KasaDevice device = CreateSmartDimmer (out FakeDeviceTransport transport);
		await device.UpdateAsync ().ConfigureAwait (false);
		Assert.AreEqual (DeviceType.Dimmer, device.DeviceType);

		await Assert.ThrowsExactlyAsync<InvalidOperationException> (
			() => device.SetHsvAsync (120, 50, 40)).ConfigureAwait (false);

		foreach (string command in transport.SentCommands)
			{
			Assert.IsFalse (command.Contains ("\"hue\""), $"No hue/saturation command should reach a dimmer, but got: {command}");
			}
		}

	/// <summary>
	/// A SMART-protocol dimmer accepts brightness through set_device_info, and nothing else.
	/// </summary>
	[TestMethod]
	public async Task SetBrightnessAsync_WithSmartDimmer_SendsBrightnessOnly ()
		{
		KasaDevice device = CreateSmartDimmer (out FakeDeviceTransport transport);
		await device.UpdateAsync ().ConfigureAwait (false);
		Assert.AreEqual (DeviceType.Dimmer, device.DeviceType);

		int commandsBefore = transport.SentCommands.Count;
		await device.SetBrightnessAsync (40).ConfigureAwait (false);

		string setCommand = FindCommand (transport, commandsBefore, "set_device_info")
			?? throw new AssertFailedException ("SetBrightnessAsync should have issued a set_device_info request.");
		StringAssert.Contains (setCommand, "\"brightness\":40");
		Assert.IsFalse (setCommand.Contains ("color_temp"), "Brightness-only change must not carry color_temp.");
		Assert.IsFalse (setCommand.Contains ("\"hue\""), "Brightness-only change must not carry hue.");
		Assert.IsFalse (setCommand.Contains ("saturation"), "Brightness-only change must not carry saturation.");
		}

	/// <summary>
	/// KS240 is a dimmer + fan combo. It reports SMART.KASASWITCH *with* a child_device component,
	/// so the "SWITCH and child_device" branch claims it before the dimmer_calibration branch is
	/// reached, and it classifies as WallSwitch - not DeviceType.Dimmer. Brightness must still work
	/// because the device advertises the "brightness" component; gating on DeviceType alone can
	/// never reach this device.
	/// </summary>
	[TestMethod]
	public async Task SetBrightnessAsync_WithKs240_SucceedsDespiteWallSwitchClassification ()
		{
		KasaDevice device = CreateSmartDevice ("KS240", "SMART.KASASWITCH", "RmFuIERpbW1lcg==", DIMMER_WITH_CHILDREN_COMPONENTS, out FakeDeviceTransport transport);
		await device.UpdateAsync ().ConfigureAwait (false);
		Assert.AreEqual (DeviceType.WallSwitch, device.DeviceType);

		int commandsBefore = transport.SentCommands.Count;
		await device.SetBrightnessAsync (40).ConfigureAwait (false);

		string setCommand = FindCommand (transport, commandsBefore, "set_device_info")
			?? throw new AssertFailedException ("A device advertising the brightness component should accept SetBrightnessAsync.");
		StringAssert.Contains (setCommand, "\"brightness\":40");
		}

	/// <summary>
	/// P135 is a dimmable plug: it advertises both brightness and dimmer_calibration, but reports
	/// SMART.TAPOPLUG, and the "PLUG" branch is tested first in DetermineSmartDeviceType - so it
	/// classifies as Plug and can never reach the dimmer branch. Brightness must still work via the
	/// advertised component.
	/// </summary>
	[TestMethod]
	public async Task SetBrightnessAsync_WithDimmablePlug_SucceedsDespitePlugClassification ()
		{
		KasaDevice device = CreateSmartDevice ("P135", "SMART.TAPOPLUG", "RGltbWFibGUgUGx1Zw==", DIMMER_COMPONENTS, out FakeDeviceTransport transport);
		await device.UpdateAsync ().ConfigureAwait (false);
		Assert.AreEqual (DeviceType.Plug, device.DeviceType);

		int commandsBefore = transport.SentCommands.Count;
		await device.SetBrightnessAsync (40).ConfigureAwait (false);

		string setCommand = FindCommand (transport, commandsBefore, "set_device_info")
			?? throw new AssertFailedException ("A device advertising the brightness component should accept SetBrightnessAsync.");
		StringAssert.Contains (setCommand, "\"brightness\":40");
		}
	}
