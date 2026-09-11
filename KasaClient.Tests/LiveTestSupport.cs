// Copyright © 2026 Neil Colvin.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Sockets;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System.Threading;
using System.Threading.Tasks;

using KasaTapoClient;

using NUnit.Framework;

namespace KasaClient.Tests;

internal static class LiveTestSupport
	{
	private static readonly JsonSerializerSettings JSON_OPTIONS = new ()
		{
		Converters = { new StringEnumConverter () },
		MissingMemberHandling = MissingMemberHandling.Ignore,
		};

	private static LiveDiscoveryCache? _runDiscovery;

	internal static void BeginRun () => _runDiscovery = CreateDiscoveryCache ();
	internal static void EndRun () => _runDiscovery = null;
	private static LiveDiscoveryCache CreateDiscoveryCache () => new (timeout => Discover.DiscoverAsync (timeout));

	internal static IDisposable Measure (string phase) => new PhaseTimer (phase);

	private sealed class PhaseTimer : IDisposable
		{
		private readonly string _phase;
		private readonly Stopwatch _elapsed = Stopwatch.StartNew ();
		internal PhaseTimer (string phase) => _phase = phase;
		public void Dispose () => TestContext.Progress.WriteLine ($"[Live timing] {_phase}: {_elapsed.Elapsed.TotalSeconds:F3} s");
		}

	private static LiveTestSettings? Settings => LoadSettings ();

	internal static bool IsEnabled => Settings?.Enabled == true;

	public static IEnumerable<object[]> PlugDevices => GetDeviceCases ("plug");

	public static IEnumerable<object[]> LightDevices => GetDeviceCases ("light");

	public static IEnumerable<object[]> StripDevices => GetDeviceCases ("strip");

	public static IEnumerable<object[]> HubDevices => GetDeviceCases ("hub");

	internal static async Task<KasaDevice> ConnectAsync (string role)
		=> await ConnectAsync (role, null).ConfigureAwait (false);

	internal static async Task<KasaDevice> ConnectAsync (string role, string? host)
		{
		LiveTestSettings settings = GetSettingsOrInconclusive ();
		LiveDeviceSettings deviceSettings = GetDeviceSettings (settings, role, host);
		DeviceCredentials? credentials = string.IsNullOrWhiteSpace (settings.Credentials?.UserName)
			&& string.IsNullOrWhiteSpace (settings.Credentials?.Password)
			? null
			: new DeviceCredentials (settings.Credentials?.UserName, settings.Credentials?.Password);
		var target = new LiveDeviceTarget (deviceSettings.DeviceId, deviceSettings.Alias, deviceSettings.Host);
		TimeSpan timeout = TimeSpan.FromSeconds (settings.TimeoutSeconds ?? 10);

		LiveDiscoveryCache discovery = _runDiscovery ?? CreateDiscoveryCache ();
		Exception? lastError = null;
		for (int attempt = 1; attempt <= 3; attempt++)
			{
			try
				{
				DeviceConfiguration configuration;
				if (target.UsesDiscovery)
					{
					using IDisposable discoveryTiming = Measure ($"Discovery, attempt {attempt}");
					(DiscoveryResult discovered, bool fromCache) = await discovery.ResolveAsync (target, timeout, refresh: attempt > 1).ConfigureAwait (false);
					TestContext.Progress.WriteLine (fromCache ? "Using discovery results from this run." : "Completed a fresh discovery scan for this run.");
					TestContext.Progress.WriteLine ($"Resolved live target '{target.Key}' to {discovered.Host} ({discovered.Model}).");
					configuration = Discover.CreateConfiguration (discovered, credentials, timeout);
					}
				else
					{
					configuration = new DeviceConfiguration (deviceSettings.Host!, deviceSettings.Port ?? 9999, credentials, CreateConnectionOptions (deviceSettings), timeout);
					}
				using IDisposable connectionTiming = Measure ($"Connection and initial state, attempt {attempt}");
				return await Discover.ConnectAsync (configuration).ConfigureAwait (false);
				}
			catch (Exception ex) when (attempt < 3 && IsTransientConnectionFailure (ex))
				{
				lastError = ex;
				TestContext.Progress.WriteLine ($"Connection attempt {attempt} failed ({ex.GetType ().Name}); retrying with fresh discovery.");
				await Task.Delay (TimeSpan.FromMilliseconds (500)).ConfigureAwait (false);
				}
			}

		throw lastError ?? new InvalidOperationException ($"Live test role '{role}' could not connect to host '{deviceSettings.Host}'.");
		}

	internal static string GetRequiredSetting (string settingKey)
		=> GetRequiredSetting (settingKey, null);

	internal static async Task WaitForDevicePowerStateAsync (KasaDevice device, bool expectedState, string description)
		{
		await WaitForConditionAsync (
			async () =>
				{
					await device.UpdateAsync ().ConfigureAwait (false);
					return device.IsOn == expectedState;
				},
			$"{description} did not report power state '{expectedState}' within the live test timeout.").ConfigureAwait (false);
		}

	internal static async Task WaitForLightStateAsync (KasaDevice device, bool expectedPowerState, int? expectedBrightness, string description)
		{
		await WaitForConditionAsync (
			async () =>
				{
					await device.UpdateAsync ().ConfigureAwait (false);
					LightState? lightState = device.Light.State;
					if (lightState?.IsOn != expectedPowerState)
						{
						return false;
						}

					return expectedBrightness is null || lightState.Brightness == expectedBrightness;
				},
			$"{description} did not report the expected light state within the live test timeout.").ConfigureAwait (false);
		}

	internal static async Task HoldObservableStateAsync (string description)
		{
		int milliseconds = GetSettingsOrInconclusive ().ObservationDelayMilliseconds;
		if (milliseconds == 0)
			return;
		using IDisposable timing = Measure ($"Observation pause ({description})");
		await Task.Delay (milliseconds).ConfigureAwait (false);
		}

	internal static string GetRequiredSetting (string settingKey, string? host)
		{
		LiveTestSettings settings = GetSettingsOrInconclusive ();
		return settingKey switch
			{
				"KASA_LIVE_STRIP_CHILD_ID" => GetRequiredValue (GetDeviceSettings (settings, "strip", host).ChildDeviceId, settingKey),
				"KASA_LIVE_HUB_CHILD_ID" => GetRequiredValue (GetDeviceSettings (settings, "hub", host).ChildDeviceId, settingKey),
				"KASA_LIVE_HUB_TEMPERATURE_CHILD_ID" => GetRequiredValue (GetDeviceSettings (settings, "hub", host).TemperatureChildDeviceId, settingKey),
				_ => AssertInconclusive<string> ($"Unsupported live test setting key '{settingKey}'."),
				};
		}

	private static IEnumerable<object[]> GetDeviceCases (string role)
		{
		LiveTestSettings? settings = Settings;
		if (settings is null || !settings.Enabled)
			{
			return new[] { new object[] { null! } };
			}

		if (!settings.Devices.TryGetValue (role, out LiveDeviceSettings? singleDevice) || singleDevice is null)
			{
			return new[] { new object[] { null! } };
			}

		IReadOnlyList<LiveDeviceSettings> devices = singleDevice.Hosts is { Count: > 0 }
			? singleDevice.Hosts
			: new[] { singleDevice };

		string[] selectors = devices
			.Select (static device => new LiveDeviceTarget (device.DeviceId, device.Alias, device.Host).Key)
			.Where (static selector => selector != null)
			.Cast<string> ()
			.ToArray ();
		if (selectors.Length == 0)
			return new[] { new object[] { null! } };
		if (selectors.Distinct (StringComparer.OrdinalIgnoreCase).Count () != selectors.Length)
			throw new InvalidOperationException ($"Live test role '{role}' contains duplicate device selectors.");
		return selectors.Select (static selector => new object[] { selector }).ToArray ();
		}

	private static IReadOnlyList<LiveDeviceSettings> GetConfiguredDevices (LiveTestSettings settings, string role)
		{
		if (!settings.Devices.TryGetValue (role, out LiveDeviceSettings? singleDevice) || singleDevice is null)
			{
			Assert.Inconclusive ($"Live test role '{role}' is not configured in LiveTestSettings.json.");
			}

		if (singleDevice.Hosts is { Count: > 0 })
			{
			return singleDevice.Hosts;
			}

		return new[] { singleDevice };
		}

	private static LiveDeviceSettings GetDeviceSettings (LiveTestSettings settings, string role, string? host)
		{
		IReadOnlyList<LiveDeviceSettings> devices = GetConfiguredDevices (settings, role);
		LiveDeviceSettings? deviceSettings = string.IsNullOrWhiteSpace (host)
			? devices.FirstOrDefault ()
			: devices.FirstOrDefault (device => string.Equals (new LiveDeviceTarget (device.DeviceId, device.Alias, device.Host).Key, host, StringComparison.OrdinalIgnoreCase));

		if (deviceSettings is null)
			{
			Assert.Inconclusive ($"Live test role '{role}' does not define host '{host}' in LiveTestSettings.json.");
			}

		if (new LiveDeviceTarget (deviceSettings.DeviceId, deviceSettings.Alias, deviceSettings.Host).Key == null)
			{
			Assert.Inconclusive ($"Live test role '{role}' does not define a device ID, alias or host in LiveTestSettings.json.");
			}

		return deviceSettings;
		}

	private static DeviceConnectionOptions? CreateConnectionOptions (LiveDeviceSettings deviceSettings)
		{
		if (deviceSettings.TransportKind is null
			&& deviceSettings.DeviceFamily is null
			&& deviceSettings.EncryptionKind is null
			&& deviceSettings.UseSsl is null
			&& deviceSettings.HttpPort is null
			&& string.IsNullOrWhiteSpace (deviceSettings.ApplicationPath))
			{
			return null;
			}

		DeviceConnectionParameters? connectionParameters = deviceSettings.DeviceFamily is DeviceFamilyKind deviceFamily
			? new DeviceConnectionParameters (deviceFamily, deviceSettings.EncryptionKind ?? DeviceEncryptionKind.Unknown, useHttps: deviceSettings.UseSsl == true, httpPort: deviceSettings.HttpPort)
			: null;
		return new DeviceConnectionOptions (
			deviceSettings.TransportKind ?? DeviceTransportKind.Auto,
			connectionParameters,
			useSsl: deviceSettings.UseSsl == true,
			applicationPath: string.IsNullOrWhiteSpace (deviceSettings.ApplicationPath) ? "/app" : deviceSettings.ApplicationPath!);
		}

	private static LiveTestSettings GetSettingsOrInconclusive ()
		{
		LiveTestSettings? settings = Settings;
		if (settings is null)
			Assert.Inconclusive ("Live tests require LiveTestSettings.json in the test output directory or the NUnit TestDataDirectory parameter location.");
		if (!settings!.Enabled)
			{
			Assert.Inconclusive ("Live tests are disabled. Set the NUnit EnableLiveTests parameter to true or enable them in LiveTestSettings.json.");
			}

		return settings!;
		}

	private static LiveTestSettings? LoadSettings ()
		{
		string configPath = Path.Combine (TestContext.Parameters.Get ("TestDataDirectory", AppContext.BaseDirectory), "LiveTestSettings.json");
		if (!File.Exists (configPath))
			{
			return null;
			}

		string json = File.ReadAllText (configPath);
		LiveTestSettings? settings = JsonConvert.DeserializeObject<LiveTestSettings> (json, JSON_OPTIONS);
		if (settings != null && (settings.ObservationDelayMilliseconds < 0 || settings.ObservationDelayMilliseconds > 60000))
			throw new InvalidDataException ("observationDelayMilliseconds must be between 0 and 60000.");
		// An explicit NUnit parameter overrides the configuration for this operation only.
		// Use the JSON setting when no parameter override is supplied.
		if (settings != null && bool.TryParse (TestContext.Parameters.Get ("EnableLiveTests", ""), out bool enabled))
			settings.Enabled = enabled;
		return settings;
		}

	private static string GetRequiredValue (string? value, string settingKey)
		{
		if (string.IsNullOrWhiteSpace (value))
			{
			Assert.Inconclusive ($"Missing required live test setting '{settingKey}' in LiveTestSettings.json.");
			}

		return value!;
		}

	private static T AssertInconclusive<T> (string message)
		{
		Assert.Inconclusive (message);
		return default!;
		}

	internal static async Task WaitForConditionAsync (Func<Task<bool>> condition, string timeoutMessage)
		{
		LiveTestSettings settings = GetSettingsOrInconclusive ();
		TimeSpan timeout = TimeSpan.FromSeconds (settings.TimeoutSeconds ?? 10);
		TimeSpan pollInterval = TimeSpan.FromMilliseconds (250);
		DateTime deadline = DateTime.UtcNow + timeout;

		while (DateTime.UtcNow <= deadline)
			{
			if (await condition ().ConfigureAwait (false))
				{
				return;
				}

			TimeSpan remaining = deadline - DateTime.UtcNow;
			if (remaining <= TimeSpan.Zero)
				{
				break;
				}

			await Task.Delay (remaining < pollInterval ? remaining : pollInterval, CancellationToken.None).ConfigureAwait (false);
			}

		Assert.Fail (timeoutMessage);
		}

	private static bool IsTransientConnectionFailure (Exception exception)
		{
		for (Exception? current = exception; current is not null; current = current.InnerException)
			{
			if (current is TaskCanceledException
				|| current is TimeoutException
				|| current is HttpRequestException
				|| current is IOException
				|| current is SocketException)
				{
				return true;
				}
			}

		return false;
		}

	private sealed class LiveTestSettings
		{
		public bool Enabled
			{
			get; set;
			}

		public int? TimeoutSeconds
			{
			get; set;
			}

		public int ObservationDelayMilliseconds
			{
			get; set;
			}

		public LiveCredentialsSettings? Credentials
			{
			get; set;
			}

		public System.Collections.Generic.Dictionary<string, LiveDeviceSettings> Devices { get; set; } = new (StringComparer.OrdinalIgnoreCase);
		}

	private sealed class LiveCredentialsSettings
		{
		public string? UserName
			{
			get; set;
			}

		public string? Password
			{
			get; set;
			}
		}

	private sealed class LiveDeviceSettings
		{
		public string? DeviceId
			{
			get; set;
			}
		public string? Alias
			{
			get; set;
			}

		public string? Host
			{
			get; set;
			}

		public List<LiveDeviceSettings>? Hosts
			{
			get; set;
			}

		public int? Port
			{
			get; set;
			}

		public DeviceTransportKind? TransportKind
			{
			get; set;
			}

		public DeviceFamilyKind? DeviceFamily
			{
			get; set;
			}

		public DeviceEncryptionKind? EncryptionKind
			{
			get; set;
			}

		public bool? UseSsl
			{
			get; set;
			}

		public int? HttpPort
			{
			get; set;
			}

		public string? ApplicationPath
			{
			get; set;
			}

		public string? TemperatureChildDeviceId
			{
			get; set;
			}

		public string? ChildDeviceId
			{
			get; set;
			}
		}
	}