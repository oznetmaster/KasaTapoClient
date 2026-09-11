// Copyright © 2026 Neil Colvin.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using KasaTapoClient;
using KasaTapoClient.Internal;

using NUnit.Framework;

namespace KasaClient.Tests;

[TestFixture]
public sealed class KasaResponseParserTests
	{
	[Test]
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

		Assert.That (state.SystemInfo.DeviceType, Is.EqualTo (DeviceType.Bulb));
		Assert.That (state.SystemInfo.Alias, Is.EqualTo ("Test Bulb"));
		Assert.That (state.LightState, Is.Not.Null);
		Assert.That (state.LightState.IsOn, Is.EqualTo (true));
		Assert.That (state.LightState.Brightness, Is.EqualTo (75));
		Assert.That (state.LightState.ColorTemperature, Is.EqualTo (2700));
		Assert.That (state.LightState.Hue, Is.EqualTo (120));
		Assert.That (state.LightState.Saturation, Is.EqualTo (80));
		}

	[Test]
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

		Assert.That (state.SystemInfo.DeviceType, Is.EqualTo (DeviceType.Bulb));
		Assert.That (state.SystemInfo.Alias, Is.EqualTo ("Smart Bulb"));
		Assert.That (state.LightState, Is.Not.Null);
		Assert.That (state.LightState.IsOn, Is.EqualTo (true));
		Assert.That (state.LightState.Brightness, Is.EqualTo (60));
		Assert.That (state.LightState.ColorTemperature, Is.EqualTo (3500));
		Assert.That (state.AutoOffState, Is.Not.Null);
		Assert.That (state.AutoOffState.Enabled, Is.EqualTo (true));
		Assert.That (state.AutoOffState.DelayMinutes, Is.EqualTo (15));
		Assert.That (state.LedState, Is.Not.Null);
		Assert.That (state.LedState.Enabled, Is.EqualTo (true));
		Assert.That (state.CloudState, Is.Not.Null);
		Assert.That (state.CloudState.IsConnected, Is.EqualTo (true));
		Assert.That (state.TimeState, Is.Not.Null);
		Assert.That (state.TimeState.Region, Is.EqualTo ("UTC"));
		}

	[Test]
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
					 "on_state": { "enable": true, "duration": 12, "max_duration": 40 },
					 "off_state": { "enable": false, "duration": 30, "max_duration": 45 }
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

		Assert.That (state.LightPresetState, Is.Not.Null);
		Assert.That (state.LightPresetState.ActivePreset, Is.EqualTo ("Light preset 1"));
		Assert.That (state.LightPresetState.Presets.Count, Is.EqualTo (2));
		Assert.That (state.LightPresetState.Presets[0].Brightness, Is.EqualTo (50));
		Assert.That (state.LightTransitionState, Is.Not.Null);
		Assert.That (state.LightTransitionState.IsEnabled, Is.EqualTo (true));
		Assert.That (state.LightTransitionState.IsTransitionOnEnabled, Is.EqualTo (true));
		Assert.That (state.LightTransitionState.TransitionOnDurationSeconds, Is.EqualTo (12));
		Assert.That (state.LightTransitionState.TransitionOnMaximumDurationSeconds, Is.EqualTo (40));
		Assert.That (state.LightTransitionState.TransitionOnSeconds, Is.EqualTo (12));
		Assert.That (state.LightTransitionState.IsTransitionOffEnabled, Is.EqualTo (false));
		Assert.That (state.LightTransitionState.TransitionOffDurationSeconds, Is.EqualTo (30));
		Assert.That (state.LightTransitionState.TransitionOffMaximumDurationSeconds, Is.EqualTo (45));
		Assert.That (state.LightTransitionState.TransitionOffSeconds, Is.EqualTo (0));
		Assert.That (state.AlarmState, Is.Not.Null);
		Assert.That (state.AlarmState.IsActive, Is.EqualTo (true));
		Assert.That (state.AlarmState.Source, Is.EqualTo ("motion"));
		Assert.That (state.AlarmState.Sound, Is.EqualTo ("siren"));
		Assert.That (state.AlarmState.Volume, Is.EqualTo ("high"));
		Assert.That (state.AlarmState.VolumeLevel, Is.EqualTo (3));
		Assert.That (state.AlarmState.DurationSeconds, Is.EqualTo (120));
		Assert.That (state.EnergyUsage, Is.Not.Null);
		Assert.That (state.EnergyUsage.CurrentPowerWatts, Is.EqualTo (12.345d));
		Assert.That (state.EnergyUsage.VoltageVolts, Is.EqualTo (230d));
		Assert.That (state.EnergyUsage.CurrentAmps, Is.EqualTo (0.1d));
		Assert.That (state.EnergyUsage.TotalKilowattHours, Is.EqualTo (0.678d));
		}
	}