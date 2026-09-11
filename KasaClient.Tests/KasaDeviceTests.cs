// Copyright © 2026 Neil Colvin.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Threading;
using System.Threading.Tasks;

using KasaTapoClient;

using NUnit.Framework;

namespace KasaClient.Tests;

[TestFixture]
public sealed class KasaDeviceTests
	{
	[Test]
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

		Assert.That (device.SystemInfo, Is.Not.Null);
		Assert.That (device.SystemInfo.Alias, Is.EqualTo ("Test Plug"));
		Assert.That (device.DeviceType, Is.EqualTo (DeviceType.Plug));
		Assert.That (device.IsOn, Is.EqualTo (true));
		Assert.That (transport.SentCommands.Count, Is.EqualTo (1));
		Assert.That (transport.SentManyCommands.Count, Is.EqualTo (1));
		Assert.That (transport.SentCommands[0], Is.EqualTo (KasaTapoClient.Internal.KasaCommands.GET_SYSTEM_INFO));
		Assert.That (device.GetFeature ("state"), Is.Not.Null);
		Assert.That (device.GetFeature ("reboot"), Is.Not.Null);
		}

	[Test]
	public async Task UpdateEnergyUsageAsync_WithSmartDevice_RefreshesViaSmartProtocolNotLegacy ()
		{
		// Regression test: UpdateEnergyUsageAsync() used to always send the legacy emeter commands
		// (SendManyAsync), which SMART/KLAP devices don't understand - this device only ever queues
		// SendAsync responses, so the test fails with "No queued SendManyAsync response" if the bug
		// regresses.
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
							 "model": "KP125M",
							 "type": "SMART.KASAPLUG",
							 "device_id": "kp125m-1",
							 "nickname": "S1AxMjVN",
							 "device_on": true,
							 "fw_ver": "1.2.5",
							 "hw_ver": "1.0"
						  }
						},
						{
						  "method": "component_nego",
						  "result": {
							 "component_list": [
								{ "id": "energy_monitoring", "ver_code": 2 }
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
						{ "method": "get_energy_usage", "result": { "current_power": 5000, "today_energy": 120 } },
						{ "method": "get_current_power", "result": { "current_power": 5.0 } },
						{ "method": "get_emeter_data", "result": { "voltage_mv": 120000, "current_ma": 42 } }
					 ]
				  }
				}
				""",
				"""
				{
				  "result": {
					 "responses": [
						{ "method": "get_emeter_data", "result": { "power_mw": 7500, "voltage_mv": 120500, "current_ma": 62 } }
					 ]
				  }
				}
				"""
			]);
		DeviceConfiguration configuration = new (
			"127.0.0.1",
			connectionOptions: new DeviceConnectionOptions (
				connectionParameters: new DeviceConnectionParameters (DeviceFamilyKind.SmartKasaPlug, DeviceEncryptionKind.Aes)));
		var device = new KasaDevice (configuration, transport);

		await device.UpdateAsync ().ConfigureAwait (false);
		Assert.That (device.EnergyUsage, Is.Not.Null);
		Assert.That (device.EnergyUsage.CurrentPowerWatts, Is.EqualTo (5.0d));

		bool result = await device.UpdateEnergyUsageAsync ().ConfigureAwait (false);

		Assert.That (result, Is.True);
		Assert.That (device.EnergyUsage, Is.Not.Null);
		Assert.That (device.EnergyUsage.CurrentPowerWatts, Is.EqualTo (7.5d));
		Assert.That (transport.SentCommands.Count, Is.EqualTo (3));
		Assert.That (transport.SentManyCommands.Count, Is.EqualTo (0));
		}

	[Test]
	public async Task UpdateEnergyUsageAsync_WithSmartV2OptionalEmeterError_UsesEnergyUsageFallback ()
		{
		var transport = new FakeDeviceTransport (
			sendResponses:
			[
				"""{ "result": { "responses": [{ "method": "get_device_info", "result": { "model": "S515D", "type": "SMART.KASAPLUG", "device_id": "s515d-1" } }, { "method": "component_nego", "result": { "component_list": [{ "id": "energy_monitoring", "ver_code": 2 }] } }] } }""",
				"""{ "result": { "responses": [{ "method": "get_emeter_data", "error_code": -1002 }] } }""",
				"""{ "result": { "responses": [{ "method": "get_energy_usage", "result": { "current_power": 7500, "today_energy": 130 } }] } }"""
			]);
		DeviceConfiguration configuration = new (
			"127.0.0.1",
			connectionOptions: new DeviceConnectionOptions (
				connectionParameters: new DeviceConnectionParameters (DeviceFamilyKind.SmartKasaPlug, DeviceEncryptionKind.Aes)));
		var device = new KasaDevice (configuration, transport);

		bool result = await device.UpdateEnergyUsageAsync ().ConfigureAwait (false);

		Assert.That (result, Is.True);
		Assert.That (device.EnergyUsage, Is.Not.Null);
		Assert.That (device.EnergyUsage.CurrentPowerWatts, Is.EqualTo (7.5d));
		Assert.That (transport.SentCommands.Count, Is.EqualTo (3));
		Assert.That (transport.SentCommands[1], Does.Contain ("\"method\":\"get_emeter_data\""));
		Assert.That (transport.SentCommands[2], Does.Contain ("\"method\":\"get_energy_usage\""));
		}

	[Test]
	public async Task UpdateEnergyUsageAsync_WithSmartV2NonOptionalEmeterError_Throws ()
		{
		var transport = new FakeDeviceTransport (
			sendResponses:
			[
				"""{ "result": { "responses": [{ "method": "get_device_info", "result": { "model": "S515D", "type": "SMART.KASAPLUG", "device_id": "s515d-1" } }, { "method": "component_nego", "result": { "component_list": [{ "id": "energy_monitoring", "ver_code": 2 }] } }] } }""",
				"""{ "result": { "responses": [{ "method": "get_emeter_data", "error_code": -1003 }] } }"""
			]);
		DeviceConfiguration configuration = new (
			"127.0.0.1",
			connectionOptions: new DeviceConnectionOptions (
				connectionParameters: new DeviceConnectionParameters (DeviceFamilyKind.SmartKasaPlug, DeviceEncryptionKind.Aes)));
		var device = new KasaDevice (configuration, transport);

		await Assert.ThatAsync (() => device.UpdateEnergyUsageAsync (),
			Throws.TypeOf<InvalidOperationException> ().With.Message.Contains ("-1003")).ConfigureAwait (false);
		}

	[Test]
	public async Task UpdateEnergyUsageAsync_WithSmartV2OptionalCurrentPowerError_ReturnsUsageWithoutPower ()
		{
		var transport = new FakeDeviceTransport (
			sendResponses:
			[
				"""{ "result": { "responses": [{ "method": "get_device_info", "result": { "model": "S515D", "type": "SMART.KASAPLUG", "device_id": "s515d-1" } }, { "method": "component_nego", "result": { "component_list": [{ "id": "energy_monitoring", "ver_code": 2 }] } }] } }""",
				"""{ "result": { "responses": [{ "method": "get_emeter_data", "error_code": -1008 }] } }""",
				"""{ "result": { "responses": [{ "method": "get_energy_usage", "result": { "today_energy": 130 } }] } }""",
				"""{ "result": { "responses": [{ "method": "get_current_power", "error_code": -1002 }] } }"""
			]);
		DeviceConfiguration configuration = new (
			"127.0.0.1",
			connectionOptions: new DeviceConnectionOptions (
				connectionParameters: new DeviceConnectionParameters (DeviceFamilyKind.SmartKasaPlug, DeviceEncryptionKind.Aes)));
		var device = new KasaDevice (configuration, transport);

		bool result = await device.UpdateEnergyUsageAsync ().ConfigureAwait (false);

		Assert.That (result, Is.True);
		Assert.That (device.EnergyUsage, Is.Not.Null);
		Assert.That (device.EnergyUsage.CurrentPowerWatts, Is.Null);
		Assert.That (device.EnergyUsage.TotalKilowattHours, Is.EqualTo (0.13d));
		Assert.That (transport.SentCommands.Count, Is.EqualTo (4));
		}

	[Test]
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

		Assert.That (device.SystemInfo, Is.Not.Null);
		Assert.That (device.SystemInfo.Alias, Is.EqualTo ("Smart Plug"));
		Assert.That (device.DeviceType, Is.EqualTo (DeviceType.Plug));
		Assert.That (device.IsOn, Is.EqualTo (true));
		Assert.That (transport.SentCommands.Count, Is.EqualTo (2));
		Assert.That (transport.SentManyCommands.Count, Is.EqualTo (0));
		Assert.That (device.CloudState, Is.Not.Null);
		Assert.That (device.CloudState.IsConnected, Is.EqualTo (true));
		Assert.That (device.AutoOffState, Is.Not.Null);
		Assert.That (device.AutoOffState.DelayMinutes, Is.EqualTo (20));
		Assert.That (device.LedState, Is.Not.Null);
		Assert.That (device.LedState.Enabled, Is.EqualTo (false));
		Assert.That (device.EnergyUsage, Is.Not.Null);
		Assert.That (device.EnergyUsage.CurrentPowerWatts, Is.EqualTo (12.345d));
		Assert.That (device.TimeState, Is.Not.Null);
		Assert.That (device.TimeState.Region, Is.EqualTo ("UTC"));
		Assert.That (device.GetFeature ("state"), Is.Not.Null);
		}

	[Test]
	public async Task UpdateAsync_WithPaginatedChildDeviceList_FetchesRemainingPagesAndMergesFullList ()
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
							 "device_id": "hub-paginated",
							 "nickname": "UGFnaW5hdGVkIEh1Yg=="
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
							 "sum": 3,
							 "start_index": 0,
							 "child_device_list": [
								{ "device_id": "child-1", "nickname": "Q2hpbGQgMQ==", "model": "S200B", "category": "subg.trigger.button", "device_on": true }
							 ]
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
					 "sum": 3,
					 "start_index": 1,
					 "child_device_list": [
						{ "device_id": "child-2", "nickname": "Q2hpbGQgMg==", "model": "S200B", "category": "subg.trigger.button", "device_on": true }
					 ]
				  }
				}
				""",
				"""
				{
				  "result": {
					 "sum": 3,
					 "start_index": 2,
					 "child_device_list": [
						{ "device_id": "child-3", "nickname": "Q2hpbGQgMw==", "model": "S200B", "category": "subg.trigger.button", "device_on": true }
					 ]
				  }
				}
				""",
			]);
		DeviceConfiguration configuration = new (
			"127.0.0.1",
			connectionOptions: new DeviceConnectionOptions (
				connectionParameters: new DeviceConnectionParameters (DeviceFamilyKind.SmartTapoHub, DeviceEncryptionKind.Aes)));
		var device = new KasaDevice (configuration, transport);

		await device.UpdateAsync ().ConfigureAwait (false);

		Assert.That (device.Children.Count, Is.EqualTo (3), "The full, paginated child device list should be merged rather than only the first page.");
		Assert.That (device.GetChild ("child-1"), Is.Not.Null);
		Assert.That (device.GetChild ("child-2"), Is.Not.Null);
		Assert.That (device.GetChild ("child-3"), Is.Not.Null);
		Assert.That (transport.SentCommands.Count, Is.EqualTo (3), "One initial multi-request plus two follow-up get_child_device_list page requests should be sent.");
		Assert.That (transport.SentCommands[1], Does.Contain ("\"start_index\":1"));
		Assert.That (transport.SentCommands[2], Does.Contain ("\"start_index\":2"));
		}

	[Test]
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

		Assert.That (device.DeviceType, Is.EqualTo (DeviceType.Hub));
		Assert.That (device.Children.Count, Is.EqualTo (1));
		ChildDeviceInfo? child = device.GetChild ("child-1");
		Assert.That (child, Is.Not.Null);
		Assert.That (child.Alias, Is.EqualTo ("Button 1"));
		Assert.That (child.DeviceType, Is.EqualTo (DeviceType.Sensor));
		ChildDevice? childDevice = device.GetChildDevice ("child-1");
		Assert.That (childDevice, Is.Not.Null);
		Assert.That (childDevice.DoubleClick.Enabled, Is.EqualTo (true));
		Assert.That (childDevice.TriggerLogs.Logs.Count, Is.EqualTo (1));
		Assert.That (childDevice.TriggerLogs.Logs[0].EventName, Is.EqualTo ("click"));
		Assert.That (transport.SentCommands.Count, Is.EqualTo (2));
		}

	[Test]
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

		Assert.That (transport.SentCommands.Count, Is.EqualTo (3));
		Assert.That (transport.SentCommands[1], Does.Contain ("\"child_ids\":[\"child-1\"]"));
		Assert.That (transport.SentCommands[1], Does.Contain ("\"state\":0"));
		Assert.That (device.GetChild ("child-1")!.IsOn, Is.EqualTo (false));
		}

	[Test]
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

		Assert.That (transport.SentCommands.Count, Is.EqualTo (3));
		Assert.That (transport.SentCommands[1], Does.Contain ("\"brightness\":60"));
		Assert.That (transport.SentCommands[1], Does.Contain ("\"transition_period\":1500"));
		Assert.That (device.LightState, Is.Not.Null);
		Assert.That (device.LightState.Brightness, Is.EqualTo (60));
		Assert.That (device.LightState.IsOn, Is.EqualTo (true));
		}

	[Test]
	public async Task UpdateAsync_WithKl400L5_ExposesSupportedColorTemperatureRange ()
		{
		var transport = new FakeDeviceTransport (
			sendResponses:
			[
				"""
				{
				  "system": {
					 "get_sysinfo": {
						"alias": "KL400L5",
						"model": "KL400L5(US)",
						"deviceId": "kl400l5-1",
						"is_dimmable": 1,
						"is_variable_color_temp": 1,
						"light_state": { "on_off": 1, "brightness": 70, "color_temp": 3015, "hue": 0, "saturation": 0 }
					 }
				  }
				}
				"""
			],
			sendManyResponses:
			[
				"{" +
				"\"smartlife.iot.common.emeter\":{\"get_realtime\":{\"err_code\":0,\"power_mw\":9600,\"total_wh\":1193}}," +
				"\"smartlife.iot.common.timesetting\":{\"get_time\":{\"year\":2026,\"month\":8,\"mday\":3,\"hour\":12,\"min\":0,\"sec\":0}}," +
				"\"smartlife.iot.common.cloud\":{\"get_info\":{}},\"countdown\":{\"get_rules\":{\"rule_list\":[]}}," +
				"\"smartlife.iot.common.schedule\":{\"get_rules\":{\"rule_list\":[]}},\"smartlife.iot.common.anti_theft\":{\"get_rules\":{\"rule_list\":[]}}}"
			]);
		var device = new KasaDevice (new DeviceConfiguration ("127.0.0.1"), transport);

		await device.UpdateAsync ().ConfigureAwait (false);

		DeviceFeature? feature = device.GetFeature ("color_temperature");
		Assert.That (feature, Is.Not.Null);
		Assert.That (feature.MinimumValue, Is.EqualTo (2500d));
		Assert.That (feature.MaximumValue, Is.EqualTo (9000d));
		}

	[Test]
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

		Assert.That (transport.SentCommands.Count, Is.EqualTo (3));
		Assert.That (transport.SentCommands[1], Does.Contain ("\"method\":\"set_device_info\""));
		Assert.That (transport.SentCommands[1], Does.Contain ("\"device_on\":true"));
		Assert.That (transport.SentCommands[1], Does.Contain ("\"brightness\":80"));
		Assert.That (transport.SentCommands[1], Does.Contain ("\"hue\":120"));
		Assert.That (transport.SentCommands[1], Does.Contain ("\"saturation\":90"));
		Assert.That (transport.SentCommands[1], Does.Contain ("\"color_temp\":0"));
		Assert.That (device.LightState, Is.Not.Null);
		Assert.That (device.LightState.Brightness, Is.EqualTo (80));
		Assert.That (device.LightState.Hue, Is.EqualTo (120));
		Assert.That (device.LightState.Saturation, Is.EqualTo (90));
		Assert.That (device.LightState.ColorTemperature, Is.EqualTo (0));
		}

	[Test]
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

		Assert.That (transport.SentCommands.Count, Is.EqualTo (7));
		Assert.That (transport.SentCommands[4], Does.Contain ("\"set_dynamic_light_effect_rule_enable\""));
		Assert.That (transport.SentCommands[4], Does.Contain ("\"enable\":1"));
		Assert.That (transport.SentCommands[4], Does.Contain ("\"id\":\"L1\""));
		Assert.That (device.LightEffect, Is.Not.Null);
		Assert.That (device.LightEffect.IsEnabled, Is.EqualTo (true));
		Assert.That (device.LightEffect.Identifier, Is.EqualTo ("L1"));
		Assert.That (device.LightEffect.Name, Is.EqualTo ("Party"));
		Assert.That (device.AvailableLightEffects.Count, Is.EqualTo (2));
		}

	[Test]
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

		Assert.That (transport.SentCommands.Count, Is.EqualTo (6));
		Assert.That (transport.SentCommands[2], Does.Contain ("\"method\":\"set_on_off_gradually_info\""));
		Assert.That (transport.SentCommands[2], Does.Contain ("\"on_state\":{"));
		Assert.That (transport.SentCommands[2], Does.Contain ("\"duration\":0"));
		Assert.That (transport.SentCommands[2], Does.Contain ("\"enable\":true"));
		Assert.That (transport.SentCommands[3], Does.Contain ("\"method\":\"set_on_off_gradually_info\""));
		Assert.That (transport.SentCommands[3], Does.Contain ("\"off_state\":{"));
		Assert.That (transport.SentCommands[3], Does.Contain ("\"duration\":0"));
		Assert.That (transport.SentCommands[3], Does.Contain ("\"enable\":true"));
		Assert.That (device.LightTransitionState, Is.Not.Null);
		Assert.That (device.LightTransitionState.TransitionOnSeconds, Is.EqualTo (12));
		Assert.That (device.LightTransitionState.TransitionOnDurationSeconds, Is.EqualTo (12));
		Assert.That (device.LightTransitionState.TransitionOffSeconds, Is.EqualTo (8));
		Assert.That (device.LightTransitionState.TransitionOffDurationSeconds, Is.EqualTo (8));
		}

	[Test]
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

		Assert.That (transport.SentCommands.Count, Is.EqualTo (5));
		Assert.That (transport.SentCommands[2], Does.Contain ("\"method\":\"set_on_off_gradually_info\""));
		Assert.That (transport.SentCommands[2], Does.Contain ("\"enable\":true"));
		Assert.That (transport.SentCommands[2].Contains ("\"on_state\":"), Is.False);
		Assert.That (transport.SentCommands[2].Contains ("\"off_state\":"), Is.False);
		Assert.That (device.LightTransitionState, Is.Not.Null);
		Assert.That (device.LightTransitionState.IsEnabled, Is.EqualTo (true));
		}

	[Test]
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

		Assert.That (device.GetSmartComponentVersion ("on_off_gradually"), Is.EqualTo (1));
		Assert.That (device.GetFeature ("smooth_transitions"), Is.Not.Null);
		Assert.That (device.GetFeature ("smooth_transition_on"), Is.Null);
		Assert.That (device.GetFeature ("smooth_transition_off"), Is.Null);
		}

	[Test]
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

		Assert.That (device.GetSmartComponentVersion ("on_off_gradually"), Is.EqualTo (2));
		DeviceFeature? onFeature = device.GetFeature ("smooth_transition_on");
		DeviceFeature? offFeature = device.GetFeature ("smooth_transition_off");
		Assert.That (onFeature, Is.Not.Null);
		Assert.That (offFeature, Is.Not.Null);
		Assert.That (onFeature.MaximumValue, Is.EqualTo (40d));
		Assert.That (offFeature.MaximumValue, Is.EqualTo (45d));
		Assert.That (device.GetFeature ("smooth_transitions"), Is.Null);
		}

	[Test]
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

		Assert.That (device.LightTransitionState, Is.Not.Null);
		Assert.That (device.LightTransitionState.TransitionOnSeconds, Is.EqualTo (90));
		Assert.That (device.LightTransitionState.TransitionOnMaximumDurationSeconds, Is.EqualTo (90));
		}

	[Test]
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

		Assert.That (transport.SentCommands.Count, Is.EqualTo (5));
		Assert.That (transport.SentCommands[2], Does.Contain ("\"method\":\"set_on_off_gradually_info\""));
		Assert.That (transport.SentCommands[2], Does.Contain ("\"on_state\":{"));
		Assert.That (transport.SentCommands[2], Does.Contain ("\"enable\":true"));
		Assert.That (transport.SentCommands[2], Does.Contain ("\"duration\":15"));
		Assert.That (device.LightTransitionState, Is.Not.Null);
		Assert.That (device.LightTransitionState.IsEnabled, Is.EqualTo (true));
		Assert.That (device.LightTransitionState.IsTransitionOnEnabled, Is.EqualTo (true));
		Assert.That (device.LightTransitionState.TransitionOnSeconds, Is.EqualTo (15));
		Assert.That (device.LightTransitionState.TransitionOnDurationSeconds, Is.EqualTo (15));
		Assert.That (device.LightTransitionState.IsTransitionOffEnabled, Is.EqualTo (true));
		Assert.That (device.LightTransitionState.TransitionOffSeconds, Is.EqualTo (8));
		Assert.That (device.LightTransitionState.TransitionOffDurationSeconds, Is.EqualTo (8));
		}

	[Test]
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

		Assert.That (transport.SentCommands.Count, Is.EqualTo (5));
		Assert.That (transport.SentCommands[2], Does.Contain ("\"method\":\"set_on_off_gradually_info\""));
		Assert.That (transport.SentCommands[2], Does.Contain ("\"off_state\":{"));
		Assert.That (transport.SentCommands[2], Does.Contain ("\"enable\":false"));
		Assert.That (transport.SentCommands[2], Does.Contain ("\"duration\":8"));
		Assert.That (device.LightTransitionState, Is.Not.Null);
		Assert.That (device.LightTransitionState.IsEnabled, Is.EqualTo (true));
		Assert.That (device.LightTransitionState.IsTransitionOnEnabled, Is.EqualTo (true));
		Assert.That (device.LightTransitionState.TransitionOnSeconds, Is.EqualTo (12));
		Assert.That (device.LightTransitionState.TransitionOnDurationSeconds, Is.EqualTo (12));
		Assert.That (device.LightTransitionState.IsTransitionOffEnabled, Is.EqualTo (false));
		Assert.That (device.LightTransitionState.TransitionOffSeconds, Is.EqualTo (0));
		Assert.That (device.LightTransitionState.TransitionOffDurationSeconds, Is.EqualTo (8));
		}

	[Test]
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

		Assert.That (transport.SentCommands.Count, Is.EqualTo (4));
		Assert.That (transport.SentCommands[2], Does.Contain ("\"method\":\"set_lighting_effect\""));
		Assert.That (transport.SentCommands[2], Does.Contain ("\"enable\":0"));
		Assert.That (device.LightEffect, Is.Not.Null);
		Assert.That (device.LightEffect.IsEnabled, Is.EqualTo (false));
		Assert.That (device.LightStripEffect.State, Is.Not.Null);
		Assert.That (device.LightStripEffect.AvailableEffects.Count, Is.EqualTo (17));
		}

	[Test]
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

		Assert.That ((System.Collections.ICollection)result.SupportedCategories, Is.EquivalentTo (new[] { "subg.trigger.button", "subg.sensor.contact" }));
		Assert.That (result.DetectedDevices.Count, Is.EqualTo (2));
		Assert.That (result.DetectedDevices[0].DeviceId, Is.EqualTo ("scan-1"));
		Assert.That (result.DetectedDevices[0].Model, Is.EqualTo ("S200B"));
		Assert.That (result.DetectedDevices[0].Category, Is.EqualTo ("subg.trigger.button"));
		Assert.That (transport.SentCommands[1], Does.Contain ("\"method\":\"get_scan_child_device_list\""));
		Assert.That (transport.SentCommands[1], Does.Contain ("subg.trigger.button"));
		Assert.That (transport.SentCommands[1], Does.Contain ("subg.sensor.contact"));
		}

	[Test]
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

		Assert.That (added.Count, Is.EqualTo (1));
		Assert.That (added[0].DeviceId, Is.EqualTo ("scan-1"));
		Assert.That (transport.SentCommands[1], Does.Contain ("\"method\":\"add_child_device_list\""));
		Assert.That (transport.SentCommands[1], Does.Contain ("\"device_id\":\"scan-1\""));
		Assert.That (transport.SentCommands[3], Does.Contain ("\"method\":\"remove_child_device_list\""));
		Assert.That (transport.SentCommands[3], Does.Contain ("\"device_id\":\"scan-1\""));
		Assert.That (device.GetChild ("scan-1"), Is.Null);
		}

	[Test]
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
		Assert.That (sensor.Battery.BatteryLevel, Is.EqualTo (85));
		Assert.That (sensor.Battery.BatteryLow, Is.EqualTo (true));
		Assert.That (sensor.Contact.IsOpen, Is.EqualTo (true));
		Assert.That (sensor.Motion.MotionDetected, Is.EqualTo (true));
		Assert.That (sensor.WaterLeak.State!.Status, Is.EqualTo ("wet"));
		Assert.That (sensor.WaterLeak.State.Alert, Is.EqualTo (true));
		Assert.That (sensor.WaterLeak.State.AlertTimestamp, Is.EqualTo (1700000001L));
		Assert.That (sensor.Temperature.Temperature, Is.EqualTo (21.5d));
		Assert.That (sensor.Temperature.Warning, Is.EqualTo (true));
		Assert.That (sensor.Temperature.Unit, Is.EqualTo ("celsius"));
		Assert.That (sensor.Humidity.MinimumComfortHumidity, Is.EqualTo (40d));
		Assert.That (sensor.Humidity.MaximumComfortHumidity, Is.EqualTo (70d));

		ChildDevice trv = device.GetChildDevice ("trv-1")!;
		Assert.That (trv.FrostProtection.Enabled, Is.EqualTo (true));
		Assert.That (trv.FrostProtection.MinimumTemperature, Is.EqualTo (7));
		Assert.That (trv.ChildProtection.Enabled, Is.EqualTo (true));
		Assert.That (trv.TemperatureControl.TargetTemperature, Is.EqualTo (23.5d));
		Assert.That (trv.TemperatureControl.MinimumTargetTemperature, Is.EqualTo (5));
		Assert.That (trv.TemperatureControl.MaximumTargetTemperature, Is.EqualTo (30));
		Assert.That (trv.TemperatureControl.TemperatureOffset, Is.EqualTo (2));
		Assert.That ((System.Collections.ICollection)trv.TemperatureControl.States, Is.EqualTo (new[] { "heating", "window_open" }));
		Assert.That (trv.Thermostat.CurrentTemperature, Is.EqualTo (22d));
		Assert.That (trv.Thermostat.Unit, Is.EqualTo ("celsius"));
		Assert.That (trv.TriggerLogs.Logs.Count, Is.EqualTo (1));
		Assert.That (trv.TriggerLogs.Logs[0].EventName, Is.EqualTo ("started"));
		Assert.That (transport.SentCommands.Count, Is.EqualTo (3));
		}

	[Test]
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

		await Assert.ThatAsync (() => device.SetLightEffectAsync ("Aurora"), Throws.TypeOf<InvalidOperationException> ()).ConfigureAwait (false);
		}

	[Test]
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
		await Assert.ThatAsync (() => device.TurnChildOnAsync ("missing-child"), Throws.TypeOf<InvalidOperationException> ()).ConfigureAwait (false);
		}

	[Test]
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
		await Assert.ThatAsync (() => device.GetScannedChildDevicesAsync (), Throws.TypeOf<InvalidOperationException> ()).ConfigureAwait (false);
		}

	[Test]
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

		Assert.That (response, Is.EqualTo ("{\"system\":{\"set_relay_state\":{\"err_code\":0}}}"));
		Assert.That (transport.SentCommands.Count, Is.EqualTo (2));
		Assert.That (transport.SentManyCommands.Count, Is.EqualTo (1));
		Assert.That (device.SystemInfo?.Alias, Is.EqualTo ("Updated Plug"));
		Assert.That (device.IsOn, Is.EqualTo (true));
		}

	[Test]
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

		Assert.That (response, Is.EqualTo ("{\"result\":{\"error_code\":0}}"));
		Assert.That (transport.SentCommands.Count, Is.EqualTo (3));
		Assert.That (transport.SentCommands[0], Does.Contain ("\"method\":\"set_device_info\""));
		Assert.That (transport.SentCommands[0], Does.Contain ("\"request_time_milis\""));
		Assert.That (transport.SentCommands[0], Does.Contain ("\"terminal_uuid\""));
		Assert.That (device.Alias, Is.EqualTo ("Plug"));
		Assert.That (device.IsOn, Is.EqualTo (true));
		}

	[Test]
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

		Assert.That (transport.SentCommands.Count, Is.EqualTo (3));
		Assert.That (maxActiveSends, Is.EqualTo (1));
		}
	}