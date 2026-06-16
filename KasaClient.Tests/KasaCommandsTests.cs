// Copyright © 2026 Neil Colvin.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Text.Json.Nodes;

using KasaTapoClient;
using KasaTapoClient.Internal;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace KasaClient.Tests;

[TestClass]
public sealed class KasaCommandsTests
	{
	[TestMethod]
	public void CreateSetChildRelayStateCommand_EmbedsChildContextAndRequestedState ()
		{
		string command = KasaCommands.CreateSetChildRelayStateCommand ("child-1", true);
		JsonObject root = JsonNode.Parse (command)!.AsObject ();

		Assert.AreEqual ("child-1", root["context"]!["child_ids"]![0]!.GetValue<string> ());
		Assert.AreEqual (1, root["system"]!["set_relay_state"]!["state"]!.GetValue<int> ());
		}

	[TestMethod]
	public void CreateSetLightStateCommand_ForBulb_AddsIgnoreDefaultAndTransitionPeriod ()
		{
		string command = KasaCommands.CreateSetLightStateCommand (DeviceType.Bulb, isOn: true, brightness: 25);
		JsonObject root = JsonNode.Parse (command)!.AsObject ();
		JsonObject payload = root["smartlife.iot.smartbulb.lightingservice"]!["transition_light_state"]!.AsObject ();

		Assert.AreEqual (1, payload["on_off"]!.GetValue<int> ());
		Assert.AreEqual (25, payload["brightness"]!.GetValue<int> ());
		Assert.AreEqual (1, payload["ignore_default"]!.GetValue<int> ());
		Assert.AreEqual (0, payload["transition_period"]!.GetValue<int> ());
		}

	[TestMethod]
	public void CreateSetLightEffectCommand_ForBulb_UsesDynamicEffectPayload ()
		{
		string command = KasaCommands.CreateSetLightEffectCommand (DeviceType.Bulb, "L1");
		JsonObject root = JsonNode.Parse (command)!.AsObject ();
		JsonObject payload = root["smartlife.iot.smartbulb.lightingservice"]!["set_dynamic_light_effect_rule_enable"]!.AsObject ();

		Assert.AreEqual (1, payload["enable"]!.GetValue<int> ());
		Assert.AreEqual ("L1", payload["id"]!.GetValue<string> ());
		}

	[TestMethod]
	public void CreateSmartChildRequest_EmbedsChildIdAndRequestedMethod ()
		{
		string command = KasaCommands.CreateSmartChildRequest ("child-42", "get_device_info");
		JsonObject root = JsonNode.Parse (command)!.AsObject ();
		JsonObject payload = root["params"]!.AsObject ();
		JsonObject requestData = payload["requestData"]!.AsObject ();

		Assert.AreEqual ("control_child", root["method"]!.GetValue<string> ());
		Assert.AreEqual ("child-42", payload["device_id"]!.GetValue<string> ());
		Assert.AreEqual ("get_device_info", requestData["method"]!.GetValue<string> ());
		}

	[TestMethod]
	public void CreateSetLightStateCommand_ForColorUpdate_ResetsColorTemperature ()
		{
		string command = KasaCommands.CreateSetLightStateCommand (DeviceType.Bulb, hue: 180, saturation: 50, brightness: 40);
		JsonObject root = JsonNode.Parse (command)!.AsObject ();
		JsonObject payload = root["smartlife.iot.smartbulb.lightingservice"]!["transition_light_state"]!.AsObject ();

		Assert.AreEqual (180, payload["hue"]!.GetValue<int> ());
		Assert.AreEqual (50, payload["saturation"]!.GetValue<int> ());
		Assert.AreEqual (40, payload["brightness"]!.GetValue<int> ());
		Assert.AreEqual (0, payload["color_temp"]!.GetValue<int> ());
		}

	[TestMethod]
	public void CreateSetLightEffectCommand_ForLightStrip_UsesNamedStripPayload ()
		{
		string command = KasaCommands.CreateSetLightEffectCommand (DeviceType.LightStrip, "Aurora");
		JsonObject root = JsonNode.Parse (command)!.AsObject ();
		JsonObject payload = root["smartlife.iot.lightStrip"]!["set_lighting_effect"]!.AsObject ();

		Assert.AreEqual (1, payload["enable"]!.GetValue<int> ());
		Assert.AreEqual ("Aurora", payload["name"]!.GetValue<string> ());
		}
	}
