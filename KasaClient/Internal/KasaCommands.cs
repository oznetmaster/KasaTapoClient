// Copyright © 2026 Neil Colvin.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// Behavior modeled after the independent python-kasa project (https://github.com/python-kasa/python-kasa)
// for protocol/compatibility reference only; no python-kasa source was copied. See ATTRIBUTIONS.md.

using System;
using System.Collections.Generic;
using System.Globalization;

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
	public const string SMART_SET_DOUBLE_CLICK_INFO_METHOD = "set_double_click_info";
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
	public const string SMART_SET_ON_OFF_GRADUALLY_INFO_METHOD = "set_on_off_gradually_info";
	public const string SMART_GET_DYNAMIC_LIGHT_EFFECT_RULES_METHOD = "get_dynamic_light_effect_rules";
	public const string SMART_GET_ALARM_CONFIG_METHOD = "get_alarm_configure";

	public static string CreateSetRelayStateCommand (bool isOn) => CreateLegacyRequest ("system", "set_relay_state", new LegacyRequestParametersDto { State = isOn ? 1 : 0 });

	public static string CreateSetChildRelayStateCommand (string childDeviceId, bool isOn)
		{
		if (string.IsNullOrWhiteSpace (childDeviceId)) throw new ArgumentException ("A child device identifier is required.", nameof (childDeviceId));
		return CreateLegacyRequest ("system", "set_relay_state", new LegacyRequestParametersDto { State = isOn ? 1 : 0 }, childDeviceId);
		}

	public static string CreateGetEmeterDayStatCommand (int year, int month) => CreateLegacyRequest ("emeter", "get_daystat", new LegacyRequestParametersDto { Year = year, Month = month });
	public static string CreateGetBulbEmeterDayStatCommand (int year, int month) => CreateLegacyRequest ("smartlife.iot.common.emeter", "get_daystat", new LegacyRequestParametersDto { Year = year, Month = month });
	public static string CreateGetEmeterMonthStatCommand (int year) => CreateLegacyRequest ("emeter", "get_monthstat", new LegacyRequestParametersDto { Year = year });
	public static string CreateGetBulbEmeterMonthStatCommand (int year) => CreateLegacyRequest ("smartlife.iot.common.emeter", "get_monthstat", new LegacyRequestParametersDto { Year = year });

	public static string CreateSetLightStateCommand (DeviceType deviceType, bool? isOn = null, int? brightness = null, int? colorTemperature = null, int? hue = null, int? saturation = null, int? transitionMilliseconds = null)
		{
		if (isOn is null && brightness is null && colorTemperature is null && hue is null && saturation is null) throw new ArgumentException ("At least one light-state value is required.");
		string service = LightService (deviceType);
		var parameters = new LegacyRequestParametersDto
			{
			OnOff = isOn is bool on ? (on ? 1 : 0) : null,
			Brightness = brightness, ColorTemperature = hue is not null || saturation is not null ? 0 : colorTemperature,
			Hue = hue, Saturation = saturation,
			IgnoreDefault = deviceType == DeviceType.Bulb ? 1 : null,
			TransitionPeriod = deviceType == DeviceType.Bulb ? transitionMilliseconds ?? 0 : null,
			};
		return CreateLegacyRequest (service, deviceType == DeviceType.Bulb ? "transition_light_state" : "set_light_state", parameters);
		}

	public static string CreateSetLightEffectCommand (DeviceType deviceType, string? effect)
		{
		string service = LightService (deviceType);
		object parameters = deviceType == DeviceType.Bulb
			? new LegacyRequestParametersDto { Enable = string.IsNullOrWhiteSpace (effect) ? 0 : 1, Id = string.IsNullOrWhiteSpace (effect) ? null : effect }
			: KasaResponseParser.CreateSmartLightStripEffectPayload (effect);
		return CreateLegacyRequest (service, deviceType == DeviceType.Bulb ? "set_dynamic_light_effect_rule_enable" : "set_lighting_effect", parameters);
		}

	private static string LightService (DeviceType deviceType) => deviceType switch
		{
		DeviceType.Bulb => "smartlife.iot.smartbulb.lightingservice",
		DeviceType.LightStrip => "smartlife.iot.lightStrip",
		_ => throw new InvalidOperationException ($"Device type '{deviceType}' does not support light control."),
		};

	// Legacy services and methods are protocol-defined dynamic keys; parameter values are typed contracts.
	private static string CreateLegacyRequest (string service, string method, object parameters, string? childDeviceId = null)
		{
		var request = new Dictionary<string, object> { [service] = new Dictionary<string, object> { [method] = parameters } };
		if (childDeviceId is not null) request["context"] = new ChildContextDto { ChildIds = new[] { childDeviceId } };
		return WireJson.Serialize (request);
		}

	public static string CreateSmartRequest (string method, object? parameters = null)
		{
		if (string.IsNullOrWhiteSpace (method)) throw new ArgumentException ("A smart method name is required.", nameof (method));
		return WireJson.Serialize (new WireRequest<object>
			{
			Method = method, Parameters = parameters,
			RequestTimeMilliseconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds (),
			TerminalUuid = Convert.ToBase64String (Guid.NewGuid ().ToByteArray ()),
			});
		}

	public static string CreateSetSmartLightTransitionEnabledCommand (bool enabled) => CreateSmartRequest (SMART_SET_ON_OFF_GRADUALLY_INFO_METHOD, new TransitionParametersDto { Enable = enabled });
	public static string CreateSetSmartLightTransitionEnabledCommand (bool enabled, int onDurationSeconds, int offDurationSeconds) =>
		CreateSmartRequest (SMART_SET_ON_OFF_GRADUALLY_INFO_METHOD, new TransitionParametersDto { OnState = new TransitionParametersDto { Enable = enabled, Duration = onDurationSeconds }, OffState = new TransitionParametersDto { Enable = enabled, Duration = offDurationSeconds } });
	public static string CreateSetSmartLightTransitionOnCommand (bool enabled, int durationSeconds) =>
		CreateSmartRequest (SMART_SET_ON_OFF_GRADUALLY_INFO_METHOD, new TransitionParametersDto { OnState = new TransitionParametersDto { Enable = enabled, Duration = durationSeconds } });
	public static string CreateSetSmartLightTransitionOffCommand (bool enabled, int durationSeconds) =>
		CreateSmartRequest (SMART_SET_ON_OFF_GRADUALLY_INFO_METHOD, new TransitionParametersDto { OffState = new TransitionParametersDto { Enable = enabled, Duration = durationSeconds } });

	private static MultipleRequestParametersDto CreateMultipleParameters (IReadOnlyDictionary<string, object?> requests)
		{
		if (requests.Count == 0) throw new ArgumentException ("At least one smart request is required.", nameof (requests));
		var items = new List<WireRequest<object>> (requests.Count);
		foreach (KeyValuePair<string, object?> request in requests) items.Add (new WireRequest<object> { Method = request.Key, Parameters = request.Value });
		return new MultipleRequestParametersDto { Requests = items };
		}

	public static string CreateSmartMultipleRequest (IReadOnlyDictionary<string, object?> requests) => CreateSmartRequest ("multipleRequest", CreateMultipleParameters (requests));
	public static string CreateSmartChildRequest (string childDeviceId, string method, object? parameters = null)
		{
		if (string.IsNullOrWhiteSpace (childDeviceId)) throw new ArgumentException ("A child device identifier is required.", nameof (childDeviceId));
		if (string.IsNullOrWhiteSpace (method)) throw new ArgumentException ("A smart method name is required.", nameof (method));
		return CreateSmartRequest ("control_child", new ChildRequestParametersDto { DeviceId = childDeviceId, RequestData = new WireRequest<object> { Method = method, Parameters = parameters } });
		}
	public static string CreateSmartChildMultipleRequest (string childDeviceId, IReadOnlyDictionary<string, object?> requests) => CreateSmartChildRequest (childDeviceId, "multipleRequest", CreateMultipleParameters (requests));
	}
