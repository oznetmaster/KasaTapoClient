// Copyright © 2026 Neil Colvin.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// Behavior modeled after the independent python-kasa project (https://github.com/python-kasa/python-kasa)
// for protocol/compatibility reference only; no python-kasa source was copied. See ATTRIBUTIONS.md.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace KasaTapoClient.Internal;

internal static partial class KasaResponseParser
	{
	private static readonly Dictionary<string, JsonObject> SMART_LIGHT_STRIP_EFFECT_PAYLOADS =
		new Dictionary<string, JsonObject> (StringComparer.OrdinalIgnoreCase)
			{
			["Aurora"] = ParseSmartLightStripEffectPayload ("""
				{"custom":0,"id":"TapoStrip_1MClvV18i15Jq3bvJVf0eP","brightness":100,"name":"Aurora","enable":1,"segments":[0],"expansion_strategy":1,"display_colors":[[120,100,100],[240,100,100],[260,100,100],[280,100,100]],"type":"sequence","duration":0,"transition":1500,"direction":4,"spread":7,"repeat_times":0,"sequence":[[120,100,100],[240,100,100],[260,100,100],[280,100,100]]}
				"""),
			["Bubbling Cauldron"] = ParseSmartLightStripEffectPayload ("""
				{"custom":0,"id":"TapoStrip_6DlumDwO2NdfHppy50vJtu","brightness":100,"name":"Bubbling Cauldron","enable":1,"segments":[0],"expansion_strategy":1,"display_colors":[[100,100,100],[270,100,100]],"type":"random","hue_range":[100,270],"saturation_range":[80,100],"brightness_range":[50,100],"duration":0,"transition":200,"init_states":[[270,100,100]],"fadeoff":1000,"random_seed":24,"backgrounds":[[270,40,50]]}
				"""),
			["Candy Cane"] = ParseSmartLightStripEffectPayload ("""
				{"custom":0,"id":"TapoStrip_6Dy0Nc45vlhFPEzG021Pe9","brightness":100,"name":"Candy Cane","enable":1,"segments":[0],"expansion_strategy":1,"display_colors":[[0,0,100],[0,81,100]],"type":"sequence","duration":700,"transition":500,"direction":1,"spread":1,"repeat_times":0,"sequence":[[0,0,100],[0,0,100],[360,81,100],[0,0,100],[0,0,100],[360,81,100],[360,81,100],[0,0,100],[0,0,100],[360,81,100],[360,81,100],[360,81,100],[360,81,100],[0,0,100],[0,0,100],[360,81,100]]}
				"""),
			["Christmas"] = ParseSmartLightStripEffectPayload ("""
				{"custom":0,"id":"TapoStrip_5zkiG6avJ1IbhjiZbRlWvh","brightness":100,"name":"Christmas","enable":1,"segments":[0],"expansion_strategy":1,"display_colors":[[136,98,100],[350,97,100]],"type":"random","hue_range":[136,146],"saturation_range":[90,100],"brightness_range":[50,100],"duration":5000,"transition":0,"init_states":[[136,0,100]],"fadeoff":2000,"random_seed":100,"backgrounds":[[136,98,75],[136,0,0],[350,0,100],[350,97,94]]}
				"""),
			["Flicker"] = ParseSmartLightStripEffectPayload ("""
				{"custom":0,"id":"TapoStrip_4HVKmMc6vEzjm36jXaGwMs","brightness":100,"name":"Flicker","enable":1,"segments":[1],"expansion_strategy":1,"display_colors":[[30,81,100],[40,100,100]],"type":"random","hue_range":[30,40],"saturation_range":[100,100],"brightness_range":[50,100],"duration":0,"transition":0,"transition_range":[375,500],"init_states":[[30,81,80]]}
				"""),
			["Grandma's Christmas Lights"] = ParseSmartLightStripEffectPayload ("""
				{"custom":0,"id":"TapoStrip_3Gk6CmXOXbjCiwz9iD543C","brightness":100,"name":"Grandma's Christmas Lights","enable":1,"segments":[0],"expansion_strategy":1,"display_colors":[[30,100,100],[240,100,100],[130,100,100],[0,100,100]],"type":"sequence","duration":5000,"transition":100,"direction":1,"spread":1,"repeat_times":0,"sequence":[[30,100,100],[30,0,0],[30,0,0],[240,100,100],[240,0,0],[240,0,0],[240,0,100],[240,0,0],[240,0,0],[130,100,100],[130,0,0],[130,0,0],[0,100,100],[0,0,0],[0,0,0]]}
				"""),
			["Hanukkah"] = ParseSmartLightStripEffectPayload ("""
				{"custom":0,"id":"TapoStrip_2YTk4wramLKv5XZ9KFDVYm","brightness":100,"name":"Hanukkah","enable":1,"segments":[1],"expansion_strategy":1,"display_colors":[[200,100,100]],"type":"random","hue_range":[200,210],"saturation_range":[0,100],"brightness_range":[50,100],"duration":1500,"transition":0,"transition_range":[400,500],"init_states":[[35,81,80]]}
				"""),
			["Haunted Mansion"] = ParseSmartLightStripEffectPayload ("""
				{"custom":0,"id":"TapoStrip_4rJ6JwC7I9st3tQ8j4lwlI","brightness":100,"name":"Haunted Mansion","enable":1,"segments":[80],"expansion_strategy":2,"display_colors":[[44,9,100]],"type":"random","hue_range":[45,45],"saturation_range":[10,10],"brightness_range":[0,80],"duration":0,"transition":0,"transition_range":[50,1500],"init_states":[[45,10,100]],"fadeoff":200,"random_seed":1,"backgrounds":[[45,10,100]]}
				"""),
			["Icicle"] = ParseSmartLightStripEffectPayload ("""
				{"custom":0,"id":"TapoStrip_7UcYLeJbiaxVIXCxr21tpx","brightness":100,"name":"Icicle","enable":1,"segments":[0],"expansion_strategy":1,"display_colors":[[190,100,100]],"type":"sequence","duration":0,"transition":400,"direction":4,"spread":3,"repeat_times":0,"sequence":[[190,100,70],[190,100,70],[190,30,50],[190,100,70],[190,100,70]]}
				"""),
			["Lightning"] = ParseSmartLightStripEffectPayload ("""
				{"custom":0,"id":"TapoStrip_7OGzfSfnOdhoO2ri4gOHWn","brightness":100,"name":"Lightning","enable":1,"segments":[7],"expansion_strategy":1,"display_colors":[[210,9,100],[200,50,100],[200,100,100]],"type":"random","hue_range":[240,240],"saturation_range":[10,11],"brightness_range":[90,100],"duration":0,"transition":50,"init_states":[[240,30,100]],"fadeoff":150,"random_seed":50,"backgrounds":[[200,100,100],[200,50,10],[210,10,50],[240,10,0]]}
				"""),
			["Ocean"] = ParseSmartLightStripEffectPayload ("""
				{"custom":0,"id":"TapoStrip_0fOleCdwSgR0nfjkReeYfw","brightness":100,"name":"Ocean","enable":1,"segments":[0],"expansion_strategy":1,"display_colors":[[198,84,100]],"type":"sequence","duration":0,"transition":2000,"direction":3,"spread":16,"repeat_times":0,"sequence":[[198,84,30],[198,70,30],[198,10,30]]}
				"""),
			["Rainbow"] = ParseSmartLightStripEffectPayload ("""
				{"custom":0,"id":"TapoStrip_7CC5y4lsL8pETYvmz7UOpQ","brightness":100,"name":"Rainbow","enable":1,"segments":[0],"expansion_strategy":1,"display_colors":[[0,100,100],[100,100,100],[200,100,100],[300,100,100]],"type":"sequence","duration":0,"transition":1500,"direction":1,"spread":12,"repeat_times":0,"sequence":[[0,100,100],[100,100,100],[200,100,100],[300,100,100]]}
				"""),
			["Raindrop"] = ParseSmartLightStripEffectPayload ("""
				{"custom":0,"id":"TapoStrip_1t2nWlTBkV8KXBZ0TWvBjs","brightness":100,"name":"Raindrop","enable":1,"segments":[0],"expansion_strategy":1,"display_colors":[[200,9,100],[200,19,100]],"type":"random","hue_range":[200,200],"saturation_range":[10,20],"brightness_range":[10,30],"duration":0,"transition":1000,"init_states":[[200,40,100]],"fadeoff":1000,"random_seed":24,"backgrounds":[[200,40,0]]}
				"""),
			["Spring"] = ParseSmartLightStripEffectPayload ("""
				{"custom":0,"id":"TapoStrip_1nL6GqZ5soOxj71YDJOlZL","brightness":100,"name":"Spring","enable":1,"segments":[0],"expansion_strategy":1,"display_colors":[[0,30,100],[130,100,100]],"type":"random","hue_range":[0,90],"saturation_range":[30,100],"brightness_range":[90,100],"duration":600,"transition":0,"transition_range":[2000,6000],"init_states":[[80,30,100]],"fadeoff":1000,"random_seed":20,"backgrounds":[[130,100,40]]}
				"""),
			["Sunrise"] = ParseSmartLightStripEffectPayload ("""
				{"custom":0,"id":"TapoStrip_1OVSyXIsDxrt4j7OxyRvqi","brightness":100,"name":"Sunrise","enable":1,"segments":[0],"expansion_strategy":2,"display_colors":[[0,0,100],[30,95,100],[0,100,100]],"type":"pulse","duration":600,"transition":60000,"direction":1,"spread":1,"repeat_times":1,"run_time":0,"sequence":[[0,100,5],[0,100,5],[10,100,6],[15,100,7],[20,100,8],[20,100,10],[30,100,12],[30,95,15],[30,90,20],[30,80,25],[30,75,30],[30,70,40],[30,60,50],[30,50,60],[30,20,70],[30,0,100]],"trans_sequence":[]}
				"""),
			["Sunset"] = ParseSmartLightStripEffectPayload ("""
				{"custom":0,"id":"TapoStrip_5NiN0Y8GAUD78p4neKk9EL","brightness":100,"name":"Sunset","enable":1,"segments":[0],"expansion_strategy":2,"display_colors":[[0,100,100],[30,95,100],[0,0,100]],"type":"pulse","duration":600,"transition":60000,"direction":1,"spread":1,"repeat_times":1,"run_time":0,"sequence":[[30,0,100],[30,20,100],[30,50,99],[30,60,98],[30,70,97],[30,75,95],[30,80,93],[30,90,90],[30,95,85],[30,100,80],[20,100,70],[20,100,60],[15,100,50],[10,100,40],[0,100,30],[0,100,0]],"trans_sequence":[]}
				"""),
			["Valentines"] = ParseSmartLightStripEffectPayload ("""
				{"custom":0,"id":"TapoStrip_2q1Vio9sSjHmaC7JS9d30l","brightness":100,"name":"Valentines","enable":1,"segments":[0],"expansion_strategy":1,"display_colors":[[339,19,100],[19,50,100],[0,100,100],[339,40,100]],"type":"random","hue_range":[340,340],"saturation_range":[30,40],"brightness_range":[90,100],"duration":600,"transition":2000,"init_states":[[340,30,100]],"fadeoff":3000,"random_seed":100,"backgrounds":[[340,20,50],[20,50,50],[0,100,50]]}
				""")
			};

	private static readonly IReadOnlyList<LightEffectDefinition> SMART_LIGHT_STRIP_EFFECTS =
		[
		new LightEffectDefinition ("Aurora", "Aurora"),
		new LightEffectDefinition ("Bubbling Cauldron", "Bubbling Cauldron"),
		new LightEffectDefinition ("Candy Cane", "Candy Cane"),
		new LightEffectDefinition ("Christmas", "Christmas"),
		new LightEffectDefinition ("Flicker", "Flicker"),
		new LightEffectDefinition ("Grandma's Christmas Lights", "Grandma's Christmas Lights"),
		new LightEffectDefinition ("Hanukkah", "Hanukkah"),
		new LightEffectDefinition ("Haunted Mansion", "Haunted Mansion"),
		new LightEffectDefinition ("Icicle", "Icicle"),
		new LightEffectDefinition ("Lightning", "Lightning"),
		new LightEffectDefinition ("Ocean", "Ocean"),
		new LightEffectDefinition ("Rainbow", "Rainbow"),
		new LightEffectDefinition ("Raindrop", "Raindrop"),
		new LightEffectDefinition ("Spring", "Spring"),
		new LightEffectDefinition ("Sunrise", "Sunrise"),
		new LightEffectDefinition ("Sunset", "Sunset"),
		new LightEffectDefinition ("Valentines", "Valentines"),
		];
	private const int SMART_LIGHT_TRANSITION_DEFAULT_MAXIMUM_SECONDS = 60;

	internal static JsonObject CreateSmartLightStripEffectPayload (string? effect)
		{
		if (string.IsNullOrWhiteSpace (effect))
			{
			return new JsonObject
				{
				["enable"] = 0,
				};
			}

		string effectName = effect!;
		if (!SMART_LIGHT_STRIP_EFFECT_PAYLOADS.TryGetValue (effectName, out JsonObject? payload))
			{
			throw new ArgumentException ($"Unknown smart light-strip effect '{effectName}'.", nameof (effect));
			}

		return (JsonObject?)payload.DeepClone ()
			?? throw new InvalidOperationException ($"The smart light-strip effect '{effectName}' could not be cloned.");
		}

	private static JsonObject ParseSmartLightStripEffectPayload (string json) =>
		JsonNode.Parse (json)?.AsObject ()
		?? throw new InvalidOperationException ("The built-in smart light-strip effect payload could not be parsed.");

	internal sealed class ParsedResponse
		{
		internal ParsedResponse (
			string rawJson,
			LegacySystemInfoDto systemInfo,
			LegacyEmeterModuleDto? emeter,
			LegacyEmeterRealtimeDto? emeterInfo,
			LegacyRuleListDto? countdownRules,
			LegacyRuleListDto? scheduleRules,
			LegacyRuleListDto? antitheftRules,
			LegacyTimeModuleDto? time,
			LegacyCloudModuleDto? cloud,
			LegacyHomeKitModuleDto? homeKit)
			{
			RawJson = rawJson;
			SystemInfo = systemInfo;
			Emeter = emeter;
			EmeterInfo = emeterInfo;
			CountdownRules = countdownRules;
			ScheduleRules = scheduleRules;
			AntitheftRules = antitheftRules;
			Time = time;
			Cloud = cloud;
			HomeKit = homeKit;
			}

		internal string RawJson
			{
			get;
			}

		internal LegacySystemInfoDto SystemInfo
			{
			get;
			}

		internal LegacyEmeterRealtimeDto? EmeterInfo
			{
			get;
			}

		internal LegacyEmeterModuleDto? Emeter
			{
			get;
			}

		internal LegacyRuleListDto? CountdownRules
			{
			get;
			}

		internal LegacyRuleListDto? ScheduleRules
			{
			get;
			}

		internal LegacyRuleListDto? AntitheftRules
			{
			get;
			}

		internal LegacyTimeModuleDto? Time
			{
			get;
			}

		internal LegacyCloudModuleDto? Cloud
			{
			get;
			}

		internal LegacyHomeKitModuleDto? HomeKit
			{
			get;
			}
		}

	internal sealed class ParsedDeviceState
		{
		public ParsedDeviceState (
			DeviceSystemInfo systemInfo,
			EnergyUsage? energyUsage,
			LightState? lightState,
			LightPresetState? lightPresetState,
			LightTransitionState? lightTransitionState,
			LightStripEffectState? lightStripEffectState,
			AlarmState? alarmState,
			OverheatProtectionState? overheatProtectionState,
			PowerProtectionState? powerProtectionState,
			FanState? fanState,
			SpeakerState? speakerState,
			RuleModuleState? ruleState,
			FirmwareState? firmwareState,
			CloudConnectionState? cloudState,
			DeviceTimeState? timeState,
			MatterSetupInfo? matterSetup,
			HomeKitSetupInfo? homeKitSetup,
			AutoOffState? autoOffState,
			LedState? ledState,
			ChildLockState? childLockState,
			int? rssi)
			{
			SystemInfo = systemInfo;
			EnergyUsage = energyUsage;
			LightState = lightState;
			LightPresetState = lightPresetState;
			LightTransitionState = lightTransitionState;
			LightStripEffectState = lightStripEffectState;
			AlarmState = alarmState;
			OverheatProtectionState = overheatProtectionState;
			PowerProtectionState = powerProtectionState;
			FanState = fanState;
			SpeakerState = speakerState;
			RuleState = ruleState;
			FirmwareState = firmwareState;
			CloudState = cloudState;
			TimeState = timeState;
			MatterSetup = matterSetup;
			HomeKitSetup = homeKitSetup;
			AutoOffState = autoOffState;
			LedState = ledState;
			ChildLockState = childLockState;
			Rssi = rssi;
			}

		public DeviceSystemInfo SystemInfo
			{
			get;
			}

		public EnergyUsage? EnergyUsage
			{
			get;
			}

		public LightState? LightState
			{
			get;
			}

		public LightPresetState? LightPresetState
			{
			get;
			}

		public LightTransitionState? LightTransitionState
			{
			get;
			}

		public LightStripEffectState? LightStripEffectState
			{
			get;
			}

		public AlarmState? AlarmState
			{
			get;
			}

		public OverheatProtectionState? OverheatProtectionState
			{
			get;
			}

		public PowerProtectionState? PowerProtectionState
			{
			get;
			}

		public FanState? FanState
			{
			get;
			}

		public SpeakerState? SpeakerState
			{
			get;
			}

		public RuleModuleState? RuleState
			{
			get;
			}

		public FirmwareState? FirmwareState
			{
			get;
			}

		public CloudConnectionState? CloudState
			{
			get;
			}

		public DeviceTimeState? TimeState
			{
			get;
			}

		public MatterSetupInfo? MatterSetup
			{
			get;
			}

		public HomeKitSetupInfo? HomeKitSetup
			{
			get;
			}

		public AutoOffState? AutoOffState
			{
			get;
			}

		public LedState? LedState
			{
			get;
			}

		public ChildLockState? ChildLockState
			{
			get;
			}

		public int? Rssi
			{
			get;
			}
		}

	internal sealed class SmartParsedResponse
		{
		internal SmartParsedResponse (
			string rawJson,
			SmartDeviceInfoDto deviceInfo,
			IReadOnlyList<string> componentIds,
			IReadOnlyDictionary<string, int> componentVersions,
			SmartChildDeviceListDto? childDeviceList,
			IReadOnlyDictionary<string, IReadOnlyList<string>> childComponentIds,
			IReadOnlyDictionary<string, SmartChildDeviceDto> childOverrides,
			IReadOnlyDictionary<string, JsonObject> moduleResults)
			{
			RawJson = rawJson;
			DeviceInfo = deviceInfo;
			ComponentIds = componentIds;
			ComponentVersions = componentVersions;
			ChildDeviceList = childDeviceList;
			ChildComponentIds = childComponentIds;
			ChildOverrides = childOverrides;
			ModuleResults = moduleResults;
			}

		internal string RawJson { get; }
		internal SmartDeviceInfoDto DeviceInfo { get; }
		internal IReadOnlyList<string> ComponentIds { get; }
		internal IReadOnlyDictionary<string, int> ComponentVersions { get; }
		internal SmartChildDeviceListDto? ChildDeviceList { get; }
		internal IReadOnlyDictionary<string, IReadOnlyList<string>> ChildComponentIds { get; }
		internal IReadOnlyDictionary<string, SmartChildDeviceDto> ChildOverrides { get; }
		internal IReadOnlyDictionary<string, JsonObject> ModuleResults { get; }
		}

	internal sealed class SmartEnvelopeDto
		{
		[JsonPropertyName ("result")]
		public SmartEnvelopeResultDto? Result { get; set; }
		}

	internal sealed class SmartEnvelopeResultDto
		{
		[JsonPropertyName ("responses")]
		public List<SmartMethodResponseDto>? Responses { get; set; }

		[JsonPropertyName ("component_list")]
		public List<SmartComponentDto>? ComponentList { get; set; }

		[JsonPropertyName ("child_device_list")]
		public List<SmartChildDeviceDto>? ChildDeviceList { get; set; }

		[JsonPropertyName ("child_component_list")]
		public List<SmartChildComponentDto>? ChildComponentList { get; set; }

		[JsonPropertyName ("model")]
		public string? Model { get; set; }

		[JsonPropertyName ("type")]
		public string? Type { get; set; }

		[JsonPropertyName ("device_id")]
		public string? DeviceId { get; set; }

		[JsonPropertyName ("nickname")]
		public string? Nickname { get; set; }

		[JsonPropertyName ("avatar")]
		public string? Avatar { get; set; }

		[JsonPropertyName ("device_on")]
		public bool? DeviceOn { get; set; }

		[JsonPropertyName ("fw_ver")]
		public string? FirmwareVersion { get; set; }

		[JsonPropertyName ("hw_ver")]
		public string? HardwareVersion { get; set; }

		[JsonPropertyName ("mac")]
		public string? Mac { get; set; }

		[JsonPropertyName ("rssi")]
		public int? Rssi { get; set; }

		[JsonPropertyName ("signal_level")]
		public int? SignalLevel { get; set; }

		[JsonPropertyName ("ssid")]
		public string? Ssid { get; set; }

		[JsonPropertyName ("on_time")]
		public int? OnTimeSeconds { get; set; }

		[JsonPropertyName ("specs")]
		public string? Specs { get; set; }

		[JsonPropertyName ("device_category_list")]
		public List<SmartChildSetupCategoryDto>? DeviceCategoryList { get; set; }

		[JsonPropertyName ("brightness")]
		public int? Brightness { get; set; }

		[JsonPropertyName ("hue")]
		public int? Hue { get; set; }

		[JsonPropertyName ("saturation")]
		public int? Saturation { get; set; }

		[JsonPropertyName ("color_temp")]
		public int? ColorTemperature { get; set; }

		[JsonPropertyName ("lighting_effect")]
		public LegacyLightingEffectDto? LightingEffect { get; set; }
		}

	internal sealed class SmartMethodResponseDto
		{
		[JsonPropertyName ("method")]
		public string? Method { get; set; }

		[JsonPropertyName ("result")]
		public SmartEnvelopeResultDto? Result { get; set; }
		}

	internal sealed class SmartComponentDto
		{
		[JsonPropertyName ("id")]
		public string? Id { get; set; }

		[JsonPropertyName ("ver_code")]
		public int? VersionCode { get; set; }
		}

	internal sealed class SmartDeviceInfoDto
		{
		internal SmartDeviceInfoDto (string? model, string? type, string? deviceId, string? nickname, bool? deviceOn, string? firmwareVersion, string? hardwareVersion, string? mac, int? signalLevel, int? rssi, string? ssid, string? specs, List<SmartChildSetupCategoryDto>? deviceCategoryList, int? brightness, int? hue, int? saturation, int? colorTemperature, LegacyLightingEffectDto? lightingEffect)
			{
			Model = model;
			Type = type;
			DeviceId = deviceId;
			Nickname = nickname;
			DeviceOn = deviceOn;
			FirmwareVersion = firmwareVersion;
			HardwareVersion = hardwareVersion;
			Mac = mac;
			SignalLevel = signalLevel;
			Rssi = rssi;
			Ssid = ssid;
			Specs = specs;
			DeviceCategoryList = deviceCategoryList;
			Brightness = brightness;
			Hue = hue;
			Saturation = saturation;
			ColorTemperature = colorTemperature;
			LightingEffect = lightingEffect;
			}

		internal string? Model { get; }
		internal string? Type { get; }
		internal string? DeviceId { get; }
		internal string? Nickname { get; }
		internal bool? DeviceOn { get; }
		internal string? FirmwareVersion { get; }
		internal string? HardwareVersion { get; }
		internal string? Mac { get; }
		internal int? SignalLevel { get; }
		internal int? Rssi { get; }
		internal string? Ssid { get; }
		internal int? OnTimeSeconds { get; }
		internal string? Specs { get; }
		internal List<SmartChildSetupCategoryDto>? DeviceCategoryList { get; }
		internal int? Brightness { get; }
		internal int? Hue { get; }
		internal int? Saturation { get; }
		internal int? ColorTemperature { get; }
		internal LegacyLightingEffectDto? LightingEffect { get; }
		}

	internal sealed class SmartChildDeviceListDto
		{
		internal SmartChildDeviceListDto (IReadOnlyList<SmartChildDeviceDto> childDevices) => ChildDevices = childDevices;
		internal IReadOnlyList<SmartChildDeviceDto> ChildDevices { get; }
		}

	internal sealed class SmartChildDeviceDto
		{
		[JsonPropertyName ("device_id")]
		public string? DeviceId { get; set; }

		[JsonPropertyName ("nickname")]
		public string? Nickname { get; set; }

		[JsonPropertyName ("model")]
		public string? Model { get; set; }

		[JsonPropertyName ("category")]
		public string? Category { get; set; }

		[JsonPropertyName ("type")]
		public string? Type { get; set; }

		[JsonPropertyName ("fw_ver")]
		public string? FirmwareVersion { get; set; }

		[JsonPropertyName ("signal_level")]
		public int? SignalLevel { get; set; }

		[JsonPropertyName ("rssi")]
		public int? Rssi { get; set; }

		[JsonPropertyName ("status")]
		public string? Status { get; set; }

		[JsonPropertyName ("battery_percentage")]
		public int? BatteryPercentage { get; set; }

		[JsonPropertyName ("at_low_battery")]
		public bool? AtLowBattery { get; set; }

		[JsonPropertyName ("is_low")]
		public bool? IsLowBattery { get; set; }

		[JsonPropertyName ("current_temp")]
		public double? CurrentTemperature { get; set; }

		[JsonPropertyName ("current_temp_exception")]
		[JsonConverter (typeof (NullableFlexibleInt32Converter))]
		public int? CurrentTemperatureException { get; set; }

		[JsonPropertyName ("current_humidity")]
		public int? CurrentHumidity { get; set; }

		[JsonPropertyName ("current_humidity_exception")]
		[JsonConverter (typeof (NullableFlexibleInt32Converter))]
		public int? CurrentHumidityException { get; set; }

		[JsonPropertyName ("temp_unit")]
		public string? TemperatureUnit { get; set; }

		[JsonPropertyName ("report_interval")]
		public int? ReportInterval { get; set; }

		[JsonPropertyName ("detected")]
		public bool? Detected { get; set; }

		[JsonPropertyName ("open")]
		public bool? Open { get; set; }

		[JsonPropertyName ("in_alarm")]
		public bool? InAlarm { get; set; }

		[JsonPropertyName ("water_leak_status")]
		public string? WaterLeakStatus { get; set; }

		[JsonPropertyName ("trigger_timestamp")]
		public long? TriggerTimestamp { get; set; }

		[JsonPropertyName ("double_click_info")]
		public SmartDoubleClickInfoDto? DoubleClickInfo { get; set; }

		[JsonPropertyName ("trigger_logs")]
		public SmartTriggerLogListDto? TriggerLogs { get; set; }

		[JsonPropertyName ("comfort_temp_config")]
		public SmartComfortValueConfigDto? ComfortTemperatureConfig { get; set; }

		[JsonPropertyName ("comfort_humidity_config")]
		public SmartComfortValueConfigDto? ComfortHumidityConfig { get; set; }

		[JsonPropertyName ("frost_protection")]
		public SmartFrostProtectionDto? FrostProtection { get; set; }

		[JsonPropertyName ("frost_protection_on")]
		public bool? FrostProtectionOn { get; set; }

		[JsonPropertyName ("target_temp")]
		public double? TargetTemperature { get; set; }

		[JsonPropertyName ("min_control_temp")]
		public int? MinimumControlTemperature { get; set; }

		[JsonPropertyName ("max_control_temp")]
		public int? MaximumControlTemperature { get; set; }

		[JsonPropertyName ("temp_offset")]
		public int? TemperatureOffset { get; set; }

		[JsonPropertyName ("child_protection")]
		public bool? ChildProtection { get; set; }

		[JsonPropertyName ("trv_states")]
		public List<string>? TrvStates { get; set; }

		[JsonPropertyName ("device_on")]
		public bool? DeviceOn { get; set; }
		}

	internal sealed class SmartChildComponentDto
		{
		[JsonPropertyName ("device_id")]
		public string? DeviceId { get; set; }

		[JsonPropertyName ("component_list")]
		public List<SmartComponentDto>? ComponentList { get; set; }
		}

	internal sealed class SmartChildSetupCategoryDto
		{
		[JsonPropertyName ("category")]
		public string? Category { get; set; }
		}

	internal sealed class SmartScannedChildDeviceListDto
		{
		[JsonPropertyName ("child_device_list")]
		public List<SmartScannedChildDeviceDto>? ChildDeviceList { get; set; }
		}

	internal sealed class SmartScannedChildDeviceDto
		{
		[JsonPropertyName ("device_id")]
		public string? DeviceId { get; set; }

		[JsonPropertyName ("device_model")]
		public string? DeviceModel { get; set; }

		[JsonPropertyName ("category")]
		public string? Category { get; set; }
		}

	internal sealed class SmartDoubleClickInfoDto
		{
		[JsonPropertyName ("enable")]
		public bool? Enable { get; set; }
		}

	internal sealed class SmartTriggerLogListDto
		{
		[JsonPropertyName ("logs")]
		public List<SmartTriggerLogDto>? Logs { get; set; }
		}

	internal sealed class SmartTriggerLogDto
		{
		[JsonPropertyName ("id")]
		public int? Id { get; set; }

		[JsonPropertyName ("event")]
		public string? Event { get; set; }

		[JsonPropertyName ("eventId")]
		public string? EventId { get; set; }

		[JsonPropertyName ("timestamp")]
		public long? Timestamp { get; set; }
		}

	internal sealed class SmartComfortValueConfigDto
		{
		[JsonPropertyName ("min_value")]
		public double? MinValue { get; set; }

		[JsonPropertyName ("max_value")]
		public double? MaxValue { get; set; }
		}

	internal sealed class SmartFrostProtectionDto
		{
		[JsonPropertyName ("min_temp")]
		public int? MinimumTemperature { get; set; }

		[JsonPropertyName ("temp_unit")]
		public string? TemperatureUnit { get; set; }
		}

	internal sealed class SmartCloudConnectStateDto
		{
		[JsonPropertyName ("status")]
		public int? Status { get; set; }
		}

	internal sealed class SmartAutoUpdateInfoDto
		{
		[JsonPropertyName ("enable")]
		public bool? Enable { get; set; }
		}

	internal sealed class SmartLatestFirmwareDto
		{
		[JsonPropertyName ("type")]
		public int? Type { get; set; }

		[JsonPropertyName ("fw_ver")]
		public string? FirmwareVersion { get; set; }
		}

	internal sealed class SmartAutoOffConfigDto
		{
		[JsonPropertyName ("enable")]
		public bool? Enable { get; set; }

		[JsonPropertyName ("delay_min")]
		public int? DelayMinutes { get; set; }
		}

	internal sealed class SmartEnergyUsageDto
		{
		[JsonPropertyName ("current_power")]
		public double? CurrentPower { get; set; }

		[JsonPropertyName ("today_energy")]
		public double? TodayEnergyWattHours { get; set; }

		[JsonPropertyName ("month_energy")]
		public double? MonthEnergyWattHours { get; set; }
		}

	internal sealed class SmartCurrentPowerDto
		{
		[JsonPropertyName ("current_power")]
		public double? CurrentPowerWatts { get; set; }
		}

	internal sealed class SmartEmeterDataDto
		{
		[JsonPropertyName ("power_mw")]
		public double? PowerMilliwatts { get; set; }

		[JsonPropertyName ("voltage_mv")]
		public double? VoltageMillivolts { get; set; }

		[JsonPropertyName ("current_ma")]
		public double? CurrentMilliamps { get; set; }
		}

	internal sealed class SmartLedInfoDto
		{
		[JsonPropertyName ("led_rule")]
		public string? LedRule { get; set; }

		[JsonPropertyName ("start_time")]
		public int? StartTime { get; set; }

		[JsonPropertyName ("end_time")]
		public int? EndTime { get; set; }

		[JsonPropertyName ("night_mode_type")]
		public string? NightModeType { get; set; }

		[JsonPropertyName ("sunrise_offset")]
		public int? SunriseOffset { get; set; }

		[JsonPropertyName ("sunset_offset")]
		public int? SunsetOffset { get; set; }
		}

	internal sealed class SmartDeviceTimeDto
		{
		[JsonPropertyName ("timestamp")]
		public long? Timestamp { get; set; }

		[JsonPropertyName ("time_diff")]
		public int? TimeDifferenceMinutes { get; set; }

		[JsonPropertyName ("region")]
		public string? Region { get; set; }
		}

	internal sealed class SmartMatterSetupDto
		{
		[JsonPropertyName ("setup_code")]
		public string? SetupCode { get; set; }

		[JsonPropertyName ("setup_payload")]
		public string? SetupPayload { get; set; }
		}

	internal sealed class SmartHomeKitInfoDto
		{
		[JsonPropertyName ("mfi_setup_code")]
		public string? SetupCode { get; set; }
		}

	internal sealed class SmartChildLockInfoDto
		{
		[JsonPropertyName ("child_lock_status")]
		public bool? ChildLockStatus { get; set; }
		}

	internal sealed class SmartPresetRulesDto
		{
		[JsonPropertyName ("states")]
		public List<LegacyLightPresetDto>? States { get; set; }

		[JsonPropertyName ("brightness")]
		public List<int>? BrightnessLevels { get; set; }
		}

	internal sealed class SmartOnOffGraduallyInfoDto
		{
		[JsonPropertyName ("enable")]
		public bool? Enable { get; set; }

		[JsonPropertyName ("on_state")]
		public SmartOnOffGraduallyStateDto? OnState { get; set; }

		[JsonPropertyName ("off_state")]
		public SmartOnOffGraduallyStateDto? OffState { get; set; }
		}

	internal sealed class SmartOnOffGraduallyStateDto
		{
		[JsonPropertyName ("duration")]
		public int? Duration { get; set; }

		[JsonPropertyName ("enable")]
		public bool? Enable { get; set; }

		[JsonPropertyName ("max_duration")]
		public int? MaximumDuration { get; set; }
		}

	internal sealed class SmartDynamicLightEffectRulesDto
		{
		[JsonPropertyName ("rule_list")]
		public List<SmartDynamicLightEffectRuleDto>? RuleList { get; set; }

		[JsonPropertyName ("enable")]
		public bool? Enable { get; set; }

		[JsonPropertyName ("current_rule_id")]
		public string? CurrentRuleId { get; set; }
		}

	internal sealed class SmartDynamicLightEffectRuleDto
		{
		[JsonPropertyName ("id")]
		public string? Id { get; set; }

		[JsonPropertyName ("scene_name")]
		public string? SceneName { get; set; }

		[JsonPropertyName ("color_status_list")]
		public List<List<int>>? ColorStatusList { get; set; }
		}

	internal sealed class LegacyResponseDto
		{
		[JsonPropertyName ("system")]
		public LegacySystemModuleDto? System
			{
			get; set;
			}

		[JsonPropertyName ("emeter")]
		public LegacyEmeterModuleDto? Emeter
			{
			get; set;
			}

		[JsonPropertyName ("smartlife.iot.common.emeter")]
		public LegacyEmeterModuleDto? SmartEmeter
			{
			get; set;
			}

		[JsonPropertyName ("count_down")]
		public LegacyRuleModuleDto? CountDown
			{
			get; set;
			}

		[JsonPropertyName ("countdown")]
		public LegacyRuleModuleDto? BulbCountDown
			{
			get; set;
			}

		[JsonPropertyName ("schedule")]
		public LegacyRuleModuleDto? Schedule
			{
			get; set;
			}

		[JsonPropertyName ("smartlife.iot.common.schedule")]
		public LegacyRuleModuleDto? SmartSchedule
			{
			get; set;
			}

		[JsonPropertyName ("anti_theft")]
		public LegacyRuleModuleDto? AntiTheft
			{
			get; set;
			}

		[JsonPropertyName ("smartlife.iot.common.anti_theft")]
		public LegacyRuleModuleDto? SmartAntiTheft
			{
			get; set;
			}

		[JsonPropertyName ("time")]
		public LegacyTimeModuleDto? Time
			{
			get; set;
			}

		[JsonPropertyName ("smartlife.iot.common.timesetting")]
		public LegacyTimeModuleDto? SmartTime
			{
			get; set;
			}

		[JsonPropertyName ("cnCloud")]
		public LegacyCloudModuleDto? Cloud
			{
			get; set;
			}

		[JsonPropertyName ("smartlife.iot.common.cloud")]
		public LegacyCloudModuleDto? SmartCloud
			{
			get; set;
			}

		[JsonPropertyName ("smartlife.iot.homekit")]
		public LegacyHomeKitModuleDto? HomeKit
			{
			get; set;
			}
		}

	internal sealed class LegacyTimeModuleDto
		{
		[JsonPropertyName ("get_time")]
		public LegacyTimeInfoDto? GetTime { get; set; }

		[JsonPropertyName ("get_timezone")]
		public LegacyTimezoneInfoDto? GetTimezone { get; set; }
		}

	internal sealed class LegacyTimeInfoDto
		{
		[JsonPropertyName ("year")]
		public int? Year { get; set; }

		[JsonPropertyName ("month")]
		public int? Month { get; set; }

		[JsonPropertyName ("mday")]
		public int? Day { get; set; }

		[JsonPropertyName ("hour")]
		public int? Hour { get; set; }

		[JsonPropertyName ("min")]
		public int? Minute { get; set; }

		[JsonPropertyName ("sec")]
		public int? Second { get; set; }
		}

	internal sealed class LegacyTimezoneInfoDto
		{
		[JsonPropertyName ("index")]
		public int? Index { get; set; }
		}

	internal sealed class LegacyCloudModuleDto
		{
		[JsonPropertyName ("get_info")]
		public LegacyCloudInfoDto? GetInfo { get; set; }
		}

	internal sealed class LegacyCloudInfoDto
		{
		[JsonPropertyName ("binded")]
		public int? Binded { get; set; }

		[JsonPropertyName ("cld_connection")]
		public int? CloudConnection { get; set; }

		[JsonPropertyName ("server")]
		public string? Server { get; set; }

		[JsonPropertyName ("username")]
		public string? UserName { get; set; }
		}

	internal sealed class LegacyHomeKitModuleDto
		{
		[JsonPropertyName ("setup_info_get")]
		public LegacyHomeKitInfoDto? SetupInfoGet { get; set; }
		}

	internal sealed class LegacyHomeKitInfoDto
		{
		[JsonPropertyName ("setup_code")]
		public string? SetupCode { get; set; }

		[JsonPropertyName ("setup_payload")]
		public string? SetupPayload { get; set; }
		}

	internal sealed class LegacyRuleModuleDto
		{
		[JsonPropertyName ("get_rules")]
		public LegacyRuleListDto? GetRules
			{
			get; set;
			}
		}

	internal sealed class LegacyRuleListDto
		{
		[JsonPropertyName ("enable")]
		public int? Enable
			{
			get; set;
			}

		[JsonPropertyName ("rule_list")]
		public List<LegacyRuleDto>? RuleList
			{
			get; set;
			}
		}

	internal sealed class LegacyRuleDto
		{
		[JsonPropertyName ("id")]
		public string? Id
			{
			get; set;
			}

		[JsonPropertyName ("name")]
		public string? Name
			{
			get; set;
			}

		[JsonPropertyName ("enable")]
		public int? Enable
			{
			get; set;
			}

		[JsonPropertyName ("wday")]
		public List<int>? WeekDays
			{
			get; set;
			}

		[JsonPropertyName ("smin")]
		public int? StartMinute
			{
			get; set;
			}

		[JsonPropertyName ("emin")]
		public int? EndMinute
			{
			get; set;
			}

		[JsonPropertyName ("delay")]
		public int? DelaySeconds
			{
			get; set;
			}

		[JsonPropertyName ("act")]
		public int? Action
			{
			get; set;
			}

		[JsonPropertyName ("remain")]
		public int? RemainingSeconds
			{
			get; set;
			}

		[JsonPropertyName ("latitude")]
		public int? Latitude
			{
			get; set;
			}

		[JsonPropertyName ("longitude")]
		public int? Longitude
			{
			get; set;
			}
		}

	internal sealed class LegacySystemModuleDto
		{
		[JsonPropertyName ("get_sysinfo")]
		public LegacySystemInfoDto? GetSystemInfo
			{
			get; set;
			}
		}

	internal sealed class LegacyEmeterModuleDto
		{
		[JsonPropertyName ("get_realtime")]
		public LegacyEmeterRealtimeDto? GetRealtime
			{
			get; set;
			}

		[JsonPropertyName ("get_daystat")]
		public LegacyEmeterDailyStatDto? GetDayStat
			{
			get; set;
			}

		[JsonPropertyName ("get_monthstat")]
		public LegacyEmeterMonthlyStatDto? GetMonthStat
			{
			get; set;
			}
		}

	internal sealed class LegacySystemInfoDto
		{
		[JsonPropertyName ("alias")]
		public string? Alias
			{
			get; set;
			}

		[JsonPropertyName ("nickname")]
		public string? Nickname
			{
			get; set;
			}

		[JsonPropertyName ("model")]
		public string? Model
			{
			get; set;
			}

		[JsonPropertyName ("device_model")]
		public string? DeviceModel
			{
			get; set;
			}

		[JsonPropertyName ("deviceId")]
		public string? DeviceId
			{
			get; set;
			}

		[JsonPropertyName ("device_id")]
		public string? DeviceIdUnderscore
			{
			get; set;
			}

		[JsonPropertyName ("mac")]
		public string? Mac
			{
			get; set;
			}

		[JsonPropertyName ("mic_mac")]
		public string? MicMac
			{
			get; set;
			}

		[JsonPropertyName ("hw_ver")]
		public string? HardwareVersion
			{
			get; set;
			}

		[JsonPropertyName ("hwVersion")]
		public string? HardwareVersionAlt
			{
			get; set;
			}

		[JsonPropertyName ("sw_ver")]
		public string? SoftwareVersion
			{
			get; set;
			}

		[JsonPropertyName ("swVersion")]
		public string? SoftwareVersionAlt
			{
			get; set;
			}

		[JsonPropertyName ("type")]
		public string? Type
			{
			get; set;
			}

		[JsonPropertyName ("mic_type")]
		public string? MicType
			{
			get; set;
			}

		[JsonPropertyName ("device_type")]
		public string? DeviceType
			{
			get; set;
			}

		[JsonPropertyName ("relay_state")]
		public int? RelayState
			{
			get; set;
			}

		[JsonPropertyName ("device_on")]
		public bool? DeviceOn
			{
			get; set;
			}

		[JsonPropertyName ("auto_off_status")]
		public string? AutoOffStatus
			{
			get; set;
			}

		[JsonPropertyName ("auto_off_remain_time")]
		public int? AutoOffRemainTimeSeconds
			{
			get; set;
			}

		[JsonPropertyName ("led_off")]
		public int? LedOff
			{
			get; set;
			}

		[JsonPropertyName ("on_time")]
		public int? OnTimeSeconds
			{
			get; set;
			}

		[JsonPropertyName ("rssi")]
		public int? Rssi
			{
			get; set;
			}

		[JsonPropertyName ("children")]
		public List<LegacyChildDeviceDto>? Children
			{
			get; set;
			}

		[JsonPropertyName ("light_state")]
		public LegacyLightStateDto? LightState
			{
			get; set;
			}

		[JsonPropertyName ("preferred_state")]
		public List<LegacyLightPresetDto>? PreferredState
			{
			get; set;
			}
		}

	internal sealed class LegacyChildDeviceDto
		{
		[JsonPropertyName ("id")]
		public string? Id
			{
			get; set;
			}

		[JsonPropertyName ("device_id")]
		public string? DeviceId
			{
			get; set;
			}

		[JsonPropertyName ("alias")]
		public string? Alias
			{
			get; set;
			}

		[JsonPropertyName ("nickname")]
		public string? Nickname
			{
			get; set;
			}

		[JsonPropertyName ("model")]
		public string? Model
			{
			get; set;
			}

		[JsonPropertyName ("device_model")]
		public string? DeviceModel
			{
			get; set;
			}

		[JsonPropertyName ("relay_state")]
		public int? RelayState
			{
			get; set;
			}

		[JsonPropertyName ("state")]
		public int? State
			{
			get; set;
			}

		[JsonPropertyName ("device_on")]
		public bool? DeviceOn
			{
			get; set;
			}
		}

	internal sealed class LegacyLightStateDto
		{
		[JsonPropertyName ("on_off")]
		public int? OnOff
			{
			get; set;
			}

		[JsonPropertyName ("brightness")]
		public int? Brightness
			{
			get; set;
			}

		[JsonPropertyName ("color_temp")]
		public int? ColorTemperature
			{
			get; set;
			}

		[JsonPropertyName ("hue")]
		public int? Hue
			{
			get; set;
			}

		[JsonPropertyName ("saturation")]
		public int? Saturation
			{
			get; set;
			}

		[JsonPropertyName ("dynamic_light_effect_enable")]
		public int? DynamicLightEffectEnable
			{
			get; set;
			}

		[JsonPropertyName ("dynamic_light_effect_id")]
		public string? DynamicLightEffectId
			{
			get; set;
			}

		[JsonPropertyName ("dynamic_light_effect_rule_list")]
		public List<LegacyDynamicLightEffectRuleDto>? DynamicLightEffectRuleList
			{
			get; set;
			}

		[JsonPropertyName ("lighting_effect")]
		public LegacyLightingEffectDto? LightingEffect
			{
			get; set;
			}

		[JsonPropertyName ("dft_on_state")]
		public LegacyLightStateDto? DefaultOnState
			{
			get; set;
			}
		}

	internal sealed class LegacyLightPresetDto
		{
		[JsonPropertyName ("brightness")]
		public int? Brightness
			{
			get; set;
			}

		[JsonPropertyName ("color_temp")]
		public int? ColorTemperature
			{
			get; set;
			}

		[JsonPropertyName ("hue")]
		public int? Hue
			{
			get; set;
			}

		[JsonPropertyName ("saturation")]
		public int? Saturation
			{
			get; set;
			}

		[JsonPropertyName ("custom")]
		public int? Custom
			{
			get; set;
			}

		[JsonPropertyName ("id")]
		public string? Id
			{
			get; set;
			}

		[JsonPropertyName ("mode")]
		public int? Mode
			{
			get; set;
			}
		}

	internal sealed class LegacyDynamicLightEffectRuleDto
		{
		[JsonPropertyName ("id")]
		public string? Id
			{
			get; set;
			}

		[JsonPropertyName ("name")]
		public string? Name
			{
			get; set;
			}
		}

	internal sealed class LegacyLightingEffectDto
		{
		[JsonPropertyName ("enable")]
		public int? Enable
			{
			get; set;
			}

		[JsonPropertyName ("id")]
		public string? Id
			{
			get; set;
			}

		[JsonPropertyName ("name")]
		public string? Name
			{
			get; set;
			}

		[JsonPropertyName ("brightness")]
		public int? Brightness
			{
			get; set;
			}
		}

	internal sealed class LegacyEmeterRealtimeDto
		{
		[JsonPropertyName ("power")]
		public double? Power
			{
			get; set;
			}

		[JsonPropertyName ("power_mw")]
		public double? PowerMilliwatts
			{
			get; set;
			}

		[JsonPropertyName ("voltage")]
		public double? Voltage
			{
			get; set;
			}

		[JsonPropertyName ("voltage_mv")]
		public double? VoltageMillivolts
			{
			get; set;
			}

		[JsonPropertyName ("current")]
		public double? Current
			{
			get; set;
			}

		[JsonPropertyName ("current_ma")]
		public double? CurrentMilliamps
			{
			get; set;
			}

		[JsonPropertyName ("total")]
		public double? Total
			{
			get; set;
			}

		[JsonPropertyName ("total_wh")]
		public double? TotalWattHours
			{
			get; set;
			}

		[JsonPropertyName ("energy")]
		public double? Energy
			{
			get; set;
			}

		[JsonPropertyName ("energy_wh")]
		public double? EnergyWattHours
			{
			get; set;
			}
		}

	internal sealed class LegacyEmeterDailyStatDto
		{
		[JsonPropertyName ("day_list")]
		public List<LegacyEmeterDayStatEntryDto>? DayList { get; set; }
		}

	internal sealed class LegacyEmeterMonthlyStatDto
		{
		[JsonPropertyName ("month_list")]
		public List<LegacyEmeterMonthStatEntryDto>? MonthList { get; set; }
		}

	internal sealed class LegacyEmeterDayStatEntryDto
		{
		[JsonPropertyName ("day")]
		public int? Day { get; set; }

		[JsonPropertyName ("energy")]
		public double? EnergyKilowattHours { get; set; }

		[JsonPropertyName ("energy_wh")]
		public double? EnergyWattHours { get; set; }
		}

	internal sealed class LegacyEmeterMonthStatEntryDto
		{
		[JsonPropertyName ("month")]
		public int? Month { get; set; }

		[JsonPropertyName ("energy")]
		public double? EnergyKilowattHours { get; set; }

		[JsonPropertyName ("energy_wh")]
		public double? EnergyWattHours { get; set; }
		}

	}

internal static class KasaCipher
	{
	private const byte INITIAL_KEY = 171;

	public static byte[] Encrypt (string value)
		{
		byte[] plainBytes = Encoding.UTF8.GetBytes (value);
		var encryptedBytes = new byte[plainBytes.Length];
		byte key = INITIAL_KEY;
		for (int i = 0; i < plainBytes.Length; i++)
			{
			byte encryptedByte = (byte)(plainBytes[i] ^ key);
			encryptedBytes[i] = encryptedByte;
			key = encryptedByte;
			}

		return encryptedBytes;
		}

	public static byte[] EncryptWithHeader (string value)
		{
		byte[] payload = Encrypt (value);
		var framedPayload = new byte[payload.Length + 4];
		framedPayload[0] = (byte)((payload.Length >> 24) & 0xFF);
		framedPayload[1] = (byte)((payload.Length >> 16) & 0xFF);
		framedPayload[2] = (byte)((payload.Length >> 8) & 0xFF);
		framedPayload[3] = (byte)(payload.Length & 0xFF);
		Buffer.BlockCopy (payload, 0, framedPayload, 4, payload.Length);
		return framedPayload;
		}

	public static string Decrypt (byte[] encryptedBytes)
		{
		var plainBytes = new byte[encryptedBytes.Length];
		byte key = INITIAL_KEY;
		for (int i = 0; i < encryptedBytes.Length; i++)
			{
			byte encryptedByte = encryptedBytes[i];
			plainBytes[i] = (byte)(encryptedByte ^ key);
			key = encryptedByte;
			}

		return Encoding.UTF8.GetString (plainBytes);
		}
	}
