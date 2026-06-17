// Copyright © 2026 Neil Colvin.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

using KasaTapoClient;

const string PROFILE_SAVE_OPTION = "--save";
const string PROFILE_USE_OPTION = "--profile";
const string PROFILE_CLEAR_OPTION = "--clear-profile";
const string DEFAULT_PROFILE_NAME = "default";
const string IMPLICIT_PROFILE_NAME = "__implicit__";
const int DEFAULT_DISCOVERY_TIMEOUT_MILLISECONDS = 8000;

return await RunAsync (args).ConfigureAwait (false);

static async Task<int> RunAsync (IReadOnlyList<string> arguments)
	{
	if (arguments.Count == 0)
		{
		return await RunInteractiveAsync ().ConfigureAwait (false);
		}

	string command = ResolveKeyword (arguments[0], ConsoleCommandLexicon.TopLevelCommands);
	if (command == "help")
		{
		PrintGeneralUsage ();
		return 0;
		}

	return command switch
		{
			"exit" => 0,
			"quit" => 0,
			"discover" => await RunDiscoverAsync (arguments).ConfigureAwait (false),
			"host" => await RunHostAsync (arguments).ConfigureAwait (false),
			_ => Fail ($"Unknown command '{arguments[0]}'."),
			};
	}

static async Task<int> RunDiscoverAsync (IReadOnlyList<string> arguments)
	{
	if (HasHelpArgument (arguments, 1))
		{
		PrintDiscoverUsage ();
		return 0;
		}

	bool verbose = arguments.Any (static argument => IsDiscoverVerboseOption (argument));
	List<string> positionalArguments = arguments
		.Skip (1)
		.Where (static argument => !IsDiscoverVerboseOption (argument))
		.ToList ();

	TimeSpan timeout = positionalArguments.Count > 0 && int.TryParse (positionalArguments[0], out int timeoutMilliseconds)
		? TimeSpan.FromMilliseconds (timeoutMilliseconds)
		: TimeSpan.FromMilliseconds (DEFAULT_DISCOVERY_TIMEOUT_MILLISECONDS);
	string target = positionalArguments.Count > 1 ? positionalArguments[1] : "255.255.255.255";

	IReadOnlyList<DiscoveryResult> devices = await Discover.DiscoverAsync (timeout, target).ConfigureAwait (false);
	if (devices.Count == 0)
		{
		Console.WriteLine ("No devices responded.");
		return 0;
		}

	foreach (DiscoveryResult device in devices)
		{
		string? displayAlias = device.Alias;
		bool shouldExpandChildren = device.DeviceType == DeviceType.Hub || IsDiscoveryChildExpansionCandidate (device);
		KasaDevice? connectedDevice = null;
		if (verbose)
			{
			connectedDevice = await TryConnectForDiscoveryDisplayAsync (device, timeout).ConfigureAwait (false);
			if (connectedDevice is not null)
				{
				PrintDiscoveredDeviceVerbose (connectedDevice);
				continue;
				}
			}

		if (string.IsNullOrWhiteSpace (displayAlias) || shouldExpandChildren)
			{
				TimeSpan aliasTimeout = timeout > TimeSpan.FromSeconds (4)
					? TimeSpan.FromSeconds (4)
					: timeout;
				connectedDevice = await TryConnectForDiscoveryDisplayAsync (device, aliasTimeout).ConfigureAwait (false);
				if (connectedDevice is not null && !string.IsNullOrWhiteSpace (connectedDevice.Alias))
					{
					displayAlias = connectedDevice.Alias;
					}
			}

		Console.WriteLine ($"{device.Host} | {device.DeviceType} | {displayAlias ?? "(no alias)"} | {device.Model ?? "(unknown model)"}");
		if (!shouldExpandChildren)
			{
			continue;
			}

		TimeSpan childExpansionTimeout = timeout > TimeSpan.FromSeconds (2)
			? TimeSpan.FromSeconds (2)
			: timeout;
		try
			{
				connectedDevice ??= await TryConnectForDiscoveryDisplayAsync (device, childExpansionTimeout).ConfigureAwait (false);
				if (connectedDevice is null)
					{
					continue;
					}
				if (connectedDevice.Children.Count > 0)
					{
					for (int i = 0; i < connectedDevice.Children.Count; i++)
						{
					ChildDeviceInfo childInfo = connectedDevice.Children[i];
					ChildDevice? child = connectedDevice.GetChildDevice (childInfo.Id);
					Console.WriteLine ($"  {i + 1} | {childInfo.Id} | {childInfo.Alias ?? "(no alias)"} | {childInfo.Model ?? "(unknown model)"} | {(child is null ? FormatPowerState (childInfo.IsOn) : FormatChildState (child, childInfo))}");
						}
					}
			}
		catch
			{
			}
		}

	return 0;
	}

static bool IsDiscoveryChildExpansionCandidate (DiscoveryResult device)
	{
	if (device.DeviceType == DeviceType.Strip)
		{
		return true;
		}

	string modelText = (device.Model ?? string.Empty).Trim ().ToUpperInvariant ();
	return modelText.StartsWith ("KP303", StringComparison.Ordinal)
		|| modelText.StartsWith ("HS300", StringComparison.Ordinal)
		|| modelText.StartsWith ("P300", StringComparison.Ordinal);
	}

static async Task<KasaDevice?> TryConnectForDiscoveryDisplayAsync (DiscoveryResult device, TimeSpan timeout)
	{
	foreach (DeviceCredentials? credentials in GetDiscoveryCredentialCandidates (device.Host))
		{
		try
			{
				return await Discover.ConnectAsync (device, true, credentials, timeout).ConfigureAwait (false);
			}
		catch
			{
			}
		}

	try
		{
			DeviceConfiguration configuration = Discover.CreateConfiguration (device, credentials: null, timeout);
			return await Discover.ConnectAsync (configuration, updateState: true).ConfigureAwait (false);
		}
	catch
		{
		}

	return null;
	}

static IReadOnlyList<DeviceCredentials?> GetDiscoveryCredentialCandidates (string? host)
	{
	var candidates = new List<DeviceCredentials?> ();
	SavedConnectionProfile? implicitProfile = ConsoleImplicitProfileStore.Load (host);
	DeviceCredentials? implicitCredentials = CreateCredentialsFromProfile (implicitProfile);
	void AddCandidate (DeviceCredentials? candidate)
		{
		if (candidate is null)
			{
			if (!candidates.Contains (null))
				{
				candidates.Add (null);
				}

			return;
			}

		foreach (DeviceCredentials? existingCandidate in candidates)
			{
				if (existingCandidate is not null
					&& string.Equals (existingCandidate.UserName, candidate.UserName, StringComparison.Ordinal)
					&& string.Equals (existingCandidate.Password, candidate.Password, StringComparison.Ordinal))
					{
					return;
					}
			}

		candidates.Add (candidate);
		}

	AddCandidate (ResolveDiscoveryCredentials (host));
	AddCandidate (CreateCredentialsFromProfile (implicitProfile));
	if (implicitCredentials is not null)
		{
		foreach (SavedConnectionProfile profile in ConsoleProfileStore.LoadAll ())
			{
				DeviceCredentials? profileCredentials = CreateCredentialsFromProfile (profile);
				if (profileCredentials is not null
					&& string.Equals (profileCredentials.UserName, implicitCredentials.UserName, StringComparison.Ordinal)
					&& string.Equals (profileCredentials.Password, implicitCredentials.Password, StringComparison.Ordinal))
					{
					AddCandidate (profileCredentials);
					}
			}
		}
	foreach (SavedConnectionProfile profile in ConsoleProfileStore.LoadAll ())
		{
		AddCandidate (CreateCredentialsFromProfile (profile));
		}
	AddCandidate (null);

	return candidates;
	}

static DeviceCredentials? ResolveDiscoveryCredentials (string? host)
	{
	if (string.IsNullOrWhiteSpace (host))
		{
		return null;
		}

	SavedConnectionProfile? implicitProfile = ConsoleImplicitProfileStore.Load (host);
	if (string.Equals (implicitProfile?.Host, host, StringComparison.OrdinalIgnoreCase))
		{
		DeviceCredentials? implicitCredentials = CreateCredentialsFromProfile (implicitProfile);
		if (implicitCredentials is not null)
			{
			return implicitCredentials;
			}
		}

	SavedConnectionProfile? savedProfile = ConsoleProfileStore.LoadByHost (host);
	DeviceCredentials? savedCredentials = CreateCredentialsFromProfile (savedProfile);
	if (savedCredentials is not null)
		{
		return savedCredentials;
		}

	DeviceCredentials? anyImplicitCredentials = CreateCredentialsFromProfile (implicitProfile);
	if (anyImplicitCredentials is not null)
		{
		return anyImplicitCredentials;
		}

	foreach (SavedConnectionProfile profile in ConsoleProfileStore.LoadAll ())
		{
		DeviceCredentials? profileCredentials = CreateCredentialsFromProfile (profile);
		if (profileCredentials is not null)
			{
			return profileCredentials;
			}
		}

	return null;
	}

static DeviceCredentials? CreateCredentialsFromProfile (SavedConnectionProfile? profile)
	{
	if (profile is null)
		{
		return null;
		}

	return string.IsNullOrWhiteSpace (profile.UserName) && string.IsNullOrWhiteSpace (profile.Password)
		? null
		: new DeviceCredentials (profile.UserName, profile.Password);
	}

static async Task<int> RunHostAsync (IReadOnlyList<string> arguments)
	{
	if (arguments.Count < 2)
		{
		if (TryRunProfilesWithoutHost (arguments, out int profileCommandResult))
			{
			return profileCommandResult;
			}
		}

	if (!TryResolveHost (arguments, out string? host, out int commandIndex, out bool hostWasExplicit, out string? errorMessage))
		{
		return Fail (errorMessage!);
		}

	if (HasHelpArgument (arguments, commandIndex))
		{
		PrintHostUsage ();
		return 0;
		}

	string? hostCommand = arguments.Count > commandIndex && !arguments[commandIndex].StartsWith ("--", StringComparison.Ordinal)
		? ResolveHostKeyword (arguments[commandIndex])
		: null;

	if (TryRunProfilesWithResolvedHost (host!, arguments, commandIndex, out int resolvedProfileCommandResult))
		{
		return resolvedProfileCommandResult;
		}

	if (hostCommand == "child")
		{
		return await RunChildAsync (host!, arguments, commandIndex).ConfigureAwait (false);
		}

	if (hostCommand == "setup")
		{
		return await RunChildSetupAsync (host!, arguments, commandIndex).ConfigureAwait (false);
		}

	if (hostCommand == "light")
		{
		return await RunLightAsync (host!, arguments, commandIndex).ConfigureAwait (false);
		}

	bool hasExplicitAction = arguments.Count > commandIndex && !arguments[commandIndex].StartsWith ("--", StringComparison.Ordinal);
	string action = hasExplicitAction
		? hostCommand ?? ResolveKeyword (arguments[commandIndex], ConsoleCommandLexicon.HostActions)
		: "state";

	DeviceConfiguration configuration = CreateHostConfiguration (host!, hostWasExplicit, action, arguments, hasExplicitAction ? commandIndex + 1 : commandIndex);
	bool requireInitialState = RequiresInitialStateBeforeHostAction (action, configuration);
	KasaDevice device = await Discover.ConnectAsync (configuration, requireInitialState).ConfigureAwait (false);
	ConsoleRecentHostStore.Save (device.Host);
	ConsoleImplicitProfileStore.Save (CreateImplicitProfile (device));
	switch (action)
		{
		case "on":
			await device.TurnOnAsync ().ConfigureAwait (false);
			break;
		case "off":
			await device.TurnOffAsync ().ConfigureAwait (false);
			break;
		case "state":
			break;
		default:
			return Fail ($"Unknown host action '{action}'. Use 'state', 'on', or 'off'.");
		}
	PrintDiscoveredDeviceVerbose (device);

	return 0;
	}

static void PrintDeviceModuleState (KasaDevice device)
	{
	if (device.Firmware.State is FirmwareState firmwareState)
		{
		Console.WriteLine ("Firmware Module:");
		if (!string.IsNullOrWhiteSpace (firmwareState.CurrentFirmwareVersion))
			{
			Console.WriteLine ($"  Current firmware: {firmwareState.CurrentFirmwareVersion}");
			}
		if (!string.IsNullOrWhiteSpace (firmwareState.CurrentHardwareVersion))
			{
			Console.WriteLine ($"  Current hardware: {firmwareState.CurrentHardwareVersion}");
			}
		if (!string.IsNullOrWhiteSpace (firmwareState.AvailableFirmwareVersion))
			{
			Console.WriteLine ($"  Available firmware: {firmwareState.AvailableFirmwareVersion}");
			}
		if (firmwareState.AutoUpdateEnabled is bool autoUpdateEnabled)
			{
			Console.WriteLine ($"  Auto update enabled: {FormatNullableBoolean (autoUpdateEnabled)}");
			}
		if (firmwareState.UpdateAvailable is bool updateAvailable)
			{
			Console.WriteLine ($"  Update available: {FormatNullableBoolean (updateAvailable)}");
			}
		}

	if (device.Cloud.State is CloudConnectionState cloudState)
		{
		Console.WriteLine ("Cloud Module:");
		if (cloudState.IsConnected is bool isConnected)
			{
			Console.WriteLine ($"  Connected: {FormatNullableBoolean (isConnected)}");
			}
		if (cloudState.IsProvisioned is bool isProvisioned)
			{
			Console.WriteLine ($"  Provisioned: {FormatNullableBoolean (isProvisioned)}");
			}
		if (!string.IsNullOrWhiteSpace (cloudState.Server))
			{
			Console.WriteLine ($"  Server: {cloudState.Server}");
			}
		if (!string.IsNullOrWhiteSpace (cloudState.UserName))
			{
			Console.WriteLine ($"  User name: {cloudState.UserName}");
			}
		}

	if (device.Time.State is DeviceTimeState timeState)
		{
		Console.WriteLine ("Time Module:");
		if (timeState.LocalTime is DateTime localTime)
			{
			Console.WriteLine ($"  Local time: {localTime:O}");
			}
		if (!string.IsNullOrWhiteSpace (timeState.Region))
			{
			Console.WriteLine ($"  Region: {timeState.Region}");
			}
		if (timeState.TimeDifferenceMinutes is int offsetMinutes)
			{
			Console.WriteLine ($"  UTC offset minutes: {offsetMinutes}");
			}
		}

	if (device.Matter.Info is MatterSetupInfo matterInfo)
		{
		Console.WriteLine ("Matter Module:");
		if (!string.IsNullOrWhiteSpace (matterInfo.SetupCode))
			{
			Console.WriteLine ($"  Setup code: {matterInfo.SetupCode}");
			}
		if (!string.IsNullOrWhiteSpace (matterInfo.SetupPayload))
			{
			Console.WriteLine ($"  Setup payload: {matterInfo.SetupPayload}");
			}
		}

	if (device.HomeKit.Info is HomeKitSetupInfo homeKitInfo)
		{
		Console.WriteLine ("HomeKit Module:");
		if (!string.IsNullOrWhiteSpace (homeKitInfo.SetupCode))
			{
			Console.WriteLine ($"  Setup code: {homeKitInfo.SetupCode}");
			}
		if (!string.IsNullOrWhiteSpace (homeKitInfo.SetupPayload))
			{
			Console.WriteLine ($"  Setup payload: {homeKitInfo.SetupPayload}");
			}
		}

	if (device.AutoOff.State is AutoOffState autoOffState)
		{
		Console.WriteLine ("Auto Off Module:");
		if (autoOffState.Enabled is bool enabled)
			{
			Console.WriteLine ($"  Enabled: {FormatNullableBoolean (enabled)}");
			}
		if (autoOffState.DelayMinutes is int delayMinutes)
			{
			Console.WriteLine ($"  Delay: {delayMinutes} min");
			}
		if (autoOffState.TimerActive is bool timerActive)
			{
			Console.WriteLine ($"  Timer active: {FormatNullableBoolean (timerActive)}");
			}
		if (autoOffState.AutoOffAt is DateTime autoOffAt)
			{
			Console.WriteLine ($"  Auto off at: {autoOffAt:O}");
			}
		}

	if (device.Led.State is LedState ledState)
		{
		Console.WriteLine ("LED Module:");
		if (ledState.Enabled is bool enabled)
			{
			Console.WriteLine ($"  Enabled: {FormatNullableBoolean (enabled)}");
			}
		if (!string.IsNullOrWhiteSpace (ledState.Mode))
			{
			Console.WriteLine ($"  Mode: {ledState.Mode}");
			}
		if (ledState.NightModeSettings is LedNightModeSettings nightMode)
			{
			Console.WriteLine ($"  Night mode start: {nightMode.StartMinute?.ToString (CultureInfo.InvariantCulture) ?? "(unknown)"}");
			Console.WriteLine ($"  Night mode end: {nightMode.EndMinute?.ToString (CultureInfo.InvariantCulture) ?? "(unknown)"}");
			if (!string.IsNullOrWhiteSpace (nightMode.ModeType))
				{
				Console.WriteLine ($"  Night mode type: {nightMode.ModeType}");
				}
			}
		}

	if (device.ChildLock.State is ChildLockState childLockState)
		{
		Console.WriteLine ("Child Lock Module:");
		if (childLockState.Enabled is bool enabled)
			{
			Console.WriteLine ($"  Enabled: {FormatNullableBoolean (enabled)}");
			}
		}

	if (device.EnergyUsage is not null)
		{
		Console.WriteLine ("Energy Module:");
		Console.WriteLine ($"  Power: {FormatNumber (device.EnergyUsage.CurrentPowerWatts, "W")}");
		Console.WriteLine ($"  Voltage: {FormatNumber (device.EnergyUsage.VoltageVolts, "V")}");
		Console.WriteLine ($"  Current: {FormatNumber (device.EnergyUsage.CurrentAmps, "A")}");
		Console.WriteLine ($"  Today's consumption: {FormatNumber (device.EnergyUsage.TodayKilowattHours, "kWh")}");
		Console.WriteLine ($"  This month's consumption: {FormatNumber (device.EnergyUsage.MonthKilowattHours, "kWh")}");
		Console.WriteLine ($"  Total energy: {FormatNumber (device.EnergyUsage.TotalKilowattHours, "kWh")}");
		}
	}

static void PrintDiscoveredDeviceVerbose (KasaDevice device)
	{
	Console.WriteLine ($"Host: {device.Host}");
	Console.WriteLine ($"Alias: {device.Alias}");
	Console.WriteLine ($"Transport: {device.Configuration.ConnectionOptions.TransportKind}");
	Console.WriteLine ($"Type: {device.DeviceType}");
	Console.WriteLine ($"Model: {device.SystemInfo?.Model ?? "(unknown)"}");
	Console.WriteLine ($"State: {FormatPowerState (device.IsOn)}");
	Console.WriteLine ($"Firmware: {device.SystemInfo?.SoftwareVersion ?? "(unknown)"}");
	if (!string.IsNullOrWhiteSpace (device.SystemInfo?.MacAddress))
		{
		Console.WriteLine ($"MAC: {device.SystemInfo?.MacAddress}");
		}
	if (device.Rssi is int rssi)
		{
		Console.WriteLine ($"RSSI: {rssi} dBm");
		}
	if (device.OnSince is DateTimeOffset onSince)
		{
		Console.WriteLine ($"On since: {onSince:O}");
		}
	PrintDeviceModuleState (device);

	if (device.Children.Count > 0)
		{
		Console.WriteLine ("Children:");
		for (int i = 0; i < device.Children.Count; i++)
			{
			ChildDevice? child = device.GetChildDevice (device.Children[i].Id);
			if (child?.Info is not ChildDeviceInfo childInfo)
				{
				continue;
				}

			Console.WriteLine ($"  Child {i + 1}:");
			PrintChildSummary (device, child, childInfo, "  ");
			}
		}

	if (device.RuleState is not null)
		{
		PrintRuleState (device.RuleState);
		}

	Console.WriteLine ("Features:");
	foreach (DeviceFeature feature in device.Features)
		{
		Console.WriteLine ($"  {feature.Name} ({feature.Id}): {FormatFeatureValue (feature)}");
		}
	Console.WriteLine ();
	}

static async Task<int> RunChildAsync (string host, IReadOnlyList<string> arguments, int commandIndex)
	{
	if (arguments.Count <= commandIndex + 1)
		{
		return Fail ("The 'child' command requires a child device identifier.");
		}

	if (HasHelpArgument (arguments, commandIndex + 2))
		{
		PrintChildUsage ();
		return 0;
		}

	string childSelector = arguments[commandIndex + 1];
	bool hasExplicitAction = arguments.Count > commandIndex + 2 && !arguments[commandIndex + 2].StartsWith ("--", StringComparison.Ordinal);
	string action = hasExplicitAction ? ResolveKeyword (arguments[commandIndex + 2], ConsoleCommandLexicon.ChildActions) : "state";
	if (action == "logs")
		{
		return await RunChildLogsAsync (host, childSelector, arguments, hasExplicitAction ? commandIndex + 3 : commandIndex + 2).ConfigureAwait (false);
		}
	if (action == "watch")
		{
		return await RunChildWatchAsync (host, childSelector, arguments, hasExplicitAction ? commandIndex + 3 : commandIndex + 2).ConfigureAwait (false);
		}
	DeviceConfiguration configuration = CreateHostConfiguration (host, true, action, arguments, hasExplicitAction ? commandIndex + 3 : commandIndex + 2);
	KasaDevice device = await Discover.ConnectAsync (configuration).ConfigureAwait (false);
	ConsoleRecentHostStore.Save (device.Host);
	ConsoleImplicitProfileStore.Save (CreateImplicitProfile (device));
	string childDeviceId = ResolveChildIdentifier (device, childSelector);

	switch (action)
		{
		case "on":
			await device.TurnChildOnAsync (childDeviceId).ConfigureAwait (false);
			break;
		case "off":
			await device.TurnChildOffAsync (childDeviceId).ConfigureAwait (false);
			break;
		case "state":
			break;
		default:
			return Fail ($"Unknown child action '{action}'. Use 'state', 'on', 'off', 'logs', or 'watch'.");
		}

	ChildDevice? child = device.GetChildDevice (childDeviceId);
	if (child is null)
		{
		return Fail ($"Child device '{childDeviceId}' was not found on host '{host}'.");
		}
	ChildDeviceInfo? childInfo = child.Info;
	if (childInfo is null)
		{
		return Fail ($"Child device '{childDeviceId}' did not return current information from host '{host}'.");
		}

	Console.WriteLine ($"Host: {device.Host}");
	Console.WriteLine ($"Child ID: {child.Id}");
	Console.WriteLine ($"Alias: {child.Alias ?? "(no alias)"}");
	Console.WriteLine ($"Model: {child.Model ?? "(unknown)"}");
	Console.WriteLine ($"Type: {childInfo.DeviceType}");
	Console.WriteLine ($"State: {FormatChildState (child, childInfo)}");
	PrintTypedChildModuleState (child);
	if (childInfo.Features.Count > 0)
		{
		Console.WriteLine ("Features:");
		foreach (DeviceFeature feature in childInfo.Features)
			{
			Console.WriteLine ($"  {feature.Name} ({feature.Id}): {FormatFeatureValue (feature)}");
			}
		}
	return 0;
	}

static async Task<int> RunChildLogsAsync (string host, string childSelector, IReadOnlyList<string> arguments, int optionStartIndex)
	{
	DeviceConfiguration configuration = CreateHostConfiguration (host, true, "state", arguments, optionStartIndex);
	KasaDevice device = await Discover.ConnectAsync (configuration).ConfigureAwait (false);
	ConsoleRecentHostStore.Save (device.Host);
	ConsoleImplicitProfileStore.Save (CreateImplicitProfile (device));
	string childDeviceId = ResolveChildIdentifier (device, childSelector);
	ChildDevice? child = device.GetChildDevice (childDeviceId);
	if (child?.Info is not ChildDeviceInfo childInfo)
		{
		return Fail ($"Child device '{childDeviceId}' did not return current information from host '{host}'.");
		}

	PrintChildSummary (device, child, childInfo);
	PrintRecentTriggerDetails (child, childInfo, 10);
	return 0;
	}

static async Task<int> RunChildSetupAsync (string host, IReadOnlyList<string> arguments, int commandIndex)
	{
	if (HasHelpArgument (arguments, commandIndex + 1))
		{
		PrintChildSetupUsage ();
		return 0;
		}

	bool hasExplicitAction = arguments.Count > commandIndex + 1 && !arguments[commandIndex + 1].StartsWith ("--", StringComparison.Ordinal);
	string action = hasExplicitAction ? ResolveKeyword (arguments[commandIndex + 1], ConsoleCommandLexicon.SetupActions) : "scan";
	int optionStartIndex = hasExplicitAction ? commandIndex + 2 : commandIndex + 1;
	if (string.Equals (action, "scan", StringComparison.Ordinal)
		&& TryGetTimeoutSeconds (arguments, optionStartIndex).HasValue)
		{
		optionStartIndex++;
		}
	DeviceConfiguration configuration = CreateHostConfiguration (host, true, action, arguments, optionStartIndex);
	KasaDevice device = await Discover.ConnectAsync (configuration).ConfigureAwait (false);
	ConsoleRecentHostStore.Save (device.Host);
	ConsoleImplicitProfileStore.Save (CreateImplicitProfile (device));

	switch (action)
		{
		case "scan":
			{
			int timeoutSeconds = TryGetTimeoutSeconds (arguments, hasExplicitAction ? commandIndex + 2 : commandIndex + 1) ?? 10;
			ChildSetupScanResult result = await device.ChildSetup.ScanAsync (timeoutSeconds).ConfigureAwait (false);
			PrintChildSetupScanResult (device, result);
			return 0;
			}
		case "detected":
			{
			ChildSetupScanResult result = await device.ChildSetup.GetDetectedDevicesAsync ().ConfigureAwait (false);
			PrintChildSetupScanResult (device, result);
			return 0;
			}
		case "pair":
			{
			ChildSetupScanResult detected = await device.ChildSetup.GetDetectedDevicesAsync ().ConfigureAwait (false);
			IReadOnlyList<DetectedChildDevice> paired = await device.ChildSetup.PairAsync (detected.DetectedDevices).ConfigureAwait (false);
			Console.WriteLine ($"Paired devices: {paired.Count}");
			foreach (DetectedChildDevice child in paired)
				{
				Console.WriteLine ($"  {child.DeviceId} | {child.Model ?? "(unknown model)"} | {child.Category ?? "(unknown category)"}");
				}
			return 0;
			}
		case "unpair":
			{
			if (arguments.Count <= commandIndex + 2)
				{
				return Fail ("The 'setup unpair' command requires a child device identifier.");
				}

			string childDeviceId = ResolveChildIdentifier (device, arguments[commandIndex + 2]);
			await device.ChildSetup.UnpairAsync (childDeviceId).ConfigureAwait (false);
			Console.WriteLine ($"Unpaired child device: {childDeviceId}");
			return 0;
			}
		default:
			return Fail ($"Unknown setup action '{action}'. Use 'scan', 'detected', 'pair', or 'unpair'.");
		}
	}

static async Task<int> RunChildWatchAsync (string host, string childSelector, IReadOnlyList<string> arguments, int optionStartIndex)
	{
	DeviceConfiguration configuration = CreateHostConfiguration (host, true, "state", arguments, optionStartIndex);
	KasaDevice device = await Discover.ConnectAsync (configuration).ConfigureAwait (false);
	ConsoleRecentHostStore.Save (device.Host);
	ConsoleImplicitProfileStore.Save (CreateImplicitProfile (device));
	string childDeviceId = ResolveChildIdentifier (device, childSelector);
	if (device.GetChildDevice (childDeviceId) is not ChildDevice child || child.Info is not ChildDeviceInfo initialChildInfo)
		{
		return Fail ($"Child device '{childDeviceId}' did not return current information from host '{host}'.");
		}

	PrintChildSummary (device, child, initialChildInfo);
	Console.WriteLine ("Watching child events. Press Esc to stop.");
	ChildDeviceInfo currentChildInfo = initialChildInfo;
	string? previousSignature = BuildChildWatchSignature (currentChildInfo);
	HashSet<string> seenTriggerKeys = GetTriggerLogKeys (child);
	while (true)
		{
			if (Console.KeyAvailable && Console.ReadKey (intercept: true).Key == ConsoleKey.Escape)
				{
				return 0;
				}

			await Task.Delay (1000).ConfigureAwait (false);
			await device.UpdateAsync ().ConfigureAwait (false);
			if (device.GetChild (childDeviceId) is not ChildDeviceInfo updatedChildInfo)
				{
				continue;
				}

			currentChildInfo = updatedChildInfo;
			string? signature = BuildChildWatchSignature (currentChildInfo);
			if (string.Equals (signature, previousSignature, StringComparison.Ordinal))
				{
				continue;
				}

			previousSignature = signature;
			List<string> newTriggerLines = GetTriggerLogEntries (child)
				.Where (static entry => !string.IsNullOrWhiteSpace (entry.Key))
				.Where (entry => seenTriggerKeys.Add (entry.Key))
				.Select (static entry => entry.DisplayText)
				.ToList ();
			if (newTriggerLines.Count == 0)
				{
				continue;
				}

			Console.WriteLine ($"[{DateTime.Now:HH:mm:ss}] New trigger events:");
			foreach (string line in newTriggerLines)
				{
				Console.WriteLine ($"  {line}");
				}
		}
	}

static void PrintChildSummary (KasaDevice device, ChildDevice child, ChildDeviceInfo childInfo, string indent = "")
	{
	Console.WriteLine ($"{indent}Host: {device.Host}");
	Console.WriteLine ($"{indent}Child ID: {child.Id}");
	Console.WriteLine ($"{indent}Alias: {child.Alias ?? "(no alias)"}");
	Console.WriteLine ($"{indent}Model: {child.Model ?? "(unknown)"}");
	Console.WriteLine ($"{indent}Type: {childInfo.DeviceType}");
	Console.WriteLine ($"{indent}State: {FormatChildState (child, childInfo)}");
	PrintTypedChildModuleState (child, indent);
	if (childInfo.Features.Count > 0)
		{
		Console.WriteLine ($"{indent}Features:");
		foreach (DeviceFeature feature in childInfo.Features)
			{
			Console.WriteLine ($"{indent}  {feature.Name} ({feature.Id}): {FormatFeatureValue (feature)}");
			}
		}
	Console.WriteLine ();
	}

static void PrintRecentTriggerDetails (ChildDevice child, ChildDeviceInfo childInfo, int maxLogCount)
	{
	if (child.Motion.MotionDetected is bool motionDetected)
		{
		Console.WriteLine ($"Motion detected: {FormatNullableBoolean (motionDetected)}");
		}
	if (child.Contact.IsOpen is bool isOpen)
		{
		Console.WriteLine ($"Open: {FormatNullableBoolean (isOpen)}");
		}
	if (child.WaterLeak.Alert is bool waterAlert)
		{
		Console.WriteLine ($"Water alert: {FormatNullableBoolean (waterAlert)}");
		}
	if (TryGetFeature (childInfo, "double_click_enabled") is DeviceFeature doubleClickEnabled)
		{
		Console.WriteLine ($"Double click enabled: {FormatFeatureValue (doubleClickEnabled)}");
		}
	if (child.WaterLeak.AlertTimestamp is long triggerTimestamp)
		{
		Console.WriteLine ($"Last alert timestamp: {FormatUnixTimestamp (triggerTimestamp.ToString (CultureInfo.InvariantCulture))}");
		}

	List<TriggerLogEntry> logs = GetTriggerLogEntries (child)
		.Take (maxLogCount)
		.ToList ();
	if (logs.Count == 0)
		{
		Console.WriteLine ("Recent triggers: none reported");
		return;
		}

	Console.WriteLine ("Recent triggers:");
	foreach (TriggerLogEntry log in logs)
		{
			Console.WriteLine ($"  {log.DisplayText}");
		}
	}

static void PrintTypedChildModuleState (ChildDevice child, string indent = "")
	{
	if (child.Battery.State is ChildBatterySensorState batteryState)
		{
		Console.WriteLine ($"{indent}Battery Module:");
		if (batteryState.BatteryLevel is int batteryLevel)
			{
			Console.WriteLine ($"{indent}  Battery level: {batteryLevel} %");
			}
		if (batteryState.BatteryLow is bool batteryLow)
			{
			Console.WriteLine ($"{indent}  Battery low: {FormatNullableBoolean (batteryLow)}");
			}
		}

	if (child.Temperature.State is ChildTemperatureSensorState temperatureState)
		{
		Console.WriteLine ($"{indent}Temperature Module:");
		if (temperatureState.Temperature is double temperature)
			{
			Console.WriteLine ($"{indent}  Temperature: {temperature.ToString (CultureInfo.InvariantCulture)} {temperatureState.Unit ?? "celsius"}");
			}
		if (temperatureState.Warning is bool warning)
			{
			Console.WriteLine ($"{indent}  Warning: {FormatNullableBoolean (warning)}");
			}
		if (temperatureState.MinimumComfortTemperature is double minimumComfortTemperature)
			{
			Console.WriteLine ($"{indent}  Minimum comfort temperature: {minimumComfortTemperature.ToString (CultureInfo.InvariantCulture)} {temperatureState.Unit ?? "celsius"}");
			}
		if (temperatureState.MaximumComfortTemperature is double maximumComfortTemperature)
			{
			Console.WriteLine ($"{indent}  Maximum comfort temperature: {maximumComfortTemperature.ToString (CultureInfo.InvariantCulture)} {temperatureState.Unit ?? "celsius"}");
			}
		}

	if (child.Humidity.State is ChildHumiditySensorState humidityState)
		{
		Console.WriteLine ($"{indent}Humidity Module:");
		if (humidityState.Humidity is int humidity)
			{
			Console.WriteLine ($"{indent}  Humidity: {humidity} %");
			}
		if (humidityState.Warning is bool warning)
			{
			Console.WriteLine ($"{indent}  Warning: {FormatNullableBoolean (warning)}");
			}
		if (humidityState.MinimumComfortHumidity is double minimumComfortHumidity)
			{
			Console.WriteLine ($"{indent}  Minimum comfort humidity: {minimumComfortHumidity.ToString (CultureInfo.InvariantCulture)} %");
			}
		if (humidityState.MaximumComfortHumidity is double maximumComfortHumidity)
			{
			Console.WriteLine ($"{indent}  Maximum comfort humidity: {maximumComfortHumidity.ToString (CultureInfo.InvariantCulture)} %");
			}
		}

	if (child.ReportMode.State is ChildReportModeState reportModeState)
		{
		Console.WriteLine ($"{indent}Report Mode Module:");
		if (reportModeState.ReportInterval is int reportInterval)
			{
			Console.WriteLine ($"{indent}  Report interval: {reportInterval} s");
			}
		}

	if (child.DoubleClick.State is ChildDoubleClickState doubleClickState)
		{
		Console.WriteLine ($"{indent}Double Click Module:");
		if (doubleClickState.Enabled is bool enabled)
			{
			Console.WriteLine ($"{indent}  Enabled: {FormatNullableBoolean (enabled)}");
			}
		}

	if (child.FrostProtection.State is ChildFrostProtectionState frostProtectionState)
		{
		Console.WriteLine ($"{indent}Frost Protection Module:");
		if (frostProtectionState.Enabled is bool enabled)
			{
			Console.WriteLine ($"{indent}  Enabled: {FormatNullableBoolean (enabled)}");
			}
		if (frostProtectionState.MinimumTemperature is int minimumTemperature)
			{
			Console.WriteLine ($"{indent}  Minimum temperature: {minimumTemperature} {frostProtectionState.Unit ?? "celsius"}");
			}
		}

	if (child.ChildProtection.State is ChildProtectionState childProtectionState)
		{
		Console.WriteLine ($"{indent}Child Protection Module:");
		if (childProtectionState.Enabled is bool enabled)
			{
			Console.WriteLine ($"{indent}  Enabled: {FormatNullableBoolean (enabled)}");
			}
		}

	if (child.TemperatureControl.State is ChildTemperatureControlState temperatureControlState)
		{
		Console.WriteLine ($"{indent}Temperature Control Module:");
		if (temperatureControlState.Enabled is bool enabled)
			{
			Console.WriteLine ($"{indent}  Enabled: {FormatNullableBoolean (enabled)}");
			}
		if (temperatureControlState.TargetTemperature is double targetTemperature)
			{
			Console.WriteLine ($"{indent}  Target temperature: {targetTemperature.ToString (CultureInfo.InvariantCulture)} {child.Thermostat.Unit ?? child.Temperature.Unit ?? "celsius"}");
			}
		if (temperatureControlState.MinimumTargetTemperature is int minimumTargetTemperature)
			{
			Console.WriteLine ($"{indent}  Minimum target temperature: {minimumTargetTemperature} {child.Thermostat.Unit ?? child.Temperature.Unit ?? "celsius"}");
			}
		if (temperatureControlState.MaximumTargetTemperature is int maximumTargetTemperature)
			{
			Console.WriteLine ($"{indent}  Maximum target temperature: {maximumTargetTemperature} {child.Thermostat.Unit ?? child.Temperature.Unit ?? "celsius"}");
			}
		if (temperatureControlState.TemperatureOffset is int temperatureOffset)
			{
			Console.WriteLine ($"{indent}  Temperature offset: {temperatureOffset} {child.Thermostat.Unit ?? child.Temperature.Unit ?? "celsius"}");
			}
		if (temperatureControlState.States.Count > 0)
			{
			Console.WriteLine ($"{indent}  States: {string.Join (", ", temperatureControlState.States)}");
			}
		}

	if (child.Thermostat.State is ChildThermostatState thermostatState)
		{
		Console.WriteLine ($"{indent}Thermostat Module:");
		if (thermostatState.Enabled is bool enabled)
			{
			Console.WriteLine ($"{indent}  Enabled: {FormatNullableBoolean (enabled)}");
			}
		if (thermostatState.CurrentTemperature is double currentTemperature)
			{
			Console.WriteLine ($"{indent}  Current temperature: {currentTemperature.ToString (CultureInfo.InvariantCulture)} {thermostatState.Unit ?? "celsius"}");
			}
		if (thermostatState.TargetTemperature is double targetTemperature)
			{
			Console.WriteLine ($"{indent}  Target temperature: {targetTemperature.ToString (CultureInfo.InvariantCulture)} {thermostatState.Unit ?? "celsius"}");
			}
		if (thermostatState.States.Count > 0)
			{
			Console.WriteLine ($"{indent}  States: {string.Join (", ", thermostatState.States)}");
			}
		}

	if (child.Motion.State is ChildMotionSensorState motionState)
		{
		Console.WriteLine ($"{indent}Motion Module:");
		if (motionState.MotionDetected is bool motionDetected)
			{
			Console.WriteLine ($"{indent}  Motion detected: {FormatNullableBoolean (motionDetected)}");
			}
		}

	if (child.Contact.State is ChildContactSensorState contactState)
		{
		Console.WriteLine ($"{indent}Contact Module:");
		if (contactState.IsOpen is bool isOpen)
			{
			Console.WriteLine ($"{indent}  Open: {FormatNullableBoolean (isOpen)}");
			}
		}

	if (child.WaterLeak.State is ChildWaterLeakSensorState waterLeakState)
		{
		Console.WriteLine ($"{indent}Water Leak Module:");
		if (!string.IsNullOrWhiteSpace (waterLeakState.Status))
			{
			Console.WriteLine ($"{indent}  Status: {waterLeakState.Status}");
			}
		if (waterLeakState.Alert is bool alert)
			{
			Console.WriteLine ($"{indent}  Alert: {FormatNullableBoolean (alert)}");
			}
		if (waterLeakState.AlertTimestamp is long alertTimestamp)
			{
			Console.WriteLine ($"{indent}  Alert timestamp: {FormatUnixTimestamp (alertTimestamp.ToString (CultureInfo.InvariantCulture))}");
			}
		}
	}

static DeviceFeature? TryGetFeature (ChildDeviceInfo childInfo, string featureId) =>
	childInfo.Features.FirstOrDefault (feature => string.Equals (feature.Id, featureId, StringComparison.OrdinalIgnoreCase));

static List<TriggerLogEntry> GetTriggerLogEntries (ChildDevice child)
	{
	IReadOnlyList<ChildTriggerLogEntry> logs = child.TriggerLogs.Logs;
	if (logs.Count == 0)
		{
		return [];
		}

	var entries = new List<TriggerLogEntry> ();
	foreach (ChildTriggerLogEntry log in logs)
		{
			string eventName = log.EventName ?? "(unknown event)";
			string eventId = log.EventId ?? string.Empty;
			string timestampRaw = log.Timestamp?.ToString (CultureInfo.InvariantCulture) ?? string.Empty;
			string timestamp = string.IsNullOrWhiteSpace (timestampRaw)
				? "(no timestamp)"
				: FormatUnixTimestamp (timestampRaw);
			string displayText = $"{timestamp} | {eventName}{(string.IsNullOrWhiteSpace (eventId) ? string.Empty : " | " + eventId)}";
			string key = string.IsNullOrWhiteSpace (eventId)
				? timestampRaw + "|" + eventName
				: eventId;
			entries.Add (new TriggerLogEntry (key, displayText));
		}

	return entries;
	}

static HashSet<string> GetTriggerLogKeys (ChildDevice child) =>
	new (GetTriggerLogEntries (child)
		.Where (static entry => !string.IsNullOrWhiteSpace (entry.Key))
		.Select (static entry => entry.Key), StringComparer.OrdinalIgnoreCase);

static string? BuildChildWatchSignature (ChildDeviceInfo childInfo)
	{
	using JsonDocument document = JsonDocument.Parse (childInfo.RawJson);
	string triggerLogs = document.RootElement.TryGetProperty ("trigger_logs", out JsonElement triggerLogsElement)
		&& triggerLogsElement.ValueKind == JsonValueKind.Object
		? triggerLogsElement.GetRawText ()
		: string.Empty;
	string triggerTimestamp = document.RootElement.TryGetProperty ("trigger_timestamp", out JsonElement timestampElement)
		? timestampElement.ToString ()
		: string.Empty;
	return triggerTimestamp + "|" + triggerLogs;
	}

static async Task<int> RunLightAsync (string host, IReadOnlyList<string> arguments, int commandIndex)
	{
	if (HasHelpArgument (arguments, commandIndex + 1))
		{
		PrintLightUsage ();
		return 0;
		}

	bool hasExplicitAction = arguments.Count > commandIndex + 1 && !arguments[commandIndex + 1].StartsWith ("--", StringComparison.Ordinal);
	string action = hasExplicitAction ? ResolveKeyword (arguments[commandIndex + 1], ConsoleCommandLexicon.LightActions) : "state";
	int optionStartIndex = hasExplicitAction
		? action switch
			{
				"brightness" => commandIndex + 3,
				"temp" => commandIndex + 3,
				"hsv" => commandIndex + 5,
				"color" => DetermineColorOptionStartIndex (arguments, commandIndex),
				"effect" => DetermineEffectOptionStartIndex (arguments, commandIndex),
				_ => commandIndex + 2,
			}
		: commandIndex + 1;
	DeviceConfiguration configuration = CreateHostConfiguration (host, true, action, arguments, optionStartIndex);
	KasaDevice device = await Discover.ConnectAsync (configuration).ConfigureAwait (false);
	ConsoleRecentHostStore.Save (device.Host);
	ConsoleImplicitProfileStore.Save (CreateImplicitProfile (device));

	switch (action)
		{
		case "state":
			break;
		case "on":
			await device.Light.TurnOnAsync ().ConfigureAwait (false);
			break;
		case "off":
			await device.Light.TurnOffAsync ().ConfigureAwait (false);
			break;
		case "brightness":
			await device.Light.SetBrightnessAsync (int.Parse (GetRequiredValue (arguments, commandIndex + 2, "brightness"), CultureInfo.InvariantCulture)).ConfigureAwait (false);
			break;
		case "temp":
			await device.Light.SetColorTemperatureAsync (int.Parse (GetRequiredValue (arguments, commandIndex + 2, "temp"), CultureInfo.InvariantCulture)).ConfigureAwait (false);
			break;
		case "hsv":
			await device.Light.SetHsvAsync (
				int.Parse (GetRequiredValue (arguments, commandIndex + 2, "hue"), CultureInfo.InvariantCulture),
				int.Parse (GetRequiredValue (arguments, commandIndex + 3, "saturation"), CultureInfo.InvariantCulture),
				int.Parse (GetRequiredValue (arguments, commandIndex + 4, "value"), CultureInfo.InvariantCulture)).ConfigureAwait (false);
			break;
		case "color":
			LightColorDefinition color = ParseColorArguments (arguments, commandIndex);
			if (color.ColorTemperature is int whiteColorTemperature)
				{
				await device.Light.SetColorTemperatureAsync (whiteColorTemperature).ConfigureAwait (false);
				if (color.Brightness is int whiteBrightness)
					{
					await device.Light.SetBrightnessAsync (whiteBrightness).ConfigureAwait (false);
					}
				}
			else
				{
				await device.Light.SetHsvAsync (color.Hue, color.Saturation, color.Value).ConfigureAwait (false);
				}
			break;
		case "effect":
			if (arguments.Count <= commandIndex + 2)
				{
				break;
				}
			if (!device.Light.SupportsEffects)
				{
				return Fail ($"The device '{device.Host}' does not report light-effect support.");
				}

			string effectValue = GetRequiredValue (arguments, commandIndex + 2, "effect");
			if (ResolveEffectListValue (effectValue))
				{
				PrintAvailableEffects (device.Light.AvailableEffects);
				break;
				}

			string effectAction = ResolveEffectValue (effectValue);
			if (effectAction == "off")
				{
				await device.Light.ClearEffectAsync ().ConfigureAwait (false);
				}
			else
				{
				await device.Light.SetEffectAsync (effectValue).ConfigureAwait (false);
				}
			break;
		default:
			return Fail ($"Unknown light action '{action}'. Use 'state', 'on', 'off', 'brightness', 'temp', 'hsv', 'color', or 'effect'.");
		}

	Console.WriteLine ($"Host: {device.Host}");
	Console.WriteLine ($"Type: {device.DeviceType}");
	Console.WriteLine ($"State: {FormatPowerState (device.Light.State?.IsOn)}");
	Console.WriteLine ($"Brightness: {FormatNullableInt (device.Light.State?.Brightness, "%")}" );
	bool hasColorTemperature = device.Light.State?.ColorTemperature is int colorTemperature && colorTemperature > 0;
	bool hasColor = device.Light.State?.Hue is int hue && hue != 0
		|| device.Light.State?.Saturation is int saturation && saturation != 0;
	Console.WriteLine ($"Color Temperature: {(hasColorTemperature ? FormatNullableInt (device.Light.State?.ColorTemperature, "K") : "N/A")}" );
	Console.WriteLine ($"Hue: {(hasColor ? FormatNullableInt (device.Light.State?.Hue, null) : "N/A")}" );
	Console.WriteLine ($"Saturation: {(hasColor ? FormatNullableInt (device.Light.State?.Saturation, null) : "N/A")}" );
	Console.WriteLine ($"Value: {(hasColor ? FormatNullableInt (device.Light.Hsv?.Value, "%") : "N/A")}" );
	Console.WriteLine ($"Effect: {FormatEffectName (device.Light.Effect)}" );
	Console.WriteLine ($"Effect Enabled: {FormatNullableBoolean (device.Light.Effect?.IsEnabled)}" );
	return 0;
	}

static async Task<int> RunInteractiveAsync ()
	{
	PrintGeneralUsage ();
	Console.WriteLine ();
	Console.WriteLine ("Interactive mode. Type 'help' for usage or 'exit' to quit.");

	while (true)
		{
		Console.Write ("> ");
		string? line = Console.ReadLine ();
		if (line is null)
			{
			return 0;
			}

		string trimmed = line.Trim ();
		if (trimmed.Length == 0)
			{
			continue;
			}

		string command = trimmed.ToLowerInvariant ();
		if (command is "exit" or "quit")
			{
			return 0;
			}

		try
			{
			await RunAsync (SplitArguments (trimmed)).ConfigureAwait (false);
			}
		catch (Exception ex)
			{
			Console.Error.WriteLine (FormatExceptionDetails (ex));
			}
		}
	}

static string FormatExceptionDetails (Exception exception)
	{
	var messages = new List<string> ();
	Exception? current = exception;
	while (current is not null)
		{
		messages.Add ($"{current.GetType ().Name}: {current.Message}");
		current = current.InnerException;
		}

	return string.Join (Environment.NewLine + "  -> ", messages);
	}

static string FormatPowerState (bool? isOn) => isOn switch
	{
		true => "On",
		false => "Off",
		null => "Unknown",
		};

static string FormatChildState (ChildDevice child, ChildDeviceInfo childInfo)
	{
	if (child.Thermostat.CurrentTemperature is double currentTemperature)
		{
		string unit = child.Thermostat.Unit ?? "celsius";
		return currentTemperature.ToString (CultureInfo.InvariantCulture) + " " + unit;
		}

	if (child.Temperature.Temperature is double temperature)
		{
		string unit = child.Temperature.Unit ?? "celsius";
		return temperature.ToString (CultureInfo.InvariantCulture) + " " + unit;
		}

	if (child.Thermostat.TargetTemperature is double targetTemperature)
		{
		string unit = child.Thermostat.Unit ?? "celsius";
		return "Target " + targetTemperature.ToString (CultureInfo.InvariantCulture) + " " + unit;
		}

	return childInfo.DeviceType switch
		{
			DeviceType.Sensor => "N/A",
			DeviceType.Thermostat => "N/A",
			DeviceType.Camera => "N/A",
			DeviceType.Hub => "N/A",
			_ => FormatPowerState (childInfo.IsOn),
		};
	}

static string FormatFeatureValue (DeviceFeature feature)
	{
	if (feature.Kind == FeatureKind.Action)
		{
		return "<Action>";
		}

	string value = feature.Value?.ToString () ?? "None";
	if (feature.Choices.Count > 0)
		{
		if (string.Equals (value, "None", StringComparison.Ordinal))
			{
			return value;
			}

		var formattedChoices = new List<string> (feature.Choices.Count);
		foreach (string choice in feature.Choices)
			{
			formattedChoices.Add (string.Equals (choice, value, StringComparison.Ordinal)
				? $"*{choice}*"
				: choice);
			}

		return string.Join (" ", formattedChoices);
		}

	if (string.Equals (feature.Id, "ssid", StringComparison.OrdinalIgnoreCase))
		{
		return DecodeConsoleBase64Text (value);
		}

	if (string.Equals (value, "None", StringComparison.Ordinal))
		{
		return value;
		}
	if (string.Equals (feature.Id, "water_alert_timestamp", StringComparison.OrdinalIgnoreCase))
		{
		return FormatUnixTimestamp (value);
		}

	if (feature.MinimumValue is double minimumValue || feature.MaximumValue is double maximumValue)
		{
		string minimumText = feature.MinimumValue?.ToString (CultureInfo.InvariantCulture) ?? "?";
		string maximumText = feature.MaximumValue?.ToString (CultureInfo.InvariantCulture) ?? "?";
		return $"{value} (range: {minimumText}-{maximumText})";
		}

	return string.IsNullOrWhiteSpace (feature.Unit) ? value : $"{value} {feature.Unit}";
	}

static string FormatUnixTimestamp (string? value)
	{
	if (!long.TryParse (value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long timestamp))
		{
		return value ?? "N/A";
		}

	DateTimeOffset offset = timestamp >= 1000000000000
		? DateTimeOffset.FromUnixTimeMilliseconds (timestamp)
		: DateTimeOffset.FromUnixTimeSeconds (timestamp);
	return $"{offset.LocalDateTime:yyyy-MM-dd HH:mm:ss} ({timestamp})";
	}

static string DecodeConsoleBase64Text (string value)
	{
	if (string.IsNullOrWhiteSpace (value) || value.Length % 4 != 0)
		{
		return value;
		}

	foreach (char character in value)
		{
		if ((character >= 'A' && character <= 'Z')
			|| (character >= 'a' && character <= 'z')
			|| (character >= '0' && character <= '9')
			|| character == '+'
			|| character == '/'
			|| character == '=')
			{
			continue;
			}

		return value;
		}

	try
		{
		string decoded = System.Text.Encoding.UTF8.GetString (Convert.FromBase64String (value));
		return string.IsNullOrWhiteSpace (decoded) ? value : decoded;
		}
	catch
		{
		return value;
		}
	}

static string FormatNumber (double? value, string unit) => value is double numericValue
	? $"{numericValue} {unit}"
	: "(unsupported)";

static string FormatNullableInt (int? value, string? unit) => value is int intValue
	? string.IsNullOrWhiteSpace (unit) ? intValue.ToString (CultureInfo.InvariantCulture) : $"{intValue} {unit}"
	: "(unsupported)";

static string FormatNullableBoolean (bool? value) => value switch
	{
		true => "On",
		false => "Off",
		null => "N/A",
		};

static string FormatEffectName (LightEffectState? effect)
	{
	if (effect is null)
		{
		return "N/A";
		}

	return effect.Name ?? effect.Identifier ?? "N/A";
	}

static void PrintRuleState (RuleModuleState ruleState)
	{
	if (ruleState.Countdown is not null)
		{
		Console.WriteLine ("Countdown:");
		Console.WriteLine ($"  Enabled: {FormatNullableBoolean (ruleState.Countdown.IsEnabled)}");
		Console.WriteLine ($"  Active: {FormatNullableBoolean (ruleState.Countdown.IsActive)}");
		Console.WriteLine ($"  Delay: {FormatNullableInt (ruleState.Countdown.DelaySeconds, "s")}" );
		Console.WriteLine ($"  Action: {FormatActionState (ruleState.Countdown.ActionTurnsOn)}");
		}

	if (ruleState.Schedules.Count > 0)
		{
		Console.WriteLine ("Schedules:");
		foreach (ScheduledRule rule in ruleState.Schedules)
			{
			Console.WriteLine ($"  {rule.Id} | {rule.Name ?? "(unnamed)"} | {FormatNullableBoolean (rule.IsEnabled)} | {FormatActionState (rule.ActionTurnsOn)} | {FormatRuleWindow (rule)}");
			}
		}

	if (ruleState.AntitheftRules.Count > 0)
		{
		Console.WriteLine ("Antitheft:");
		foreach (ScheduledRule rule in ruleState.AntitheftRules)
			{
			Console.WriteLine ($"  {rule.Id} | {rule.Name ?? "(unnamed)"} | {FormatNullableBoolean (rule.IsEnabled)} | {FormatActionState (rule.ActionTurnsOn)} | {FormatRuleWindow (rule)}");
			}
		}
	}

static string FormatActionState (bool? turnsOn) => turnsOn switch
	{
		true => "On",
		false => "Off",
		null => "N/A",
		};

static string FormatRuleWindow (ScheduledRule rule)
	{
	if (rule.StartMinute is not int startMinute)
		{
		return "N/A";
		}

	string start = FormatMinuteOfDay (startMinute);
	return rule.EndMinute is int endMinute
		? $"{start} - {FormatMinuteOfDay (endMinute)}"
		: start;
	}

static string FormatMinuteOfDay (int minuteOfDay)
	{
	int normalizedMinute = ((minuteOfDay % 1440) + 1440) % 1440;
	int hour = normalizedMinute / 60;
	int minute = normalizedMinute % 60;
	return $"{hour:D2}:{minute:D2}";
	}

static void PrintAvailableEffects (IReadOnlyList<LightEffectDefinition> availableEffects)
	{
	if (availableEffects.Count == 0)
		{
		Console.WriteLine ("Available Effects: none reported");
		return;
		}

	Console.WriteLine ("Available Effects:");
	foreach (LightEffectDefinition effect in availableEffects)
		{
		string displayName = effect.Name ?? "(no display name)";
		Console.WriteLine (StringComparer.OrdinalIgnoreCase.Equals (effect.Identifier, displayName)
			? $"  {displayName}"
			: $"  {effect.Identifier} | {displayName}");
		}
	}

static int DetermineColorOptionStartIndex (IReadOnlyList<string> arguments, int commandIndex)
	{
	string colorValue = GetRequiredValue (arguments, commandIndex + 2, "color");
	return TryParseNamedColor (colorValue, out _)
		? commandIndex + 3
		: commandIndex + 5;
	}

static int DetermineEffectOptionStartIndex (IReadOnlyList<string> arguments, int commandIndex) =>
	arguments.Count > commandIndex + 2 ? commandIndex + 3 : commandIndex + 2;

static LightColorDefinition ParseColorArguments (IReadOnlyList<string> arguments, int commandIndex)
	{
	string colorValue = GetRequiredValue (arguments, commandIndex + 2, "color");
	if (TryParseNamedColor (colorValue, out LightColorDefinition? namedColor) && namedColor is not null)
		{
		return namedColor;
		}

	return LightColorDefinition.FromHsv (
		int.Parse (colorValue, CultureInfo.InvariantCulture),
		int.Parse (GetRequiredValue (arguments, commandIndex + 3, "saturation"), CultureInfo.InvariantCulture),
		int.Parse (GetRequiredValue (arguments, commandIndex + 4, "value"), CultureInfo.InvariantCulture));
	}

static bool TryParseNamedColor (string value, out LightColorDefinition? color)
	{
	switch (value.Trim ().ToLowerInvariant ())
		{
		case "red":
			color = LightColorDefinition.FromHsv (0, 100, 100);
			return true;
		case "orange":
			color = LightColorDefinition.FromHsv (30, 100, 100);
			return true;
		case "yellow":
			color = LightColorDefinition.FromHsv (60, 100, 100);
			return true;
		case "green":
			color = LightColorDefinition.FromHsv (120, 100, 100);
			return true;
		case "cyan":
			color = LightColorDefinition.FromHsv (180, 100, 100);
			return true;
		case "blue":
			color = LightColorDefinition.FromHsv (240, 100, 100);
			return true;
		case "purple":
		case "violet":
			color = LightColorDefinition.FromHsv (270, 100, 100);
			return true;
		case "magenta":
		case "pink":
			color = LightColorDefinition.FromHsv (300, 100, 100);
			return true;
		case "white":
			color = LightColorDefinition.FromWhiteTemperature (4000, 100);
			return true;
		case "warmwhite":
		case "warm-white":
			color = LightColorDefinition.FromWhiteTemperature (2700, 75);
			return true;
		case "softwhite":
		case "soft-white":
			color = LightColorDefinition.FromWhiteTemperature (3000, 85);
			return true;
		case "daylight":
			color = LightColorDefinition.FromWhiteTemperature (5500, 100);
			return true;
		default:
			color = null;
			return false;
		}
	}

static string ResolveEffectValue (string value)
	{
	string candidate = value.Trim ().ToLowerInvariant ();
	return candidate switch
		{
			"off" => "off",
			"none" => "off",
			"clear" => "off",
			_ => value,
			};
	}

static bool ResolveEffectListValue (string value)
	{
	string candidate = value.Trim ().ToLowerInvariant ();
	return candidate is "list" or "ls";
	}

static DeviceConfiguration CreateHostConfiguration (string host, bool hostWasExplicit, string action, IReadOnlyList<string> arguments, int optionStartIndex)
	{
	string? requestedProfileName = null;
	string? saveProfileName = null;
	string? clearProfileName = null;
	for (int i = optionStartIndex; i < arguments.Count; i++)
		{
		string option = arguments[i];
		switch (option)
			{
			case PROFILE_USE_OPTION:
				requestedProfileName = GetRequiredValue (arguments, ++i, option);
				break;
			case PROFILE_SAVE_OPTION:
				saveProfileName = GetOptionalNamedValue (arguments, ref i, option);
				break;
			case PROFILE_CLEAR_OPTION:
				clearProfileName = GetOptionalNamedValue (arguments, ref i, option);
				break;
			}
		}

	if (!string.IsNullOrWhiteSpace (clearProfileName))
		{
		ConsoleProfileStore.Remove (clearProfileName!);
		}

	string explicitHost = host;
	SavedConnectionProfile? savedProfile = string.IsNullOrWhiteSpace (requestedProfileName)
		? null
		: ConsoleProfileStore.Load (requestedProfileName!);
	host = savedProfile?.Host ?? host;
	if (savedProfile is not null)
		{
		ConsoleRecentHostStore.Save (host);
		ConsoleImplicitProfileStore.Save (CreateImplicitProfileFromSavedProfile (savedProfile));
		}
	SavedConnectionProfile? implicitProfile = string.IsNullOrWhiteSpace (requestedProfileName)
		? ConsoleImplicitProfileStore.Load (host)
		: null;
	SavedConnectionProfile? recentHostProfile = savedProfile;
	if (recentHostProfile is null
		&& implicitProfile is not null
		&& string.Equals (implicitProfile.Host, host, StringComparison.OrdinalIgnoreCase))
		{
		recentHostProfile = implicitProfile;
		}
	DeviceTransportKind transportKind = DeviceTransportKind.Auto;
	bool useSsl = false;
	int port = 9999;
	string? userName = null;
	string? password = null;
	DefaultCredentialProfile defaultCredentialProfile = DefaultCredentialProfile.None;
	string applicationPath = "/app";
	bool useSecurePassthrough = true;
	DeviceConnectionParameters? connectionParameters = null;

	if (recentHostProfile is not null)
		{
		userName = recentHostProfile.UserName;
		password = recentHostProfile.Password;
		defaultCredentialProfile = recentHostProfile.DefaultCredentialProfile;
		if (savedProfile is not null
			|| recentHostProfile.TransportKind == DeviceTransportKind.LegacyXor
			|| recentHostProfile.ConnectionParameters is not null)
			{
			transportKind = recentHostProfile.TransportKind;
			useSsl = recentHostProfile.UseSsl;
			port = recentHostProfile.Port;
			applicationPath = recentHostProfile.ApplicationPath;
			useSecurePassthrough = recentHostProfile.UseSecurePassthrough;
			connectionParameters = recentHostProfile.ConnectionParameters;
			}
		}
	else if (implicitProfile is not null)
		{
		userName = implicitProfile.UserName;
		password = implicitProfile.Password;
		defaultCredentialProfile = implicitProfile.DefaultCredentialProfile;
		}

	for (int i = optionStartIndex; i < arguments.Count; i++)
		{
		string option = arguments[i];
		switch (option)
			{
			case "--http":
					connectionParameters = null;
				transportKind = DeviceTransportKind.HttpToken;
				useSsl = false;
				break;
			case "--https":
			case "--ssl":
					connectionParameters = null;
				transportKind = DeviceTransportKind.HttpToken;
				useSsl = true;
				break;
			case "--transport":
					connectionParameters = null;
				transportKind = ParseTransportKind (GetRequiredValue (arguments, ++i, option));
				break;
			case "--port":
					connectionParameters = null;
				port = int.Parse (GetRequiredValue (arguments, ++i, option), CultureInfo.InvariantCulture);
				break;
			case "--username":
				userName = GetRequiredValue (arguments, ++i, option);
				break;
			case "--password":
				password = GetRequiredValue (arguments, ++i, option);
				break;
			case "--default-creds":
					connectionParameters = null;
				defaultCredentialProfile = ParseDefaultCredentialProfile (GetRequiredValue (arguments, ++i, option));
				transportKind = DeviceTransportKind.HttpToken;
				break;
			case "--app-path":
					connectionParameters = null;
				applicationPath = GetRequiredValue (arguments, ++i, option);
				transportKind = DeviceTransportKind.HttpToken;
				break;
			case "--passthrough":
					connectionParameters = null;
				useSecurePassthrough = true;
				transportKind = DeviceTransportKind.HttpToken;
				break;
			case "--no-passthrough":
					connectionParameters = null;
				useSecurePassthrough = false;
				transportKind = DeviceTransportKind.HttpToken;
				break;
			case PROFILE_USE_OPTION:
				++i;
				break;
			case PROFILE_SAVE_OPTION:
				if (HasFollowingNamedValue (arguments, i))
					{
					i++;
					}
				break;
			case PROFILE_CLEAR_OPTION:
				if (HasFollowingNamedValue (arguments, i))
					{
					i++;
					}
				break;
			default:
				throw new ArgumentException ($"Unknown option '{option}' for host action '{action}'.");
			}
		}

	DeviceCredentials? credentials = string.IsNullOrWhiteSpace (userName) && string.IsNullOrWhiteSpace (password)
		? null
		: new DeviceCredentials (userName, password);

	var connectionOptions = new DeviceConnectionOptions (
		transportKind,
		connectionParameters: connectionParameters,
		useSsl: useSsl,
		useDefaultCredentials: defaultCredentialProfile != DefaultCredentialProfile.None,
		defaultCredentialProfile: defaultCredentialProfile,
		applicationPath: applicationPath,
		useSecurePassthrough: useSecurePassthrough);
	string hostToUse = hostWasExplicit ? explicitHost : host;
	var configuration = new DeviceConfiguration (hostToUse, port, credentials, connectionOptions);
	if (saveProfileName is not null)
		{
		ConsoleProfileStore.Save (
			new SavedConnectionProfile (
				saveProfileName,
				hostToUse,
				transportKind,
				port,
				useSsl,
				userName,
				password,
				defaultCredentialProfile,
				applicationPath,
				useSecurePassthrough));
		ConsoleRecentHostStore.Save (hostToUse);
		}

	return configuration;
	}

static bool RequiresInitialStateBeforeHostAction (string action, DeviceConfiguration configuration)
	{
	if (action == "state")
		{
		return true;
		}

	DeviceConnectionParameters? connectionParameters = configuration.ConnectionOptions.ConnectionParameters;
	if (connectionParameters is null)
		{
		return true;
		}

	return connectionParameters.DeviceFamily is DeviceFamilyKind.IotSmartPlugSwitch
		or DeviceFamilyKind.IotSmartBulb
		or DeviceFamilyKind.IotIpCamera;
	}

static string GetRequiredValue (IReadOnlyList<string> arguments, int index, string option)
	{
	if (index >= arguments.Count)
		{
		throw new ArgumentException ($"Option '{option}' requires a value.");
		}

	return arguments[index];
	}

static bool TryResolveHost (IReadOnlyList<string> arguments, out string? host, out int commandIndex, out bool hostWasExplicit, out string? errorMessage)
	{
	host = null;
	commandIndex = 1;
	hostWasExplicit = false;
	errorMessage = null;
	if (arguments.Count < 2)
		{
		host = ConsoleRecentHostStore.Load ();
		if (string.IsNullOrWhiteSpace (host))
			{
			errorMessage = "The 'host' command requires a host name or IP address unless a recent host has been persisted.";
			return false;
			}

		return true;
		}

	string candidate = arguments[1].Trim ();
	if (!candidate.StartsWith ("--", StringComparison.Ordinal)
		&& !IsHostActionName (candidate))
		{
		host = candidate;
		commandIndex = 2;
		hostWasExplicit = true;
		ConsoleRecentHostStore.Save (host);
		return true;
		}

	host = ConsoleRecentHostStore.Load ();
	if (string.IsNullOrWhiteSpace (host))
		{
		errorMessage = "No persisted host is available. Specify a host name or IP address first.";
		return false;
		}

	commandIndex = 1;
	return true;
	}

static bool IsHostActionName (string value)
	{
	if (IsHelpCommand (value))
		{
		return true;
		}

	return TryResolveHostKeyword (value, out _);
	}

static bool TryRunProfilesWithoutHost (IReadOnlyList<string> arguments, out int result)
	{
	result = 0;
	if (arguments.Count == 2 && ResolveHostKeyword (arguments[1]) == "profiles")
		{
		result = RunProfilesCommand (arguments, 2);
		return true;
		}

	return false;
	}

static bool TryRunProfilesWithResolvedHost (string host, IReadOnlyList<string> arguments, int commandIndex, out int result)
	{
	result = 0;
	if (arguments.Count > commandIndex && ResolveHostKeyword (arguments[commandIndex]) == "profiles")
		{
		result = RunProfilesCommand (arguments, commandIndex + 1);
		ConsoleRecentHostStore.Save (host);
		return true;
		}

	return false;
	}

static SavedConnectionProfile CreateImplicitProfile (KasaDevice device)
	{
	DeviceConnectionOptions connectionOptions = device.Configuration.ConnectionOptions;
	SavedConnectionProfile? existingImplicitProfile = ConsoleImplicitProfileStore.Load (device.Host);
	bool preserveExistingCredentials = existingImplicitProfile is not null
		&& string.IsNullOrWhiteSpace (device.Configuration.Credentials?.UserName)
		&& string.IsNullOrWhiteSpace (device.Configuration.Credentials?.Password)
		&& connectionOptions.DefaultCredentialProfile == DefaultCredentialProfile.None;
	string? userName = preserveExistingCredentials ? existingImplicitProfile!.UserName : device.Configuration.Credentials?.UserName;
	string? password = preserveExistingCredentials ? existingImplicitProfile!.Password : device.Configuration.Credentials?.Password;
	DefaultCredentialProfile defaultCredentialProfile = preserveExistingCredentials
		? existingImplicitProfile!.DefaultCredentialProfile
		: connectionOptions.DefaultCredentialProfile;
	return new SavedConnectionProfile (
		IMPLICIT_PROFILE_NAME,
		device.Host,
		connectionOptions.TransportKind,
		device.Configuration.Port,
		connectionOptions.UseSsl,
		userName,
		password,
		defaultCredentialProfile,
		connectionOptions.ApplicationPath,
		connectionOptions.UseSecurePassthrough,
		connectionOptions.ConnectionParameters);
	}

static SavedConnectionProfile CreateImplicitProfileFromSavedProfile (SavedConnectionProfile profile) =>
	new (
		IMPLICIT_PROFILE_NAME,
		profile.Host,
		profile.TransportKind,
		profile.Port,
		profile.UseSsl,
		profile.UserName,
		profile.Password,
		profile.DefaultCredentialProfile,
		profile.ApplicationPath,
		profile.UseSecurePassthrough,
		profile.ConnectionParameters);

static string? GetOptionalNamedValue (IReadOnlyList<string> arguments, ref int index, string option)
	{
	if (HasFollowingNamedValue (arguments, index))
		{
		index++;
		return GetRequiredValue (arguments, index, option);
		}

	return DEFAULT_PROFILE_NAME;
	}

static bool HasFollowingNamedValue (IReadOnlyList<string> arguments, int index) =>
	index + 1 < arguments.Count && !arguments[index + 1].StartsWith ("--", StringComparison.Ordinal);

static string ResolveKeyword (string value, IReadOnlyList<string> keywords)
	{
	if (IsHelpCommand (value))
		{
		return "help";
		}

	if (TryResolveKeyword (value, keywords, out string? resolvedKeyword))
		{
		return resolvedKeyword!;
		}

	throw new ArgumentException ($"Unknown command '{value}'.");
	}

static string ResolveHostKeyword (string value)
	{
	if (IsHelpCommand (value))
		{
		return "help";
		}

	if (TryResolveHostKeyword (value, out string? resolvedKeyword))
		{
		return resolvedKeyword!;
		}

	throw new ArgumentException ($"Unknown command '{value}'.");
	}

static bool TryResolveKeyword (string value, IReadOnlyList<string> keywords, out string? resolvedKeyword)
	{
	string candidate = value.Trim ().ToLowerInvariant ();
	resolvedKeyword = null;
	var matches = new List<string> ();
	foreach (string keyword in keywords)
		{
		if (keyword.StartsWith (candidate, StringComparison.OrdinalIgnoreCase))
			{
			matches.Add (keyword);
			}
		}

	if (matches.Count == 1)
		{
		resolvedKeyword = matches[0];
		return true;
		}

	if (matches.Count > 1)
		{
		throw new ArgumentException ($"Ambiguous command '{value}'. Matches: {string.Join (", ", matches)}.");
		}

	return false;
	}

static bool TryResolveHostKeyword (string value, out string? resolvedKeyword)
	{
	string candidate = value.Trim ().ToLowerInvariant ();
	if (candidate is "s" or "st")
		{
		resolvedKeyword = "state";
		return true;
		}

	if (candidate is "se" or "set")
		{
		resolvedKeyword = "setup";
		return true;
		}

	return TryResolveKeyword (value, ConsoleCommandLexicon.HostCommands, out resolvedKeyword);
	}

static DeviceTransportKind ParseTransportKind (string value) => value.Trim ().ToLowerInvariant () switch
	{
		"auto" => DeviceTransportKind.Auto,
		"legacy" => DeviceTransportKind.LegacyXor,
		"legacyxor" => DeviceTransportKind.LegacyXor,
		"http" => DeviceTransportKind.HttpToken,
		"httptoken" => DeviceTransportKind.HttpToken,
		_ => throw new ArgumentException ($"Unknown transport '{value}'."),
		};

static DefaultCredentialProfile ParseDefaultCredentialProfile (string value) => value.Trim ().ToLowerInvariant () switch
	{
		"kasa" => DefaultCredentialProfile.Kasa,
		"kasa-camera" => DefaultCredentialProfile.KasaCamera,
		"tapo" => DefaultCredentialProfile.Tapo,
		"tapo-camera" => DefaultCredentialProfile.TapoCamera,
		"tapo-camera-lv3" => DefaultCredentialProfile.TapoCameraLv3,
		_ => throw new ArgumentException ($"Unknown default credential profile '{value}'."),
		};

static int Fail (string message)
	{
	Console.Error.WriteLine (message);
	return 1;
	}

static IReadOnlyList<string> SplitArguments (string commandLine)
	{
	var arguments = new List<string> ();
	var current = new List<char> ();
	bool inQuotes = false;

	for (int i = 0; i < commandLine.Length; i++)
		{
		char character = commandLine[i];
		if (character == '"')
			{
			inQuotes = !inQuotes;
			continue;
			}

		if (char.IsWhiteSpace (character) && !inQuotes)
			{
			if (current.Count > 0)
				{
				arguments.Add (new string ([.. current]));
				current.Clear ();
				}

			continue;
			}

		current.Add (character);
		}

	if (current.Count > 0)
		{
		arguments.Add (new string ([.. current]));
		}

	if (inQuotes)
		{
		throw new ArgumentException ("Unterminated quoted string in command input.");
		}

	return arguments;
	}

static bool IsHelpCommand (string value) => value is "help" or "--help" or "-h" or "/?";

static bool IsDiscoverVerboseOption (string value)
	{
	if (string.IsNullOrWhiteSpace (value)
		|| !value.StartsWith ("--", StringComparison.Ordinal))
		{
		return false;
		}

	string optionText = value.Substring (2);
	return optionText.Length > 0
		&& "verbose".StartsWith (optionText, StringComparison.OrdinalIgnoreCase);
	}

static bool HasHelpArgument (IReadOnlyList<string> arguments, int index) =>
	arguments.Count > index && IsHelpCommand (arguments[index].Trim ().ToLowerInvariant ());

static void PrintGeneralUsage ()
	{
	Console.WriteLine ("KasaTapoClient.Console");
	Console.WriteLine ("Commands:");
	Console.WriteLine ("  he[lp]");
	Console.WriteLine ("  e[xit]");
	Console.WriteLine ("  q[uit]");
	Console.WriteLine ("  d[iscover] [timeoutMs] [target] [--[v]erbose]");
	Console.WriteLine ("  ho[st] <address> [s[tate]|on|of[f]] [options]");
	Console.WriteLine ("  ho[st] <address> c[hild] <childId|index> [s[tate]|on|of[f]|l[ogs]|w[atch]] [options]");
	Console.WriteLine ("  ho[st] <address> l[ight] [s[tate]|on|of[f]|b[rightness] <0-100>|t[emp] <kelvin>|h[sv] <hue> <saturation> <value>|c[olor] <name>|c[olor] <hue> <saturation> <value>|e[ffect] [name|off|list]] [options]");
	Console.WriteLine ();
	Console.WriteLine ("Use 'discover --help', 'host <address> --help', 'host <address> child --help', or 'host <address> light --help' for details.");
	}

static void PrintDiscoverUsage ()
	{
	Console.WriteLine ("d[iscover] [timeoutMs] [target] [--[v]erbose]");
	Console.WriteLine ($"  timeoutMs  Optional discovery timeout in milliseconds. Default: {DEFAULT_DISCOVERY_TIMEOUT_MILLISECONDS}");
	Console.WriteLine ("  target     Optional IPv4 target or broadcast address. Default: 255.255.255.255");
	Console.WriteLine ("  --[v]erbose  Connect to each discovered device and print full status output.");
	}

static void PrintHostUsage ()
	{
	Console.WriteLine ("ho[st] <address> [s[tate]|on|of[f]] [options]");
	Console.WriteLine ("ho[st] <address> c[hild] <childId|index> [s[tate]|on|of[f]|l[ogs]|w[atch]] [options]");
	Console.WriteLine ("ho[st] <address> l[ight] [s[tate]|on|of[f]|b[rightness] <0-100>|t[emp] <kelvin>|h[sv] <h> <s> <v>|c[olor] <hex>|e[ffect] <name>] [options]");
	Console.WriteLine ("ho[st] <address> se[tup] [s[can] [seconds]|d[etected]|p[air]|u[npair] <childId|index>] [options]");
	Console.WriteLine ("ho[st] <address> p[rofiles] [l[ist]|r[emove] <name>] [options]");
	PrintCommonOptions ();
	}

static void PrintChildUsage ()
	{
	Console.WriteLine ("ho[st] <address> c[hild] <childId|index> [s[tate]|on|of[f]|l[ogs]|w[atch]] [options]");
	Console.WriteLine ("  logs   Show recent trigger events reported by event-driven children.");
	Console.WriteLine ("  watch  Poll for trigger/event changes until Esc is pressed.");
	PrintCommonOptions ();
	}

static void PrintChildSetupUsage ()
	{
	Console.WriteLine ("ho[st] <address> s[etup] [s[can] [seconds]|d[etected]|p[air]|u[npair] <childId|index>] [options]");
	Console.WriteLine ("  scan      Start a hub child-device scan and wait the specified number of seconds. Default: 10.");
	Console.WriteLine ("  detected  Show currently detected child devices from the hub scan state.");
	Console.WriteLine ("  pair      Pair all currently detected child devices.");
	Console.WriteLine ("  unpair    Remove the specified child device from the hub.");
	PrintCommonOptions ();
	}

static int? TryGetTimeoutSeconds (IReadOnlyList<string> arguments, int index)
	{
	if (arguments.Count <= index)
		{
		return null;
		}

	return int.TryParse (arguments[index], NumberStyles.Integer, CultureInfo.InvariantCulture, out int timeoutSeconds)
		? timeoutSeconds
		: null;
	}

static void PrintChildSetupScanResult (KasaDevice device, ChildSetupScanResult result)
	{
	Console.WriteLine ($"Host: {device.Host}");
	Console.WriteLine ($"Supported categories: {(result.SupportedCategories.Count == 0 ? "(none reported)" : string.Join (", ", result.SupportedCategories))}");
	if (result.DetectedDevices.Count == 0)
		{
		Console.WriteLine ("Detected child devices: none");
		return;
		}

	Console.WriteLine ("Detected child devices:");
	foreach (DetectedChildDevice child in result.DetectedDevices)
		{
		Console.WriteLine ($"  {child.DeviceId} | {child.Model ?? "(unknown model)"} | {child.Category ?? "(unknown category)"}");
		}
	}

static string ResolveChildIdentifier (KasaDevice device, string childSelector)
	{
	if (int.TryParse (childSelector, NumberStyles.None, CultureInfo.InvariantCulture, out int childIndex))
		{
		if (childIndex < 1 || childIndex > device.Children.Count)
			{
			throw new ArgumentOutOfRangeException (nameof (childSelector), childSelector, $"Child index must be between 1 and {device.Children.Count}.");
			}

		return device.Children[childIndex - 1].Id;
		}

	return childSelector;
	}

static void PrintLightUsage ()
	{
	Console.WriteLine ("ho[st] <address> l[ight] [s[tate]|on|of[f]|b[rightness] <0-100>|t[emp] <kelvin>|h[sv] <hue> <saturation> <value>|c[olor] <name>|c[olor] <hue> <saturation> <value>|e[ffect] [name|off|list]] [options]");
	Console.WriteLine ("Named colors: red, orange, yellow, green, cyan, blue, purple, violet, magenta, pink, white, warmwhite, softwhite, daylight");
	Console.WriteLine ("Effects: use a device-specific effect name to enable one, 'effect off' to disable the current effect, or 'effect list' to show reported effects.");
	PrintCommonOptions ();
	}

static void PrintCommonOptions ()
	{
	Console.WriteLine ("Options:");
	Console.WriteLine ("  --transport auto|legacy|http   Select the transport implementation.");
	Console.WriteLine ("  --port <port>             Override the default port.");
	Console.WriteLine ("  --http                    Use HTTP token transport.");
	Console.WriteLine ("  --https                   Use HTTPS token transport.");
	Console.WriteLine ("  --username <user>         Provide a user name or email.");
	Console.WriteLine ("  --password <password>     Provide a password.");
	Console.WriteLine ("  --default-creds <profile> Use a known default credential profile.");
	Console.WriteLine ("  --app-path <path>         Override the HTTP application path (default: /app).");
	Console.WriteLine ("  --passthrough             Use secure passthrough request wrapping for HTTP token transport.");
	Console.WriteLine ("  --no-passthrough          Disable secure passthrough request wrapping for HTTP token transport.");
	Console.WriteLine ($"  {PROFILE_USE_OPTION} <name>         Load a saved connection profile.");
	Console.WriteLine ($"  {PROFILE_SAVE_OPTION} [name]         Save the resolved connection settings. Default profile name: '{DEFAULT_PROFILE_NAME}'.");
	Console.WriteLine ($"  {PROFILE_CLEAR_OPTION} [name] Clear a saved profile before running. Default profile name: '{DEFAULT_PROFILE_NAME}'.");
	}

static int RunProfilesCommand (IReadOnlyList<string> arguments, int optionStartIndex)
	{
	string action = arguments.Count > optionStartIndex ? ResolveKeyword (arguments[optionStartIndex], ConsoleCommandLexicon.ProfileActions) : "list";
	switch (action)
		{
		case "list":
			foreach (SavedConnectionProfile profile in ConsoleProfileStore.LoadAll ())
				{
				Console.WriteLine ($"{profile.Name} | {profile.Host} | {profile.TransportKind} | Port {profile.Port}");
				}
			return 0;
		case "remove":
			if (arguments.Count <= optionStartIndex + 1)
				{
				return Fail ("The 'profiles remove' command requires a profile name.");
				}

			ConsoleProfileStore.Remove (arguments[optionStartIndex + 1]);
			return 0;
		default:
			return Fail ($"Unknown profiles action '{action}'. Use 'list' or 'remove <name>'.");
		}
	}

static class ConsoleProfileStore
	{
	private static readonly string PROFILE_PATH = Path.Combine (
		Environment.GetFolderPath (Environment.SpecialFolder.ApplicationData),
		"KasaTapoClient",
		"console-profiles.json");
	private static readonly string LEGACY_PROFILE_PATH = Path.Combine (
		Environment.GetFolderPath (Environment.SpecialFolder.ApplicationData),
		"KasaClient",
		"console-profiles.json");
	private static readonly JsonSerializerOptions SERIALIZER_OPTIONS = new ()
		{
		WriteIndented = true,
		DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
		};

	public static void Save (SavedConnectionProfile profile)
		{
		Dictionary<string, SavedConnectionProfile> profiles = LoadMap ();
		profiles[profile.Name] = profile;
		Persist (profiles);
		}

	public static SavedConnectionProfile? Load (string name)
		{
		Dictionary<string, SavedConnectionProfile> profiles = LoadMap ();
		return profiles.TryGetValue (name, out SavedConnectionProfile? profile) ? profile : null;
		}

	public static SavedConnectionProfile? LoadByHost (string? host)
		{
		if (string.IsNullOrWhiteSpace (host))
			{
			return null;
			}

		foreach (SavedConnectionProfile profile in LoadMap ().Values)
			{
			if (string.Equals (profile.Host, host, StringComparison.OrdinalIgnoreCase))
				{
				return profile;
				}
			}

		return null;
		}

	public static IReadOnlyList<SavedConnectionProfile> LoadAll () => new List<SavedConnectionProfile> (LoadMap ().Values);

	public static void Remove (string name)
		{
		Dictionary<string, SavedConnectionProfile> profiles = LoadMap ();
		if (profiles.Remove (name))
			{
			Persist (profiles);
			}
		}

	private static Dictionary<string, SavedConnectionProfile> LoadMap ()
		{
		string? path = ResolveReadPath (PROFILE_PATH, LEGACY_PROFILE_PATH);
		if (path is null)
			{
			return new Dictionary<string, SavedConnectionProfile> (StringComparer.OrdinalIgnoreCase);
			}

		string json = File.ReadAllText (path);
		Dictionary<string, SavedConnectionProfile>? profiles = JsonSerializer.Deserialize<Dictionary<string, SavedConnectionProfile>> (json);
		return profiles is null
			? new Dictionary<string, SavedConnectionProfile> (StringComparer.OrdinalIgnoreCase)
			: new Dictionary<string, SavedConnectionProfile> (profiles, StringComparer.OrdinalIgnoreCase);
		}

	private static void Persist (Dictionary<string, SavedConnectionProfile> profiles)
		{
		string? directory = Path.GetDirectoryName (PROFILE_PATH);
		if (!string.IsNullOrWhiteSpace (directory))
			{
			Directory.CreateDirectory (directory);
			}

		string json = JsonSerializer.Serialize (profiles, SERIALIZER_OPTIONS);
		File.WriteAllText (PROFILE_PATH, json);
		}

	private static string? ResolveReadPath (string currentPath, string legacyPath)
		{
		if (File.Exists (currentPath))
			{
			return currentPath;
			}

		return File.Exists (legacyPath) ? legacyPath : null;
		}
	}

static class ConsoleRecentHostStore
	{
	private static readonly string RECENT_HOST_PATH = Path.Combine (
		Environment.GetFolderPath (Environment.SpecialFolder.ApplicationData),
		"KasaTapoClient",
		"console-recent-host.txt");

	public static string? Load ()
		{
		if (!File.Exists (RECENT_HOST_PATH))
			{
			return null;
			}

		string host = File.ReadAllText (RECENT_HOST_PATH).Trim ();
		return string.IsNullOrWhiteSpace (host) ? null : host;
		}

	public static void Save (string host)
		{
		if (string.IsNullOrWhiteSpace (host))
			{
			return;
			}

		string? directory = Path.GetDirectoryName (RECENT_HOST_PATH);
		if (!string.IsNullOrWhiteSpace (directory))
			{
			Directory.CreateDirectory (directory);
			}

		File.WriteAllText (RECENT_HOST_PATH, host.Trim ());
		}
	}

static class ConsoleImplicitProfileStore
	{
	private static readonly string IMPLICIT_PROFILE_PATH = Path.Combine (
		Environment.GetFolderPath (Environment.SpecialFolder.ApplicationData),
		"KasaTapoClient",
		"console-implicit-profile.json");
	private static readonly string LEGACY_IMPLICIT_PROFILE_PATH = Path.Combine (
		Environment.GetFolderPath (Environment.SpecialFolder.ApplicationData),
		"KasaClient",
		"console-implicit-profile.json");
	private static readonly JsonSerializerOptions SERIALIZER_OPTIONS = new ()
		{
		WriteIndented = true,
		DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
		};

	public static SavedConnectionProfile? Load (string? preferredHost = null)
		{
		SavedConnectionProfile? currentProfile = LoadProfile (IMPLICIT_PROFILE_PATH);
		SavedConnectionProfile? legacyProfile = LoadProfile (LEGACY_IMPLICIT_PROFILE_PATH);

		if (string.IsNullOrWhiteSpace (preferredHost))
			{
			return currentProfile ?? legacyProfile;
			}

		if (string.Equals (currentProfile?.Host, preferredHost, StringComparison.OrdinalIgnoreCase))
			{
			return currentProfile;
			}

		if (string.Equals (legacyProfile?.Host, preferredHost, StringComparison.OrdinalIgnoreCase))
			{
			return legacyProfile;
			}

		if (currentProfile is not null)
			{
			return currentProfile;
			}

		return legacyProfile;
		}

	public static void Save (SavedConnectionProfile profile)
		{
		string? directory = Path.GetDirectoryName (IMPLICIT_PROFILE_PATH);
		if (!string.IsNullOrWhiteSpace (directory))
			{
			Directory.CreateDirectory (directory);
			}

		string json = JsonSerializer.Serialize (profile, SERIALIZER_OPTIONS);
		File.WriteAllText (IMPLICIT_PROFILE_PATH, json);
		}

	private static SavedConnectionProfile? LoadProfile (string path)
		{
		if (!File.Exists (path))
			{
			return null;
			}

		string json = File.ReadAllText (path);
		return JsonSerializer.Deserialize<SavedConnectionProfile> (json);
		}
	}

static class ConsoleCommandLexicon
	{
	public static readonly string[] TopLevelCommands = ["help", "exit", "quit", "discover", "host"];
	public static readonly string[] HostCommands = ["child", "light", "setup", "profiles", "state", "on", "off"];
	public static readonly string[] HostActions = ["state", "on", "off", "scan", "detected", "pair", "unpair"];
	public static readonly string[] ChildActions = ["state", "on", "off", "logs", "watch"];
	public static readonly string[] SetupActions = ["scan", "detected", "pair", "unpair"];
	public static readonly string[] LightActions = ["state", "on", "off", "brightness", "temp", "hsv", "color", "effect"];
	public static readonly string[] ProfileActions = ["list", "remove"];
	}

sealed class SavedConnectionProfile
	{
	[JsonConstructor]
	public SavedConnectionProfile (
		string name,
		string host,
		DeviceTransportKind transportKind,
		int port,
		bool useSsl,
		string? userName,
		string? password,
		DefaultCredentialProfile defaultCredentialProfile,
		string applicationPath,
		bool useSecurePassthrough,
		DeviceConnectionParameters? connectionParameters = null)
		{
		Name = name;
		Host = host;
		TransportKind = transportKind;
		Port = port;
		UseSsl = useSsl;
		UserName = userName;
		Password = password;
		DefaultCredentialProfile = defaultCredentialProfile;
		ApplicationPath = applicationPath;
		UseSecurePassthrough = useSecurePassthrough;
		ConnectionParameters = connectionParameters;
		}

	public string Name
		{
		get;
		}

	public string Host
		{
		get;
		}

	public DeviceTransportKind TransportKind
		{
		get;
		}

	public int Port
		{
		get;
		}

	public bool UseSsl
		{
		get;
		}

	public string? UserName
		{
		get;
		}

	public string? Password
		{
		get;
		}

	public DefaultCredentialProfile DefaultCredentialProfile
		{
		get;
		}

	public string ApplicationPath
		{
		get;
		}

	public bool UseSecurePassthrough
		{
		get;
		}

	public DeviceConnectionParameters? ConnectionParameters
		{
		get;
		}
	}

sealed class LightColorDefinition
	{
	private LightColorDefinition (int hue, int saturation, int value, int? colorTemperature, int? brightness)
		{
		Hue = hue;
		Saturation = saturation;
		Value = value;
		ColorTemperature = colorTemperature;
		Brightness = brightness;
		}

	public static LightColorDefinition FromHsv (int hue, int saturation, int value) => new (hue, saturation, value, null, null);

	public static LightColorDefinition FromWhiteTemperature (int colorTemperature, int brightness) => new (0, 0, brightness, colorTemperature, brightness);

	public int Hue
		{
		get;
		}

	public int Saturation
		{
		get;
		}

	public int Value
		{
		get;
		}

	public int? ColorTemperature
		{
		get;
		}

	public int? Brightness
		{
		get;
		}
	}

readonly struct TriggerLogEntry
	{
	public TriggerLogEntry (string key, string displayText)
		{
		Key = key;
		DisplayText = displayText;
		}

	public string Key
		{
		get;
		}

	public string DisplayText
		{
		get;
		}
	}
