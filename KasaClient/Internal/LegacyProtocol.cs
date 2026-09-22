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

namespace KasaTapoClient.Internal;

internal static partial class KasaResponseParser
	{
	private static readonly Dictionary<string, Func<LightStripEffectParametersDto>> SMART_LIGHT_STRIP_EFFECT_PAYLOADS =
		new Dictionary<string, Func<LightStripEffectParametersDto>> (StringComparer.OrdinalIgnoreCase)
			{
			["Aurora"] = () => new LightStripEffectParametersDto
				{
				Custom = 0,
				Id = "TapoStrip_1MClvV18i15Jq3bvJVf0eP",
				Brightness = 100,
				Name = "Aurora",
				Enable = 1,
				Segments = new int[] { 0 },
				ExpansionStrategy = 1,
				DisplayColors = new int[][] { new int[] { 120, 100, 100 }, new int[] { 240, 100, 100 }, new int[] { 260, 100, 100 }, new int[] { 280, 100, 100 } },
				Type = "sequence",
				Duration = 0,
				Transition = 1500,
				Direction = 4,
				Spread = 7,
				RepeatTimes = 0,
				Sequence = new int[][] { new int[] { 120, 100, 100 }, new int[] { 240, 100, 100 }, new int[] { 260, 100, 100 }, new int[] { 280, 100, 100 } },
				},
			["Bubbling Cauldron"] = () => new LightStripEffectParametersDto
				{
				Custom = 0,
				Id = "TapoStrip_6DlumDwO2NdfHppy50vJtu",
				Brightness = 100,
				Name = "Bubbling Cauldron",
				Enable = 1,
				Segments = new int[] { 0 },
				ExpansionStrategy = 1,
				DisplayColors = new int[][] { new int[] { 100, 100, 100 }, new int[] { 270, 100, 100 } },
				Type = "random",
				HueRange = new int[] { 100, 270 },
				SaturationRange = new int[] { 80, 100 },
				BrightnessRange = new int[] { 50, 100 },
				Duration = 0,
				Transition = 200,
				InitStates = new int[][] { new int[] { 270, 100, 100 } },
				Fadeoff = 1000,
				RandomSeed = 24,
				Backgrounds = new int[][] { new int[] { 270, 40, 50 } },
				},
			["Candy Cane"] = () => new LightStripEffectParametersDto
				{
				Custom = 0,
				Id = "TapoStrip_6Dy0Nc45vlhFPEzG021Pe9",
				Brightness = 100,
				Name = "Candy Cane",
				Enable = 1,
				Segments = new int[] { 0 },
				ExpansionStrategy = 1,
				DisplayColors = new int[][] { new int[] { 0, 0, 100 }, new int[] { 0, 81, 100 } },
				Type = "sequence",
				Duration = 700,
				Transition = 500,
				Direction = 1,
				Spread = 1,
				RepeatTimes = 0,
				Sequence = new int[][] { new int[] { 0, 0, 100 }, new int[] { 0, 0, 100 }, new int[] { 360, 81, 100 }, new int[] { 0, 0, 100 }, new int[] { 0, 0, 100 }, new int[] { 360, 81, 100 }, new int[] { 360, 81, 100 }, new int[] { 0, 0, 100 }, new int[] { 0, 0, 100 }, new int[] { 360, 81, 100 }, new int[] { 360, 81, 100 }, new int[] { 360, 81, 100 }, new int[] { 360, 81, 100 }, new int[] { 0, 0, 100 }, new int[] { 0, 0, 100 }, new int[] { 360, 81, 100 } },
				},
			["Christmas"] = () => new LightStripEffectParametersDto
				{
				Custom = 0,
				Id = "TapoStrip_5zkiG6avJ1IbhjiZbRlWvh",
				Brightness = 100,
				Name = "Christmas",
				Enable = 1,
				Segments = new int[] { 0 },
				ExpansionStrategy = 1,
				DisplayColors = new int[][] { new int[] { 136, 98, 100 }, new int[] { 350, 97, 100 } },
				Type = "random",
				HueRange = new int[] { 136, 146 },
				SaturationRange = new int[] { 90, 100 },
				BrightnessRange = new int[] { 50, 100 },
				Duration = 5000,
				Transition = 0,
				InitStates = new int[][] { new int[] { 136, 0, 100 } },
				Fadeoff = 2000,
				RandomSeed = 100,
				Backgrounds = new int[][] { new int[] { 136, 98, 75 }, new int[] { 136, 0, 0 }, new int[] { 350, 0, 100 }, new int[] { 350, 97, 94 } },
				},
			["Flicker"] = () => new LightStripEffectParametersDto
				{
				Custom = 0,
				Id = "TapoStrip_4HVKmMc6vEzjm36jXaGwMs",
				Brightness = 100,
				Name = "Flicker",
				Enable = 1,
				Segments = new int[] { 1 },
				ExpansionStrategy = 1,
				DisplayColors = new int[][] { new int[] { 30, 81, 100 }, new int[] { 40, 100, 100 } },
				Type = "random",
				HueRange = new int[] { 30, 40 },
				SaturationRange = new int[] { 100, 100 },
				BrightnessRange = new int[] { 50, 100 },
				Duration = 0,
				Transition = 0,
				TransitionRange = new int[] { 375, 500 },
				InitStates = new int[][] { new int[] { 30, 81, 80 } },
				},
			["Grandma's Christmas Lights"] = () => new LightStripEffectParametersDto
				{
				Custom = 0,
				Id = "TapoStrip_3Gk6CmXOXbjCiwz9iD543C",
				Brightness = 100,
				Name = "Grandma's Christmas Lights",
				Enable = 1,
				Segments = new int[] { 0 },
				ExpansionStrategy = 1,
				DisplayColors = new int[][] { new int[] { 30, 100, 100 }, new int[] { 240, 100, 100 }, new int[] { 130, 100, 100 }, new int[] { 0, 100, 100 } },
				Type = "sequence",
				Duration = 5000,
				Transition = 100,
				Direction = 1,
				Spread = 1,
				RepeatTimes = 0,
				Sequence = new int[][] { new int[] { 30, 100, 100 }, new int[] { 30, 0, 0 }, new int[] { 30, 0, 0 }, new int[] { 240, 100, 100 }, new int[] { 240, 0, 0 }, new int[] { 240, 0, 0 }, new int[] { 240, 0, 100 }, new int[] { 240, 0, 0 }, new int[] { 240, 0, 0 }, new int[] { 130, 100, 100 }, new int[] { 130, 0, 0 }, new int[] { 130, 0, 0 }, new int[] { 0, 100, 100 }, new int[] { 0, 0, 0 }, new int[] { 0, 0, 0 } },
				},
			["Hanukkah"] = () => new LightStripEffectParametersDto
				{
				Custom = 0,
				Id = "TapoStrip_2YTk4wramLKv5XZ9KFDVYm",
				Brightness = 100,
				Name = "Hanukkah",
				Enable = 1,
				Segments = new int[] { 1 },
				ExpansionStrategy = 1,
				DisplayColors = new int[][] { new int[] { 200, 100, 100 } },
				Type = "random",
				HueRange = new int[] { 200, 210 },
				SaturationRange = new int[] { 0, 100 },
				BrightnessRange = new int[] { 50, 100 },
				Duration = 1500,
				Transition = 0,
				TransitionRange = new int[] { 400, 500 },
				InitStates = new int[][] { new int[] { 35, 81, 80 } },
				},
			["Haunted Mansion"] = () => new LightStripEffectParametersDto
				{
				Custom = 0,
				Id = "TapoStrip_4rJ6JwC7I9st3tQ8j4lwlI",
				Brightness = 100,
				Name = "Haunted Mansion",
				Enable = 1,
				Segments = new int[] { 80 },
				ExpansionStrategy = 2,
				DisplayColors = new int[][] { new int[] { 44, 9, 100 } },
				Type = "random",
				HueRange = new int[] { 45, 45 },
				SaturationRange = new int[] { 10, 10 },
				BrightnessRange = new int[] { 0, 80 },
				Duration = 0,
				Transition = 0,
				TransitionRange = new int[] { 50, 1500 },
				InitStates = new int[][] { new int[] { 45, 10, 100 } },
				Fadeoff = 200,
				RandomSeed = 1,
				Backgrounds = new int[][] { new int[] { 45, 10, 100 } },
				},
			["Icicle"] = () => new LightStripEffectParametersDto
				{
				Custom = 0,
				Id = "TapoStrip_7UcYLeJbiaxVIXCxr21tpx",
				Brightness = 100,
				Name = "Icicle",
				Enable = 1,
				Segments = new int[] { 0 },
				ExpansionStrategy = 1,
				DisplayColors = new int[][] { new int[] { 190, 100, 100 } },
				Type = "sequence",
				Duration = 0,
				Transition = 400,
				Direction = 4,
				Spread = 3,
				RepeatTimes = 0,
				Sequence = new int[][] { new int[] { 190, 100, 70 }, new int[] { 190, 100, 70 }, new int[] { 190, 30, 50 }, new int[] { 190, 100, 70 }, new int[] { 190, 100, 70 } },
				},
			["Lightning"] = () => new LightStripEffectParametersDto
				{
				Custom = 0,
				Id = "TapoStrip_7OGzfSfnOdhoO2ri4gOHWn",
				Brightness = 100,
				Name = "Lightning",
				Enable = 1,
				Segments = new int[] { 7 },
				ExpansionStrategy = 1,
				DisplayColors = new int[][] { new int[] { 210, 9, 100 }, new int[] { 200, 50, 100 }, new int[] { 200, 100, 100 } },
				Type = "random",
				HueRange = new int[] { 240, 240 },
				SaturationRange = new int[] { 10, 11 },
				BrightnessRange = new int[] { 90, 100 },
				Duration = 0,
				Transition = 50,
				InitStates = new int[][] { new int[] { 240, 30, 100 } },
				Fadeoff = 150,
				RandomSeed = 50,
				Backgrounds = new int[][] { new int[] { 200, 100, 100 }, new int[] { 200, 50, 10 }, new int[] { 210, 10, 50 }, new int[] { 240, 10, 0 } },
				},
			["Ocean"] = () => new LightStripEffectParametersDto
				{
				Custom = 0,
				Id = "TapoStrip_0fOleCdwSgR0nfjkReeYfw",
				Brightness = 100,
				Name = "Ocean",
				Enable = 1,
				Segments = new int[] { 0 },
				ExpansionStrategy = 1,
				DisplayColors = new int[][] { new int[] { 198, 84, 100 } },
				Type = "sequence",
				Duration = 0,
				Transition = 2000,
				Direction = 3,
				Spread = 16,
				RepeatTimes = 0,
				Sequence = new int[][] { new int[] { 198, 84, 30 }, new int[] { 198, 70, 30 }, new int[] { 198, 10, 30 } },
				},
			["Rainbow"] = () => new LightStripEffectParametersDto
				{
				Custom = 0,
				Id = "TapoStrip_7CC5y4lsL8pETYvmz7UOpQ",
				Brightness = 100,
				Name = "Rainbow",
				Enable = 1,
				Segments = new int[] { 0 },
				ExpansionStrategy = 1,
				DisplayColors = new int[][] { new int[] { 0, 100, 100 }, new int[] { 100, 100, 100 }, new int[] { 200, 100, 100 }, new int[] { 300, 100, 100 } },
				Type = "sequence",
				Duration = 0,
				Transition = 1500,
				Direction = 1,
				Spread = 12,
				RepeatTimes = 0,
				Sequence = new int[][] { new int[] { 0, 100, 100 }, new int[] { 100, 100, 100 }, new int[] { 200, 100, 100 }, new int[] { 300, 100, 100 } },
				},
			["Raindrop"] = () => new LightStripEffectParametersDto
				{
				Custom = 0,
				Id = "TapoStrip_1t2nWlTBkV8KXBZ0TWvBjs",
				Brightness = 100,
				Name = "Raindrop",
				Enable = 1,
				Segments = new int[] { 0 },
				ExpansionStrategy = 1,
				DisplayColors = new int[][] { new int[] { 200, 9, 100 }, new int[] { 200, 19, 100 } },
				Type = "random",
				HueRange = new int[] { 200, 200 },
				SaturationRange = new int[] { 10, 20 },
				BrightnessRange = new int[] { 10, 30 },
				Duration = 0,
				Transition = 1000,
				InitStates = new int[][] { new int[] { 200, 40, 100 } },
				Fadeoff = 1000,
				RandomSeed = 24,
				Backgrounds = new int[][] { new int[] { 200, 40, 0 } },
				},
			["Spring"] = () => new LightStripEffectParametersDto
				{
				Custom = 0,
				Id = "TapoStrip_1nL6GqZ5soOxj71YDJOlZL",
				Brightness = 100,
				Name = "Spring",
				Enable = 1,
				Segments = new int[] { 0 },
				ExpansionStrategy = 1,
				DisplayColors = new int[][] { new int[] { 0, 30, 100 }, new int[] { 130, 100, 100 } },
				Type = "random",
				HueRange = new int[] { 0, 90 },
				SaturationRange = new int[] { 30, 100 },
				BrightnessRange = new int[] { 90, 100 },
				Duration = 600,
				Transition = 0,
				TransitionRange = new int[] { 2000, 6000 },
				InitStates = new int[][] { new int[] { 80, 30, 100 } },
				Fadeoff = 1000,
				RandomSeed = 20,
				Backgrounds = new int[][] { new int[] { 130, 100, 40 } },
				},
			["Sunrise"] = () => new LightStripEffectParametersDto
				{
				Custom = 0,
				Id = "TapoStrip_1OVSyXIsDxrt4j7OxyRvqi",
				Brightness = 100,
				Name = "Sunrise",
				Enable = 1,
				Segments = new int[] { 0 },
				ExpansionStrategy = 2,
				DisplayColors = new int[][] { new int[] { 0, 0, 100 }, new int[] { 30, 95, 100 }, new int[] { 0, 100, 100 } },
				Type = "pulse",
				Duration = 600,
				Transition = 60000,
				Direction = 1,
				Spread = 1,
				RepeatTimes = 1,
				RunTime = 0,
				Sequence = new int[][] { new int[] { 0, 100, 5 }, new int[] { 0, 100, 5 }, new int[] { 10, 100, 6 }, new int[] { 15, 100, 7 }, new int[] { 20, 100, 8 }, new int[] { 20, 100, 10 }, new int[] { 30, 100, 12 }, new int[] { 30, 95, 15 }, new int[] { 30, 90, 20 }, new int[] { 30, 80, 25 }, new int[] { 30, 75, 30 }, new int[] { 30, 70, 40 }, new int[] { 30, 60, 50 }, new int[] { 30, 50, 60 }, new int[] { 30, 20, 70 }, new int[] { 30, 0, 100 } },
				TransSequence = Array.Empty<int[]> (),
				},
			["Sunset"] = () => new LightStripEffectParametersDto
				{
				Custom = 0,
				Id = "TapoStrip_5NiN0Y8GAUD78p4neKk9EL",
				Brightness = 100,
				Name = "Sunset",
				Enable = 1,
				Segments = new int[] { 0 },
				ExpansionStrategy = 2,
				DisplayColors = new int[][] { new int[] { 0, 100, 100 }, new int[] { 30, 95, 100 }, new int[] { 0, 0, 100 } },
				Type = "pulse",
				Duration = 600,
				Transition = 60000,
				Direction = 1,
				Spread = 1,
				RepeatTimes = 1,
				RunTime = 0,
				Sequence = new int[][] { new int[] { 30, 0, 100 }, new int[] { 30, 20, 100 }, new int[] { 30, 50, 99 }, new int[] { 30, 60, 98 }, new int[] { 30, 70, 97 }, new int[] { 30, 75, 95 }, new int[] { 30, 80, 93 }, new int[] { 30, 90, 90 }, new int[] { 30, 95, 85 }, new int[] { 30, 100, 80 }, new int[] { 20, 100, 70 }, new int[] { 20, 100, 60 }, new int[] { 15, 100, 50 }, new int[] { 10, 100, 40 }, new int[] { 0, 100, 30 }, new int[] { 0, 100, 0 } },
				TransSequence = Array.Empty<int[]> (),
				},
			["Valentines"] = () => new LightStripEffectParametersDto
				{
				Custom = 0,
				Id = "TapoStrip_2q1Vio9sSjHmaC7JS9d30l",
				Brightness = 100,
				Name = "Valentines",
				Enable = 1,
				Segments = new int[] { 0 },
				ExpansionStrategy = 1,
				DisplayColors = new int[][] { new int[] { 339, 19, 100 }, new int[] { 19, 50, 100 }, new int[] { 0, 100, 100 }, new int[] { 339, 40, 100 } },
				Type = "random",
				HueRange = new int[] { 340, 340 },
				SaturationRange = new int[] { 30, 40 },
				BrightnessRange = new int[] { 90, 100 },
				Duration = 600,
				Transition = 2000,
				InitStates = new int[][] { new int[] { 340, 30, 100 } },
				Fadeoff = 3000,
				RandomSeed = 100,
				Backgrounds = new int[][] { new int[] { 340, 20, 50 }, new int[] { 20, 50, 50 }, new int[] { 0, 100, 50 } },
				}
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

	internal static LightStripEffectParametersDto CreateSmartLightStripEffectPayload (string? effect)
		{
		if (string.IsNullOrWhiteSpace (effect)) return new LightStripEffectParametersDto { Enable = 0 };
		if (!SMART_LIGHT_STRIP_EFFECT_PAYLOADS.TryGetValue (effect!, out Func<LightStripEffectParametersDto>? factory))
			throw new ArgumentException ($"Unknown smart light-strip effect '{effect}'.", nameof (effect));
		return factory ();
		}

	internal sealed class ParsedResponse
		{
		internal ParsedResponse (
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
			SmartDeviceInfoDto deviceInfo,
			IReadOnlyList<string> componentIds,
			IReadOnlyDictionary<string, int> componentVersions,
			SmartChildDeviceListDto? childDeviceList,
			IReadOnlyDictionary<string, IReadOnlyList<string>> childComponentIds,
			IReadOnlyDictionary<string, SmartChildDeviceDto> childOverrides,
			IReadOnlyDictionary<string, object> moduleResults)
			{
			DeviceInfo = deviceInfo;
			ComponentIds = componentIds;
			ComponentVersions = componentVersions;
			ChildDeviceList = childDeviceList;
			ChildComponentIds = childComponentIds;
			ChildOverrides = childOverrides;
			ModuleResults = moduleResults;
			}
		internal SmartDeviceInfoDto DeviceInfo { get; }
		internal IReadOnlyList<string> ComponentIds { get; }
		internal IReadOnlyDictionary<string, int> ComponentVersions { get; }
		internal SmartChildDeviceListDto? ChildDeviceList { get; }
		internal IReadOnlyDictionary<string, IReadOnlyList<string>> ChildComponentIds { get; }
		internal IReadOnlyDictionary<string, SmartChildDeviceDto> ChildOverrides { get; }
		internal IReadOnlyDictionary<string, object> ModuleResults { get; }
		}

	internal sealed class SmartEnvelopeDto
		{
		[System.Text.Json.Serialization.JsonPropertyName ("result")]
		public SmartEnvelopeResultDto? Result { get; set; }
		}

	internal sealed class SmartEnvelopeResultDto
		{
		[System.Text.Json.Serialization.JsonPropertyName ("responses")]
		public List<SmartMethodResponseDto>? Responses { get; set; }

		[System.Text.Json.Serialization.JsonPropertyName ("component_list")]
		public List<SmartComponentDto>? ComponentList { get; set; }

		[System.Text.Json.Serialization.JsonPropertyName ("child_device_list")]
		public List<SmartChildDeviceDto>? ChildDeviceList { get; set; }

		[System.Text.Json.Serialization.JsonPropertyName ("child_component_list")]
		public List<SmartChildComponentDto>? ChildComponentList { get; set; }

		[System.Text.Json.Serialization.JsonPropertyName ("sum")]
		public int? Sum { get; set; }

		[System.Text.Json.Serialization.JsonPropertyName ("start_index")]
		public int? StartIndex { get; set; }

		[System.Text.Json.Serialization.JsonPropertyName ("model")]
		public string? Model { get; set; }

		[System.Text.Json.Serialization.JsonPropertyName ("type")]
		public string? Type { get; set; }

		[System.Text.Json.Serialization.JsonPropertyName ("device_id")]
		public string? DeviceId { get; set; }

		[System.Text.Json.Serialization.JsonPropertyName ("nickname")]
		public string? Nickname { get; set; }

		[System.Text.Json.Serialization.JsonPropertyName ("avatar")]
		public string? Avatar { get; set; }

		[System.Text.Json.Serialization.JsonPropertyName ("device_on")]
		public bool? DeviceOn { get; set; }

		[System.Text.Json.Serialization.JsonPropertyName ("fw_ver")]
		public string? FirmwareVersion { get; set; }

		[System.Text.Json.Serialization.JsonPropertyName ("hw_ver")]
		public string? HardwareVersion { get; set; }

		[System.Text.Json.Serialization.JsonPropertyName ("mac")]
		public string? Mac { get; set; }

		[System.Text.Json.Serialization.JsonPropertyName ("rssi")]
		public int? Rssi { get; set; }

		[System.Text.Json.Serialization.JsonPropertyName ("signal_level")]
		public int? SignalLevel { get; set; }

		[System.Text.Json.Serialization.JsonPropertyName ("ssid")]
		public string? Ssid { get; set; }

		[System.Text.Json.Serialization.JsonPropertyName ("on_time")]
		public int? OnTimeSeconds { get; set; }

		[System.Text.Json.Serialization.JsonPropertyName ("specs")]
		public string? Specs { get; set; }

		[System.Text.Json.Serialization.JsonPropertyName ("device_category_list")]
		public List<SmartChildSetupCategoryDto>? DeviceCategoryList { get; set; }

		[System.Text.Json.Serialization.JsonPropertyName ("brightness")]
		public int? Brightness { get; set; }

		[System.Text.Json.Serialization.JsonPropertyName ("hue")]
		public int? Hue { get; set; }

		[System.Text.Json.Serialization.JsonPropertyName ("saturation")]
		public int? Saturation { get; set; }

		[System.Text.Json.Serialization.JsonPropertyName ("color_temp")]
		public int? ColorTemperature { get; set; }

		[System.Text.Json.Serialization.JsonPropertyName ("lighting_effect")]
		public LegacyLightingEffectDto? LightingEffect { get; set; }

		[System.Text.Json.Serialization.JsonPropertyName ("overheated")]
		public bool? Overheated { get; set; }

		[System.Text.Json.Serialization.JsonPropertyName ("power_protection")]
		public bool? PowerProtection { get; set; }

		[System.Text.Json.Serialization.JsonPropertyName ("power_protect")]
		public bool? PowerProtect { get; set; }

		[System.Text.Json.Serialization.JsonPropertyName ("speaker")]
		public bool? Speaker { get; set; }

		[System.Text.Json.Serialization.JsonPropertyName ("smooth_transition_on")]
		public int? SmoothTransitionOn { get; set; }

		[System.Text.Json.Serialization.JsonPropertyName ("smooth_transition_off")]
		public int? SmoothTransitionOff { get; set; }

		[System.Text.Json.Serialization.JsonPropertyName ("transition_period")]
		public int? TransitionPeriod { get; set; }
		}

	[System.Text.Json.Serialization.JsonConverter (typeof (SmartMethodResponseConverter))]
	internal sealed class SmartMethodResponseDto
		{
		public string? Method { get; set; }
		public int? ErrorCode { get; set; }
		public object? Result { get; set; }
		}

	internal sealed class SmartComponentDto
		{
		[System.Text.Json.Serialization.JsonPropertyName ("id")]
		public string? Id { get; set; }

		[System.Text.Json.Serialization.JsonPropertyName ("ver_code")]
		public int? VersionCode { get; set; }
		}

	internal sealed class SmartDeviceInfoDto
		{
		internal SmartDeviceInfoDto (string? model, string? type, string? deviceId, string? nickname, bool? deviceOn, string? firmwareVersion, string? hardwareVersion, string? mac, int? signalLevel, int? rssi, string? ssid, string? specs, List<SmartChildSetupCategoryDto>? deviceCategoryList, int? brightness, int? hue, int? saturation, int? colorTemperature, LegacyLightingEffectDto? lightingEffect, bool? overheated, bool? powerProtection, bool? powerProtect, bool? speaker, int? smoothTransitionOn, int? smoothTransitionOff, int? transitionPeriod)
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
			Overheated = overheated;
			PowerProtection = powerProtection;
			PowerProtect = powerProtect;
			Speaker = speaker;
			SmoothTransitionOn = smoothTransitionOn;
			SmoothTransitionOff = smoothTransitionOff;
			TransitionPeriod = transitionPeriod;
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
		internal bool? Overheated { get; }
		internal bool? PowerProtection { get; }
		internal bool? PowerProtect { get; }
		internal bool? Speaker { get; }
		internal int? SmoothTransitionOn { get; }
		internal int? SmoothTransitionOff { get; }
		internal int? TransitionPeriod { get; }
		}

	internal sealed class SmartChildDeviceListDto
		{
		internal SmartChildDeviceListDto (IReadOnlyList<SmartChildDeviceDto> childDevices, int? sum = null, int? startIndex = null)
			{
			ChildDevices = childDevices;
			Sum = sum;
			StartIndex = startIndex;
			}

		internal IReadOnlyList<SmartChildDeviceDto> ChildDevices { get; }
		internal int? Sum { get; }
		internal int? StartIndex { get; }
		}

	internal sealed class SmartChildDeviceDto
		{
		[System.Text.Json.Serialization.JsonPropertyName ("device_id")]
		public string? DeviceId { get; set; }

		[System.Text.Json.Serialization.JsonPropertyName ("nickname")]
		public string? Nickname { get; set; }

		[System.Text.Json.Serialization.JsonPropertyName ("model")]
		public string? Model { get; set; }

		[System.Text.Json.Serialization.JsonPropertyName ("category")]
		public string? Category { get; set; }

		[System.Text.Json.Serialization.JsonPropertyName ("type")]
		public string? Type { get; set; }

		[System.Text.Json.Serialization.JsonPropertyName ("fw_ver")]
		public string? FirmwareVersion { get; set; }

		[System.Text.Json.Serialization.JsonPropertyName ("signal_level")]
		public int? SignalLevel { get; set; }

		[System.Text.Json.Serialization.JsonPropertyName ("rssi")]
		public int? Rssi { get; set; }

		[System.Text.Json.Serialization.JsonPropertyName ("status")]
		public string? Status { get; set; }

		[System.Text.Json.Serialization.JsonPropertyName ("battery_percentage")]
		public int? BatteryPercentage { get; set; }

		[System.Text.Json.Serialization.JsonPropertyName ("at_low_battery")]
		public bool? AtLowBattery { get; set; }

		[System.Text.Json.Serialization.JsonPropertyName ("is_low")]
		public bool? IsLowBattery { get; set; }

		[System.Text.Json.Serialization.JsonPropertyName ("current_temp")]
		public double? CurrentTemperature { get; set; }

		[System.Text.Json.Serialization.JsonPropertyName ("current_temp_exception")]
		[System.Text.Json.Serialization.JsonConverter (typeof (FlexibleNullableInt32Converter))]
		public int? CurrentTemperatureException { get; set; }

		[System.Text.Json.Serialization.JsonPropertyName ("current_humidity")]
		public int? CurrentHumidity { get; set; }

		[System.Text.Json.Serialization.JsonPropertyName ("current_humidity_exception")]
		[System.Text.Json.Serialization.JsonConverter (typeof (FlexibleNullableInt32Converter))]
		public int? CurrentHumidityException { get; set; }

		[System.Text.Json.Serialization.JsonPropertyName ("temp_unit")]
		public string? TemperatureUnit { get; set; }

		[System.Text.Json.Serialization.JsonPropertyName ("report_interval")]
		public int? ReportInterval { get; set; }

		[System.Text.Json.Serialization.JsonPropertyName ("detected")]
		public bool? Detected { get; set; }

		[System.Text.Json.Serialization.JsonPropertyName ("open")]
		public bool? Open { get; set; }

		[System.Text.Json.Serialization.JsonPropertyName ("in_alarm")]
		public bool? InAlarm { get; set; }

		[System.Text.Json.Serialization.JsonPropertyName ("water_leak_status")]
		public string? WaterLeakStatus { get; set; }

		[System.Text.Json.Serialization.JsonPropertyName ("trigger_timestamp")]
		public long? TriggerTimestamp { get; set; }

		[System.Text.Json.Serialization.JsonPropertyName ("double_click_info")]
		public SmartDoubleClickInfoDto? DoubleClickInfo { get; set; }

		[System.Text.Json.Serialization.JsonPropertyName ("trigger_logs")]
		public SmartTriggerLogListDto? TriggerLogs { get; set; }

		[System.Text.Json.Serialization.JsonPropertyName ("comfort_temp_config")]
		public SmartComfortValueConfigDto? ComfortTemperatureConfig { get; set; }

		[System.Text.Json.Serialization.JsonPropertyName ("comfort_humidity_config")]
		public SmartComfortValueConfigDto? ComfortHumidityConfig { get; set; }

		[System.Text.Json.Serialization.JsonPropertyName ("frost_protection")]
		public SmartFrostProtectionDto? FrostProtection { get; set; }

		[System.Text.Json.Serialization.JsonPropertyName ("frost_protection_on")]
		public bool? FrostProtectionOn { get; set; }

		[System.Text.Json.Serialization.JsonPropertyName ("target_temp")]
		public double? TargetTemperature { get; set; }

		[System.Text.Json.Serialization.JsonPropertyName ("min_control_temp")]
		public int? MinimumControlTemperature { get; set; }

		[System.Text.Json.Serialization.JsonPropertyName ("max_control_temp")]
		public int? MaximumControlTemperature { get; set; }

		[System.Text.Json.Serialization.JsonPropertyName ("temp_offset")]
		public int? TemperatureOffset { get; set; }

		[System.Text.Json.Serialization.JsonPropertyName ("child_protection")]
		public bool? ChildProtection { get; set; }

		[System.Text.Json.Serialization.JsonPropertyName ("trv_states")]
		public List<string>? TrvStates { get; set; }

		[System.Text.Json.Serialization.JsonPropertyName ("device_on")]
		public bool? DeviceOn { get; set; }
		[System.Text.Json.Serialization.JsonPropertyName ("responses")]
		public List<SmartMethodResponseDto>? Responses { get; set; }

		internal SmartChildDeviceDto Overlay (SmartChildDeviceDto update)
			{
			var merged = (SmartChildDeviceDto)MemberwiseClone ();
			merged.DeviceId = update.DeviceId ?? DeviceId;
			merged.Nickname = update.Nickname ?? Nickname;
			merged.Model = update.Model ?? Model;
			merged.Category = update.Category ?? Category;
			merged.Type = update.Type ?? Type;
			merged.FirmwareVersion = update.FirmwareVersion ?? FirmwareVersion;
			merged.SignalLevel = update.SignalLevel ?? SignalLevel;
			merged.Rssi = update.Rssi ?? Rssi;
			merged.Status = update.Status ?? Status;
			merged.BatteryPercentage = update.BatteryPercentage ?? BatteryPercentage;
			merged.AtLowBattery = update.AtLowBattery ?? AtLowBattery;
			merged.IsLowBattery = update.IsLowBattery ?? IsLowBattery;
			merged.CurrentTemperature = update.CurrentTemperature ?? CurrentTemperature;
			merged.CurrentTemperatureException = update.CurrentTemperatureException ?? CurrentTemperatureException;
			merged.CurrentHumidity = update.CurrentHumidity ?? CurrentHumidity;
			merged.CurrentHumidityException = update.CurrentHumidityException ?? CurrentHumidityException;
			merged.TemperatureUnit = update.TemperatureUnit ?? TemperatureUnit;
			merged.ReportInterval = update.ReportInterval ?? ReportInterval;
			merged.Detected = update.Detected ?? Detected;
			merged.Open = update.Open ?? Open;
			merged.InAlarm = update.InAlarm ?? InAlarm;
			merged.WaterLeakStatus = update.WaterLeakStatus ?? WaterLeakStatus;
			merged.TriggerTimestamp = update.TriggerTimestamp ?? TriggerTimestamp;
			merged.DoubleClickInfo = update.DoubleClickInfo ?? DoubleClickInfo;
			merged.TriggerLogs = update.TriggerLogs ?? TriggerLogs;
			merged.ComfortTemperatureConfig = update.ComfortTemperatureConfig ?? ComfortTemperatureConfig;
			merged.ComfortHumidityConfig = update.ComfortHumidityConfig ?? ComfortHumidityConfig;
			merged.FrostProtection = update.FrostProtection ?? FrostProtection;
			merged.FrostProtectionOn = update.FrostProtectionOn ?? FrostProtectionOn;
			merged.TargetTemperature = update.TargetTemperature ?? TargetTemperature;
			merged.MinimumControlTemperature = update.MinimumControlTemperature ?? MinimumControlTemperature;
			merged.MaximumControlTemperature = update.MaximumControlTemperature ?? MaximumControlTemperature;
			merged.TemperatureOffset = update.TemperatureOffset ?? TemperatureOffset;
			merged.ChildProtection = update.ChildProtection ?? ChildProtection;
			merged.TrvStates = update.TrvStates ?? TrvStates;
			merged.DeviceOn = update.DeviceOn ?? DeviceOn;
			return merged;
			}
		}

	internal sealed class SmartChildComponentDto
		{
		[System.Text.Json.Serialization.JsonPropertyName ("device_id")]
		public string? DeviceId { get; set; }

		[System.Text.Json.Serialization.JsonPropertyName ("component_list")]
		public List<SmartComponentDto>? ComponentList { get; set; }
		}

	internal sealed class SmartChildSetupCategoryDto
		{
		[System.Text.Json.Serialization.JsonPropertyName ("category")]
		public string? Category { get; set; }
		}

	internal sealed class SmartScannedChildDeviceListDto
		{
		[System.Text.Json.Serialization.JsonPropertyName ("child_device_list")]
		public List<SmartScannedChildDeviceDto>? ChildDeviceList { get; set; }
		}

	internal sealed class SmartScannedChildDeviceDto
		{
		[System.Text.Json.Serialization.JsonPropertyName ("device_id")]
		public string? DeviceId { get; set; }

		[System.Text.Json.Serialization.JsonPropertyName ("device_model")]
		public string? DeviceModel { get; set; }

		[System.Text.Json.Serialization.JsonPropertyName ("category")]
		public string? Category { get; set; }
		}

	internal sealed class SmartDoubleClickInfoDto
		{
		[System.Text.Json.Serialization.JsonPropertyName ("enable")]
		public bool? Enable { get; set; }
		}

	internal sealed class SmartTriggerLogListDto
		{
		[System.Text.Json.Serialization.JsonPropertyName ("logs")]
		public List<SmartTriggerLogDto>? Logs { get; set; }
		}

	internal sealed class SmartTriggerLogDto
		{
		[System.Text.Json.Serialization.JsonPropertyName ("id")]
		public int? Id { get; set; }

		[System.Text.Json.Serialization.JsonPropertyName ("event")]
		public string? Event { get; set; }

		[System.Text.Json.Serialization.JsonPropertyName ("eventId")]
		public string? EventId { get; set; }

		[System.Text.Json.Serialization.JsonPropertyName ("timestamp")]
		public long? Timestamp { get; set; }
		}

	internal sealed class SmartComfortValueConfigDto
		{
		[System.Text.Json.Serialization.JsonPropertyName ("min_value")]
		public double? MinValue { get; set; }

		[System.Text.Json.Serialization.JsonPropertyName ("max_value")]
		public double? MaxValue { get; set; }
		}

	internal sealed class SmartFrostProtectionDto
		{
		[System.Text.Json.Serialization.JsonPropertyName ("min_temp")]
		public int? MinimumTemperature { get; set; }

		[System.Text.Json.Serialization.JsonPropertyName ("temp_unit")]
		public string? TemperatureUnit { get; set; }
		}

	internal sealed class SmartCloudConnectStateDto
		{
		[System.Text.Json.Serialization.JsonPropertyName ("status")]
		public int? Status { get; set; }
		}

	internal sealed class SmartAutoUpdateInfoDto
		{
		[System.Text.Json.Serialization.JsonPropertyName ("enable")]
		public bool? Enable { get; set; }
		}

	internal sealed class SmartLatestFirmwareDto
		{
		[System.Text.Json.Serialization.JsonPropertyName ("type")]
		public int? Type { get; set; }

		[System.Text.Json.Serialization.JsonPropertyName ("fw_ver")]
		public string? FirmwareVersion { get; set; }
		}

	internal sealed class SmartAutoOffConfigDto
		{
		[System.Text.Json.Serialization.JsonPropertyName ("enable")]
		public bool? Enable { get; set; }

		[System.Text.Json.Serialization.JsonPropertyName ("delay_min")]
		public int? DelayMinutes { get; set; }
		}

	internal sealed class SmartEnergyUsageDto
		{
		[System.Text.Json.Serialization.JsonPropertyName ("current_power")]
		public double? CurrentPower { get; set; }

		[System.Text.Json.Serialization.JsonPropertyName ("today_energy")]
		public double? TodayEnergyWattHours { get; set; }

		[System.Text.Json.Serialization.JsonPropertyName ("month_energy")]
		public double? MonthEnergyWattHours { get; set; }
		}

	internal sealed class SmartCurrentPowerDto
		{
		[System.Text.Json.Serialization.JsonPropertyName ("current_power")]
		public double? CurrentPowerWatts { get; set; }
		}

	internal sealed class SmartEmeterDataDto
		{
		[System.Text.Json.Serialization.JsonPropertyName ("power_mw")]
		public double? PowerMilliwatts { get; set; }

		[System.Text.Json.Serialization.JsonPropertyName ("voltage_mv")]
		public double? VoltageMillivolts { get; set; }

		[System.Text.Json.Serialization.JsonPropertyName ("current_ma")]
		public double? CurrentMilliamps { get; set; }
		}

	internal sealed class SmartLedInfoDto
		{
		[System.Text.Json.Serialization.JsonPropertyName ("led_rule")]
		public string? LedRule { get; set; }

		[System.Text.Json.Serialization.JsonPropertyName ("start_time")]
		public int? StartTime { get; set; }

		[System.Text.Json.Serialization.JsonPropertyName ("end_time")]
		public int? EndTime { get; set; }

		[System.Text.Json.Serialization.JsonPropertyName ("night_mode_type")]
		public string? NightModeType { get; set; }

		[System.Text.Json.Serialization.JsonPropertyName ("sunrise_offset")]
		public int? SunriseOffset { get; set; }

		[System.Text.Json.Serialization.JsonPropertyName ("sunset_offset")]
		public int? SunsetOffset { get; set; }
		}

	internal sealed class SmartDeviceTimeDto
		{
		[System.Text.Json.Serialization.JsonPropertyName ("timestamp")]
		public long? Timestamp { get; set; }

		[System.Text.Json.Serialization.JsonPropertyName ("time_diff")]
		public int? TimeDifferenceMinutes { get; set; }

		[System.Text.Json.Serialization.JsonPropertyName ("region")]
		public string? Region { get; set; }
		}

	internal sealed class SmartMatterSetupDto
		{
		[System.Text.Json.Serialization.JsonPropertyName ("setup_code")]
		public string? SetupCode { get; set; }

		[System.Text.Json.Serialization.JsonPropertyName ("setup_payload")]
		public string? SetupPayload { get; set; }
		}

	internal sealed class SmartHomeKitInfoDto
		{
		[System.Text.Json.Serialization.JsonPropertyName ("mfi_setup_code")]
		public string? SetupCode { get; set; }
		}

	internal sealed class SmartChildLockInfoDto
		{
		[System.Text.Json.Serialization.JsonPropertyName ("child_lock_status")]
		public bool? ChildLockStatus { get; set; }
		}

	internal sealed class SmartAlarmInfoDto
		{
		[System.Text.Json.Serialization.JsonPropertyName ("in_alarm")]
		public bool? InAlarm { get; set; }

		[System.Text.Json.Serialization.JsonPropertyName ("alarm")]
		public bool? Alarm { get; set; }

		[System.Text.Json.Serialization.JsonPropertyName ("guard_on")]
		public bool? GuardOn { get; set; }

		[System.Text.Json.Serialization.JsonPropertyName ("alarm_source")]
		public string? AlarmSource { get; set; }

		[System.Text.Json.Serialization.JsonPropertyName ("guard_mode")]
		public string? GuardMode { get; set; }

		[System.Text.Json.Serialization.JsonPropertyName ("alarm_type")]
		public string? AlarmType { get; set; }

		[System.Text.Json.Serialization.JsonPropertyName ("alarm_sound")]
		public string? AlarmSound { get; set; }

		[System.Text.Json.Serialization.JsonPropertyName ("type")]
		public string? Type { get; set; }

		[System.Text.Json.Serialization.JsonPropertyName ("alarm_volume")]
		public string? AlarmVolume { get; set; }

		[System.Text.Json.Serialization.JsonPropertyName ("volume")]
		public string? Volume { get; set; }

		[System.Text.Json.Serialization.JsonPropertyName ("alarm_volume_level")]
		public int? AlarmVolumeLevel { get; set; }

		[System.Text.Json.Serialization.JsonPropertyName ("alarm_duration")]
		public int? AlarmDuration { get; set; }

		[System.Text.Json.Serialization.JsonPropertyName ("duration")]
		public int? Duration { get; set; }
		}

	internal sealed class SmartPresetRulesDto
		{
		[System.Text.Json.Serialization.JsonPropertyName ("states")]
		public List<LegacyLightPresetDto>? States { get; set; }

		[System.Text.Json.Serialization.JsonPropertyName ("brightness")]
		public List<int>? BrightnessLevels { get; set; }
		}

	internal sealed class SmartOnOffGraduallyInfoDto
		{
		[System.Text.Json.Serialization.JsonPropertyName ("enable")]
		public bool? Enable { get; set; }

		[System.Text.Json.Serialization.JsonPropertyName ("on_state")]
		public SmartOnOffGraduallyStateDto? OnState { get; set; }

		[System.Text.Json.Serialization.JsonPropertyName ("off_state")]
		public SmartOnOffGraduallyStateDto? OffState { get; set; }
		}

	internal sealed class SmartOnOffGraduallyStateDto
		{
		[System.Text.Json.Serialization.JsonPropertyName ("duration")]
		public int? Duration { get; set; }

		[System.Text.Json.Serialization.JsonPropertyName ("enable")]
		public bool? Enable { get; set; }

		[System.Text.Json.Serialization.JsonPropertyName ("max_duration")]
		public int? MaximumDuration { get; set; }
		}

	internal sealed class SmartDynamicLightEffectRulesDto
		{
		[System.Text.Json.Serialization.JsonPropertyName ("rule_list")]
		public List<SmartDynamicLightEffectRuleDto>? RuleList { get; set; }

		[System.Text.Json.Serialization.JsonPropertyName ("enable")]
		public bool? Enable { get; set; }

		[System.Text.Json.Serialization.JsonPropertyName ("current_rule_id")]
		public string? CurrentRuleId { get; set; }
		}

	internal sealed class SmartDynamicLightEffectRuleDto
		{
		[System.Text.Json.Serialization.JsonPropertyName ("id")]
		public string? Id { get; set; }

		[System.Text.Json.Serialization.JsonPropertyName ("scene_name")]
		public string? SceneName { get; set; }

		[System.Text.Json.Serialization.JsonPropertyName ("color_status_list")]
		public List<List<int>>? ColorStatusList { get; set; }
		}

	internal sealed class LegacyResponseDto
		{
		[System.Text.Json.Serialization.JsonPropertyName ("system")]
		public LegacySystemModuleDto? System
			{
			get; set;
			}

		[System.Text.Json.Serialization.JsonPropertyName ("emeter")]
		public LegacyEmeterModuleDto? Emeter
			{
			get; set;
			}

		[System.Text.Json.Serialization.JsonPropertyName ("smartlife.iot.common.emeter")]
		public LegacyEmeterModuleDto? SmartEmeter
			{
			get; set;
			}

		[System.Text.Json.Serialization.JsonPropertyName ("count_down")]
		public LegacyRuleModuleDto? CountDown
			{
			get; set;
			}

		[System.Text.Json.Serialization.JsonPropertyName ("countdown")]
		public LegacyRuleModuleDto? BulbCountDown
			{
			get; set;
			}

		[System.Text.Json.Serialization.JsonPropertyName ("schedule")]
		public LegacyRuleModuleDto? Schedule
			{
			get; set;
			}

		[System.Text.Json.Serialization.JsonPropertyName ("smartlife.iot.common.schedule")]
		public LegacyRuleModuleDto? SmartSchedule
			{
			get; set;
			}

		[System.Text.Json.Serialization.JsonPropertyName ("anti_theft")]
		public LegacyRuleModuleDto? AntiTheft
			{
			get; set;
			}

		[System.Text.Json.Serialization.JsonPropertyName ("smartlife.iot.common.anti_theft")]
		public LegacyRuleModuleDto? SmartAntiTheft
			{
			get; set;
			}

		[System.Text.Json.Serialization.JsonPropertyName ("time")]
		public LegacyTimeModuleDto? Time
			{
			get; set;
			}

		[System.Text.Json.Serialization.JsonPropertyName ("smartlife.iot.common.timesetting")]
		public LegacyTimeModuleDto? SmartTime
			{
			get; set;
			}

		[System.Text.Json.Serialization.JsonPropertyName ("cnCloud")]
		public LegacyCloudModuleDto? Cloud
			{
			get; set;
			}

		[System.Text.Json.Serialization.JsonPropertyName ("smartlife.iot.common.cloud")]
		public LegacyCloudModuleDto? SmartCloud
			{
			get; set;
			}

		[System.Text.Json.Serialization.JsonPropertyName ("smartlife.iot.homekit")]
		public LegacyHomeKitModuleDto? HomeKit
			{
			get; set;
			}

		internal void Merge (LegacyResponseDto update)
			{
			if (System is not null && update.System is not null) System.Merge (update.System);
			else System = update.System ?? System;
			if (Emeter is not null && update.Emeter is not null) Emeter.Merge (update.Emeter);
			else Emeter = update.Emeter ?? Emeter;
			if (SmartEmeter is not null && update.SmartEmeter is not null) SmartEmeter.Merge (update.SmartEmeter);
			else SmartEmeter = update.SmartEmeter ?? SmartEmeter;
			if (CountDown is not null && update.CountDown is not null) CountDown.Merge (update.CountDown);
			else CountDown = update.CountDown ?? CountDown;
			if (BulbCountDown is not null && update.BulbCountDown is not null) BulbCountDown.Merge (update.BulbCountDown);
			else BulbCountDown = update.BulbCountDown ?? BulbCountDown;
			if (Schedule is not null && update.Schedule is not null) Schedule.Merge (update.Schedule);
			else Schedule = update.Schedule ?? Schedule;
			if (SmartSchedule is not null && update.SmartSchedule is not null) SmartSchedule.Merge (update.SmartSchedule);
			else SmartSchedule = update.SmartSchedule ?? SmartSchedule;
			if (AntiTheft is not null && update.AntiTheft is not null) AntiTheft.Merge (update.AntiTheft);
			else AntiTheft = update.AntiTheft ?? AntiTheft;
			if (SmartAntiTheft is not null && update.SmartAntiTheft is not null) SmartAntiTheft.Merge (update.SmartAntiTheft);
			else SmartAntiTheft = update.SmartAntiTheft ?? SmartAntiTheft;
			if (Time is not null && update.Time is not null) Time.Merge (update.Time);
			else Time = update.Time ?? Time;
			if (SmartTime is not null && update.SmartTime is not null) SmartTime.Merge (update.SmartTime);
			else SmartTime = update.SmartTime ?? SmartTime;
			if (Cloud is not null && update.Cloud is not null) Cloud.Merge (update.Cloud);
			else Cloud = update.Cloud ?? Cloud;
			if (SmartCloud is not null && update.SmartCloud is not null) SmartCloud.Merge (update.SmartCloud);
			else SmartCloud = update.SmartCloud ?? SmartCloud;
			if (HomeKit is not null && update.HomeKit is not null) HomeKit.Merge (update.HomeKit);
			else HomeKit = update.HomeKit ?? HomeKit;
			}
		}

	internal sealed class LegacyTimeModuleDto
		{
		[System.Text.Json.Serialization.JsonPropertyName ("get_time")]
		public LegacyTimeInfoDto? GetTime { get; set; }

		[System.Text.Json.Serialization.JsonPropertyName ("get_timezone")]
		public LegacyTimezoneInfoDto? GetTimezone { get; set; }

		internal void Merge (LegacyTimeModuleDto update)
			{
			if (GetTime is not null && update.GetTime is not null) GetTime.Merge (update.GetTime);
			else GetTime = update.GetTime ?? GetTime;
			if (GetTimezone is not null && update.GetTimezone is not null) GetTimezone.Merge (update.GetTimezone);
			else GetTimezone = update.GetTimezone ?? GetTimezone;
			}
		}

	internal sealed class LegacyTimeInfoDto
		{
		[System.Text.Json.Serialization.JsonPropertyName ("year")]
		public int? Year { get; set; }

		[System.Text.Json.Serialization.JsonPropertyName ("month")]
		public int? Month { get; set; }

		[System.Text.Json.Serialization.JsonPropertyName ("mday")]
		public int? Day { get; set; }

		[System.Text.Json.Serialization.JsonPropertyName ("hour")]
		public int? Hour { get; set; }

		[System.Text.Json.Serialization.JsonPropertyName ("min")]
		public int? Minute { get; set; }

		[System.Text.Json.Serialization.JsonPropertyName ("sec")]
		public int? Second { get; set; }

		internal void Merge (LegacyTimeInfoDto update)
			{
			Year = update.Year ?? Year;
			Month = update.Month ?? Month;
			Day = update.Day ?? Day;
			Hour = update.Hour ?? Hour;
			Minute = update.Minute ?? Minute;
			Second = update.Second ?? Second;
			}
		}

	internal sealed class LegacyTimezoneInfoDto
		{
		[System.Text.Json.Serialization.JsonPropertyName ("index")]
		public int? Index { get; set; }

		internal void Merge (LegacyTimezoneInfoDto update)
			{
			Index = update.Index ?? Index;
			}
		}

	internal sealed class LegacyCloudModuleDto
		{
		[System.Text.Json.Serialization.JsonPropertyName ("get_info")]
		public LegacyCloudInfoDto? GetInfo { get; set; }

		internal void Merge (LegacyCloudModuleDto update)
			{
			if (GetInfo is not null && update.GetInfo is not null) GetInfo.Merge (update.GetInfo);
			else GetInfo = update.GetInfo ?? GetInfo;
			}
		}

	internal sealed class LegacyCloudInfoDto
		{
		[System.Text.Json.Serialization.JsonPropertyName ("binded")]
		public int? Binded { get; set; }

		[System.Text.Json.Serialization.JsonPropertyName ("cld_connection")]
		public int? CloudConnection { get; set; }

		[System.Text.Json.Serialization.JsonPropertyName ("server")]
		public string? Server { get; set; }

		[System.Text.Json.Serialization.JsonPropertyName ("username")]
		public string? UserName { get; set; }

		internal void Merge (LegacyCloudInfoDto update)
			{
			Binded = update.Binded ?? Binded;
			CloudConnection = update.CloudConnection ?? CloudConnection;
			Server = update.Server ?? Server;
			UserName = update.UserName ?? UserName;
			}
		}

	internal sealed class LegacyHomeKitModuleDto
		{
		[System.Text.Json.Serialization.JsonPropertyName ("setup_info_get")]
		public LegacyHomeKitInfoDto? SetupInfoGet { get; set; }

		internal void Merge (LegacyHomeKitModuleDto update)
			{
			if (SetupInfoGet is not null && update.SetupInfoGet is not null) SetupInfoGet.Merge (update.SetupInfoGet);
			else SetupInfoGet = update.SetupInfoGet ?? SetupInfoGet;
			}
		}

	internal sealed class LegacyHomeKitInfoDto
		{
		[System.Text.Json.Serialization.JsonPropertyName ("setup_code")]
		public string? SetupCode { get; set; }

		[System.Text.Json.Serialization.JsonPropertyName ("setup_payload")]
		public string? SetupPayload { get; set; }

		internal void Merge (LegacyHomeKitInfoDto update)
			{
			SetupCode = update.SetupCode ?? SetupCode;
			SetupPayload = update.SetupPayload ?? SetupPayload;
			}
		}

	internal sealed class LegacyRuleModuleDto
		{
		[System.Text.Json.Serialization.JsonPropertyName ("get_rules")]
		public LegacyRuleListDto? GetRules
			{
			get; set;
			}

		internal void Merge (LegacyRuleModuleDto update)
			{
			if (GetRules is not null && update.GetRules is not null) GetRules.Merge (update.GetRules);
			else GetRules = update.GetRules ?? GetRules;
			}
		}

	internal sealed class LegacyRuleListDto
		{
		[System.Text.Json.Serialization.JsonPropertyName ("enable")]
		public int? Enable
			{
			get; set;
			}

		[System.Text.Json.Serialization.JsonPropertyName ("rule_list")]
		public List<LegacyRuleDto>? RuleList
			{
			get; set;
			}

		internal void Merge (LegacyRuleListDto update)
			{
			Enable = update.Enable ?? Enable;
			RuleList = update.RuleList ?? RuleList;
			}
		}

	internal sealed class LegacyRuleDto
		{
		[System.Text.Json.Serialization.JsonPropertyName ("id")]
		public string? Id
			{
			get; set;
			}

		[System.Text.Json.Serialization.JsonPropertyName ("name")]
		public string? Name
			{
			get; set;
			}

		[System.Text.Json.Serialization.JsonPropertyName ("enable")]
		public int? Enable
			{
			get; set;
			}

		[System.Text.Json.Serialization.JsonPropertyName ("wday")]
		public List<int>? WeekDays
			{
			get; set;
			}

		[System.Text.Json.Serialization.JsonPropertyName ("smin")]
		public int? StartMinute
			{
			get; set;
			}

		[System.Text.Json.Serialization.JsonPropertyName ("emin")]
		public int? EndMinute
			{
			get; set;
			}

		[System.Text.Json.Serialization.JsonPropertyName ("delay")]
		public int? DelaySeconds
			{
			get; set;
			}

		[System.Text.Json.Serialization.JsonPropertyName ("act")]
		public int? Action
			{
			get; set;
			}

		[System.Text.Json.Serialization.JsonPropertyName ("remain")]
		public int? RemainingSeconds
			{
			get; set;
			}

		[System.Text.Json.Serialization.JsonPropertyName ("latitude")]
		public int? Latitude
			{
			get; set;
			}

		[System.Text.Json.Serialization.JsonPropertyName ("longitude")]
		public int? Longitude
			{
			get; set;
			}

		internal void Merge (LegacyRuleDto update)
			{
			Id = update.Id ?? Id;
			Name = update.Name ?? Name;
			Enable = update.Enable ?? Enable;
			WeekDays = update.WeekDays ?? WeekDays;
			StartMinute = update.StartMinute ?? StartMinute;
			EndMinute = update.EndMinute ?? EndMinute;
			DelaySeconds = update.DelaySeconds ?? DelaySeconds;
			Action = update.Action ?? Action;
			RemainingSeconds = update.RemainingSeconds ?? RemainingSeconds;
			Latitude = update.Latitude ?? Latitude;
			Longitude = update.Longitude ?? Longitude;
			}
		}

	internal sealed class LegacySystemModuleDto
		{
		[System.Text.Json.Serialization.JsonPropertyName ("get_sysinfo")]
		public LegacySystemInfoDto? GetSystemInfo
			{
			get; set;
			}

		internal void Merge (LegacySystemModuleDto update)
			{
			if (GetSystemInfo is not null && update.GetSystemInfo is not null) GetSystemInfo.Merge (update.GetSystemInfo);
			else GetSystemInfo = update.GetSystemInfo ?? GetSystemInfo;
			}
		}

	internal sealed class LegacyEmeterModuleDto
		{
		[System.Text.Json.Serialization.JsonPropertyName ("get_realtime")]
		public LegacyEmeterRealtimeDto? GetRealtime
			{
			get; set;
			}

		[System.Text.Json.Serialization.JsonPropertyName ("get_daystat")]
		public LegacyEmeterDailyStatDto? GetDayStat
			{
			get; set;
			}

		[System.Text.Json.Serialization.JsonPropertyName ("get_monthstat")]
		public LegacyEmeterMonthlyStatDto? GetMonthStat
			{
			get; set;
			}

		internal void Merge (LegacyEmeterModuleDto update)
			{
			if (GetRealtime is not null && update.GetRealtime is not null) GetRealtime.Merge (update.GetRealtime);
			else GetRealtime = update.GetRealtime ?? GetRealtime;
			if (GetDayStat is not null && update.GetDayStat is not null) GetDayStat.Merge (update.GetDayStat);
			else GetDayStat = update.GetDayStat ?? GetDayStat;
			if (GetMonthStat is not null && update.GetMonthStat is not null) GetMonthStat.Merge (update.GetMonthStat);
			else GetMonthStat = update.GetMonthStat ?? GetMonthStat;
			}
		}

	internal sealed class LegacySystemInfoDto
		{
		[System.Text.Json.Serialization.JsonPropertyName ("alias")]
		public string? Alias
			{
			get; set;
			}

		[System.Text.Json.Serialization.JsonPropertyName ("nickname")]
		public string? Nickname
			{
			get; set;
			}

		[System.Text.Json.Serialization.JsonPropertyName ("model")]
		public string? Model
			{
			get; set;
			}

		[System.Text.Json.Serialization.JsonPropertyName ("device_model")]
		public string? DeviceModel
			{
			get; set;
			}

		[System.Text.Json.Serialization.JsonPropertyName ("deviceId")]
		public string? DeviceId
			{
			get; set;
			}

		[System.Text.Json.Serialization.JsonPropertyName ("device_id")]
		public string? DeviceIdUnderscore
			{
			get; set;
			}

		[System.Text.Json.Serialization.JsonPropertyName ("mac")]
		public string? Mac
			{
			get; set;
			}

		[System.Text.Json.Serialization.JsonPropertyName ("mic_mac")]
		public string? MicMac
			{
			get; set;
			}

		[System.Text.Json.Serialization.JsonPropertyName ("hw_ver")]
		public string? HardwareVersion
			{
			get; set;
			}

		[System.Text.Json.Serialization.JsonPropertyName ("hwVersion")]
		public string? HardwareVersionAlt
			{
			get; set;
			}

		[System.Text.Json.Serialization.JsonPropertyName ("sw_ver")]
		public string? SoftwareVersion
			{
			get; set;
			}

		[System.Text.Json.Serialization.JsonPropertyName ("swVersion")]
		public string? SoftwareVersionAlt
			{
			get; set;
			}

		[System.Text.Json.Serialization.JsonPropertyName ("type")]
		public string? Type
			{
			get; set;
			}

		[System.Text.Json.Serialization.JsonPropertyName ("mic_type")]
		public string? MicType
			{
			get; set;
			}

		[System.Text.Json.Serialization.JsonPropertyName ("device_type")]
		public string? DeviceType
			{
			get; set;
			}

		[System.Text.Json.Serialization.JsonPropertyName ("relay_state")]
		public int? RelayState
			{
			get; set;
			}

		[System.Text.Json.Serialization.JsonPropertyName ("device_on")]
		public bool? DeviceOn
			{
			get; set;
			}

		[System.Text.Json.Serialization.JsonPropertyName ("auto_off_status")]
		public string? AutoOffStatus
			{
			get; set;
			}

		[System.Text.Json.Serialization.JsonPropertyName ("auto_off_remain_time")]
		public int? AutoOffRemainTimeSeconds
			{
			get; set;
			}

		[System.Text.Json.Serialization.JsonPropertyName ("led_off")]
		public int? LedOff
			{
			get; set;
			}

		[System.Text.Json.Serialization.JsonPropertyName ("on_time")]
		public int? OnTimeSeconds
			{
			get; set;
			}

		[System.Text.Json.Serialization.JsonPropertyName ("rssi")]
		public int? Rssi
			{
			get; set;
			}

		[System.Text.Json.Serialization.JsonPropertyName ("children")]
		public List<LegacyChildDeviceDto>? Children
			{
			get; set;
			}

		[System.Text.Json.Serialization.JsonPropertyName ("light_state")]
		public LegacyLightStateDto? LightState
			{
			get; set;
			}

		[System.Text.Json.Serialization.JsonPropertyName ("preferred_state")]
		public List<LegacyLightPresetDto>? PreferredState
			{
			get; set;
			}

		internal void Merge (LegacySystemInfoDto update)
			{
			Alias = update.Alias ?? Alias;
			Nickname = update.Nickname ?? Nickname;
			Model = update.Model ?? Model;
			DeviceModel = update.DeviceModel ?? DeviceModel;
			DeviceId = update.DeviceId ?? DeviceId;
			DeviceIdUnderscore = update.DeviceIdUnderscore ?? DeviceIdUnderscore;
			Mac = update.Mac ?? Mac;
			MicMac = update.MicMac ?? MicMac;
			HardwareVersion = update.HardwareVersion ?? HardwareVersion;
			HardwareVersionAlt = update.HardwareVersionAlt ?? HardwareVersionAlt;
			SoftwareVersion = update.SoftwareVersion ?? SoftwareVersion;
			SoftwareVersionAlt = update.SoftwareVersionAlt ?? SoftwareVersionAlt;
			Type = update.Type ?? Type;
			MicType = update.MicType ?? MicType;
			DeviceType = update.DeviceType ?? DeviceType;
			RelayState = update.RelayState ?? RelayState;
			DeviceOn = update.DeviceOn ?? DeviceOn;
			AutoOffStatus = update.AutoOffStatus ?? AutoOffStatus;
			AutoOffRemainTimeSeconds = update.AutoOffRemainTimeSeconds ?? AutoOffRemainTimeSeconds;
			LedOff = update.LedOff ?? LedOff;
			OnTimeSeconds = update.OnTimeSeconds ?? OnTimeSeconds;
			Rssi = update.Rssi ?? Rssi;
			Children = update.Children ?? Children;
			if (LightState is not null && update.LightState is not null) LightState.Merge (update.LightState);
			else LightState = update.LightState ?? LightState;
			PreferredState = update.PreferredState ?? PreferredState;
			}
		}

	internal sealed class LegacyChildDeviceDto
		{
		[System.Text.Json.Serialization.JsonPropertyName ("id")]
		public string? Id
			{
			get; set;
			}

		[System.Text.Json.Serialization.JsonPropertyName ("device_id")]
		public string? DeviceId
			{
			get; set;
			}

		[System.Text.Json.Serialization.JsonPropertyName ("alias")]
		public string? Alias
			{
			get; set;
			}

		[System.Text.Json.Serialization.JsonPropertyName ("nickname")]
		public string? Nickname
			{
			get; set;
			}

		[System.Text.Json.Serialization.JsonPropertyName ("model")]
		public string? Model
			{
			get; set;
			}

		[System.Text.Json.Serialization.JsonPropertyName ("device_model")]
		public string? DeviceModel
			{
			get; set;
			}

		[System.Text.Json.Serialization.JsonPropertyName ("relay_state")]
		public int? RelayState
			{
			get; set;
			}

		[System.Text.Json.Serialization.JsonPropertyName ("state")]
		public int? State
			{
			get; set;
			}

		[System.Text.Json.Serialization.JsonPropertyName ("device_on")]
		public bool? DeviceOn
			{
			get; set;
			}

		internal void Merge (LegacyChildDeviceDto update)
			{
			Id = update.Id ?? Id;
			DeviceId = update.DeviceId ?? DeviceId;
			Alias = update.Alias ?? Alias;
			Nickname = update.Nickname ?? Nickname;
			Model = update.Model ?? Model;
			DeviceModel = update.DeviceModel ?? DeviceModel;
			RelayState = update.RelayState ?? RelayState;
			State = update.State ?? State;
			DeviceOn = update.DeviceOn ?? DeviceOn;
			}
		}

	internal sealed class LegacyLightStateDto
		{
		[System.Text.Json.Serialization.JsonPropertyName ("on_off")]
		public int? OnOff
			{
			get; set;
			}

		[System.Text.Json.Serialization.JsonPropertyName ("transition_period")]
		public int? TransitionPeriod
			{
			get; set;
			}

		[System.Text.Json.Serialization.JsonPropertyName ("brightness")]
		public int? Brightness
			{
			get; set;
			}

		[System.Text.Json.Serialization.JsonPropertyName ("color_temp")]
		public int? ColorTemperature
			{
			get; set;
			}

		[System.Text.Json.Serialization.JsonPropertyName ("hue")]
		public int? Hue
			{
			get; set;
			}

		[System.Text.Json.Serialization.JsonPropertyName ("saturation")]
		public int? Saturation
			{
			get; set;
			}

		[System.Text.Json.Serialization.JsonPropertyName ("dynamic_light_effect_enable")]
		public int? DynamicLightEffectEnable
			{
			get; set;
			}

		[System.Text.Json.Serialization.JsonPropertyName ("dynamic_light_effect_id")]
		public string? DynamicLightEffectId
			{
			get; set;
			}

		[System.Text.Json.Serialization.JsonPropertyName ("dynamic_light_effect_rule_list")]
		public List<LegacyDynamicLightEffectRuleDto>? DynamicLightEffectRuleList
			{
			get; set;
			}

		[System.Text.Json.Serialization.JsonPropertyName ("lighting_effect")]
		public LegacyLightingEffectDto? LightingEffect
			{
			get; set;
			}

		[System.Text.Json.Serialization.JsonPropertyName ("dft_on_state")]
		public LegacyLightStateDto? DefaultOnState
			{
			get; set;
			}

		internal void Merge (LegacyLightStateDto update)
			{
			OnOff = update.OnOff ?? OnOff;
			TransitionPeriod = update.TransitionPeriod ?? TransitionPeriod;
			Brightness = update.Brightness ?? Brightness;
			ColorTemperature = update.ColorTemperature ?? ColorTemperature;
			Hue = update.Hue ?? Hue;
			Saturation = update.Saturation ?? Saturation;
			DynamicLightEffectEnable = update.DynamicLightEffectEnable ?? DynamicLightEffectEnable;
			DynamicLightEffectId = update.DynamicLightEffectId ?? DynamicLightEffectId;
			DynamicLightEffectRuleList = update.DynamicLightEffectRuleList ?? DynamicLightEffectRuleList;
			if (LightingEffect is not null && update.LightingEffect is not null) LightingEffect.Merge (update.LightingEffect);
			else LightingEffect = update.LightingEffect ?? LightingEffect;
			if (DefaultOnState is not null && update.DefaultOnState is not null) DefaultOnState.Merge (update.DefaultOnState);
			else DefaultOnState = update.DefaultOnState ?? DefaultOnState;
			}
		}

	internal sealed class LegacyLightPresetDto
		{
		[System.Text.Json.Serialization.JsonPropertyName ("brightness")]
		public int? Brightness
			{
			get; set;
			}

		[System.Text.Json.Serialization.JsonPropertyName ("color_temp")]
		public int? ColorTemperature
			{
			get; set;
			}

		[System.Text.Json.Serialization.JsonPropertyName ("hue")]
		public int? Hue
			{
			get; set;
			}

		[System.Text.Json.Serialization.JsonPropertyName ("saturation")]
		public int? Saturation
			{
			get; set;
			}

		[System.Text.Json.Serialization.JsonPropertyName ("custom")]
		public int? Custom
			{
			get; set;
			}

		[System.Text.Json.Serialization.JsonPropertyName ("id")]
		public string? Id
			{
			get; set;
			}

		[System.Text.Json.Serialization.JsonPropertyName ("mode")]
		public int? Mode
			{
			get; set;
			}

		internal void Merge (LegacyLightPresetDto update)
			{
			Brightness = update.Brightness ?? Brightness;
			ColorTemperature = update.ColorTemperature ?? ColorTemperature;
			Hue = update.Hue ?? Hue;
			Saturation = update.Saturation ?? Saturation;
			Custom = update.Custom ?? Custom;
			Id = update.Id ?? Id;
			Mode = update.Mode ?? Mode;
			}
		}

	internal sealed class LegacyDynamicLightEffectRuleDto
		{
		[System.Text.Json.Serialization.JsonPropertyName ("id")]
		public string? Id
			{
			get; set;
			}

		[System.Text.Json.Serialization.JsonPropertyName ("name")]
		public string? Name
			{
			get; set;
			}

		internal void Merge (LegacyDynamicLightEffectRuleDto update)
			{
			Id = update.Id ?? Id;
			Name = update.Name ?? Name;
			}
		}

	internal sealed class LegacyLightingEffectDto
		{
		[System.Text.Json.Serialization.JsonPropertyName ("enable")]
		public int? Enable
			{
			get; set;
			}

		[System.Text.Json.Serialization.JsonPropertyName ("id")]
		public string? Id
			{
			get; set;
			}

		[System.Text.Json.Serialization.JsonPropertyName ("name")]
		public string? Name
			{
			get; set;
			}

		[System.Text.Json.Serialization.JsonPropertyName ("brightness")]
		public int? Brightness
			{
			get; set;
			}

		internal void Merge (LegacyLightingEffectDto update)
			{
			Enable = update.Enable ?? Enable;
			Id = update.Id ?? Id;
			Name = update.Name ?? Name;
			Brightness = update.Brightness ?? Brightness;
			}
		}

	internal sealed class LegacyEmeterRealtimeDto
		{
		[System.Text.Json.Serialization.JsonPropertyName ("power")]
		public double? Power
			{
			get; set;
			}

		[System.Text.Json.Serialization.JsonPropertyName ("power_mw")]
		public double? PowerMilliwatts
			{
			get; set;
			}

		[System.Text.Json.Serialization.JsonPropertyName ("voltage")]
		public double? Voltage
			{
			get; set;
			}

		[System.Text.Json.Serialization.JsonPropertyName ("voltage_mv")]
		public double? VoltageMillivolts
			{
			get; set;
			}

		[System.Text.Json.Serialization.JsonPropertyName ("current")]
		public double? Current
			{
			get; set;
			}

		[System.Text.Json.Serialization.JsonPropertyName ("current_ma")]
		public double? CurrentMilliamps
			{
			get; set;
			}

		[System.Text.Json.Serialization.JsonPropertyName ("total")]
		public double? Total
			{
			get; set;
			}

		[System.Text.Json.Serialization.JsonPropertyName ("total_wh")]
		public double? TotalWattHours
			{
			get; set;
			}

		[System.Text.Json.Serialization.JsonPropertyName ("energy")]
		public double? Energy
			{
			get; set;
			}

		[System.Text.Json.Serialization.JsonPropertyName ("energy_wh")]
		public double? EnergyWattHours
			{
			get; set;
			}

		internal void Merge (LegacyEmeterRealtimeDto update)
			{
			Power = update.Power ?? Power;
			PowerMilliwatts = update.PowerMilliwatts ?? PowerMilliwatts;
			Voltage = update.Voltage ?? Voltage;
			VoltageMillivolts = update.VoltageMillivolts ?? VoltageMillivolts;
			Current = update.Current ?? Current;
			CurrentMilliamps = update.CurrentMilliamps ?? CurrentMilliamps;
			Total = update.Total ?? Total;
			TotalWattHours = update.TotalWattHours ?? TotalWattHours;
			Energy = update.Energy ?? Energy;
			EnergyWattHours = update.EnergyWattHours ?? EnergyWattHours;
			}
		}

	internal sealed class LegacyEmeterDailyStatDto
		{
		[System.Text.Json.Serialization.JsonPropertyName ("day_list")]
		public List<LegacyEmeterDayStatEntryDto>? DayList { get; set; }

		internal void Merge (LegacyEmeterDailyStatDto update)
			{
			DayList = update.DayList ?? DayList;
			}
		}

	internal sealed class LegacyEmeterMonthlyStatDto
		{
		[System.Text.Json.Serialization.JsonPropertyName ("month_list")]
		public List<LegacyEmeterMonthStatEntryDto>? MonthList { get; set; }

		internal void Merge (LegacyEmeterMonthlyStatDto update)
			{
			MonthList = update.MonthList ?? MonthList;
			}
		}

	internal sealed class LegacyEmeterDayStatEntryDto
		{
		[System.Text.Json.Serialization.JsonPropertyName ("day")]
		public int? Day { get; set; }

		[System.Text.Json.Serialization.JsonPropertyName ("energy")]
		public double? EnergyKilowattHours { get; set; }

		[System.Text.Json.Serialization.JsonPropertyName ("energy_wh")]
		public double? EnergyWattHours { get; set; }

		internal void Merge (LegacyEmeterDayStatEntryDto update)
			{
			Day = update.Day ?? Day;
			EnergyKilowattHours = update.EnergyKilowattHours ?? EnergyKilowattHours;
			EnergyWattHours = update.EnergyWattHours ?? EnergyWattHours;
			}
		}

	internal sealed class LegacyEmeterMonthStatEntryDto
		{
		[System.Text.Json.Serialization.JsonPropertyName ("month")]
		public int? Month { get; set; }

		[System.Text.Json.Serialization.JsonPropertyName ("energy")]
		public double? EnergyKilowattHours { get; set; }

		[System.Text.Json.Serialization.JsonPropertyName ("energy_wh")]
		public double? EnergyWattHours { get; set; }

		internal void Merge (LegacyEmeterMonthStatEntryDto update)
			{
			Month = update.Month ?? Month;
			EnergyKilowattHours = update.EnergyKilowattHours ?? EnergyKilowattHours;
			EnergyWattHours = update.EnergyWattHours ?? EnergyWattHours;
			}
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
		payload.AsSpan ().CopyTo (framedPayload.AsSpan (4));
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
