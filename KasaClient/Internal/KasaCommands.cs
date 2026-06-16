// Copyright © 2026 Neil Colvin.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// Adapted from python-kasa (https://github.com/python-kasa/python-kasa)
// Original work Copyright (c) python-kasa contributors, MIT License

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json.Nodes;

namespace KasaTapoClient.Internal;

internal static class KasaCommands
	{
	public const string GET_SYSTEM_INFO = "{\"system\":{\"get_sysinfo\":{}}}";
	public const string GET_EMETER_REALTIME = "{\"emeter\":{\"get_realtime\":{}}}";
	public const string GET_EMETER_DAYSTAT = "{\"emeter\":{\"get_daystat\":{\"year\":%YEAR%,\"month\":%MONTH%}}}";
	public const string GET_EMETER_MONTHSTAT = "{\"emeter\":{\"get_monthstat\":{\"year\":%YEAR%}}}";
	public const string GET_COUNTDOWN_RULE = "{\"count_down\":{\"get_rules\":{}}}";
	public const string GET_SCHEDULE_RULES = "{\"schedule\":{\"get_rules\":{}}}";
	public const string GET_ANTITHEFT_RULES = "{\"anti_theft\":{\"get_rules\":{}}}";
	public const string GET_TIME = "{\"time\":{\"get_time\":{}}}";
	public const string GET_TIMEZONE = "{\"time\":{\"get_timezone\":{}}}";
	public const string GET_CLOUD_INFO = "{\"cnCloud\":{\"get_info\":{}}}";
	public const string GET_BULB_EMETER_REALTIME = "{\"smartlife.iot.common.emeter\":{\"get_realtime\":{}}}";
	public const string GET_BULB_EMETER_DAYSTAT = "{\"smartlife.iot.common.emeter\":{\"get_daystat\":{\"year\":%YEAR%,\"month\":%MONTH%}}}";
	public const string GET_BULB_EMETER_MONTHSTAT = "{\"smartlife.iot.common.emeter\":{\"get_monthstat\":{\"year\":%YEAR%}}}";
	public const string GET_BULB_TIME = "{\"smartlife.iot.common.timesetting\":{\"get_time\":{}}}";
	public const string GET_BULB_TIMEZONE = "{\"smartlife.iot.common.timesetting\":{\"get_timezone\":{}}}";
	public const string GET_BULB_CLOUD_INFO = "{\"smartlife.iot.common.cloud\":{\"get_info\":{}}}";
	public const string GET_BULB_COUNTDOWN_RULE = "{\"countdown\":{\"get_rules\":{}}}";
	public const string GET_BULB_SCHEDULE_RULES = "{\"smartlife.iot.common.schedule\":{\"get_rules\":{}}}";
	public const string GET_BULB_ANTITHEFT_RULES = "{\"smartlife.iot.common.anti_theft\":{\"get_rules\":{}}}";
	public const string GET_LED_INFO = "{\"system\":{\"get_sysinfo\":{}}}";
	public const string GET_HOMEKIT_INFO = "{\"smartlife.iot.homekit\":{\"setup_info_get\":{}}}";
	public const string SMART_GET_DEVICE_INFO_METHOD = "get_device_info";
	public const string SMART_COMPONENT_NEGO_METHOD = "component_nego";
	public const string SMART_GET_CHILD_DEVICE_LIST_METHOD = "get_child_device_list";
	public const string SMART_GET_CHILD_DEVICE_COMPONENT_LIST_METHOD = "get_child_device_component_list";
	public const string SMART_BEGIN_SCANNING_CHILD_DEVICE_METHOD = "begin_scanning_child_device";
	public const string SMART_GET_SCAN_CHILD_DEVICE_LIST_METHOD = "get_scan_child_device_list";
	public const string SMART_ADD_CHILD_DEVICE_LIST_METHOD = "add_child_device_list";
	public const string SMART_REMOVE_CHILD_DEVICE_LIST_METHOD = "remove_child_device_list";
	public const string SMART_GET_DOUBLE_CLICK_INFO_METHOD = "get_double_click_info";
	public const string SMART_GET_TRIGGER_LOGS_METHOD = "get_trigger_logs";
	public const string SMART_GET_FROST_PROTECTION_METHOD = "get_frost_protection";
	public const string SMART_GET_BATTERY_DETECT_INFO_METHOD = "get_battery_detect_info";
	public const string SMART_GET_REPORT_MODE_METHOD = "get_report_mode";
	public const string SMART_GET_COMFORT_TEMP_CONFIG_METHOD = "get_comfort_temp_config";
	public const string SMART_GET_COMFORT_HUMIDITY_CONFIG_METHOD = "get_comfort_humidity_config";
	public const string SMART_GET_AUTO_OFF_CONFIG_METHOD = "get_auto_off_config";
	public const string SMART_SET_AUTO_OFF_CONFIG_METHOD = "set_auto_off_config";
	public const string SMART_GET_CONNECT_CLOUD_STATE_METHOD = "get_connect_cloud_state";
	public const string SMART_GET_AUTO_UPDATE_INFO_METHOD = "get_auto_update_info";
	public const string SMART_SET_AUTO_UPDATE_INFO_METHOD = "set_auto_update_info";
	public const string SMART_GET_LATEST_FW_METHOD = "get_latest_fw";
	public const string SMART_GET_LED_INFO_METHOD = "get_led_info";
	public const string SMART_SET_LED_INFO_METHOD = "set_led_info";
	public const string SMART_GET_DEVICE_TIME_METHOD = "get_device_time";
	public const string SMART_SET_DEVICE_TIME_METHOD = "set_device_time";
	public const string SMART_GET_ENERGY_USAGE_METHOD = "get_energy_usage";
	public const string SMART_GET_CURRENT_POWER_METHOD = "get_current_power";
	public const string SMART_GET_EMETER_DATA_METHOD = "get_emeter_data";
	public const string SMART_GET_EMETER_VGAIN_IGAIN_METHOD = "get_emeter_vgain_igain";
	public const string SMART_GET_MATTER_SETUP_INFO_METHOD = "get_matter_setup_info";
	public const string SMART_GET_HOMEKIT_INFO_METHOD = "get_homekit_info";
	public const string SMART_GET_CHILD_LOCK_INFO_METHOD = "getChildLockInfo";
	public const string SMART_SET_CHILD_LOCK_INFO_METHOD = "setChildLockInfo";
	public const string SMART_GET_PRESET_RULES_METHOD = "get_preset_rules";
	public const string SMART_GET_ON_OFF_GRADUALLY_INFO_METHOD = "get_on_off_gradually_info";
	public const string SMART_GET_DYNAMIC_LIGHT_EFFECT_RULES_METHOD = "get_dynamic_light_effect_rules";
	public const string SMART_GET_ALARM_CONFIG_METHOD = "get_alarm_configure";

	public static string CreateSetRelayStateCommand (bool isOn)
		{
		int relayState = isOn ? 1 : 0;
		return $"{{\"system\":{{\"set_relay_state\":{{\"state\":{relayState}}}}}}}";
		}

	public static string CreateSetChildRelayStateCommand (string childDeviceId, bool isOn)
		{
		if (string.IsNullOrWhiteSpace (childDeviceId))
			{
			throw new ArgumentException ("A child device identifier is required.", nameof (childDeviceId));
			}

		int relayState = isOn ? 1 : 0;
		var command = new JsonObject
			{
			["context"] = new JsonObject
				{
				["child_ids"] = new JsonArray (childDeviceId),
				},
			["system"] = new JsonObject
				{
				["set_relay_state"] = new JsonObject
					{
					["state"] = relayState,
					},
				},
			};

		return command.ToJsonString (JsonSupport.COMPACT_JSON);
		}

	public static string CreateGetEmeterDayStatCommand (int year, int month)
		{
		return GET_EMETER_DAYSTAT
			.Replace ("%YEAR%", year.ToString (CultureInfo.InvariantCulture))
			.Replace ("%MONTH%", month.ToString (CultureInfo.InvariantCulture));
		}

	public static string CreateGetBulbEmeterDayStatCommand (int year, int month)
		{
		return GET_BULB_EMETER_DAYSTAT
			.Replace ("%YEAR%", year.ToString (CultureInfo.InvariantCulture))
			.Replace ("%MONTH%", month.ToString (CultureInfo.InvariantCulture));
		}

	public static string CreateGetEmeterMonthStatCommand (int year)
		{
		return GET_EMETER_MONTHSTAT.Replace ("%YEAR%", year.ToString (CultureInfo.InvariantCulture));
		}

	public static string CreateGetBulbEmeterMonthStatCommand (int year)
		{
		return GET_BULB_EMETER_MONTHSTAT.Replace ("%YEAR%", year.ToString (CultureInfo.InvariantCulture));
		}

	public static string CreateSetLightStateCommand (
		DeviceType deviceType,
		bool? isOn = null,
		int? brightness = null,
		int? colorTemperature = null,
		int? hue = null,
		int? saturation = null)
		{
		string service = deviceType switch
			{
				DeviceType.Bulb => "smartlife.iot.smartbulb.lightingservice",
				DeviceType.LightStrip => "smartlife.iot.lightStrip",
				_ => throw new InvalidOperationException ($"Device type '{deviceType}' does not support light-state control."),
			};
		string method = deviceType switch
			{
				DeviceType.Bulb => "transition_light_state",
				DeviceType.LightStrip => "set_light_state",
				_ => throw new InvalidOperationException ($"Device type '{deviceType}' does not support light-state control."),
			};

		var lightState = new JsonObject ();
		if (isOn is bool powerState)
			{
			lightState["on_off"] = powerState ? 1 : 0;
			}

		if (brightness is int brightnessValue)
			{
			lightState["brightness"] = brightnessValue;
			}

		if (colorTemperature is int colorTemperatureValue)
			{
			lightState["color_temp"] = colorTemperatureValue;
			}

		if (hue is int hueValue)
			{
			lightState["hue"] = hueValue;
			}

		if (saturation is int saturationValue)
			{
			lightState["saturation"] = saturationValue;
			}

		if (hue is int || saturation is int)
			{
			lightState["color_temp"] = 0;
			}

		if (lightState.Count == 0)
			{
			throw new ArgumentException ("At least one light-state value is required.");
			}

		if (deviceType == DeviceType.Bulb)
			{
			lightState["ignore_default"] = 1;
			if (!lightState.ContainsKey ("transition_period"))
				{
				lightState["transition_period"] = 0;
				}
			}

		var command = new JsonObject
			{
			[service] = new JsonObject
				{
				[method] = lightState,
				},
			};

		return command.ToJsonString (JsonSupport.COMPACT_JSON);
		}

	public static string CreateSetLightEffectCommand (DeviceType deviceType, string? effect)
		{
		string service = deviceType switch
			{
				DeviceType.Bulb => "smartlife.iot.smartbulb.lightingservice",
				DeviceType.LightStrip => "smartlife.iot.lightStrip",
				_ => throw new InvalidOperationException ($"Device type '{deviceType}' does not support light-effect control."),
			};

		string method = deviceType switch
			{
				DeviceType.Bulb => "set_dynamic_light_effect_rule_enable",
				DeviceType.LightStrip => "set_lighting_effect",
				_ => throw new InvalidOperationException ($"Device type '{deviceType}' does not support light-effect control."),
			};

		JsonObject payload = deviceType switch
			{
				DeviceType.Bulb => new JsonObject
					{
					["enable"] = string.IsNullOrWhiteSpace (effect) ? 0 : 1,
					["id"] = string.IsNullOrWhiteSpace (effect) ? null : effect,
					},
				DeviceType.LightStrip => KasaResponseParser.CreateSmartLightStripEffectPayload (effect),
				_ => throw new InvalidOperationException ($"Device type '{deviceType}' does not support light-effect control."),
			};

		var command = new JsonObject
			{
			[service] = new JsonObject
				{
				[method] = payload,
				},
			};

		return command.ToJsonString (JsonSupport.COMPACT_JSON);
		}

	public static string CreateSmartRequest (string method, JsonObject? parameters = null)
		{
		if (string.IsNullOrWhiteSpace (method))
			{
			throw new ArgumentException ("A smart method name is required.", nameof (method));
			}

		var request = new JsonObject
			{
			["method"] = method,
			["request_time_milis"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds (),
			["terminal_uuid"] = Convert.ToBase64String (Guid.NewGuid ().ToByteArray ()),
			};
		if (parameters is not null)
			{
			request["params"] = parameters;
			}

		return request.ToJsonString (JsonSupport.COMPACT_JSON);
		}

	public static string CreateSmartMultipleRequest (IReadOnlyDictionary<string, JsonObject?> requests)
		{
		if (requests.Count == 0)
			{
			throw new ArgumentException ("At least one smart request is required.", nameof (requests));
			}

		var requestItems = new JsonArray ();
		foreach (KeyValuePair<string, JsonObject?> request in requests)
			{
			var item = new JsonObject
				{
				["method"] = request.Key,
				};
			if (request.Value is not null)
				{
				item["params"] = request.Value;
				}

			requestItems.Add (item);
			}

		return CreateSmartRequest (
			"multipleRequest",
			new JsonObject
				{
				["requests"] = requestItems,
				});
		}

	public static string CreateSmartChildRequest (string childDeviceId, string method, JsonObject? parameters = null)
		{
		if (string.IsNullOrWhiteSpace (childDeviceId))
			{
			throw new ArgumentException ("A child device identifier is required.", nameof (childDeviceId));
			}

		if (string.IsNullOrWhiteSpace (method))
			{
			throw new ArgumentException ("A smart method name is required.", nameof (method));
			}

		var requestData = new JsonObject
			{
			["method"] = method,
			};
		if (parameters is not null)
			{
			requestData["params"] = parameters;
			}

		return CreateSmartRequest (
			"control_child",
			new JsonObject
				{
				["device_id"] = childDeviceId,
				["requestData"] = requestData,
				});
		}

	public static string CreateSmartChildMultipleRequest (string childDeviceId, IReadOnlyDictionary<string, JsonObject?> requests)
		{
		if (string.IsNullOrWhiteSpace (childDeviceId))
			{
			throw new ArgumentException ("A child device identifier is required.", nameof (childDeviceId));
			}

		if (requests.Count == 0)
			{
			throw new ArgumentException ("At least one smart request is required.", nameof (requests));
			}

		var requestItems = new JsonArray ();
		foreach (KeyValuePair<string, JsonObject?> request in requests)
			{
			var item = new JsonObject
				{
				["method"] = request.Key,
				};
			if (request.Value is not null)
				{
				item["params"] = request.Value;
				}

			requestItems.Add (item);
			}

		return CreateSmartChildRequest (
			childDeviceId,
			"multipleRequest",
			new JsonObject
				{
				["requests"] = requestItems,
				});
		}
	}
