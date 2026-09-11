// Copyright © 2026 Neil Colvin.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using Newtonsoft.Json.Linq;

using KasaTapoClient;
using KasaTapoClient.Internal;

using NUnit.Framework;

namespace KasaClient.Tests;

[TestFixture]
public sealed class KasaCommandsTests
	{
	[Test]
	public void CreateSetChildRelayStateCommand_EmbedsChildContextAndRequestedState ()
		{
		string command = KasaCommands.CreateSetChildRelayStateCommand ("child-1", true);
		JObject root = (JToken.Parse (command) as JObject)!;

		Assert.That (root["context"]!["child_ids"]![0]!.GetValue<string> (), Is.EqualTo ("child-1"));
		Assert.That (root["system"]!["set_relay_state"]!["state"]!.GetValue<int> (), Is.EqualTo (1));
		}

	[Test]
	public void CreateSetLightStateCommand_ForBulb_AddsIgnoreDefaultAndTransitionPeriod ()
		{
		string command = KasaCommands.CreateSetLightStateCommand (DeviceType.Bulb, isOn: true, brightness: 25);
		JObject root = (JToken.Parse (command) as JObject)!;
		JObject payload = (root["smartlife.iot.smartbulb.lightingservice"]!["transition_light_state"] as JObject)!;

		Assert.That (payload["on_off"]!.GetValue<int> (), Is.EqualTo (1));
		Assert.That (payload["brightness"]!.GetValue<int> (), Is.EqualTo (25));
		Assert.That (payload["ignore_default"]!.GetValue<int> (), Is.EqualTo (1));
		Assert.That (payload["transition_period"]!.GetValue<int> (), Is.EqualTo (0));
		}

	[Test]
	public void CreateSetLightStateCommand_ForBulb_UsesRequestedTransitionPeriod ()
		{
		string command = KasaCommands.CreateSetLightStateCommand (DeviceType.Bulb, isOn: true, brightness: 25, transitionMilliseconds: 1500);
		JObject root = (JToken.Parse (command) as JObject)!;
		JObject payload = (root["smartlife.iot.smartbulb.lightingservice"]!["transition_light_state"] as JObject)!;

		Assert.That (payload["transition_period"]!.GetValue<int> (), Is.EqualTo (1500));
		}

	[Test]
	public void CreateSetLightEffectCommand_ForBulb_UsesDynamicEffectPayload ()
		{
		string command = KasaCommands.CreateSetLightEffectCommand (DeviceType.Bulb, "L1");
		JObject root = (JToken.Parse (command) as JObject)!;
		JObject payload = (root["smartlife.iot.smartbulb.lightingservice"]!["set_dynamic_light_effect_rule_enable"] as JObject)!;

		Assert.That (payload["enable"]!.GetValue<int> (), Is.EqualTo (1));
		Assert.That (payload["id"]!.GetValue<string> (), Is.EqualTo ("L1"));
		}

	[Test]
	public void CreateSmartChildRequest_EmbedsChildIdAndRequestedMethod ()
		{
		string command = KasaCommands.CreateSmartChildRequest ("child-42", "get_device_info");
		JObject root = (JToken.Parse (command) as JObject)!;
		JObject payload = (root["params"] as JObject)!;
		JObject requestData = (payload["requestData"] as JObject)!;

		Assert.That (root["method"]!.GetValue<string> (), Is.EqualTo ("control_child"));
		Assert.That (payload["device_id"]!.GetValue<string> (), Is.EqualTo ("child-42"));
		Assert.That (requestData["method"]!.GetValue<string> (), Is.EqualTo ("get_device_info"));
		}

	[Test]
	public void CreateSetLightStateCommand_ForColorUpdate_ResetsColorTemperature ()
		{
		string command = KasaCommands.CreateSetLightStateCommand (DeviceType.Bulb, hue: 180, saturation: 50, brightness: 40);
		JObject root = (JToken.Parse (command) as JObject)!;
		JObject payload = (root["smartlife.iot.smartbulb.lightingservice"]!["transition_light_state"] as JObject)!;

		Assert.That (payload["hue"]!.GetValue<int> (), Is.EqualTo (180));
		Assert.That (payload["saturation"]!.GetValue<int> (), Is.EqualTo (50));
		Assert.That (payload["brightness"]!.GetValue<int> (), Is.EqualTo (40));
		Assert.That (payload["color_temp"]!.GetValue<int> (), Is.EqualTo (0));
		}

	[Test]
	public void CreateSetLightEffectCommand_ForLightStrip_UsesNamedStripPayload ()
		{
		string command = KasaCommands.CreateSetLightEffectCommand (DeviceType.LightStrip, "Aurora");
		JObject root = (JToken.Parse (command) as JObject)!;
		JObject payload = (root["smartlife.iot.lightStrip"]!["set_lighting_effect"] as JObject)!;

		Assert.That (payload["enable"]!.GetValue<int> (), Is.EqualTo (1));
		Assert.That (payload["name"]!.GetValue<string> (), Is.EqualTo ("Aurora"));
		}

	[Test]
	public void CreateSetSmartLightTransitionEnabledCommand_UsesEnablePayload ()
		{
		string command = KasaCommands.CreateSetSmartLightTransitionEnabledCommand (true);
		JObject root = (JToken.Parse (command) as JObject)!;
		JObject payload = (root["params"] as JObject)!;

		Assert.That (root["method"]!.GetValue<string> (), Is.EqualTo (KasaCommands.SMART_SET_ON_OFF_GRADUALLY_INFO_METHOD));
		Assert.That (payload["enable"]!.GetValue<bool> (), Is.EqualTo (true));
		}

	[Test]
	public void CreateSetSmartLightTransitionOnCommand_UsesOnStatePayload ()
		{
		string command = KasaCommands.CreateSetSmartLightTransitionOnCommand (true, 12);
		JObject root = (JToken.Parse (command) as JObject)!;
		JObject payload = (root["params"] as JObject)!;
		JObject onState = (payload["on_state"] as JObject)!;

		Assert.That (root["method"]!.GetValue<string> (), Is.EqualTo (KasaCommands.SMART_SET_ON_OFF_GRADUALLY_INFO_METHOD));
		Assert.That (onState["enable"]!.GetValue<bool> (), Is.EqualTo (true));
		Assert.That (onState["duration"]!.GetValue<int> (), Is.EqualTo (12));
		}

	[Test]
	public void CreateSetSmartLightTransitionOffCommand_UsesOffStatePayload ()
		{
		string command = KasaCommands.CreateSetSmartLightTransitionOffCommand (false, 0);
		JObject root = (JToken.Parse (command) as JObject)!;
		JObject payload = (root["params"] as JObject)!;
		JObject offState = (payload["off_state"] as JObject)!;

		Assert.That (root["method"]!.GetValue<string> (), Is.EqualTo (KasaCommands.SMART_SET_ON_OFF_GRADUALLY_INFO_METHOD));
		Assert.That (offState["enable"]!.GetValue<bool> (), Is.EqualTo (false));
		Assert.That (offState["duration"]!.GetValue<int> (), Is.EqualTo (0));
		}
	}