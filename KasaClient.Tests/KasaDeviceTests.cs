// Copyright © 2026 Neil Colvin.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Threading;
using System.Threading.Tasks;

using KasaTapoClient;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace KasaClient.Tests;

[TestClass]
public sealed class KasaDeviceTests
	{
	[TestMethod]
	public async Task UpdateAsync_WithLegacyResponse_PopulatesSystemInfoAndFeatures ()
		{
		var transport = new FakeDeviceTransport (
			sendResponses:
			[
				"{\"system\":{\"get_sysinfo\":{\"alias\":\"Test Plug\",\"model\":\"HS100\",\"deviceId\":\"device-1\",\"relay_state\":1,\"on_time\":120}}}"
			],
			sendManyResponses:
			[
				"{\"emeter\":{\"err_code\":-1},\"time\":{\"get_time\":{\"year\":2025,\"month\":1,\"mday\":2,\"hour\":3,\"min\":4,\"sec\":5}},\"cnCloud\":{\"get_info\":{\"binded\":1,\"cld_connection\":1,\"server\":\"example\",\"username\":\"user@example.com\"}},\"count_down\":{\"get_rules\":{\"rule_list\":[]}},\"schedule\":{\"get_rules\":{\"rule_list\":[]}},\"anti_theft\":{\"get_rules\":{\"rule_list\":[]}}}"
			]);
		DeviceConfiguration configuration = new ("127.0.0.1");
		var device = new KasaDevice (configuration, transport);

		await device.UpdateAsync ().ConfigureAwait (false);

		Assert.IsNotNull (device.SystemInfo);
		Assert.AreEqual ("Test Plug", device.SystemInfo.Alias);
		Assert.AreEqual (DeviceType.Plug, device.DeviceType);
		Assert.AreEqual (true, device.IsOn);
		Assert.AreEqual (1, transport.SentCommands.Count);
		Assert.AreEqual (1, transport.SentManyCommands.Count);
		Assert.AreEqual (KasaTapoClient.Internal.KasaCommands.GET_SYSTEM_INFO, transport.SentCommands[0]);
		Assert.IsNotNull (device.GetFeature ("state"));
		Assert.IsNotNull (device.GetFeature ("reboot"));
		}

	[TestMethod]
	public async Task UpdateAsync_WithSmartResponse_PopulatesSmartStatesAndRefreshesModules ()
		{
		var transport = new FakeDeviceTransport (
			sendResponses:
			[
				"""
				{
				  "result": {
					 "responses": [
						{
						  "method": "get_device_info",
						  "result": {
							 "model": "P110",
							 "type": "SMART.TAPOPLUG",
							 "device_id": "smart-plug-1",
							 "nickname": "U21hcnQgUGx1Zw==",
							 "device_on": true,
							 "fw_ver": "1.0.0",
							 "hw_ver": "1.0"
						  }
						},
						{
						  "method": "component_nego",
						  "result": {
							 "component_list": [
								{ "id": "cloud_connect", "ver_code": 1 },
								{ "id": "energy_monitoring", "ver_code": 2 },
								{ "id": "auto_off", "ver_code": 1 },
								{ "id": "led", "ver_code": 1 },
								{ "id": "time", "ver_code": 1 }
							 ]
						  }
						}
					 ]
				  }
				}
				""",
				"""
				{
				  "result": {
					 "responses": [
						{ "method": "get_connect_cloud_state", "result": { "status": 0 } },
						{ "method": "get_energy_usage", "result": { "current_power": 12345, "today_energy": 678 } },
						{ "method": "get_current_power", "result": { "current_power": 12.345 } },
						{ "method": "get_emeter_data", "result": { "voltage_mv": 230000, "current_ma": 100 } },
						{ "method": "get_emeter_vgain_igain", "result": { } },
						{ "method": "get_auto_off_config", "result": { "enable": true, "delay_min": 20 } },
						{ "method": "get_led_info", "result": { "led_rule": "never" } },
						{ "method": "get_device_time", "result": { "timestamp": 1735787045, "region": "UTC", "time_diff": 0 } }
					 ]
				  }
				}
				"""
			]);
		DeviceConfiguration configuration = new (
			"127.0.0.1",
			connectionOptions: new DeviceConnectionOptions (
				connectionParameters: new DeviceConnectionParameters (DeviceFamilyKind.SmartTapoPlug, DeviceEncryptionKind.Aes)));
		var device = new KasaDevice (configuration, transport);

		await device.UpdateAsync ().ConfigureAwait (false);

		Assert.IsNotNull (device.SystemInfo);
		Assert.AreEqual ("Smart Plug", device.SystemInfo.Alias);
		Assert.AreEqual (DeviceType.Plug, device.DeviceType);
		Assert.AreEqual (true, device.IsOn);
		Assert.AreEqual (2, transport.SentCommands.Count);
		Assert.AreEqual (0, transport.SentManyCommands.Count);
		Assert.IsNotNull (device.CloudState);
		Assert.AreEqual (true, device.CloudState.IsConnected);
		Assert.IsNotNull (device.AutoOffState);
		Assert.AreEqual (20, device.AutoOffState.DelayMinutes);
		Assert.IsNotNull (device.LedState);
		Assert.AreEqual (false, device.LedState.Enabled);
		Assert.IsNotNull (device.EnergyUsage);
		Assert.AreEqual (12.345d, device.EnergyUsage.CurrentPowerWatts);
		Assert.IsNotNull (device.TimeState);
		Assert.AreEqual ("UTC", device.TimeState.Region);
		Assert.IsNotNull (device.GetFeature ("state"));
		}

	[TestMethod]
	public async Task UpdateAsync_WithSmartHubChildResponse_MergesChildRefreshData ()
		{
		var transport = new FakeDeviceTransport (
			sendResponses:
			[
				"""
				{
				  "result": {
					 "responses": [
						{
						  "method": "get_device_info",
						  "result": {
							 "model": "H100",
							 "type": "SMART.TAPOHUB",
							 "device_id": "hub-1",
							 "nickname": "U21hcnQgSHVi",
							 "fw_ver": "1.0.0",
							 "hw_ver": "1.0"
						  }
						},
						{
						  "method": "component_nego",
						  "result": {
							 "component_list": [
								{ "id": "child_device", "ver_code": 1 }
							 ]
						  }
						},
						{
						  "method": "get_child_device_list",
						  "result": {
							 "child_device_list": [
								{
								  "device_id": "child-1",
								  "nickname": "QnV0dG9uIDE=",
								  "model": "S200B",
								  "category": "subg.trigger.button",
								  "device_on": true
								}
							 ]
						  }
						},
						{
						  "method": "get_child_device_component_list",
						  "result": {
							 "child_component_list": [
								{
								  "device_id": "child-1",
								  "component_list": [
									 { "id": "double_click", "ver_code": 1 },
									 { "id": "trigger_log", "ver_code": 1 }
								  ]
								}
							 ]
						  }
						}
					 ]
				  }
				}
				""",
				"""
				{
				  "result": {
					 "responseData": {
						"result": {
						  "responses": [
							 { "method": "get_double_click_info", "result": { "enable": true } },
							 { "method": "get_trigger_logs", "result": { "logs": [ { "id": 7, "event_id": "singleClick", "timestamp": 1700000000, "event": "click" } ] } }
						  ]
						}
					 }
				  }
				}
				"""
			]);
		DeviceConfiguration configuration = new (
			"127.0.0.1",
			connectionOptions: new DeviceConnectionOptions (
				connectionParameters: new DeviceConnectionParameters (DeviceFamilyKind.SmartTapoHub, DeviceEncryptionKind.Aes)));
		var device = new KasaDevice (configuration, transport);

		await device.UpdateAsync ().ConfigureAwait (false);

		Assert.AreEqual (DeviceType.Hub, device.DeviceType);
		Assert.AreEqual (1, device.Children.Count);
		ChildDeviceInfo? child = device.GetChild ("child-1");
		Assert.IsNotNull (child);
		Assert.AreEqual ("Button 1", child.Alias);
		Assert.AreEqual (DeviceType.Sensor, child.DeviceType);
		ChildDevice? childDevice = device.GetChildDevice ("child-1");
		Assert.IsNotNull (childDevice);
		Assert.AreEqual (true, childDevice.DoubleClick.Enabled);
		Assert.AreEqual (1, childDevice.TriggerLogs.Logs.Count);
		Assert.AreEqual ("click", childDevice.TriggerLogs.Logs[0].EventName);
		Assert.AreEqual (2, transport.SentCommands.Count);
		}

	[TestMethod]
	public async Task TurnChildOffAsync_WithKnownChild_SendsChildRelayCommandAndRefreshesState ()
		{
		var transport = new FakeDeviceTransport (
			sendResponses:
			[
				"{" +
				"\"system\":{\"get_sysinfo\":{\"alias\":\"Strip\",\"model\":\"HS300\",\"deviceId\":\"parent-1\",\"children\":[{\"id\":\"child-1\",\"alias\":\"Outlet 1\",\"state\":1}]}}}",
				"{\"system\":{\"set_relay_state\":{\"err_code\":0}}}",
				"{" +
				"\"system\":{\"get_sysinfo\":{\"alias\":\"Strip\",\"model\":\"HS300\",\"deviceId\":\"parent-1\",\"children\":[{\"id\":\"child-1\",\"alias\":\"Outlet 1\",\"state\":0}]}}}"
			],
			sendManyResponses:
			[
				"{\"emeter\":{\"err_code\":-1},\"time\":{\"get_time\":{\"year\":2025,\"month\":1,\"mday\":2,\"hour\":3,\"min\":4,\"sec\":5}},\"cnCloud\":{\"get_info\":{\"binded\":1,\"cld_connection\":1}},\"count_down\":{\"get_rules\":{\"rule_list\":[]}},\"schedule\":{\"get_rules\":{\"rule_list\":[]}},\"anti_theft\":{\"get_rules\":{\"rule_list\":[]}}}",
				"{\"emeter\":{\"err_code\":-1},\"time\":{\"get_time\":{\"year\":2025,\"month\":1,\"mday\":2,\"hour\":3,\"min\":4,\"sec\":5}},\"cnCloud\":{\"get_info\":{\"binded\":1,\"cld_connection\":1}},\"count_down\":{\"get_rules\":{\"rule_list\":[]}},\"schedule\":{\"get_rules\":{\"rule_list\":[]}},\"anti_theft\":{\"get_rules\":{\"rule_list\":[]}}}"
			]);
		DeviceConfiguration configuration = new ("127.0.0.1");
		var device = new KasaDevice (configuration, transport);

		await device.UpdateAsync ().ConfigureAwait (false);
		await device.TurnChildOffAsync ("child-1").ConfigureAwait (false);

		Assert.AreEqual (3, transport.SentCommands.Count);
		StringAssert.Contains (transport.SentCommands[1], "\"child_ids\":[\"child-1\"]");
		StringAssert.Contains (transport.SentCommands[1], "\"state\":0");
		Assert.AreEqual (false, device.GetChild ("child-1")!.IsOn);
		}

	[TestMethod]
	public async Task SetBrightnessAsync_WithLegacyBulbTransition_SendsRequestedTransitionPeriodAndRefreshesState ()
		{
		var transport = new FakeDeviceTransport (
			sendResponses:
			[
				"{" +
				"\"system\":{\"get_sysinfo\":{\"alias\":\"Bulb\",\"model\":\"KL130\",\"deviceId\":\"bulb-1\",\"light_state\":{\"on_off\":1,\"brightness\":20,\"transition_period\":0}}}}",
				"{\"smartlife.iot.smartbulb.lightingservice\":{\"transition_light_state\":{\"err_code\":0}}}",
				"{" +
				"\"system\":{\"get_sysinfo\":{\"alias\":\"Bulb\",\"model\":\"KL130\",\"deviceId\":\"bulb-1\",\"light_state\":{\"on_off\":1,\"brightness\":60,\"transition_period\":1500}}}}"
			],
			sendManyResponses:
			[
				"{\"emeter\":{\"err_code\":-1},\"time\":{\"get_time\":{\"year\":2025,\"month\":1,\"mday\":2,\"hour\":3,\"min\":4,\"sec\":5}},\"cnCloud\":{\"get_info\":{\"binded\":1,\"cld_connection\":1}},\"count_down\":{\"get_rules\":{\"rule_list\":[]}},\"schedule\":{\"get_rules\":{\"rule_list\":[]}},\"anti_theft\":{\"get_rules\":{\"rule_list\":[]}}}",
				"{\"emeter\":{\"err_code\":-1},\"time\":{\"get_time\":{\"year\":2025,\"month\":1,\"mday\":2,\"hour\":3,\"min\":4,\"sec\":5}},\"cnCloud\":{\"get_info\":{\"binded\":1,\"cld_connection\":1}},\"count_down\":{\"get_rules\":{\"rule_list\":[]}},\"schedule\":{\"get_rules\":{\"rule_list\":[]}},\"anti_theft\":{\"get_rules\":{\"rule_list\":[]}}}"
			]);
		DeviceConfiguration configuration = new ("127.0.0.1");
		var device = new KasaDevice (configuration, transport);

		await device.UpdateAsync ().ConfigureAwait (false);
		await device.SetBrightnessAsync (60, transitionMilliseconds: 1500).ConfigureAwait (false);

		Assert.AreEqual (3, transport.SentCommands.Count);
		StringAssert.Contains (transport.SentCommands[1], "\"brightness\":60");
		StringAssert.Contains (transport.SentCommands[1], "\"transition_period\":1500");
		Assert.IsNotNull (device.LightState);
		Assert.AreEqual (60, device.LightState.Brightness);
		Assert.AreEqual (true, device.LightState.IsOn);
		}

	[TestMethod]
	public async Task SetHsvAsync_WithSmartBulb_SendsSmartCommandAndRefreshesLightState ()
		{
		var transport = new FakeDeviceTransport (
			sendResponses:
			[
				"""
				{
				  "result": {
					 "responses": [
						{
						  "method": "get_device_info",
						  "result": {
							 "model": "L530",
							 "type": "SMART.TAPOBULB",
							 "device_id": "bulb-1",
							 "nickname": "QnVsYg==",
							 "device_on": true,
							 "brightness": 25,
							 "hue": 10,
							 "saturation": 20,
							 "color_temp": 3000
						  }
						},
						{
						  "method": "component_nego",
						  "result": {
							 "component_list": [
								{ "id": "brightness", "ver_code": 1 },
								{ "id": "color", "ver_code": 1 },
								{ "id": "color_temperature", "ver_code": 1 }
							 ]
						  }
						}
					 ]
				  }
				}
				""",
				"{" +
				"\"error_code\":0}",
				"""
				{
				  "result": {
					 "responses": [
						{
						  "method": "get_device_info",
						  "result": {
							 "model": "L530",
							 "type": "SMART.TAPOBULB",
							 "device_id": "bulb-1",
							 "nickname": "QnVsYg==",
							 "device_on": true,
							 "brightness": 80,
							 "hue": 120,
							 "saturation": 90,
							 "color_temp": 0
						  }
						},
						{
						  "method": "component_nego",
						  "result": {
							 "component_list": [
								{ "id": "brightness", "ver_code": 1 },
								{ "id": "color", "ver_code": 1 },
								{ "id": "color_temperature", "ver_code": 1 }
							 ]
						  }
						}
					 ]
				  }
				}
				"""
			]);
		DeviceConfiguration configuration = new (
			"127.0.0.1",
			connectionOptions: new DeviceConnectionOptions (
				connectionParameters: new DeviceConnectionParameters (DeviceFamilyKind.SmartTapoBulb, DeviceEncryptionKind.Aes)));
		var device = new KasaDevice (configuration, transport);

		await device.UpdateAsync ().ConfigureAwait (false);
		await device.SetHsvAsync (120, 90, 80).ConfigureAwait (false);

		Assert.AreEqual (3, transport.SentCommands.Count);
		StringAssert.Contains (transport.SentCommands[1], "\"method\":\"set_device_info\"");
		StringAssert.Contains (transport.SentCommands[1], "\"device_on\":true");
		StringAssert.Contains (transport.SentCommands[1], "\"brightness\":80");
		StringAssert.Contains (transport.SentCommands[1], "\"hue\":120");
		StringAssert.Contains (transport.SentCommands[1], "\"saturation\":90");
		StringAssert.Contains (transport.SentCommands[1], "\"color_temp\":0");
		Assert.IsNotNull (device.LightState);
		Assert.AreEqual (80, device.LightState.Brightness);
		Assert.AreEqual (120, device.LightState.Hue);
		Assert.AreEqual (90, device.LightState.Saturation);
		Assert.AreEqual (0, device.LightState.ColorTemperature);
		}

	[TestMethod]
	public async Task SetLightEffectAsync_WithSmartBulb_SendsEffectCommandAndRefreshesEffectState ()
		{
		const string initialCoreResponse = """
		{
		  "result": {
			 "responses": [
				{
				  "method": "get_device_info",
				  "result": {
					 "model": "L930",
					 "type": "SMART.TAPOBULB",
					 "device_id": "bulb-effect-1",
					 "nickname": "RWZmZWN0IEJ1bGI=",
					 "device_on": true,
					 "brightness": 50,
					 "hue": 20,
					 "saturation": 40,
					 "color_temp": 3000,
					 "lighting_effect": {
						"enable": 0
					 }
				  }
				},
				{
				  "method": "component_nego",
				  "result": {
					 "component_list": [
						{ "id": "brightness", "ver_code": 1 },
						{ "id": "color", "ver_code": 1 },
						{ "id": "light_effect", "ver_code": 1 }
					 ]
				  }
				}
			 ]
		  }
		}
		""";

		const string initialModuleResponse = """
		{
		  "result": {
			 "responses": [
				{
				  "method": "get_dynamic_light_effect_rules",
				  "result": {
					 "rule_list": [
						{ "id": "L1", "scene_name": "UGFydHk=", "color_status_list": [ [ 75, 0, 0 ] ] },
						{ "id": "L2", "scene_name": "UmVsYXg=", "color_status_list": [ [ 20, 0, 0 ] ] }
					 ],
					 "enable": false
				  }
				}
			 ]
		  }
		}
		""";

		const string finalCoreResponse = """
		{
		  "result": {
			 "responses": [
				{
				  "method": "get_device_info",
				  "result": {
					 "model": "L930",
					 "type": "SMART.TAPOBULB",
					 "device_id": "bulb-effect-1",
					 "nickname": "RWZmZWN0IEJ1bGI=",
					 "device_on": true,
					 "brightness": 50,
					 "hue": 20,
					 "saturation": 40,
					 "color_temp": 3000,
					 "lighting_effect": {
						"enable": 1,
						"id": "L1",
						"name": "Party",
						"brightness": 75
					 }
				  }
				},
				{
				  "method": "component_nego",
				  "result": {
					 "component_list": [
						{ "id": "brightness", "ver_code": 1 },
						{ "id": "color", "ver_code": 1 },
						{ "id": "light_effect", "ver_code": 1 }
					 ]
				  }
				}
			 ]
		  }
		}
		""";

		const string finalModuleResponse = """
		{
		  "result": {
			 "responses": [
				{
				  "method": "get_dynamic_light_effect_rules",
				  "result": {
					 "rule_list": [
						{ "id": "L1", "scene_name": "UGFydHk=", "color_status_list": [ [ 75, 0, 0 ] ] },
						{ "id": "L2", "scene_name": "UmVsYXg=", "color_status_list": [ [ 20, 0, 0 ] ] }
					 ],
					 "current_rule_id": "L1",
					 "enable": true
				  }
				}
			 ]
		  }
		}
		""";

		var transport = new FakeDeviceTransport (
			sendResponses:
			[
				initialCoreResponse,
				initialModuleResponse,
				initialCoreResponse,
				initialModuleResponse,
				"{" +
				"\"smartlife.iot.smartbulb.lightingservice\":{\"set_dynamic_light_effect_rule_enable\":{\"err_code\":0}}}",
				finalCoreResponse,
				finalModuleResponse
			]);
		DeviceConfiguration configuration = new (
			"127.0.0.1",
			connectionOptions: new DeviceConnectionOptions (
				connectionParameters: new DeviceConnectionParameters (DeviceFamilyKind.SmartTapoBulb, DeviceEncryptionKind.Aes)));
		var device = new KasaDevice (configuration, transport);

		await device.UpdateAsync ().ConfigureAwait (false);
		await device.SetLightEffectAsync ("L1").ConfigureAwait (false);

		Assert.AreEqual (7, transport.SentCommands.Count);
		StringAssert.Contains (transport.SentCommands[4], "\"set_dynamic_light_effect_rule_enable\"");
		StringAssert.Contains (transport.SentCommands[4], "\"enable\":1");
		StringAssert.Contains (transport.SentCommands[4], "\"id\":\"L1\"");
		Assert.IsNotNull (device.LightEffect);
		Assert.AreEqual (true, device.LightEffect.IsEnabled);
		Assert.AreEqual ("L1", device.LightEffect.Identifier);
		Assert.AreEqual ("Party", device.LightEffect.Name);
		Assert.AreEqual (2, device.AvailableLightEffects.Count);
		}

	[TestMethod]
	public async Task SetLightTransitionsEnabledAsync_WithSmartBulb_SendsEnableCommandAndRefreshesTransitionState ()
		{
		const string initialResponse = """
		{
		  "result": {
			 "responses": [
				{
				  "method": "get_device_info",
				  "result": {
					 "model": "L530",
					 "type": "SMART.TAPOBULB",
					 "device_id": "bulb-transition-1",
					 "nickname": "VHJhbnNpdGlvbiBCdWxi",
					 "device_on": true,
					 "brightness": 25,
					 "hue": 10,
					 "saturation": 20,
					 "color_temp": 3000
				  }
				},
				{
				  "method": "component_nego",
				  "result": {
					 "component_list": [
						{ "id": "brightness", "ver_code": 1 },
						{ "id": "color", "ver_code": 1 },
						{ "id": "on_off_gradually", "ver_code": 2 }
					 ]
				  }
				},
				{
				  "method": "get_on_off_gradually_info",
				  "result": {
					 "on_state": { "enable": false, "duration": 0 },
					 "off_state": { "enable": false, "duration": 0 }
				  }
				}
			 ]
		  }
		}
		""";

		const string finalResponse = """
		{
		  "result": {
			 "responses": [
				{
				  "method": "get_device_info",
				  "result": {
					 "model": "L530",
					 "type": "SMART.TAPOBULB",
					 "device_id": "bulb-transition-1",
					 "nickname": "VHJhbnNpdGlvbiBCdWxi",
					 "device_on": true,
					 "brightness": 25,
					 "hue": 10,
					 "saturation": 20,
					 "color_temp": 3000
				  }
				},
				{
				  "method": "component_nego",
				  "result": {
					 "component_list": [
						{ "id": "brightness", "ver_code": 1 },
						{ "id": "color", "ver_code": 1 },
						{ "id": "on_off_gradually", "ver_code": 2 }
					 ]
				  }
				},
				{
				  "method": "get_on_off_gradually_info",
				  "result": {
					 "enable": true,
					 "on_state": { "enable": true, "duration": 12 },
					 "off_state": { "enable": true, "duration": 8 }
				  }
				}
			 ]
		  }
		}
		""";

		var transport = new FakeDeviceTransport (
			sendResponses:
			[
				initialResponse,
				initialResponse,
				"{" +
				"\"error_code\":0}",
				"{" +
				"\"error_code\":0}",
				finalResponse,
				finalResponse
			]);
		DeviceConfiguration configuration = new (
			"127.0.0.1",
			connectionOptions: new DeviceConnectionOptions (
				connectionParameters: new DeviceConnectionParameters (DeviceFamilyKind.SmartTapoBulb, DeviceEncryptionKind.Aes)));
		var device = new KasaDevice (configuration, transport);

		await device.UpdateAsync ().ConfigureAwait (false);
		await device.SetLightTransitionsEnabledAsync (true).ConfigureAwait (false);

		Assert.AreEqual (6, transport.SentCommands.Count);
		StringAssert.Contains (transport.SentCommands[2], "\"method\":\"set_on_off_gradually_info\"");
		StringAssert.Contains (transport.SentCommands[2], "\"on_state\":{");
		StringAssert.Contains (transport.SentCommands[2], "\"duration\":0");
		StringAssert.Contains (transport.SentCommands[2], "\"enable\":true");
		StringAssert.Contains (transport.SentCommands[3], "\"method\":\"set_on_off_gradually_info\"");
		StringAssert.Contains (transport.SentCommands[3], "\"off_state\":{");
		StringAssert.Contains (transport.SentCommands[3], "\"duration\":0");
		StringAssert.Contains (transport.SentCommands[3], "\"enable\":true");
		Assert.IsNotNull (device.LightTransitionState);
		Assert.AreEqual (12, device.LightTransitionState.TransitionOnSeconds);
		Assert.AreEqual (12, device.LightTransitionState.TransitionOnDurationSeconds);
		Assert.AreEqual (8, device.LightTransitionState.TransitionOffSeconds);
		Assert.AreEqual (8, device.LightTransitionState.TransitionOffDurationSeconds);
		}

	[TestMethod]
	public async Task SetLightTransitionsEnabledAsync_WithSmartBulbV1_SendsTopLevelEnableCommand ()
		{
		const string initialResponse = """
		{
		  "result": {
			 "responses": [
				{
				  "method": "get_device_info",
				  "result": {
					 "model": "L510",
					 "type": "SMART.TAPOBULB",
					 "device_id": "bulb-transition-v1",
					 "nickname": "VjEgVHJhbnNpdGlvbiBCdWxi",
					 "device_on": true,
					 "brightness": 25
				  }
				},
				{
				  "method": "component_nego",
				  "result": {
					 "component_list": [
						{ "id": "brightness", "ver_code": 1 },
						{ "id": "on_off_gradually", "ver_code": 1 }
					 ]
				  }
				},
				{
				  "method": "get_on_off_gradually_info",
				  "result": {
					 "enable": false
				  }
				}
			 ]
		  }
		}
		""";

		const string finalResponse = """
		{
		  "result": {
			 "responses": [
				{
				  "method": "get_device_info",
				  "result": {
					 "model": "L510",
					 "type": "SMART.TAPOBULB",
					 "device_id": "bulb-transition-v1",
					 "nickname": "VjEgVHJhbnNpdGlvbiBCdWxi",
					 "device_on": true,
					 "brightness": 25
				  }
				},
				{
				  "method": "component_nego",
				  "result": {
					 "component_list": [
						{ "id": "brightness", "ver_code": 1 },
						{ "id": "on_off_gradually", "ver_code": 1 }
					 ]
				  }
				},
				{
				  "method": "get_on_off_gradually_info",
				  "result": {
					 "enable": true
				  }
				}
			 ]
		  }
		}
		""";

		var transport = new FakeDeviceTransport (
			sendResponses:
			[
				initialResponse,
				initialResponse,
				"{" +
				"\"error_code\":0}",
				finalResponse,
				finalResponse
			]);
		DeviceConfiguration configuration = new (
			"127.0.0.1",
			connectionOptions: new DeviceConnectionOptions (
				connectionParameters: new DeviceConnectionParameters (DeviceFamilyKind.SmartTapoBulb, DeviceEncryptionKind.Aes)));
		var device = new KasaDevice (configuration, transport);

		await device.UpdateAsync ().ConfigureAwait (false);
		await device.SetLightTransitionsEnabledAsync (true).ConfigureAwait (false);

		Assert.AreEqual (5, transport.SentCommands.Count);
		StringAssert.Contains (transport.SentCommands[2], "\"method\":\"set_on_off_gradually_info\"");
		StringAssert.Contains (transport.SentCommands[2], "\"enable\":true");
		Assert.IsFalse (transport.SentCommands[2].Contains ("\"on_state\":"));
		Assert.IsFalse (transport.SentCommands[2].Contains ("\"off_state\":"));
		Assert.IsNotNull (device.LightTransitionState);
		Assert.AreEqual (true, device.LightTransitionState.IsEnabled);
		}

	[TestMethod]
	public async Task UpdateAsync_WithSmartBulbV1_ExposesSingleSmoothTransitionsFeature ()
		{
		const string response = """
		{
		  "result": {
			 "responses": [
				{
				  "method": "get_device_info",
				  "result": {
					 "model": "L510",
					 "type": "SMART.TAPOBULB",
					 "device_id": "bulb-feature-v1",
					 "nickname": "VjEgRmVhdHVyZSBCdWxi",
					 "device_on": true,
					 "brightness": 25
				  }
				},
				{
				  "method": "component_nego",
				  "result": {
					 "component_list": [
						{ "id": "brightness", "ver_code": 1 },
						{ "id": "on_off_gradually", "ver_code": 1 }
					 ]
				  }
				},
				{
				  "method": "get_on_off_gradually_info",
				  "result": {
					 "enable": true
				  }
				}
			 ]
		  }
		}
		""";

		var transport = new FakeDeviceTransport (sendResponses: [response, response]);
		DeviceConfiguration configuration = new (
			"127.0.0.1",
			connectionOptions: new DeviceConnectionOptions (
				connectionParameters: new DeviceConnectionParameters (DeviceFamilyKind.SmartTapoBulb, DeviceEncryptionKind.Aes)));
		var device = new KasaDevice (configuration, transport);

		await device.UpdateAsync ().ConfigureAwait (false);

		Assert.AreEqual (1, device.GetSmartComponentVersion ("on_off_gradually"));
		Assert.IsNotNull (device.GetFeature ("smooth_transitions"));
		Assert.IsNull (device.GetFeature ("smooth_transition_on"));
		Assert.IsNull (device.GetFeature ("smooth_transition_off"));
		}

	[TestMethod]
	public async Task UpdateAsync_WithSmartBulbV2_ExposesDirectionalTransitionFeaturesWithDeviceMaximums ()
		{
		const string response = """
		{
		  "result": {
			 "responses": [
				{
				  "method": "get_device_info",
				  "result": {
					 "model": "L530",
					 "type": "SMART.TAPOBULB",
					 "device_id": "bulb-feature-v2",
					 "nickname": "VjIgRmVhdHVyZSBCdWxi",
					 "device_on": true,
					 "brightness": 25,
					 "hue": 10,
					 "saturation": 20,
					 "color_temp": 3000
				  }
				},
				{
				  "method": "component_nego",
				  "result": {
					 "component_list": [
						{ "id": "brightness", "ver_code": 1 },
						{ "id": "color", "ver_code": 1 },
						{ "id": "on_off_gradually", "ver_code": 2 }
					 ]
				  }
				},
				{
				  "method": "get_on_off_gradually_info",
				  "result": {
					 "on_state": { "enable": true, "duration": 12, "max_duration": 40 },
					 "off_state": { "enable": false, "duration": 8, "max_duration": 45 }
				  }
				}
			 ]
		  }
		}
		""";

		var transport = new FakeDeviceTransport (sendResponses: [response, response]);
		DeviceConfiguration configuration = new (
			"127.0.0.1",
			connectionOptions: new DeviceConnectionOptions (
				connectionParameters: new DeviceConnectionParameters (DeviceFamilyKind.SmartTapoBulb, DeviceEncryptionKind.Aes)));
		var device = new KasaDevice (configuration, transport);

		await device.UpdateAsync ().ConfigureAwait (false);

		Assert.AreEqual (2, device.GetSmartComponentVersion ("on_off_gradually"));
		DeviceFeature? onFeature = device.GetFeature ("smooth_transition_on");
		DeviceFeature? offFeature = device.GetFeature ("smooth_transition_off");
		Assert.IsNotNull (onFeature);
		Assert.IsNotNull (offFeature);
		Assert.AreEqual (40d, onFeature.MaximumValue);
		Assert.AreEqual (45d, offFeature.MaximumValue);
		Assert.IsNull (device.GetFeature ("smooth_transitions"));
		}

	[TestMethod]
	public async Task SetLightTurnOnTransitionAsync_WithSmartBulb_UsesDeviceReportedMaximumDuration ()
		{
		const string initialResponse = """
		{
		  "result": {
			 "responses": [
				{
				  "method": "get_device_info",
				  "result": {
					 "model": "L530",
					 "type": "SMART.TAPOBULB",
					 "device_id": "bulb-transition-max",
					 "nickname": "VHJhbnNpdGlvbiBNYXg=",
					 "device_on": true,
					 "brightness": 25,
					 "hue": 10,
					 "saturation": 20,
					 "color_temp": 3000
				  }
				},
				{
				  "method": "component_nego",
				  "result": {
					 "component_list": [
						{ "id": "brightness", "ver_code": 1 },
						{ "id": "color", "ver_code": 1 },
						{ "id": "on_off_gradually", "ver_code": 2 }
					 ]
				  }
				},
				{
				  "method": "get_on_off_gradually_info",
				  "result": {
					 "on_state": { "enable": true, "duration": 12, "max_duration": 90 },
					 "off_state": { "enable": true, "duration": 8, "max_duration": 45 }
				  }
				}
			 ]
		  }
		}
		""";

		const string finalResponse = """
		{
		  "result": {
			 "responses": [
				{
				  "method": "get_device_info",
				  "result": {
					 "model": "L530",
					 "type": "SMART.TAPOBULB",
					 "device_id": "bulb-transition-max",
					 "nickname": "VHJhbnNpdGlvbiBNYXg=",
					 "device_on": true,
					 "brightness": 25,
					 "hue": 10,
					 "saturation": 20,
					 "color_temp": 3000
				  }
				},
				{
				  "method": "component_nego",
				  "result": {
					 "component_list": [
						{ "id": "brightness", "ver_code": 1 },
						{ "id": "color", "ver_code": 1 },
						{ "id": "on_off_gradually", "ver_code": 2 }
					 ]
				  }
				},
				{
				  "method": "get_on_off_gradually_info",
				  "result": {
					 "on_state": { "enable": true, "duration": 90, "max_duration": 90 },
					 "off_state": { "enable": true, "duration": 8, "max_duration": 45 }
				  }
				}
			 ]
		  }
		}
		""";

		var transport = new FakeDeviceTransport (
			sendResponses:
			[
				initialResponse,
				initialResponse,
				"{" +
				"\"error_code\":0}",
				finalResponse,
				finalResponse
			]);
		DeviceConfiguration configuration = new (
			"127.0.0.1",
			connectionOptions: new DeviceConnectionOptions (
				connectionParameters: new DeviceConnectionParameters (DeviceFamilyKind.SmartTapoBulb, DeviceEncryptionKind.Aes)));
		var device = new KasaDevice (configuration, transport);

		await device.UpdateAsync ().ConfigureAwait (false);
		await device.SetLightTurnOnTransitionAsync (90).ConfigureAwait (false);

		Assert.IsNotNull (device.LightTransitionState);
		Assert.AreEqual (90, device.LightTransitionState.TransitionOnSeconds);
		Assert.AreEqual (90, device.LightTransitionState.TransitionOnMaximumDurationSeconds);
		}

	[TestMethod]
	public async Task SetLightTurnOnTransitionAsync_WithSmartBulb_SendsOnStateCommandAndRefreshesTransitionState ()
		{
		const string initialResponse = """
		{
		  "result": {
			 "responses": [
				{
				  "method": "get_device_info",
				  "result": {
					 "model": "L530",
					 "type": "SMART.TAPOBULB",
					 "device_id": "bulb-transition-2",
					 "nickname": "VHJhbnNpdGlvbiBCdWxiIDI=",
					 "device_on": true,
					 "brightness": 25,
					 "hue": 10,
					 "saturation": 20,
					 "color_temp": 3000
				  }
				},
				{
				  "method": "component_nego",
				  "result": {
					 "component_list": [
						{ "id": "brightness", "ver_code": 1 },
						{ "id": "color", "ver_code": 1 },
						{ "id": "on_off_gradually", "ver_code": 2 }
					 ]
				  }
				},
				{
				  "method": "get_on_off_gradually_info",
				  "result": {
					 "on_state": { "enable": false, "duration": 0 },
					 "off_state": { "enable": true, "duration": 8 }
				  }
				}
			 ]
		  }
		}
		""";

		const string finalResponse = """
		{
		  "result": {
			 "responses": [
				{
				  "method": "get_device_info",
				  "result": {
					 "model": "L530",
					 "type": "SMART.TAPOBULB",
					 "device_id": "bulb-transition-2",
					 "nickname": "VHJhbnNpdGlvbiBCdWxiIDI=",
					 "device_on": true,
					 "brightness": 25,
					 "hue": 10,
					 "saturation": 20,
					 "color_temp": 3000
				  }
				},
				{
				  "method": "component_nego",
				  "result": {
					 "component_list": [
						{ "id": "brightness", "ver_code": 1 },
						{ "id": "color", "ver_code": 1 },
						{ "id": "on_off_gradually", "ver_code": 2 }
					 ]
				  }
				},
				{
				  "method": "get_on_off_gradually_info",
				  "result": {
					 "on_state": { "enable": true, "duration": 15 },
					 "off_state": { "enable": true, "duration": 8 }
				  }
				}
			 ]
		  }
		}
		""";

		var transport = new FakeDeviceTransport (
			sendResponses:
			[
				initialResponse,
				initialResponse,
				"{" +
				"\"error_code\":0}",
				finalResponse,
				finalResponse
			]);
		DeviceConfiguration configuration = new (
			"127.0.0.1",
			connectionOptions: new DeviceConnectionOptions (
				connectionParameters: new DeviceConnectionParameters (DeviceFamilyKind.SmartTapoBulb, DeviceEncryptionKind.Aes)));
		var device = new KasaDevice (configuration, transport);

		await device.UpdateAsync ().ConfigureAwait (false);
		await device.SetLightTurnOnTransitionAsync (15).ConfigureAwait (false);

		Assert.AreEqual (5, transport.SentCommands.Count);
		StringAssert.Contains (transport.SentCommands[2], "\"method\":\"set_on_off_gradually_info\"");
		StringAssert.Contains (transport.SentCommands[2], "\"on_state\":{");
		StringAssert.Contains (transport.SentCommands[2], "\"enable\":true");
		StringAssert.Contains (transport.SentCommands[2], "\"duration\":15");
		Assert.IsNotNull (device.LightTransitionState);
		Assert.AreEqual (true, device.LightTransitionState.IsEnabled);
		Assert.AreEqual (true, device.LightTransitionState.IsTransitionOnEnabled);
		Assert.AreEqual (15, device.LightTransitionState.TransitionOnSeconds);
		Assert.AreEqual (15, device.LightTransitionState.TransitionOnDurationSeconds);
		Assert.AreEqual (true, device.LightTransitionState.IsTransitionOffEnabled);
		Assert.AreEqual (8, device.LightTransitionState.TransitionOffSeconds);
		Assert.AreEqual (8, device.LightTransitionState.TransitionOffDurationSeconds);
		}

	[TestMethod]
	public async Task SetLightTurnOffTransitionAsync_WithSmartBulb_SendsOffStateCommandAndRefreshesTransitionState ()
		{
		const string initialResponse = """
		{
		  "result": {
			 "responses": [
				{
				  "method": "get_device_info",
				  "result": {
					 "model": "L530",
					 "type": "SMART.TAPOBULB",
					 "device_id": "bulb-transition-3",
					 "nickname": "VHJhbnNpdGlvbiBCdWxiIDM=",
					 "device_on": true,
					 "brightness": 25,
					 "hue": 10,
					 "saturation": 20,
					 "color_temp": 3000
				  }
				},
				{
				  "method": "component_nego",
				  "result": {
					 "component_list": [
						{ "id": "brightness", "ver_code": 1 },
						{ "id": "color", "ver_code": 1 },
						{ "id": "on_off_gradually", "ver_code": 2 }
					 ]
				  }
				},
				{
				  "method": "get_on_off_gradually_info",
				  "result": {
					 "on_state": { "enable": true, "duration": 12 },
					 "off_state": { "enable": true, "duration": 8 }
				  }
				}
			 ]
		  }
		}
		""";

		const string finalResponse = """
		{
		  "result": {
			 "responses": [
				{
				  "method": "get_device_info",
				  "result": {
					 "model": "L530",
					 "type": "SMART.TAPOBULB",
					 "device_id": "bulb-transition-3",
					 "nickname": "VHJhbnNpdGlvbiBCdWxiIDM=",
					 "device_on": true,
					 "brightness": 25,
					 "hue": 10,
					 "saturation": 20,
					 "color_temp": 3000
				  }
				},
				{
				  "method": "component_nego",
				  "result": {
					 "component_list": [
						{ "id": "brightness", "ver_code": 1 },
						{ "id": "color", "ver_code": 1 },
						{ "id": "on_off_gradually", "ver_code": 2 }
					 ]
				  }
				},
				{
				  "method": "get_on_off_gradually_info",
				  "result": {
					 "on_state": { "enable": true, "duration": 12 },
					 "off_state": { "enable": false, "duration": 8 }
				  }
				}
			 ]
		  }
		}
		""";

		var transport = new FakeDeviceTransport (
			sendResponses:
			[
				initialResponse,
				initialResponse,
				"{" +
				"\"error_code\":0}",
				finalResponse,
				finalResponse
			]);
		DeviceConfiguration configuration = new (
			"127.0.0.1",
			connectionOptions: new DeviceConnectionOptions (
				connectionParameters: new DeviceConnectionParameters (DeviceFamilyKind.SmartTapoBulb, DeviceEncryptionKind.Aes)));
		var device = new KasaDevice (configuration, transport);

		await device.UpdateAsync ().ConfigureAwait (false);
		await device.SetLightTurnOffTransitionAsync (0).ConfigureAwait (false);

		Assert.AreEqual (5, transport.SentCommands.Count);
		StringAssert.Contains (transport.SentCommands[2], "\"method\":\"set_on_off_gradually_info\"");
		StringAssert.Contains (transport.SentCommands[2], "\"off_state\":{");
		StringAssert.Contains (transport.SentCommands[2], "\"enable\":false");
		StringAssert.Contains (transport.SentCommands[2], "\"duration\":8");
		Assert.IsNotNull (device.LightTransitionState);
		Assert.AreEqual (true, device.LightTransitionState.IsEnabled);
		Assert.AreEqual (true, device.LightTransitionState.IsTransitionOnEnabled);
		Assert.AreEqual (12, device.LightTransitionState.TransitionOnSeconds);
		Assert.AreEqual (12, device.LightTransitionState.TransitionOnDurationSeconds);
		Assert.AreEqual (false, device.LightTransitionState.IsTransitionOffEnabled);
		Assert.AreEqual (0, device.LightTransitionState.TransitionOffSeconds);
		Assert.AreEqual (8, device.LightTransitionState.TransitionOffDurationSeconds);
		}

	[TestMethod]
	public async Task ClearLightEffectAsync_WithSmartLightStrip_SendsDisablePayloadAndRefreshesEffectState ()
		{
		const string initialResponse = """
		{
		  "result": {
			 "responses": [
				{
				  "method": "get_device_info",
				  "result": {
					 "model": "L900",
					 "type": "SMART.TAPOBULB",
					 "device_id": "strip-1",
					 "nickname": "U3RyaXA=",
					 "device_on": true,
					 "brightness": 100,
					 "hue": 0,
					 "saturation": 0,
					 "color_temp": 0,
					 "lighting_effect": {
						"enable": 1,
						"name": "Aurora",
						"brightness": 100
					 }
				  }
				},
				{
				  "method": "component_nego",
				  "result": {
					 "component_list": [
						{ "id": "brightness", "ver_code": 1 },
						{ "id": "light_strip", "ver_code": 1 }
					 ]
				  }
				}
			 ]
		  }
		}
		""";

		var transport = new FakeDeviceTransport (
			sendResponses:
			[
				initialResponse,
				initialResponse,
				"{" +
				"\"error_code\":0}",
				"""
				{
				  "result": {
					 "responses": [
						{
						  "method": "get_device_info",
						  "result": {
							 "model": "L900",
							 "type": "SMART.TAPOBULB",
							 "device_id": "strip-1",
							 "nickname": "U3RyaXA=",
							 "device_on": true,
							 "brightness": 100,
							 "hue": 0,
							 "saturation": 0,
							 "color_temp": 0,
							 "lighting_effect": {
								"enable": 0,
								"name": "Aurora",
								"brightness": 100
							 }
						  }
						},
						{
						  "method": "component_nego",
						  "result": {
							 "component_list": [
								{ "id": "brightness", "ver_code": 1 },
								{ "id": "light_strip", "ver_code": 1 }
							 ]
						  }
						}
					 ]
				  }
				}
				"""
			]);
		DeviceConfiguration configuration = new (
			"127.0.0.1",
			timeout: TimeSpan.FromSeconds (60),
			connectionOptions: new DeviceConnectionOptions (
				connectionParameters: new DeviceConnectionParameters (DeviceFamilyKind.SmartTapoBulb, DeviceEncryptionKind.Aes)));
		var device = new KasaDevice (configuration, transport);

		await device.UpdateAsync ().ConfigureAwait (false);
		await device.ClearLightEffectAsync ().ConfigureAwait (false);

		Assert.AreEqual (4, transport.SentCommands.Count);
		StringAssert.Contains (transport.SentCommands[2], "\"method\":\"set_lighting_effect\"");
		StringAssert.Contains (transport.SentCommands[2], "\"enable\":0");
		Assert.IsNotNull (device.LightEffect);
		Assert.AreEqual (false, device.LightEffect.IsEnabled);
		Assert.IsNotNull (device.LightStripEffect.State);
		Assert.AreEqual (17, device.LightStripEffect.AvailableEffects.Count);
		}

	[TestMethod]
	public async Task GetScannedChildDevicesAsync_WithHubCategories_ReturnsDetectedChildren ()
		{
		var transport = new FakeDeviceTransport (
			sendResponses:
			[
				"""
				{
				  "result": {
					 "responses": [
						{
						  "method": "get_device_info",
						  "result": {
							 "model": "H100",
							 "type": "SMART.TAPOHUB",
							 "device_id": "hub-scan-1",
							 "nickname": "U2NhbiBIdWI=",
							 "device_category_list": [
								{ "category": "subg.trigger.button" },
								{ "category": "subg.sensor.contact" }
							 ]
						  }
						},
						{
						  "method": "component_nego",
						  "result": {
							 "component_list": [
								{ "id": "child_device", "ver_code": 1 }
							 ]
						  }
						},
						{
						  "method": "get_child_device_list",
						  "result": {
							 "child_device_list": []
						  }
						},
						{
						  "method": "get_child_device_component_list",
						  "result": {
							 "child_component_list": []
						  }
						}
					 ]
				  }
				}
				""",
				"""
				{
				  "result": {
					 "child_device_list": [
						{
						  "device_id": "scan-1",
						  "device_model": "S200B",
						  "category": "subg.trigger.button"
						},
						{
						  "device_id": "scan-2",
						  "device_model": "T110",
						  "category": "subg.sensor.contact"
						}
					 ]
				  }
				}
				"""
			]);
		DeviceConfiguration configuration = new (
			"127.0.0.1",
			connectionOptions: new DeviceConnectionOptions (
				connectionParameters: new DeviceConnectionParameters (DeviceFamilyKind.SmartTapoHub, DeviceEncryptionKind.Aes)));
		var device = new KasaDevice (configuration, transport);

		await device.UpdateAsync ().ConfigureAwait (false);
		ChildSetupScanResult result = await device.GetScannedChildDevicesAsync ().ConfigureAwait (false);

		CollectionAssert.AreEquivalent (new[] { "subg.trigger.button", "subg.sensor.contact" }, (System.Collections.ICollection)result.SupportedCategories);
		Assert.AreEqual (2, result.DetectedDevices.Count);
		Assert.AreEqual ("scan-1", result.DetectedDevices[0].DeviceId);
		Assert.AreEqual ("S200B", result.DetectedDevices[0].Model);
		Assert.AreEqual ("subg.trigger.button", result.DetectedDevices[0].Category);
		StringAssert.Contains (transport.SentCommands[1], "\"method\":\"get_scan_child_device_list\"");
		StringAssert.Contains (transport.SentCommands[1], "subg.trigger.button");
		StringAssert.Contains (transport.SentCommands[1], "subg.sensor.contact");
		}

	[TestMethod]
	public async Task PairAndUnpairChildDeviceAsync_WithHub_SendsCommandsAndRefreshesChildren ()
		{
		var transport = new FakeDeviceTransport (
			sendResponses:
			[
				"""
				{
				  "result": {
					 "responses": [
						{
						  "method": "get_device_info",
						  "result": {
							 "model": "H100",
							 "type": "SMART.TAPOHUB",
							 "device_id": "hub-pair-1",
							 "nickname": "UGFpciBIdWI=",
							 "device_category_list": [
								{ "category": "subg.trigger.button" }
							 ]
						  }
						},
						{
						  "method": "component_nego",
						  "result": {
							 "component_list": [
								{ "id": "child_device", "ver_code": 1 }
							 ]
						  }
						},
						{
						  "method": "get_child_device_list",
						  "result": {
							 "child_device_list": []
						  }
						},
						{
						  "method": "get_child_device_component_list",
						  "result": {
							 "child_component_list": []
						  }
						}
					 ]
				  }
				}
				""",
				"{" +
				"\"error_code\":0}",
				"""
				{
				  "result": {
					 "responses": [
						{
						  "method": "get_device_info",
						  "result": {
							 "model": "H100",
							 "type": "SMART.TAPOHUB",
							 "device_id": "hub-pair-1",
							 "nickname": "UGFpciBIdWI=",
							 "device_category_list": [
								{ "category": "subg.trigger.button" }
							 ]
						  }
						},
						{
						  "method": "component_nego",
						  "result": {
							 "component_list": [
								{ "id": "child_device", "ver_code": 1 }
							 ]
						  }
						},
						{
						  "method": "get_child_device_list",
						  "result": {
							 "child_device_list": [
								{
								  "device_id": "scan-1",
								  "nickname": "UGFpcmVkIEJ1dHRvbg==",
								  "model": "S200B",
								  "category": "subg.trigger.button",
								  "device_on": true
								}
							 ]
						  }
						},
						{
						  "method": "get_child_device_component_list",
						  "result": {
							 "child_component_list": [
								{
								  "device_id": "scan-1",
								  "component_list": []
								}
							 ]
						  }
						}
					 ]
				  }
				}
				""",
				"{" +
				"\"error_code\":0}",
				"""
				{
				  "result": {
					 "responses": [
						{
						  "method": "get_device_info",
						  "result": {
							 "model": "H100",
							 "type": "SMART.TAPOHUB",
							 "device_id": "hub-pair-1",
							 "nickname": "UGFpciBIdWI=",
							 "device_category_list": [
								{ "category": "subg.trigger.button" }
							 ]
						  }
						},
						{
						  "method": "component_nego",
						  "result": {
							 "component_list": [
								{ "id": "child_device", "ver_code": 1 }
							 ]
						  }
						},
						{
						  "method": "get_child_device_list",
						  "result": {
							 "child_device_list": []
						  }
						},
						{
						  "method": "get_child_device_component_list",
						  "result": {
							 "child_component_list": []
						  }
						}
					 ]
				  }
				}
				"""
			]);
		DeviceConfiguration configuration = new (
			"127.0.0.1",
			connectionOptions: new DeviceConnectionOptions (
				connectionParameters: new DeviceConnectionParameters (DeviceFamilyKind.SmartTapoHub, DeviceEncryptionKind.Aes)));
		var device = new KasaDevice (configuration, transport);

		await device.UpdateAsync ().ConfigureAwait (false);
		ChildSetupScanResult scanResult = new (["subg.trigger.button"], [new DetectedChildDevice ("scan-1", "S200B", "subg.trigger.button", "{}")]);
		IReadOnlyList<DetectedChildDevice> added = await device.PairScannedChildDevicesAsync (scanResult.DetectedDevices).ConfigureAwait (false);
		await device.UnpairChildDeviceAsync ("scan-1").ConfigureAwait (false);

		Assert.AreEqual (1, added.Count);
		Assert.AreEqual ("scan-1", added[0].DeviceId);
		StringAssert.Contains (transport.SentCommands[1], "\"method\":\"add_child_device_list\"");
		StringAssert.Contains (transport.SentCommands[1], "\"device_id\":\"scan-1\"");
		StringAssert.Contains (transport.SentCommands[3], "\"method\":\"remove_child_device_list\"");
		StringAssert.Contains (transport.SentCommands[3], "\"device_id\":\"scan-1\"");
		Assert.IsNull (device.GetChild ("scan-1"));
		}

	[TestMethod]
	public async Task UpdateAsync_WithSensorChildRefresh_ProjectsTypedChildModules ()
		{
		var transport = new FakeDeviceTransport (
			sendResponses:
			[
				"""
				{
				  "result": {
					 "responses": [
						{
						  "method": "get_device_info",
						  "result": {
							 "model": "H100",
							 "type": "SMART.TAPOHUB",
							 "device_id": "hub-child-projections",
							 "nickname": "U2Vuc29yIEh1Yg=="
						  }
						},
						{
						  "method": "component_nego",
						  "result": {
							 "component_list": [
								{ "id": "child_device", "ver_code": 1 }
							 ]
						  }
						},
						{
						  "method": "get_child_device_list",
						  "result": {
							 "child_device_list": [
								{
								  "device_id": "sensor-1",
								  "nickname": "U2Vuc29yIDE=",
								  "model": "T315",
								  "category": "subg.sensor",
								  "device_on": true,
								  "battery_percentage": 85,
								  "at_low_battery": true,
								  "open": true,
								  "detected": true,
								  "water_leak_status": "wet",
								  "in_alarm": true,
								  "trigger_timestamp": 1700000001,
								  "current_temp": 21.5,
								  "current_temp_exception": 1,
								  "temp_unit": "celsius",
								  "current_humidity": 63,
								  "current_humidity_exception": 1
								},
								{
								  "device_id": "trv-1",
								  "nickname": "VFJWIDE=",
								  "model": "KE100",
								  "category": "subg.trv",
								  "device_on": true,
								  "target_temp": 23.5,
								  "current_temp": 22.0,
								  "min_control_temp": 5,
								  "max_control_temp": 30,
								  "temp_offset": 2,
								  "temp_unit": "celsius",
								  "trv_states": [ "heating", "window_open" ],
								  "frost_protection_on": true,
								  "child_protection": true
								}
							 ]
						  }
						},
						{
						  "method": "get_child_device_component_list",
						  "result": {
							 "child_component_list": [
								{
								  "device_id": "sensor-1",
								  "component_list": [
									 { "id": "humidity", "ver_code": 1 }
								  ]
								},
								{
								  "device_id": "trv-1",
								  "component_list": [
									 { "id": "humidity", "ver_code": 1 },
									 { "id": "trigger_log", "ver_code": 1 },
									 { "id": "frost_protection", "ver_code": 1 }
								  ]
								}
							 ]
						  }
						}
					 ]
				  }
				}
				""",
				"""
				{
				  "result": {
					 "responseData": {
						"result": {
						  "responses": [
							 { "method": "get_comfort_humidity_config", "result": { "min_value": 40, "max_value": 70 } }
						  ]
						}
					 }
				  }
				}
				""",
				"""
				{
				  "result": {
					 "responseData": {
						"result": {
						  "responses": [
							 { "method": "get_comfort_humidity_config", "result": { "min_value": 35, "max_value": 60 } },
							 { "method": "get_trigger_logs", "result": { "logs": [ { "id": 1, "event_id": "heat", "timestamp": 1700000002, "event": "started" } ] } },
							 { "method": "get_frost_protection", "result": { "min_temp": 7, "temp_unit": "celsius" } }
						  ]
						}
					 }
				  }
				}
				"""
			]);
		DeviceConfiguration configuration = new (
			"127.0.0.1",
			connectionOptions: new DeviceConnectionOptions (
				connectionParameters: new DeviceConnectionParameters (DeviceFamilyKind.SmartTapoHub, DeviceEncryptionKind.Aes)));
		var device = new KasaDevice (configuration, transport);

		await device.UpdateAsync ().ConfigureAwait (false);

		ChildDevice sensor = device.GetChildDevice ("sensor-1")!;
		Assert.AreEqual (85, sensor.Battery.BatteryLevel);
		Assert.AreEqual (true, sensor.Battery.BatteryLow);
		Assert.AreEqual (true, sensor.Contact.IsOpen);
		Assert.AreEqual (true, sensor.Motion.MotionDetected);
		Assert.AreEqual ("wet", sensor.WaterLeak.State!.Status);
		Assert.AreEqual (true, sensor.WaterLeak.State.Alert);
		Assert.AreEqual (1700000001L, sensor.WaterLeak.State.AlertTimestamp);
		Assert.AreEqual (21.5d, sensor.Temperature.Temperature);
		Assert.AreEqual (true, sensor.Temperature.Warning);
		Assert.AreEqual ("celsius", sensor.Temperature.Unit);
		Assert.AreEqual (40d, sensor.Humidity.MinimumComfortHumidity);
		Assert.AreEqual (70d, sensor.Humidity.MaximumComfortHumidity);

		ChildDevice trv = device.GetChildDevice ("trv-1")!;
		Assert.AreEqual (true, trv.FrostProtection.Enabled);
		Assert.AreEqual (7, trv.FrostProtection.MinimumTemperature);
		Assert.AreEqual (true, trv.ChildProtection.Enabled);
		Assert.AreEqual (23.5d, trv.TemperatureControl.TargetTemperature);
		Assert.AreEqual (5, trv.TemperatureControl.MinimumTargetTemperature);
		Assert.AreEqual (30, trv.TemperatureControl.MaximumTargetTemperature);
		Assert.AreEqual (2, trv.TemperatureControl.TemperatureOffset);
		CollectionAssert.AreEqual (new[] { "heating", "window_open" }, (System.Collections.ICollection)trv.TemperatureControl.States);
		Assert.AreEqual (22d, trv.Thermostat.CurrentTemperature);
		Assert.AreEqual ("celsius", trv.Thermostat.Unit);
		Assert.AreEqual (1, trv.TriggerLogs.Logs.Count);
		Assert.AreEqual ("started", trv.TriggerLogs.Logs[0].EventName);
		Assert.AreEqual (3, transport.SentCommands.Count);
		}

	[TestMethod]
	public async Task SetLightEffectAsync_WithSmartPlug_ThrowsInvalidOperationException ()
		{
		var transport = new FakeDeviceTransport (
			sendResponses:
			[
				"""
				{
				  "result": {
					 "responses": [
						{
						  "method": "get_device_info",
						  "result": {
							 "model": "P110",
							 "type": "SMART.TAPOPLUG",
							 "device_id": "plug-1",
							 "nickname": "UGx1Zw==",
							 "device_on": true
						  }
						},
						{
						  "method": "component_nego",
						  "result": {
							 "component_list": [
								{ "id": "cloud_connect", "ver_code": 1 }
							 ]
						  }
						}
					 ]
				  }
				}
				"""
			]);
		DeviceConfiguration configuration = new (
			"127.0.0.1",
			connectionOptions: new DeviceConnectionOptions (
				connectionParameters: new DeviceConnectionParameters (DeviceFamilyKind.SmartTapoPlug, DeviceEncryptionKind.Aes)));
		var device = new KasaDevice (configuration, transport);

		await Assert.ThrowsExactlyAsync<InvalidOperationException> (() => device.SetLightEffectAsync ("Aurora")).ConfigureAwait (false);
		}

	[TestMethod]
	public async Task TurnChildOnAsync_WithUnknownChild_ThrowsInvalidOperationException ()
		{
		var transport = new FakeDeviceTransport (
			sendResponses:
			[
				"{" +
				"\"system\":{\"get_sysinfo\":{\"alias\":\"Strip\",\"model\":\"HS300\",\"deviceId\":\"parent-1\",\"children\":[{\"id\":\"child-1\",\"alias\":\"Outlet 1\",\"state\":1}]}}}"
			],
			sendManyResponses:
			[
				"{\"emeter\":{\"err_code\":-1},\"time\":{\"get_time\":{\"year\":2025,\"month\":1,\"mday\":2,\"hour\":3,\"min\":4," +
				"\"sec\":5}},\"cnCloud\":{\"get_info\":{\"binded\":1,\"cld_connection\":1}},\"count_down\":{\"get_rules\":{\"rule_list\":[]}},\"schedule\":{\"get_rules\":{\"rule_list\":[]}},\"anti_theft\":{\"get_rules\":{\"rule_list\":[]}}}"
			]);
		DeviceConfiguration configuration = new ("127.0.0.1");
		var device = new KasaDevice (configuration, transport);

		await device.UpdateAsync ().ConfigureAwait (false);
		await Assert.ThrowsExactlyAsync<InvalidOperationException> (() => device.TurnChildOnAsync ("missing-child")).ConfigureAwait (false);
		}

	[TestMethod]
	public async Task GetScannedChildDevicesAsync_WithNonHub_ThrowsInvalidOperationException ()
		{
		var transport = new FakeDeviceTransport (
			sendResponses:
			[
				"""
				{
				  "result": {
					 "responses": [
						{
						  "method": "get_device_info",
						  "result": {
							 "model": "P110",
							 "type": "SMART.TAPOPLUG",
							 "device_id": "plug-2",
							 "nickname": "UGx1ZyAy",
							 "device_on": true
						  }
						},
						{
						  "method": "component_nego",
						  "result": {
							 "component_list": [
								{ "id": "cloud_connect", "ver_code": 1 }
							 ]
						  }
						}
					 ]
				  }
				}
				"""
			]);
		DeviceConfiguration configuration = new (
			"127.0.0.1",
			connectionOptions: new DeviceConnectionOptions (
				connectionParameters: new DeviceConnectionParameters (DeviceFamilyKind.SmartTapoPlug, DeviceEncryptionKind.Aes)));
		var device = new KasaDevice (configuration, transport);

		await device.UpdateAsync ().ConfigureAwait (false);
		await Assert.ThrowsExactlyAsync<InvalidOperationException> (() => device.GetScannedChildDevicesAsync ()).ConfigureAwait (false);
		}

	[TestMethod]
	public async Task ExecuteCommandAsync_WithUpdateAfterCommand_RefreshesStateBeforeReturningResponse ()
		{
		var transport = new FakeDeviceTransport (
			sendResponses:
			[
				"{\"system\":{\"set_relay_state\":{\"err_code\":0}}}",
				"{\"system\":{\"get_sysinfo\":{\"alias\":\"Updated Plug\",\"model\":\"HS100\",\"deviceId\":\"device-1\",\"relay_state\":1}}}"
			],
			sendManyResponses:
			[
				"{\"emeter\":{\"err_code\":-1},\"time\":{\"get_time\":{\"year\":2025,\"month\":1,\"mday\":2,\"hour\":3,\"min\":4,\"sec\":5}},\"cnCloud\":{\"get_info\":{\"binded\":1,\"cld_connection\":1}},\"count_down\":{\"get_rules\":{\"rule_list\":[]}},\"schedule\":{\"get_rules\":{\"rule_list\":[]}},\"anti_theft\":{\"get_rules\":{\"rule_list\":[]}}}"
			]);
		DeviceConfiguration configuration = new ("127.0.0.1");
		var device = new KasaDevice (configuration, transport);

		string response = await device.ExecuteCommandAsync (
			KasaTapoClient.Internal.KasaCommands.CreateSetRelayStateCommand (true),
			DeviceStateUpdateMode.UpdateAfterCommand).ConfigureAwait (false);

		Assert.AreEqual ("{\"system\":{\"set_relay_state\":{\"err_code\":0}}}", response);
		Assert.AreEqual (2, transport.SentCommands.Count);
		Assert.AreEqual (1, transport.SentManyCommands.Count);
		Assert.AreEqual ("Updated Plug", device.SystemInfo?.Alias);
		Assert.AreEqual (true, device.IsOn);
		}

	[TestMethod]
	public async Task ExecuteSmartCommandAsync_BuildsSmartRequestAndRefreshesStateWhenRequested ()
		{
		var transport = new FakeDeviceTransport (
			sendResponses:
			[
				"{\"result\":{\"error_code\":0}}",
				"""
				{
				  "result": {
					 "responses": [
						{
						  "method": "get_device_info",
						  "result": {
							 "model": "P110",
							 "type": "SMART.TAPOPLUG",
							 "device_id": "plug-1",
							 "nickname": "UGx1Zw==",
							 "device_on": true
						  }
						},
						{
						  "method": "component_nego",
						  "result": {
							 "component_list": [
								{ "id": "cloud_connect", "ver_code": 1 }
							 ]
						  }
						}
					 ]
				  }
				}
				"""
			]);
		DeviceConfiguration configuration = new (
			"127.0.0.1",
			connectionOptions: new DeviceConnectionOptions (
				connectionParameters: new DeviceConnectionParameters (DeviceFamilyKind.SmartTapoPlug, DeviceEncryptionKind.Aes)));
		var device = new KasaDevice (configuration, transport);

		string response = await device.ExecuteSmartCommandAsync (
			"set_device_info",
			new Newtonsoft.Json.Linq.JObject { ["device_on"] = true },
			DeviceStateUpdateMode.UpdateAfterCommand).ConfigureAwait (false);

		Assert.AreEqual ("{\"result\":{\"error_code\":0}}", response);
		Assert.AreEqual (3, transport.SentCommands.Count);
		StringAssert.Contains (transport.SentCommands[0], "\"method\":\"set_device_info\"");
		StringAssert.Contains (transport.SentCommands[0], "\"request_time_milis\"");
		StringAssert.Contains (transport.SentCommands[0], "\"terminal_uuid\"");
		Assert.AreEqual ("Plug", device.Alias);
		Assert.AreEqual (true, device.IsOn);
		}

	[TestMethod]
	public async Task ExecuteCommandAsync_ConcurrentCalls_SerializesTransportAccess ()
		{
		int activeSends = 0;
		int maxActiveSends = 0;
		var transport = new FakeDeviceTransport (
			sendHandler: async (_, cancellationToken) =>
				{
				int active = Interlocked.Increment (ref activeSends);
				int observed;
				do
					{
					observed = maxActiveSends;
					if (active <= observed)
						{
						break;
						}
					}
				while (Interlocked.CompareExchange (ref maxActiveSends, active, observed) != observed);

				try
					{
					await Task.Delay (25, cancellationToken).ConfigureAwait (false);
					return "{\"ok\":true}";
					}
				finally
					{
					Interlocked.Decrement (ref activeSends);
					}
				});
		DeviceConfiguration configuration = new ("127.0.0.1");
		var device = new KasaDevice (configuration, transport);

		await Task.WhenAll (
			device.ExecuteCommandAsync ("{\"op\":1}"),
			device.ExecuteCommandAsync ("{\"op\":2}"),
			device.ExecuteCommandAsync ("{\"op\":3}")).ConfigureAwait (false);

		Assert.AreEqual (3, transport.SentCommands.Count);
		Assert.AreEqual (1, maxActiveSends);
		}
	}

