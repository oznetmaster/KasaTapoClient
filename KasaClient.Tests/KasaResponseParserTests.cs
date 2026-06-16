// Copyright © 2026 Neil Colvin.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using KasaTapoClient;
using KasaTapoClient.Internal;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace KasaClient.Tests;

[TestClass]
public sealed class KasaResponseParserTests
	{
	[TestMethod]
	public void ParseDeviceState_WithLegacyBulbResponse_PopulatesLightStateAndSystemInfo ()
		{
		const string responseJson = """
		{
		  "system": {
			 "get_sysinfo": {
				"alias": "Test Bulb",
				"model": "LB130",
				"deviceId": "bulb-1",
				"relay_state": 1,
				"light_state": {
				  "on_off": 1,
				  "brightness": 75,
				  "color_temp": 2700,
				  "hue": 120,
				  "saturation": 80
				}
			 }
		  }
		}
		""";

		KasaResponseParser.ParsedResponse parsed = KasaResponseParser.ParseResponse (responseJson);
		KasaResponseParser.ParsedDeviceState state = KasaResponseParser.ParseDeviceState (parsed);

		Assert.AreEqual (DeviceType.Bulb, state.SystemInfo.DeviceType);
		Assert.AreEqual ("Test Bulb", state.SystemInfo.Alias);
		Assert.IsNotNull (state.LightState);
		Assert.AreEqual (true, state.LightState.IsOn);
		Assert.AreEqual (75, state.LightState.Brightness);
		Assert.AreEqual (2700, state.LightState.ColorTemperature);
		Assert.AreEqual (120, state.LightState.Hue);
		Assert.AreEqual (80, state.LightState.Saturation);
		}

	[TestMethod]
	public void ParseSmartDeviceState_WithSmartBulbResponse_DecodesAliasAndComponentStates ()
		{
		const string responseJson = """
		{
		  "result": {
			 "responses": [
				{
				  "method": "get_device_info",
				  "result": {
					 "model": "L530",
					 "type": "SMART.TAPOBULB",
					 "device_id": "smart-bulb-1",
					 "nickname": "U21hcnQgQnVsYg==",
					 "device_on": true,
					 "brightness": 60,
					 "hue": 25,
					 "saturation": 70,
					 "color_temp": 3500,
					 "fw_ver": "1.0.0",
					 "hw_ver": "1.0"
				  }
				},
				{
				  "method": "component_nego",
				  "result": {
					 "component_list": [
						{ "id": "brightness", "ver_code": 1 },
						{ "id": "color", "ver_code": 1 },
						{ "id": "color_temperature", "ver_code": 1 },
						{ "id": "auto_off", "ver_code": 1 },
						{ "id": "led", "ver_code": 1 },
						{ "id": "cloud_connect", "ver_code": 1 },
						{ "id": "time", "ver_code": 1 }
					 ]
				  }
				},
				{
				  "method": "get_auto_off_config",
				  "result": {
					 "enable": true,
					 "delay_min": 15
				  }
				},
				{
				  "method": "get_led_info",
				  "result": {
					 "led_rule": "always"
				  }
				},
				{
				  "method": "get_connect_cloud_state",
				  "result": {
					 "status": 0
				  }
				},
				{
				  "method": "get_device_time",
				  "result": {
					 "timestamp": 1735787045,
					 "region": "UTC",
					 "time_diff": 0
				  }
				}
			 ]
		  }
		}
		""";

		KasaResponseParser.SmartParsedResponse parsed = KasaResponseParser.ParseSmartResponse (responseJson);
		KasaResponseParser.ParsedDeviceState state = KasaResponseParser.ParseSmartDeviceState (parsed);

		Assert.AreEqual (DeviceType.Bulb, state.SystemInfo.DeviceType);
		Assert.AreEqual ("Smart Bulb", state.SystemInfo.Alias);
		Assert.IsNotNull (state.LightState);
		Assert.AreEqual (true, state.LightState.IsOn);
		Assert.AreEqual (60, state.LightState.Brightness);
		Assert.AreEqual (3500, state.LightState.ColorTemperature);
		Assert.IsNotNull (state.AutoOffState);
		Assert.AreEqual (true, state.AutoOffState.Enabled);
		Assert.AreEqual (15, state.AutoOffState.DelayMinutes);
		Assert.IsNotNull (state.LedState);
		Assert.AreEqual (true, state.LedState.Enabled);
		Assert.IsNotNull (state.CloudState);
		Assert.AreEqual (true, state.CloudState.IsConnected);
		Assert.IsNotNull (state.TimeState);
		Assert.AreEqual ("UTC", state.TimeState.Region);
		}

	[TestMethod]
	public void ParseSmartDeviceState_WithAlarmPresetTransitionAndEnergyData_ProjectsEdgeCaseModules ()
		{
		const string responseJson = """
		{
		  "result": {
			 "responses": [
				{
				  "method": "get_device_info",
				  "result": {
					 "model": "L535",
					 "type": "SMART.TAPOBULB",
					 "device_id": "smart-bulb-edge-1",
					 "nickname": "RWRnZSBCdWxi",
					 "device_on": true,
					 "brightness": 50,
					 "hue": 100,
					 "saturation": 80,
					 "color_temp": 2700,
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
						{ "id": "color_temperature", "ver_code": 1 },
						{ "id": "preset", "ver_code": 1 },
						{ "id": "on_off_gradually", "ver_code": 1 },
						{ "id": "energy_monitoring", "ver_code": 2 },
						{ "id": "alarm", "ver_code": 1 }
					 ]
				  }
				},
				{
				  "method": "get_preset_rules",
				  "result": {
					 "states": [
						{ "brightness": 50, "color_temp": 2700, "hue": 100, "saturation": 80 },
						{ "brightness": 10, "color_temp": 4000, "hue": 0, "saturation": 0 }
					 ]
				  }
				},
				{
				  "method": "get_on_off_gradually_info",
				  "result": {
					 "on_state": { "enable": true, "duration": 12 },
					 "off_state": { "enable": false, "duration": 30 }
				  }
				},
				{
				  "method": "get_alarm_configure",
				  "result": {
					 "in_alarm": true,
					 "alarm_source": "motion",
					 "alarm_type": "siren",
					 "alarm_volume": "high",
					 "alarm_volume_level": 3,
					 "alarm_duration": 120
				  }
				},
				{
				  "method": "get_energy_usage",
				  "result": {
					 "current_power": 12345,
					 "today_energy": 678
				  }
				},
				{
				  "method": "get_current_power",
				  "result": {
					 "current_power": 12.345
				  }
				},
				{
				  "method": "get_emeter_data",
				  "result": {
					 "voltage_mv": 230000,
					 "current_ma": 100
				  }
				}
			 ]
		  }
		}
		""";

		KasaResponseParser.SmartParsedResponse parsed = KasaResponseParser.ParseSmartResponse (responseJson);
		KasaResponseParser.ParsedDeviceState state = KasaResponseParser.ParseSmartDeviceState (parsed);

		Assert.IsNotNull (state.LightPresetState);
		Assert.AreEqual ("Light preset 1", state.LightPresetState.ActivePreset);
		Assert.AreEqual (2, state.LightPresetState.Presets.Count);
		Assert.AreEqual (50, state.LightPresetState.Presets[0].Brightness);
		Assert.IsNotNull (state.LightTransitionState);
		Assert.AreEqual (12, state.LightTransitionState.TransitionOnSeconds);
		Assert.AreEqual (0, state.LightTransitionState.TransitionOffSeconds);
		Assert.IsNotNull (state.AlarmState);
		Assert.AreEqual (true, state.AlarmState.IsActive);
		Assert.AreEqual ("motion", state.AlarmState.Source);
		Assert.AreEqual ("siren", state.AlarmState.Sound);
		Assert.AreEqual ("high", state.AlarmState.Volume);
		Assert.AreEqual (3, state.AlarmState.VolumeLevel);
		Assert.AreEqual (120, state.AlarmState.DurationSeconds);
		Assert.IsNotNull (state.EnergyUsage);
		Assert.AreEqual (12.345d, state.EnergyUsage.CurrentPowerWatts);
		Assert.AreEqual (230d, state.EnergyUsage.VoltageVolts);
		Assert.AreEqual (0.1d, state.EnergyUsage.CurrentAmps);
		Assert.AreEqual (0.678d, state.EnergyUsage.TotalKilowattHours);
		}
	}
