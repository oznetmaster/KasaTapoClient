// Copyright © 2026 Neil Colvin.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace KasaTapoClient.Internal;

internal sealed class SmartDeviceSettingsDto
	{
	[JsonPropertyName ("device_on")]
	[JsonIgnore (Condition = JsonIgnoreCondition.WhenWritingNull)]
	public bool? DeviceOn { get; set; }
	[JsonPropertyName ("brightness")]
	[JsonIgnore (Condition = JsonIgnoreCondition.WhenWritingNull)]
	public int? Brightness { get; set; }
	[JsonPropertyName ("color_temp")]
	[JsonIgnore (Condition = JsonIgnoreCondition.WhenWritingNull)]
	public int? ColorTemperature { get; set; }
	[JsonPropertyName ("hue")]
	[JsonIgnore (Condition = JsonIgnoreCondition.WhenWritingNull)]
	public int? Hue { get; set; }
	[JsonPropertyName ("saturation")]
	[JsonIgnore (Condition = JsonIgnoreCondition.WhenWritingNull)]
	public int? Saturation { get; set; }
	}

internal sealed class EnabledParametersDto
	{
	[JsonPropertyName ("enable")]
	[JsonIgnore (Condition = JsonIgnoreCondition.WhenWritingNull)]
	public bool? Enable { get; set; }
	}

internal sealed class ReportIntervalParametersDto
	{
	[JsonPropertyName ("report_interval")]
	[JsonIgnore (Condition = JsonIgnoreCondition.WhenWritingNull)]
	public int? ReportInterval { get; set; }
	}

internal sealed class PageParametersDto
	{
	[JsonPropertyName ("start_index")]
	[JsonIgnore (Condition = JsonIgnoreCondition.WhenWritingNull)]
	public int? StartIndex { get; set; }
	}

internal sealed class TriggerLogParametersDto
	{
	[JsonPropertyName ("start_id")]
	[JsonIgnore (Condition = JsonIgnoreCondition.WhenWritingNull)]
	public int? StartId { get; set; }
	}

internal sealed class ScanParametersDto
	{
	[JsonPropertyName ("scan_list")]
	[JsonIgnore (Condition = JsonIgnoreCondition.WhenWritingNull)]
	public IReadOnlyList<string>? ScanList { get; set; }
	}

internal sealed class ChildListParametersDto
	{
	[JsonPropertyName ("child_device_list")]
	[JsonIgnore (Condition = JsonIgnoreCondition.WhenWritingNull)]
	public IReadOnlyList<ChildSetupParametersDto>? ChildDevices { get; set; }
	}

internal sealed class ChildSetupParametersDto
	{
	[JsonPropertyName ("device_id")]
	[JsonIgnore (Condition = JsonIgnoreCondition.WhenWritingNull)]
	public string? DeviceId { get; set; }
	[JsonPropertyName ("device_model")]
	[JsonIgnore (Condition = JsonIgnoreCondition.WhenWritingNull)]
	public string? Model { get; set; }
	[JsonPropertyName ("category")]
	[JsonIgnore (Condition = JsonIgnoreCondition.WhenWritingNull)]
	public string? Category { get; set; }
	}

internal sealed class MultipleRequestParametersDto
	{
	[JsonPropertyName ("requests")]
	[JsonIgnore (Condition = JsonIgnoreCondition.WhenWritingNull)]
	public IReadOnlyList<WireRequest<object>>? Requests { get; set; }
	}

internal sealed class ChildRequestParametersDto
	{
	[JsonPropertyName ("device_id")]
	[JsonIgnore (Condition = JsonIgnoreCondition.WhenWritingNull)]
	public string? DeviceId { get; set; }
	[JsonPropertyName ("requestData")]
	[JsonIgnore (Condition = JsonIgnoreCondition.WhenWritingNull)]
	public WireRequest<object>? RequestData { get; set; }
	}

internal sealed class TransitionParametersDto
	{
	[JsonPropertyName ("enable")]
	[JsonIgnore (Condition = JsonIgnoreCondition.WhenWritingNull)]
	public bool? Enable { get; set; }
	[JsonPropertyName ("duration")]
	[JsonIgnore (Condition = JsonIgnoreCondition.WhenWritingNull)]
	public int? Duration { get; set; }
	[JsonPropertyName ("on_state")]
	[JsonIgnore (Condition = JsonIgnoreCondition.WhenWritingNull)]
	public TransitionParametersDto? OnState { get; set; }
	[JsonPropertyName ("off_state")]
	[JsonIgnore (Condition = JsonIgnoreCondition.WhenWritingNull)]
	public TransitionParametersDto? OffState { get; set; }
	}

internal sealed class LegacyRequestParametersDto
	{
	[JsonPropertyName ("year")]
	[JsonIgnore (Condition = JsonIgnoreCondition.WhenWritingNull)]
	public int? Year { get; set; }
	[JsonPropertyName ("month")]
	[JsonIgnore (Condition = JsonIgnoreCondition.WhenWritingNull)]
	public int? Month { get; set; }
	[JsonPropertyName ("state")]
	[JsonIgnore (Condition = JsonIgnoreCondition.WhenWritingNull)]
	public int? State { get; set; }
	[JsonPropertyName ("on_off")]
	[JsonIgnore (Condition = JsonIgnoreCondition.WhenWritingNull)]
	public int? OnOff { get; set; }
	[JsonPropertyName ("brightness")]
	[JsonIgnore (Condition = JsonIgnoreCondition.WhenWritingNull)]
	public int? Brightness { get; set; }
	[JsonPropertyName ("color_temp")]
	[JsonIgnore (Condition = JsonIgnoreCondition.WhenWritingNull)]
	public int? ColorTemperature { get; set; }
	[JsonPropertyName ("hue")]
	[JsonIgnore (Condition = JsonIgnoreCondition.WhenWritingNull)]
	public int? Hue { get; set; }
	[JsonPropertyName ("saturation")]
	[JsonIgnore (Condition = JsonIgnoreCondition.WhenWritingNull)]
	public int? Saturation { get; set; }
	[JsonPropertyName ("ignore_default")]
	[JsonIgnore (Condition = JsonIgnoreCondition.WhenWritingNull)]
	public int? IgnoreDefault { get; set; }
	[JsonPropertyName ("transition_period")]
	[JsonIgnore (Condition = JsonIgnoreCondition.WhenWritingNull)]
	public int? TransitionPeriod { get; set; }
	[JsonPropertyName ("enable")]
	[JsonIgnore (Condition = JsonIgnoreCondition.WhenWritingNull)]
	public int? Enable { get; set; }
	[JsonPropertyName ("id")]
	[JsonIgnore (Condition = JsonIgnoreCondition.WhenWritingNull)]
	public string? Id { get; set; }
	}

internal sealed class ChildContextDto
	{
	[JsonPropertyName ("child_ids")]
	[JsonIgnore (Condition = JsonIgnoreCondition.WhenWritingNull)]
	public string[]? ChildIds { get; set; }
	}

internal sealed class LightStripEffectParametersDto
	{
	[JsonPropertyName ("custom")]
	[JsonIgnore (Condition = JsonIgnoreCondition.WhenWritingNull)]
	public int? Custom { get; set; }
	[JsonPropertyName ("id")]
	[JsonIgnore (Condition = JsonIgnoreCondition.WhenWritingNull)]
	public string? Id { get; set; }
	[JsonPropertyName ("brightness")]
	[JsonIgnore (Condition = JsonIgnoreCondition.WhenWritingNull)]
	public int? Brightness { get; set; }
	[JsonPropertyName ("name")]
	[JsonIgnore (Condition = JsonIgnoreCondition.WhenWritingNull)]
	public string? Name { get; set; }
	[JsonPropertyName ("enable")]
	[JsonIgnore (Condition = JsonIgnoreCondition.WhenWritingNull)]
	public int? Enable { get; set; }
	[JsonPropertyName ("segments")]
	[JsonIgnore (Condition = JsonIgnoreCondition.WhenWritingNull)]
	public int[]? Segments { get; set; }
	[JsonPropertyName ("expansion_strategy")]
	[JsonIgnore (Condition = JsonIgnoreCondition.WhenWritingNull)]
	public int? ExpansionStrategy { get; set; }
	[JsonPropertyName ("display_colors")]
	[JsonIgnore (Condition = JsonIgnoreCondition.WhenWritingNull)]
	public int[][]? DisplayColors { get; set; }
	[JsonPropertyName ("type")]
	[JsonIgnore (Condition = JsonIgnoreCondition.WhenWritingNull)]
	public string? Type { get; set; }
	[JsonPropertyName ("duration")]
	[JsonIgnore (Condition = JsonIgnoreCondition.WhenWritingNull)]
	public int? Duration { get; set; }
	[JsonPropertyName ("transition")]
	[JsonIgnore (Condition = JsonIgnoreCondition.WhenWritingNull)]
	public int? Transition { get; set; }
	[JsonPropertyName ("direction")]
	[JsonIgnore (Condition = JsonIgnoreCondition.WhenWritingNull)]
	public int? Direction { get; set; }
	[JsonPropertyName ("spread")]
	[JsonIgnore (Condition = JsonIgnoreCondition.WhenWritingNull)]
	public int? Spread { get; set; }
	[JsonPropertyName ("repeat_times")]
	[JsonIgnore (Condition = JsonIgnoreCondition.WhenWritingNull)]
	public int? RepeatTimes { get; set; }
	[JsonPropertyName ("sequence")]
	[JsonIgnore (Condition = JsonIgnoreCondition.WhenWritingNull)]
	public int[][]? Sequence { get; set; }
	[JsonPropertyName ("hue_range")]
	[JsonIgnore (Condition = JsonIgnoreCondition.WhenWritingNull)]
	public int[]? HueRange { get; set; }
	[JsonPropertyName ("saturation_range")]
	[JsonIgnore (Condition = JsonIgnoreCondition.WhenWritingNull)]
	public int[]? SaturationRange { get; set; }
	[JsonPropertyName ("brightness_range")]
	[JsonIgnore (Condition = JsonIgnoreCondition.WhenWritingNull)]
	public int[]? BrightnessRange { get; set; }
	[JsonPropertyName ("init_states")]
	[JsonIgnore (Condition = JsonIgnoreCondition.WhenWritingNull)]
	public int[][]? InitStates { get; set; }
	[JsonPropertyName ("fadeoff")]
	[JsonIgnore (Condition = JsonIgnoreCondition.WhenWritingNull)]
	public int? Fadeoff { get; set; }
	[JsonPropertyName ("random_seed")]
	[JsonIgnore (Condition = JsonIgnoreCondition.WhenWritingNull)]
	public int? RandomSeed { get; set; }
	[JsonPropertyName ("backgrounds")]
	[JsonIgnore (Condition = JsonIgnoreCondition.WhenWritingNull)]
	public int[][]? Backgrounds { get; set; }
	[JsonPropertyName ("transition_range")]
	[JsonIgnore (Condition = JsonIgnoreCondition.WhenWritingNull)]
	public int[]? TransitionRange { get; set; }
	[JsonPropertyName ("run_time")]
	[JsonIgnore (Condition = JsonIgnoreCondition.WhenWritingNull)]
	public int? RunTime { get; set; }
	[JsonPropertyName ("trans_sequence")]
	[JsonIgnore (Condition = JsonIgnoreCondition.WhenWritingNull)]
	public int[][]? TransSequence { get; set; }
	}

