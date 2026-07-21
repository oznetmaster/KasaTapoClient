// Copyright © 2026 Neil Colvin.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// Behavior modeled after the independent python-kasa project (https://github.com/python-kasa/python-kasa)
// for protocol/compatibility reference only; no python-kasa source was copied. See ATTRIBUTIONS.md.

using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using System.Threading;
using System.Threading.Tasks;

using KasaTapoClient.Internal;

namespace KasaTapoClient;

/// <summary>
/// Represents a single TP-Link Kasa or Tapo device.
/// </summary>
public sealed partial class KasaDevice : IDisposable
	{
	private readonly IDeviceTransport _transport;
	private readonly SemaphoreSlim _operationLock = new (1, 1);
	private bool _disposed;
	private IReadOnlyList<DeviceFeature> _features = Array.Empty<DeviceFeature> ();
	private IReadOnlyDictionary<string, int> _smartComponentVersions = new Dictionary<string, int> (StringComparer.Ordinal);
	private static readonly JObject SMART_GET_TRIGGER_LOGS_PARAMETERS = new ()
		{
		["start_id"] = 0,
		};
	private static readonly IReadOnlyList<SmartRefreshContribution> SMART_PARENT_REFRESH_DEFINITIONS =
		[
		new SmartRefreshContribution ("cloud_connect", KasaCommands.SMART_GET_CONNECT_CLOUD_STATE_METHOD),
		new SmartRefreshContribution ("firmware", KasaCommands.SMART_GET_AUTO_UPDATE_INFO_METHOD, minimumSupportedVersion: 2),
		new SmartRefreshContribution ("energy_monitoring", KasaCommands.SMART_GET_ENERGY_USAGE_METHOD),
		new SmartRefreshContribution ("energy_monitoring", KasaCommands.SMART_GET_CURRENT_POWER_METHOD, minimumSupportedVersion: 2),
		new SmartRefreshContribution ("energy_monitoring", KasaCommands.SMART_GET_EMETER_DATA_METHOD, minimumSupportedVersion: 2),
		new SmartRefreshContribution ("energy_monitoring", KasaCommands.SMART_GET_EMETER_VGAIN_IGAIN_METHOD, minimumSupportedVersion: 2),
		new SmartRefreshContribution ("auto_off", KasaCommands.SMART_GET_AUTO_OFF_CONFIG_METHOD, static () => new JObject { ["start_index"] = 0 }),
		new SmartRefreshContribution ("led", KasaCommands.SMART_GET_LED_INFO_METHOD),
		new SmartRefreshContribution ("time", KasaCommands.SMART_GET_DEVICE_TIME_METHOD),
		new SmartRefreshContribution ("matter", KasaCommands.SMART_GET_MATTER_SETUP_INFO_METHOD),
		new SmartRefreshContribution ("homekit", KasaCommands.SMART_GET_HOMEKIT_INFO_METHOD),
		new SmartRefreshContribution ("button_and_led", KasaCommands.SMART_GET_CHILD_LOCK_INFO_METHOD),
		new SmartRefreshContribution ("alarm", KasaCommands.SMART_GET_ALARM_CONFIG_METHOD),
		new SmartRefreshContribution ("preset", KasaCommands.SMART_GET_PRESET_RULES_METHOD, minimumSupportedVersion: 1),
		new SmartRefreshContribution ("on_off_gradually", KasaCommands.SMART_GET_ON_OFF_GRADUALLY_INFO_METHOD),
		new SmartRefreshContribution ("light_effect", KasaCommands.SMART_GET_DYNAMIC_LIGHT_EFFECT_RULES_METHOD, static () => new JObject { ["start_index"] = 0 }),
		];
	private static readonly IReadOnlyDictionary<string, SmartChildRefreshDefinition> SMART_CHILD_REFRESH_DEFINITIONS =
		new Dictionary<string, SmartChildRefreshDefinition> (StringComparer.OrdinalIgnoreCase)
			{
			["trigger_log"] = new SmartChildRefreshDefinition (KasaCommands.SMART_GET_TRIGGER_LOGS_METHOD, static () => (JObject)SMART_GET_TRIGGER_LOGS_PARAMETERS.DeepClone (), "trigger_logs"),
			["double_click"] = new SmartChildRefreshDefinition (KasaCommands.SMART_GET_DOUBLE_CLICK_INFO_METHOD, static () => null, "double_click_info"),
			["humidity"] = new SmartChildRefreshDefinition (KasaCommands.SMART_GET_COMFORT_HUMIDITY_CONFIG_METHOD, static () => null, "comfort_humidity_config"),
			["frost_protection"] = new SmartChildRefreshDefinition (KasaCommands.SMART_GET_FROST_PROTECTION_METHOD, static () => null, "frost_protection"),
			};

	internal KasaDevice (DeviceConfiguration configuration)
		: this (configuration, null)
		{
		}

	internal KasaDevice (DeviceConfiguration configuration, IDeviceTransport? transport)
		{
		Configuration = configuration;
		_transport = transport ?? DeviceTransportFactory.Create (configuration);
		System = new SystemModule (this);
		Firmware = new FirmwareModule (this);
		Cloud = new CloudModule (this);
		Time = new TimeModule (this);
		Matter = new MatterModule (this);
		HomeKit = new HomeKitModule (this);
		AutoOff = new AutoOffModule (this);
		Led = new LedModule (this);
		ChildLock = new ChildLockModule (this);
		Energy = new EnergyModule (this);
		Usage = new UsageModule (this);
		Light = new LightModule (this);
		LightPreset = new LightPresetModule (this);
		LightTransition = new LightTransitionModule (this);
		LightStripEffect = new LightStripEffectModule (this);
		Alarm = new AlarmModule (this);
		OverheatProtection = new OverheatProtectionModule (this);
		PowerProtection = new PowerProtectionModule (this);
		Fan = new FanModule (this);
		Speaker = new SpeakerModule (this);
		Countdown = new CountdownModule (this);
		Rules = new RuleModule (this);
		ChildSetup = new ChildSetupModule (this);
		}

	/// <summary>
	/// Gets the connection settings for the device.
	/// </summary>
	public DeviceConfiguration Configuration
		{
		get;
		}

	/// <summary>
	/// Gets the current device host.
	/// </summary>
	public string Host => Configuration.Host;

	/// <summary>
	/// Gets the module-style system wrapper.
	/// </summary>
	public SystemModule System
		{
		get;
		}

	/// <summary>
	/// Gets the module-style energy wrapper.
	/// </summary>
	public EnergyModule Energy
		{
		get;
		}

	/// <summary>
	/// Gets the module-style usage wrapper.
	/// </summary>
	public UsageModule Usage
		{
		get;
		}

	/// <summary>
	/// Gets the module-style firmware wrapper.
	/// </summary>
	public FirmwareModule Firmware
		{
		get;
		}

	/// <summary>
	/// Gets the module-style cloud wrapper.
	/// </summary>
	public CloudModule Cloud
		{
		get;
		}

	/// <summary>
	/// Gets the module-style time wrapper.
	/// </summary>
	public TimeModule Time
		{
		get;
		}

	/// <summary>
	/// Gets the module-style Matter wrapper.
	/// </summary>
	public MatterModule Matter
		{
		get;
		}

	/// <summary>
	/// Gets the module-style HomeKit wrapper.
	/// </summary>
	public HomeKitModule HomeKit
		{
		get;
		}

	/// <summary>
	/// Gets the module-style auto-off wrapper.
	/// </summary>
	public AutoOffModule AutoOff
		{
		get;
		}

	/// <summary>
	/// Gets the module-style LED wrapper.
	/// </summary>
	public LedModule Led
		{
		get;
		}

	/// <summary>
	/// Gets the module-style child-lock wrapper.
	/// </summary>
	public ChildLockModule ChildLock
		{
		get;
		}

	/// <summary>
	/// Gets the module-style light wrapper.
	/// </summary>
	public LightModule Light
		{
		get;
		}

	/// <summary>
	/// Gets the module-style light preset wrapper.
	/// </summary>
	public LightPresetModule LightPreset
		{
		get;
		}

	/// <summary>
	/// Gets the module-style light transition wrapper.
	/// </summary>
	public LightTransitionModule LightTransition
		{
		get;
		}

	/// <summary>
	/// Gets the module-style light strip effect wrapper.
	/// </summary>
	public LightStripEffectModule LightStripEffect
		{
		get;
		}

	/// <summary>
	/// Gets the module-style alarm wrapper.
	/// </summary>
	public AlarmModule Alarm
		{
		get;
		}

	/// <summary>
	/// Gets the module-style overheat protection wrapper.
	/// </summary>
	public OverheatProtectionModule OverheatProtection
		{
		get;
		}

	/// <summary>
	/// Gets the module-style power protection wrapper.
	/// </summary>
	public PowerProtectionModule PowerProtection
		{
		get;
		}

	/// <summary>
	/// Gets the module-style fan wrapper.
	/// </summary>
	public FanModule Fan
		{
		get;
		}

	/// <summary>
	/// Gets the module-style speaker wrapper.
	/// </summary>
	public SpeakerModule Speaker
		{
		get;
		}

	/// <summary>
	/// Gets the module-style countdown wrapper.
	/// </summary>
	public CountdownModule Countdown
		{
		get;
		}

	/// <summary>
	/// Gets the module-style rules wrapper.
	/// </summary>
	public RuleModule Rules
		{
		get;
		}

	/// <summary>
	/// Gets the module-style child setup wrapper for smart hub devices.
	/// </summary>
	public ChildSetupModule ChildSetup
		{
		get;
		}

	/// <summary>
	/// Gets the most recent normalized system information, or <see langword="null" /> until <see cref="UpdateAsync" /> succeeds.
	/// </summary>
	public DeviceSystemInfo? SystemInfo
		{
		get; private set;
		}

	/// <summary>
	/// Gets the best known alias for the device.
	/// </summary>
	public string Alias => SystemInfo?.Alias ?? Host;

	/// <summary>
	/// Gets the most recent normalized energy usage information, when supported by the device.
	/// </summary>
	public EnergyUsage? EnergyUsage
		{
		get; private set;
		}

	/// <summary>
	/// Gets the most recent host RSSI value in dBm when the device reports it.
	/// </summary>
	public int? Rssi
		{
		get; private set;
		}

	/// <summary>
	/// Gets the derived timestamp for when the device was turned on, when reported.
	/// </summary>
	public DateTimeOffset? OnSince => TimeState?.LocalTime is DateTime localTime && SystemInfo?.OnTime is TimeSpan onTime
		&& IsOn == true
		? new DateTimeOffset (localTime).Subtract (onTime)
		: null;

	/// <summary>
	/// Gets the most recent normalized light state, when supported by the device.
	/// </summary>
	public LightState? LightState
		{
		get; private set;
		}

	/// <summary>
	/// Gets the most recent normalized light preset module state, when supported by the device.
	/// </summary>
	public LightPresetState? LightPresetState
		{
		get; private set;
		}

	/// <summary>
	/// Gets the most recent normalized light transition module state, when supported by the device.
	/// </summary>
	public LightTransitionState? LightTransitionState
		{
		get; private set;
		}

	/// <summary>
	/// Gets the most recent normalized light strip effect module state, when supported by the device.
	/// </summary>
	public LightStripEffectState? LightStripEffectState
		{
		get; private set;
		}

	/// <summary>
	/// Gets the most recent normalized alarm state, when supported by the device.
	/// </summary>
	public AlarmState? AlarmState
		{
		get; private set;
		}

	/// <summary>
	/// Gets the most recent normalized overheat protection state, when supported by the device.
	/// </summary>
	public OverheatProtectionState? OverheatProtectionState
		{
		get; private set;
		}

	/// <summary>
	/// Gets the most recent normalized power protection state, when supported by the device.
	/// </summary>
	public PowerProtectionState? PowerProtectionState
		{
		get; private set;
		}

	/// <summary>
	/// Gets the most recent normalized fan state, when supported by the device.
	/// </summary>
	public FanState? FanState
		{
		get; private set;
		}

	/// <summary>
	/// Gets the most recent normalized speaker state, when supported by the device.
	/// </summary>
	public SpeakerState? SpeakerState
		{
		get; private set;
		}

	/// <summary>
	/// Gets the most recent normalized rule module state, when supported by the device.
	/// </summary>
	public RuleModuleState? RuleState
		{
		get; private set;
		}

	/// <summary>
	/// Gets the most recent normalized firmware state, when supported by the device.
	/// </summary>
	public FirmwareState? FirmwareState
		{
		get; private set;
		}

	/// <summary>
	/// Gets the most recent normalized cloud-connection state, when supported by the device.
	/// </summary>
	public CloudConnectionState? CloudState
		{
		get; private set;
		}

	/// <summary>
	/// Gets the most recent normalized device-local time state, when supported by the device.
	/// </summary>
	public DeviceTimeState? TimeState
		{
		get; private set;
		}

	/// <summary>
	/// Gets the most recent Matter setup information, when supported by the device.
	/// </summary>
	public MatterSetupInfo? MatterSetup
		{
		get; private set;
		}

	/// <summary>
	/// Gets the most recent HomeKit setup information, when supported by the device.
	/// </summary>
	public HomeKitSetupInfo? HomeKitSetup
		{
		get; private set;
		}

	/// <summary>
	/// Gets the most recent auto-off state, when supported by the device.
	/// </summary>
	public AutoOffState? AutoOffState
		{
		get; private set;
		}

	/// <summary>
	/// Gets the most recent LED state, when supported by the device.
	/// </summary>
	public LedState? LedState
		{
		get; private set;
		}

	/// <summary>
	/// Gets the most recent child-lock state, when supported by the device.
	/// </summary>
	public ChildLockState? ChildLockState
		{
		get; private set;
		}

	/// <summary>
	/// Gets the most recent normalized light effect state, when supported by the device.
	/// </summary>
	public LightEffectState? LightEffect => LightState?.Effect;

	/// <summary>
	/// Gets the available light effects reported by the device.
	/// </summary>
	public IReadOnlyList<LightEffectDefinition> AvailableLightEffects => LightEffect?.AvailableEffects ?? Array.Empty<LightEffectDefinition> ();

	/// <summary>
	/// Gets a value indicating whether the device reports light-effect capability.
	/// </summary>
	public bool SupportsLightEffects => LightState?.SupportsEffects == true;

	/// <summary>
	/// Gets the inferred device family.
	/// </summary>
	public DeviceType DeviceType => SystemInfo?.DeviceType ?? DeviceType.Unknown;

	/// <summary>
	/// Gets a value indicating whether the device is currently on.
	/// </summary>
	public bool? IsOn => SystemInfo?.IsOn;

	/// <summary>
	/// Gets the child devices reported by the device.
	/// </summary>
	public IReadOnlyList<ChildDeviceInfo> Children => SystemInfo?.Children ?? Array.Empty<ChildDeviceInfo> ();

	/// <summary>
	/// Gets wrapper objects for child devices.
	/// </summary>
	public IReadOnlyList<ChildDevice> ChildDevices => CreateChildDevices ();

	/// <summary>
	/// Gets the current feature collection derived from the latest device state.
	/// </summary>
	public IReadOnlyList<DeviceFeature> Features => _features;

	/// <summary>
	/// Gets the negotiated smart component versions reported by the device.
	/// </summary>
	public IReadOnlyDictionary<string, int> SmartComponentVersions => _smartComponentVersions;

	/// <summary>
	/// Gets a value indicating whether <see cref="Dispose"/> has already been called on this instance.
	/// </summary>
	/// <remarks>
	/// This is primarily intended for callers (such as <see cref="Discover"/>'s shared device cache)
	/// that hold a long-lived reference to a device instance obtained from elsewhere and need to detect
	/// that it is no longer usable without having to catch <see cref="ObjectDisposedException"/>.
	/// </remarks>
	public bool IsDisposed => _disposed;

	/// <summary>
	/// Releases transport resources owned by the device.
	/// </summary>
	public void Dispose ()
		{
		if (_disposed)
			{
			return;
			}

		_disposed = true;

		if (_transport is IDisposableDeviceTransport disposableTransport)
			{
			disposableTransport.Dispose ();
			}

		_operationLock.Dispose ();
		}

	/// <summary>
	/// Refreshes <see cref="SystemInfo" /> by querying the device.
	/// </summary>
	/// <param name="cancellationToken">The cancellation token for the operation.</param>
	/// <returns>A task that completes when the device state has been refreshed.</returns>
	public Task UpdateAsync (CancellationToken cancellationToken = default) =>
		RunDeviceOperationAsync (UpdateCoreAsync, cancellationToken);

	private async Task UpdateCoreAsync (CancellationToken cancellationToken)
		{
		if (UsesSmartProtocol ())
			{
			await UpdateSmartAsync (cancellationToken).ConfigureAwait (false);
			return;
			}

		await UpdateLegacyAsync (cancellationToken).ConfigureAwait (false);
		}

	/// <summary>
	/// Refreshes <see cref="EnergyUsage" /> when the device exposes emeter telemetry.
	/// </summary>
	/// <param name="cancellationToken">The cancellation token for the operation.</param>
	/// <returns><see langword="true" /> when energy usage data was returned; otherwise, <see langword="false" />.</returns>
	public async Task<bool> UpdateEnergyUsageAsync (CancellationToken cancellationToken = default)
		{
		return await RunDeviceOperationAsync (UpdateEnergyUsageCoreAsync, cancellationToken).ConfigureAwait (false);
		}

	private async Task<bool> UpdateEnergyUsageCoreAsync (CancellationToken cancellationToken)
		{
		DateTime now = DateTime.Now;
		bool isBulb = DeviceType == DeviceType.Bulb || SystemInfo?.Model?.StartsWith ("KL", StringComparison.OrdinalIgnoreCase) == true || SystemInfo?.Model?.StartsWith ("LB", StringComparison.OrdinalIgnoreCase) == true || SystemInfo?.Model?.StartsWith ("KB", StringComparison.OrdinalIgnoreCase) == true;
		string response = await _transport.SendManyAsync (
			isBulb
				? [
				KasaCommands.GET_BULB_EMETER_REALTIME,
				KasaCommands.CreateGetBulbEmeterDayStatCommand (now.Year, now.Month),
				KasaCommands.CreateGetBulbEmeterMonthStatCommand (now.Year),
				]
				: [
				KasaCommands.GET_EMETER_REALTIME,
				KasaCommands.CreateGetEmeterDayStatCommand (now.Year, now.Month),
				KasaCommands.CreateGetEmeterMonthStatCommand (now.Year),
				],
			cancellationToken).ConfigureAwait (false);
		KasaResponseParser.ParsedResponse parsedResponse = KasaResponseParser.ParseResponse (response);
		EnergyUsage = KasaResponseParser.ParseEnergyUsage (parsedResponse);
		_features = CreateFeatures ();
		return true;
		}

	/// <summary>
	/// Returns a feature by identifier when present.
	/// </summary>
	/// <param name="featureId">The feature identifier.</param>
	/// <returns>The matching feature, or <see langword="null" /> if not found.</returns>
	public DeviceFeature? GetFeature (string featureId)
		{
		if (string.IsNullOrWhiteSpace (featureId))
			{
			throw new ArgumentException ("A feature identifier is required.", nameof (featureId));
			}

		foreach (DeviceFeature feature in _features)
			{
			if (string.Equals (feature.Id, featureId, StringComparison.OrdinalIgnoreCase))
				{
				return feature;
				}
			}

		return null;
		}

	/// <summary>
	/// Returns the negotiated smart component version when present.
	/// </summary>
	/// <param name="componentId">The smart component identifier.</param>
	/// <returns>The negotiated version, or <see langword="null" /> when the component is unavailable.</returns>
	public int? GetSmartComponentVersion (string componentId)
		{
		if (string.IsNullOrWhiteSpace (componentId))
			{
			throw new ArgumentException ("A component identifier is required.", nameof (componentId));
			}

		return _smartComponentVersions.TryGetValue (componentId, out int version)
			? version
			: null;
		}

	/// <summary>
	/// Returns a child device by identifier when present.
	/// </summary>
	/// <param name="childDeviceId">The child device identifier.</param>
	/// <returns>The matching child device, or <see langword="null" /> if not found.</returns>
	public ChildDeviceInfo? GetChild (string childDeviceId)
		{
		if (string.IsNullOrWhiteSpace (childDeviceId))
			{
			throw new ArgumentException ("A child device identifier is required.", nameof (childDeviceId));
			}

		foreach (ChildDeviceInfo child in Children)
			{
			if (string.Equals (child.Id, childDeviceId, StringComparison.OrdinalIgnoreCase))
				{
				return child;
				}
			}

		return null;
		}

	/// <summary>
	/// Returns a child-device wrapper by identifier when present.
	/// </summary>
	/// <param name="childDeviceId">The child device identifier.</param>
	/// <returns>The matching child-device wrapper, or <see langword="null" /> if not found.</returns>
	public ChildDevice? GetChildDevice (string childDeviceId)
		{
		ChildDeviceInfo? child = GetChild (childDeviceId);
		return child is null ? null : new ChildDevice (this, child);
		}

	/// <summary>
	/// Turns a child device on and refreshes the parent device state.
	/// </summary>
	/// <param name="childDeviceId">The child device identifier.</param>
	/// <param name="cancellationToken">The cancellation token for the operation.</param>
	/// <returns>A task that completes when the child device and parent state have been refreshed.</returns>
	public Task TurnChildOnAsync (string childDeviceId, CancellationToken cancellationToken = default) =>
		SetChildRelayStateAsync (childDeviceId, true, cancellationToken);

	/// <summary>
	/// Turns a child device off and refreshes the parent device state.
	/// </summary>
	/// <param name="childDeviceId">The child device identifier.</param>
	/// <param name="cancellationToken">The cancellation token for the operation.</param>
	/// <returns>A task that completes when the child device and parent state have been refreshed.</returns>
	public Task TurnChildOffAsync (string childDeviceId, CancellationToken cancellationToken = default) =>
		SetChildRelayStateAsync (childDeviceId, false, cancellationToken);

	/// <summary>
	/// Starts a smart hub child-device scan, waits for the requested interval, and returns the detected devices.
	/// </summary>
	/// <param name="timeoutSeconds">The number of seconds to wait after beginning the scan before reading detected devices.</param>
	/// <param name="cancellationToken">The cancellation token for the operation.</param>
	/// <returns>The detected child devices and supported categories reported by the hub.</returns>
	/// <exception cref="InvalidOperationException">Thrown when the device does not support smart hub child setup operations.</exception>
	public async Task<ChildSetupScanResult> ScanForChildDevicesAsync (int timeoutSeconds = 10, CancellationToken cancellationToken = default)
		{
		if (!UsesSmartProtocol () || DeviceType != DeviceType.Hub)
			{
			throw new InvalidOperationException ($"The device '{Host}' does not support smart hub child setup operations.");
			}

		await ExecuteCommandAsync (KasaCommands.CreateSmartRequest (KasaCommands.SMART_BEGIN_SCANNING_CHILD_DEVICE_METHOD), cancellationToken).ConfigureAwait (false);
		if (timeoutSeconds > 0)
			{
			await Task.Delay (TimeSpan.FromSeconds (timeoutSeconds), cancellationToken).ConfigureAwait (false);
			}

		return await GetScannedChildDevicesAsync (cancellationToken).ConfigureAwait (false);
		}

	/// <summary>
	/// Gets the currently detected child devices from the smart hub scan state.
	/// </summary>
	/// <param name="cancellationToken">The cancellation token for the operation.</param>
	/// <returns>The detected child devices and supported categories reported by the hub.</returns>
	/// <exception cref="InvalidOperationException">Thrown when the device does not support smart hub child setup operations.</exception>
	public async Task<ChildSetupScanResult> GetScannedChildDevicesAsync (CancellationToken cancellationToken = default)
		{
		return await RunDeviceOperationAsync (GetScannedChildDevicesCoreAsync, cancellationToken).ConfigureAwait (false);
		}

	private async Task<ChildSetupScanResult> GetScannedChildDevicesCoreAsync (CancellationToken cancellationToken)
		{
		if (!UsesSmartProtocol () || DeviceType != DeviceType.Hub)
			{
			throw new InvalidOperationException ($"The device '{Host}' does not support smart hub child setup operations.");
			}

		IReadOnlyList<string> supportedCategories = GetSupportedChildSetupCategories ();
		var scanList = new JArray ();
		foreach (string category in supportedCategories)
			{
			scanList.Add (category);
			}

		string response = await _transport.SendAsync (
			KasaCommands.CreateSmartRequest (
				KasaCommands.SMART_GET_SCAN_CHILD_DEVICE_LIST_METHOD,
				new JObject
					{
					["scan_list"] = scanList,
					}),
			cancellationToken).ConfigureAwait (false);

		return ParseChildSetupScanResult (response, supportedCategories);
		}

	/// <summary>
	/// Pairs the specified detected child devices with the smart hub.
	/// </summary>
	/// <param name="devices">The detected child devices to pair.</param>
	/// <param name="cancellationToken">The cancellation token for the operation.</param>
	/// <returns>The subset of requested devices that were confirmed as added after refresh.</returns>
	/// <exception cref="InvalidOperationException">Thrown when the device does not support smart hub child setup operations.</exception>
	public async Task<IReadOnlyList<DetectedChildDevice>> PairScannedChildDevicesAsync (IReadOnlyList<DetectedChildDevice> devices, CancellationToken cancellationToken = default)
		{
		return await RunDeviceOperationAsync (ct => PairScannedChildDevicesCoreAsync (devices, ct), cancellationToken).ConfigureAwait (false);
		}

	private async Task<IReadOnlyList<DetectedChildDevice>> PairScannedChildDevicesCoreAsync (IReadOnlyList<DetectedChildDevice> devices, CancellationToken cancellationToken)
		{
		if (!UsesSmartProtocol () || DeviceType != DeviceType.Hub)
			{
			throw new InvalidOperationException ($"The device '{Host}' does not support smart hub child setup operations.");
			}

		if (devices.Count == 0)
			{
			return Array.Empty<DetectedChildDevice> ();
			}

		var childDeviceList = new JArray ();
		foreach (DetectedChildDevice device in devices)
			{
			var item = new JObject
				{
				["device_id"] = device.DeviceId,
				};
			if (!string.IsNullOrWhiteSpace (device.Model))
				{
				item["device_model"] = device.Model;
				}
			if (!string.IsNullOrWhiteSpace (device.Category))
				{
				item["category"] = device.Category;
				}
			childDeviceList.Add (item);
			}

		await ExecuteCommandCoreAsync (
			KasaCommands.CreateSmartRequest (
				KasaCommands.SMART_ADD_CHILD_DEVICE_LIST_METHOD,
				new JObject
					{
					["child_device_list"] = childDeviceList,
					}),
			cancellationToken).ConfigureAwait (false);
		await UpdateCoreAsync (cancellationToken).ConfigureAwait (false);

		var addedDevices = new List<DetectedChildDevice> ();
		foreach (DetectedChildDevice device in devices)
			{
			if (GetChild (device.DeviceId) is not null)
				{
				addedDevices.Add (device);
				}
			}

		return addedDevices;
		}

	/// <summary>
	/// Removes a child device from the smart hub and refreshes state.
	/// </summary>
	/// <param name="childDeviceId">The child device identifier to remove.</param>
	/// <param name="cancellationToken">The cancellation token for the operation.</param>
	/// <returns>A task that completes when the child device has been removed and state refreshed.</returns>
	/// <exception cref="InvalidOperationException">Thrown when the device does not support smart hub child setup operations.</exception>
	public async Task UnpairChildDeviceAsync (string childDeviceId, CancellationToken cancellationToken = default)
		{
		await RunDeviceOperationAsync (ct => UnpairChildDeviceCoreAsync (childDeviceId, ct), cancellationToken).ConfigureAwait (false);
		}

	private async Task UnpairChildDeviceCoreAsync (string childDeviceId, CancellationToken cancellationToken)
		{
		if (!UsesSmartProtocol () || DeviceType != DeviceType.Hub)
			{
			throw new InvalidOperationException ($"The device '{Host}' does not support smart hub child setup operations.");
			}

		await ExecuteCommandCoreAsync (
			KasaCommands.CreateSmartRequest (
				KasaCommands.SMART_REMOVE_CHILD_DEVICE_LIST_METHOD,
				new JObject
					{
					["child_device_list"] = new JArray (
						new JObject
							{
							["device_id"] = childDeviceId,
							}),
					}),
			cancellationToken).ConfigureAwait (false);
		await UpdateCoreAsync (cancellationToken).ConfigureAwait (false);
		}

	/// <summary>
	/// Turns the device on and refreshes its state.
	/// </summary>
	/// <param name="cancellationToken">The cancellation token for the operation.</param>
	/// <returns>A task that completes when the device has been updated.</returns>
	public Task TurnOnAsync (CancellationToken cancellationToken = default) => SetRelayStateAsync (true, cancellationToken);

	/// <summary>
	/// Turns the device off and refreshes its state.
	/// </summary>
	/// <param name="cancellationToken">The cancellation token for the operation.</param>
	/// <returns>A task that completes when the device has been updated.</returns>
	public Task TurnOffAsync (CancellationToken cancellationToken = default) => SetRelayStateAsync (false, cancellationToken);

	/// <summary>
	/// Turns the light on and refreshes state.
	/// </summary>
	/// <param name="cancellationToken">The cancellation token for the operation.</param>
	/// <returns>A task that completes when the light state has been refreshed.</returns>
	public Task TurnLightOnAsync (CancellationToken cancellationToken = default) =>
		SetLightStateAsync (isOn: true, cancellationToken: cancellationToken);

	/// <summary>
	/// Turns the light on and refreshes state.
	/// </summary>
	/// <param name="transitionMilliseconds">The optional transition duration, in milliseconds, for supported legacy light devices.</param>
	/// <param name="cancellationToken">The cancellation token for the operation.</param>
	/// <returns>A task that completes when the light state has been refreshed.</returns>
	public Task TurnLightOnAsync (int transitionMilliseconds, CancellationToken cancellationToken = default) =>
		SetLightStateAsync (isOn: true, transitionMilliseconds: transitionMilliseconds, cancellationToken: cancellationToken);

	/// <summary>
	/// Turns the light off and refreshes state.
	/// </summary>
	/// <param name="cancellationToken">The cancellation token for the operation.</param>
	/// <returns>A task that completes when the light state has been refreshed.</returns>
	public Task TurnLightOffAsync (CancellationToken cancellationToken = default) =>
		SetLightStateAsync (isOn: false, cancellationToken: cancellationToken);

	/// <summary>
	/// Turns the light off and refreshes state.
	/// </summary>
	/// <param name="transitionMilliseconds">The optional transition duration, in milliseconds, for supported legacy light devices.</param>
	/// <param name="cancellationToken">The cancellation token for the operation.</param>
	/// <returns>A task that completes when the light state has been refreshed.</returns>
	public Task TurnLightOffAsync (int transitionMilliseconds, CancellationToken cancellationToken = default) =>
		SetLightStateAsync (isOn: false, transitionMilliseconds: transitionMilliseconds, cancellationToken: cancellationToken);

	/// <summary>
	/// Sets the brightness percentage and refreshes state.
	/// </summary>
	/// <param name="brightness">The brightness percentage from 0 through 100.</param>
	/// <param name="cancellationToken">The cancellation token for the operation.</param>
	/// <returns>A task that completes when the light state has been refreshed.</returns>
	/// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="brightness" /> is outside the inclusive range of 0 through 100.</exception>
	public Task SetBrightnessAsync (int brightness, CancellationToken cancellationToken = default)
		{
		if (brightness is < 0 or > 100)
			{
			throw new ArgumentOutOfRangeException (nameof (brightness), brightness, "Brightness must be between 0 and 100.");
			}

		return SetLightStateAsync (brightness: brightness, cancellationToken: cancellationToken);
		}

	/// <summary>
	/// Sets the brightness percentage and refreshes state.
	/// </summary>
	/// <param name="brightness">The brightness percentage from 0 through 100.</param>
	/// <param name="transitionMilliseconds">The optional transition duration, in milliseconds, for supported legacy light devices.</param>
	/// <param name="cancellationToken">The cancellation token for the operation.</param>
	/// <returns>A task that completes when the light state has been refreshed.</returns>
	/// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="brightness" /> is outside the inclusive range of 0 through 100.</exception>
	public Task SetBrightnessAsync (int brightness, int transitionMilliseconds, CancellationToken cancellationToken = default)
		{
		if (brightness is < 0 or > 100)
			{
			throw new ArgumentOutOfRangeException (nameof (brightness), brightness, "Brightness must be between 0 and 100.");
			}

		return SetLightStateAsync (brightness: brightness, transitionMilliseconds: transitionMilliseconds, cancellationToken: cancellationToken);
		}

	/// <summary>
	/// Sets the color temperature in kelvin and refreshes state.
	/// </summary>
	/// <param name="colorTemperature">The color temperature, in kelvin, to apply to the light.</param>
	/// <param name="cancellationToken">The cancellation token for the operation.</param>
	/// <returns>A task that completes when the light state has been refreshed.</returns>
	/// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="colorTemperature" /> is zero or negative.</exception>
	public Task SetColorTemperatureAsync (int colorTemperature, CancellationToken cancellationToken = default)
		{
		if (colorTemperature <= 0)
			{
			throw new ArgumentOutOfRangeException (nameof (colorTemperature), colorTemperature, "Color temperature must be a positive kelvin value.");
			}

		return SetLightStateAsync (colorTemperature: colorTemperature, cancellationToken: cancellationToken);
		}

	/// <summary>
	/// Sets the hue, saturation, and value components and refreshes state.
	/// </summary>
	/// <param name="hue">The hue component from 0 through 360.</param>
	/// <param name="saturation">The saturation component from 0 through 100.</param>
	/// <param name="value">The value or brightness component from 0 through 100.</param>
	/// <param name="cancellationToken">The cancellation token for the operation.</param>
	/// <returns>A task that completes when the light state has been refreshed.</returns>
	/// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="hue" />, <paramref name="saturation" />, or <paramref name="value" /> falls outside its supported range.</exception>
	public Task SetHsvAsync (int hue, int saturation, int value, CancellationToken cancellationToken = default)
		{
		if (hue is < 0 or > 360)
			{
			throw new ArgumentOutOfRangeException (nameof (hue), hue, "Hue must be between 0 and 360.");
			}

		if (saturation is < 0 or > 100)
			{
			throw new ArgumentOutOfRangeException (nameof (saturation), saturation, "Saturation must be between 0 and 100.");
			}

		if (value is < 0 or > 100)
			{
			throw new ArgumentOutOfRangeException (nameof (value), value, "Value must be between 0 and 100.");
			}

		return SetLightStateAsync (brightness: value, hue: hue, saturation: saturation, cancellationToken: cancellationToken);
		}

	/// <summary>
	/// Enables or disables persistent smooth light transitions when supported by the device and refreshes state.
	/// </summary>
	/// <param name="enabled">The value indicating whether smooth transitions should be enabled.</param>
	/// <param name="cancellationToken">The cancellation token for the operation.</param>
	/// <returns>A task that completes when the transition state has been refreshed.</returns>
	public Task SetLightTransitionsEnabledAsync (bool enabled, CancellationToken cancellationToken = default) =>
		SetLightTransitionsEnabledInternalAsync (enabled, cancellationToken);

	/// <summary>
	/// Sets the persistent smooth turn-on transition duration in seconds when supported by the device and refreshes state.
	/// </summary>
	/// <param name="seconds">The turn-on transition duration in seconds. Specify 0 to disable the turn-on transition.</param>
	/// <param name="cancellationToken">The cancellation token for the operation.</param>
	/// <returns>A task that completes when the transition state has been refreshed.</returns>
	public Task SetLightTurnOnTransitionAsync (int seconds, CancellationToken cancellationToken = default)
		{
		if (seconds < 0)
			{
			throw new ArgumentOutOfRangeException (nameof (seconds), seconds, "Transition duration must be zero or greater.");
			}

		return SetLightTurnOnTransitionInternalAsync (seconds, cancellationToken);
		}

	/// <summary>
	/// Sets the persistent smooth turn-off transition duration in seconds when supported by the device and refreshes state.
	/// </summary>
	/// <param name="seconds">The turn-off transition duration in seconds. Specify 0 to disable the turn-off transition.</param>
	/// <param name="cancellationToken">The cancellation token for the operation.</param>
	/// <returns>A task that completes when the transition state has been refreshed.</returns>
	public Task SetLightTurnOffTransitionAsync (int seconds, CancellationToken cancellationToken = default)
		{
		if (seconds < 0)
			{
			throw new ArgumentOutOfRangeException (nameof (seconds), seconds, "Transition duration must be zero or greater.");
			}

		return SetLightTurnOffTransitionInternalAsync (seconds, cancellationToken);
		}

	/// <summary>
	/// Enables a lighting effect by device-specific name or identifier and refreshes state.
	/// </summary>
	/// <param name="effect">The device-specific light effect name or identifier.</param>
	/// <param name="cancellationToken">The cancellation token for the operation.</param>
	/// <returns>A task that completes when the light effect state has been refreshed.</returns>
	/// <exception cref="ArgumentException">Thrown when <paramref name="effect" /> is empty or whitespace.</exception>
	public Task SetLightEffectAsync (string effect, CancellationToken cancellationToken = default)
		{
		if (string.IsNullOrWhiteSpace (effect))
			{
			throw new ArgumentException ("A light effect name or identifier is required.", nameof (effect));
			}

		return SetLightEffectInternalAsync (effect.Trim (), cancellationToken);
		}

	/// <summary>
	/// Disables the current lighting effect and refreshes state.
	/// </summary>
	/// <param name="cancellationToken">The cancellation token for the operation.</param>
	/// <returns>A task that completes when the light effect state has been refreshed.</returns>
	public Task ClearLightEffectAsync (CancellationToken cancellationToken = default) =>
		SetLightEffectInternalAsync (null, cancellationToken);

	/// <summary>
	/// Executes a raw JSON command against the device.
	/// </summary>
	/// <param name="commandJson">The command payload to send.</param>
	/// <param name="cancellationToken">The cancellation token for the operation.</param>
	/// <returns>The raw JSON response payload from the device.</returns>
	/// <exception cref="ArgumentException">Thrown when <paramref name="commandJson" /> is empty or whitespace.</exception>
	public Task<string> ExecuteCommandAsync (string commandJson, CancellationToken cancellationToken = default)
		{
		if (string.IsNullOrWhiteSpace (commandJson))
			{
			throw new ArgumentException ("A device command is required.", nameof (commandJson));
			}

		return RunDeviceOperationAsync (ct => ExecuteCommandCoreAsync (commandJson, ct), cancellationToken);
		}

	/// <summary>
	/// Executes a raw JSON command against the device and optionally refreshes cached device state before returning.
	/// </summary>
	/// <param name="commandJson">The command payload to send.</param>
	/// <param name="updateMode">The state update behavior to apply after the command completes.</param>
	/// <param name="cancellationToken">The cancellation token for the operation.</param>
	/// <returns>The raw JSON response payload from the device.</returns>
	/// <exception cref="ArgumentException">Thrown when <paramref name="commandJson" /> is empty or whitespace.</exception>
	/// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="updateMode" /> is unsupported.</exception>
	public Task<string> ExecuteCommandAsync (string commandJson, DeviceStateUpdateMode updateMode, CancellationToken cancellationToken = default)
		{
		if (string.IsNullOrWhiteSpace (commandJson))
			{
			throw new ArgumentException ("A device command is required.", nameof (commandJson));
			}

		if (updateMode is not DeviceStateUpdateMode.None and not DeviceStateUpdateMode.UpdateAfterCommand)
			{
			throw new ArgumentOutOfRangeException (nameof (updateMode), updateMode, "Unsupported device state update mode.");
			}

		return RunDeviceOperationAsync (
			async ct =>
				{
				string response = await ExecuteCommandCoreAsync (commandJson, ct).ConfigureAwait (false);
				if (updateMode == DeviceStateUpdateMode.UpdateAfterCommand)
					{
					await UpdateCoreAsync (ct).ConfigureAwait (false);
					}

				return response;
				},
			cancellationToken);
		}

	/// <summary>
	/// Executes a smart-protocol method against the device.
	/// </summary>
	/// <param name="method">The smart-protocol method name to execute.</param>
	/// <param name="parameters">The smart-protocol method parameters, or <see langword="null" /> when the method has no parameters.</param>
	/// <param name="cancellationToken">The cancellation token for the operation.</param>
	/// <returns>The raw JSON response payload from the device.</returns>
	/// <exception cref="ArgumentException">Thrown when <paramref name="method" /> is empty or whitespace.</exception>
	public Task<string> ExecuteSmartCommandAsync (string method, JObject? parameters = null, CancellationToken cancellationToken = default) =>
		ExecuteSmartCommandAsync (method, parameters, DeviceStateUpdateMode.None, cancellationToken);

	/// <summary>
	/// Executes a smart-protocol method against the device and optionally refreshes cached device state before returning.
	/// </summary>
	/// <param name="method">The smart-protocol method name to execute.</param>
	/// <param name="parameters">The smart-protocol method parameters, or <see langword="null" /> when the method has no parameters.</param>
	/// <param name="updateMode">The state update behavior to apply after the command completes.</param>
	/// <param name="cancellationToken">The cancellation token for the operation.</param>
	/// <returns>The raw JSON response payload from the device.</returns>
	/// <exception cref="ArgumentException">Thrown when <paramref name="method" /> is empty or whitespace.</exception>
	/// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="updateMode" /> is unsupported.</exception>
	public Task<string> ExecuteSmartCommandAsync (string method, JObject? parameters, DeviceStateUpdateMode updateMode, CancellationToken cancellationToken = default)
		{
		if (string.IsNullOrWhiteSpace (method))
			{
			throw new ArgumentException ("A smart method name is required.", nameof (method));
			}

		string commandJson = KasaCommands.CreateSmartRequest (method.Trim (), parameters);
		return ExecuteCommandAsync (commandJson, updateMode, cancellationToken);
		}

	private Task<string> ExecuteCommandCoreAsync (string commandJson, CancellationToken cancellationToken) =>
		_transport.SendAsync (commandJson, cancellationToken);

	private async Task RunDeviceOperationAsync (Func<CancellationToken, Task> operation, CancellationToken cancellationToken)
		{
		await _operationLock.WaitAsync (cancellationToken).ConfigureAwait (false);
		try
			{
			await operation (cancellationToken).ConfigureAwait (false);
			}
		finally
			{
			_operationLock.Release ();
			}
		}

	private async Task<T> RunDeviceOperationAsync<T> (Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken)
		{
		await _operationLock.WaitAsync (cancellationToken).ConfigureAwait (false);
		try
			{
			return await operation (cancellationToken).ConfigureAwait (false);
			}
		finally
			{
			_operationLock.Release ();
			}
		}



	internal bool SupportsAutoOff => AutoOffState is not null;
	internal bool SupportsCloudConnection => CloudState is not null;
	internal bool SupportsDeviceTime => TimeState is not null;
	internal bool SupportsMatterSetup => MatterSetup is not null;
	internal bool SupportsHomeKitSetup => HomeKitSetup is not null;
	internal bool SupportsLedControl => LedState is not null || !UsesSmartProtocol () && SystemInfo?.RawJson is not null;
	internal bool SupportsChildLock => ChildLockState is not null;
	internal bool SupportsFirmwareModule => FirmwareState is not null;

	private sealed class SmartChildRefreshDefinition
		{
		internal SmartChildRefreshDefinition (string method, Func<JObject?> createParameters, string? responsePropertyName = null)
			{
			Method = method;
			CreateParameters = createParameters;
			ResponsePropertyName = responsePropertyName ?? method;
			}

		internal string Method { get; }
		internal Func<JObject?> CreateParameters { get; }
		internal string ResponsePropertyName { get; }
		}

	private sealed class SmartRefreshContribution
		{
		internal SmartRefreshContribution (string requiredComponent, string method, Func<JObject?>? createParameters = null, int? minimumSupportedVersion = null)
			{
			RequiredComponent = requiredComponent;
			Method = method;
			CreateParameters = createParameters ?? (() => null);
			MinimumSupportedVersion = minimumSupportedVersion;
			}

		internal string RequiredComponent { get; }
		internal string Method { get; }
		internal Func<JObject?> CreateParameters { get; }
		internal int? MinimumSupportedVersion { get; }
		}

	}
