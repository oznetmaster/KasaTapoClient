// Copyright © 2026 Neil Colvin.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using static KasaTapoClient.Internal.KasaResponseParser;

namespace KasaTapoClient.Internal;

internal sealed class ResponseEnvelope<T>
	{
	[JsonPropertyName ("method")]
	public string? Method { get; set; }
	[JsonPropertyName ("error_code")]
	public int? ErrorCode { get; set; }
	[JsonPropertyName ("result")]
	public T? Result { get; set; }
	}

internal sealed class ResponseHeader
	{
	[JsonPropertyName ("method")]
	public string? Method { get; set; }
	[JsonPropertyName ("error_code")]
	public int? ErrorCode { get; set; }
	}

// Select a concrete result contract by method without materializing a JSON DOM.
// Reading the header from a reader copy also accepts result-before-method payloads.
internal sealed class SmartMethodResponseConverter : JsonConverter<SmartMethodResponseDto>
	{
	public override SmartMethodResponseDto Read (ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
		Utf8JsonReader headerReader = reader;
		ResponseHeader header = JsonSerializer.Deserialize<ResponseHeader> (ref headerReader, options)
			?? throw new JsonException ("Expected a smart method response.");
		object? result = null;
		if (header.ErrorCode is null or 0)
			{
			result = header.Method switch
				{
				"get_preset_rules" => JsonSerializer.Deserialize<ResponseEnvelope<SmartPresetRulesDto>> (ref reader, options)?.Result,
				"get_on_off_gradually_info" => JsonSerializer.Deserialize<ResponseEnvelope<SmartOnOffGraduallyInfoDto>> (ref reader, options)?.Result,
				"get_dynamic_light_effect_rules" => JsonSerializer.Deserialize<ResponseEnvelope<SmartDynamicLightEffectRulesDto>> (ref reader, options)?.Result,
				"get_energy_usage" => JsonSerializer.Deserialize<ResponseEnvelope<SmartEnergyUsageDto>> (ref reader, options)?.Result,
				"get_current_power" => JsonSerializer.Deserialize<ResponseEnvelope<SmartCurrentPowerDto>> (ref reader, options)?.Result,
				"get_emeter_data" => JsonSerializer.Deserialize<ResponseEnvelope<SmartEmeterDataDto>> (ref reader, options)?.Result,
				"get_auto_update_info" => JsonSerializer.Deserialize<ResponseEnvelope<SmartAutoUpdateInfoDto>> (ref reader, options)?.Result,
				"get_latest_fw" => JsonSerializer.Deserialize<ResponseEnvelope<SmartLatestFirmwareDto>> (ref reader, options)?.Result,
				"get_connect_cloud_state" => JsonSerializer.Deserialize<ResponseEnvelope<SmartCloudConnectStateDto>> (ref reader, options)?.Result,
				"get_device_time" => JsonSerializer.Deserialize<ResponseEnvelope<SmartDeviceTimeDto>> (ref reader, options)?.Result,
				"get_matter_setup_info" => JsonSerializer.Deserialize<ResponseEnvelope<SmartMatterSetupDto>> (ref reader, options)?.Result,
				"get_homekit_info" => JsonSerializer.Deserialize<ResponseEnvelope<SmartHomeKitInfoDto>> (ref reader, options)?.Result,
				"get_auto_off_config" => JsonSerializer.Deserialize<ResponseEnvelope<SmartAutoOffConfigDto>> (ref reader, options)?.Result,
				"get_led_info" => JsonSerializer.Deserialize<ResponseEnvelope<SmartLedInfoDto>> (ref reader, options)?.Result,
				"getChildLockInfo" => JsonSerializer.Deserialize<ResponseEnvelope<SmartChildLockInfoDto>> (ref reader, options)?.Result,
				"get_device_info" => JsonSerializer.Deserialize<ResponseEnvelope<SmartEnvelopeResultDto>> (ref reader, options)?.Result,
				"component_nego" => JsonSerializer.Deserialize<ResponseEnvelope<SmartEnvelopeResultDto>> (ref reader, options)?.Result,
				"get_child_device_list" => JsonSerializer.Deserialize<ResponseEnvelope<SmartEnvelopeResultDto>> (ref reader, options)?.Result,
				"get_child_device_component_list" => JsonSerializer.Deserialize<ResponseEnvelope<SmartEnvelopeResultDto>> (ref reader, options)?.Result,
				"get_alarm_configure" => JsonSerializer.Deserialize<ResponseEnvelope<SmartAlarmInfoDto>> (ref reader, options)?.Result,
				"get_alarm_config" => JsonSerializer.Deserialize<ResponseEnvelope<SmartAlarmInfoDto>> (ref reader, options)?.Result,
				"get_alarm_info" => JsonSerializer.Deserialize<ResponseEnvelope<SmartAlarmInfoDto>> (ref reader, options)?.Result,
				"get_guard_mode" => JsonSerializer.Deserialize<ResponseEnvelope<SmartAlarmInfoDto>> (ref reader, options)?.Result,
				"get_double_click_info" => JsonSerializer.Deserialize<ResponseEnvelope<SmartDoubleClickInfoDto>> (ref reader, options)?.Result,
				"get_trigger_logs" => JsonSerializer.Deserialize<ResponseEnvelope<SmartTriggerLogListDto>> (ref reader, options)?.Result,
				"get_comfort_humidity_config" => JsonSerializer.Deserialize<ResponseEnvelope<SmartComfortValueConfigDto>> (ref reader, options)?.Result,
				"get_comfort_temp_config" => JsonSerializer.Deserialize<ResponseEnvelope<SmartComfortValueConfigDto>> (ref reader, options)?.Result,
				"get_frost_protection" => JsonSerializer.Deserialize<ResponseEnvelope<SmartFrostProtectionDto>> (ref reader, options)?.Result,
				_ => null,
				};
			}
		if (result is null) reader = headerReader;
		return new SmartMethodResponseDto { Method = header.Method, ErrorCode = header.ErrorCode, Result = result };
		}

	public override void Write (Utf8JsonWriter writer, SmartMethodResponseDto value, JsonSerializerOptions options)
		{
		JsonSerializer.Serialize (writer, new ResponseEnvelope<object> { Method = value.Method, ErrorCode = value.ErrorCode, Result = value.Result }, options);
		}
	}

internal sealed class ChildControlResultDto
	{
	[JsonPropertyName ("responseData")]
	public ResponseEnvelope<SmartChildDeviceDto>? ResponseData { get; set; }
	}
