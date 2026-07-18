// Copyright © 2026 Neil Colvin.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Sockets;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System.Threading;
using System.Threading.Tasks;

using KasaTapoClient;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace KasaClient.Tests;

internal static class LiveTestSupport
	{
	private static readonly JsonSerializerSettings JSON_OPTIONS = new ()
		{
		Converters = { new StringEnumConverter () },
		MissingMemberHandling = MissingMemberHandling.Ignore,
		};

	private static readonly Lazy<LiveTestSettings?> SETTINGS = new (LoadSettings);

	internal static bool IsEnabled => SETTINGS.Value?.Enabled == true;

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
		DeviceConnectionOptions? connectionOptions = CreateConnectionOptions (deviceSettings);
		var configuration = new DeviceConfiguration (
			deviceSettings.Host!,
			deviceSettings.Port ?? 9999,
			credentials,
			connectionOptions,
			TimeSpan.FromSeconds (settings.TimeoutSeconds ?? 10));

		Exception? lastError = null;
		for (int attempt = 1; attempt <= 3; attempt++)
			{
			try
				{
				return await Discover.ConnectAsync (configuration).ConfigureAwait (false);
				}
			catch (Exception ex) when (attempt < 3 && IsTransientConnectionFailure (ex))
				{
				lastError = ex;
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

	internal static Task HoldObservableStateAsync (string description) =>
		Task.Delay (TimeSpan.FromSeconds (2));

	internal static string GetRequiredSetting (string settingKey, string? host)
		{
		LiveTestSettings settings = GetSettingsOrInconclusive ();
		return settingKey switch
			{
				"KASA_LIVE_STRIP_CHILD_ID" => GetRequiredValue (GetDeviceSettings (settings, "strip", host).ChildDeviceId, settingKey),
				"KASA_LIVE_HUB_CHILD_ID" => GetRequiredValue (GetDeviceSettings (settings, "hub", host).ChildDeviceId, settingKey),
				_ => AssertInconclusive<string> ($"Unsupported live test setting key '{settingKey}'."),
			};
		}

	private static IEnumerable<object[]> GetDeviceCases (string role)
		{
		LiveTestSettings? settings = SETTINGS.Value;
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

		string[] hosts = devices
			.Select (static device => device.Host)
			.Where (static host => !string.IsNullOrWhiteSpace (host))
			.Cast<string> ()
			.ToArray ();

		if (hosts.Length == 0)
			{
			return new[] { new object[] { null! } };
			}

		return devices
			.Where (static device => !string.IsNullOrWhiteSpace (device.Host))
			.Select (static device => new object[] { device.Host! })
			.ToArray ();
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
			: devices.FirstOrDefault (device => string.Equals (device.Host, host, StringComparison.OrdinalIgnoreCase));

		if (deviceSettings is null)
			{
			Assert.Inconclusive ($"Live test role '{role}' does not define host '{host}' in LiveTestSettings.json.");
			}

		if (string.IsNullOrWhiteSpace (deviceSettings.Host))
			{
			Assert.Inconclusive ($"Live test role '{role}' does not define a host in LiveTestSettings.json.");
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
		LiveTestSettings? settings = SETTINGS.Value;
		if (settings is null || !settings.Enabled)
			{
			Assert.Inconclusive ("Live integration tests are disabled. Enable them in KasaClient.Tests/LiveTestSettings.json.");
			}

		return settings!;
		}

	private static LiveTestSettings? LoadSettings ()
		{
		string configPath = Path.Combine (AppContext.BaseDirectory, "LiveTestSettings.json");
		if (!File.Exists (configPath))
			{
			return null;
			}

		string json = File.ReadAllText (configPath);
		return JsonConvert.DeserializeObject<LiveTestSettings> (json, JSON_OPTIONS);
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
		public bool Enabled { get; set; }

		public int? TimeoutSeconds { get; set; }

		public LiveCredentialsSettings? Credentials { get; set; }

		public System.Collections.Generic.Dictionary<string, LiveDeviceSettings> Devices { get; set; } = new (StringComparer.OrdinalIgnoreCase);
		}

	private sealed class LiveCredentialsSettings
		{
		public string? UserName { get; set; }

		public string? Password { get; set; }
		}

	private sealed class LiveDeviceSettings
		{
		public string? Host { get; set; }

		public List<LiveDeviceSettings>? Hosts { get; set; }

		public int? Port { get; set; }

		public DeviceTransportKind? TransportKind { get; set; }

		public DeviceFamilyKind? DeviceFamily { get; set; }

		public DeviceEncryptionKind? EncryptionKind { get; set; }

		public bool? UseSsl { get; set; }

		public int? HttpPort { get; set; }

		public string? ApplicationPath { get; set; }

		public string? ChildDeviceId { get; set; }
		}
	}
