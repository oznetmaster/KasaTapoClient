// Copyright © 2026 Neil Colvin.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using KasaTapoClient;
using Microsoft.VSDiagnostics;

[CPUUsageDiagnoser]
public class TapoCommandLatencyBenchmarks
{
   private BenchmarkConnectionProfile _profile = null!;
   private DeviceConfiguration _explicitConfiguration = null!;
   private DeviceConfiguration _autoConfiguration = null!;
   private KasaDevice _warmDevice = null!;

   [GlobalSetup]
   public async Task GlobalSetupAsync()
   {
      _profile = LoadProfile();
      _explicitConfiguration = await CreateResolvedConfigurationAsync(_profile).ConfigureAwait(false);
      _autoConfiguration = CreateAutoConfiguration(_profile);
      _warmDevice = await Discover.ConnectAsync(_explicitConfiguration).ConfigureAwait(false);
   }

   [GlobalCleanup]
   public void GlobalCleanup()
   {
      _warmDevice?.Dispose();
   }

   [Benchmark]
   public async Task ConnectWithAutoTransportAsync()
   {
      using KasaDevice device = await Discover.ConnectAsync(_autoConfiguration).ConfigureAwait(false);
   }

   [Benchmark]
   public async Task ConnectWithSavedTransportAsync()
   {
      using KasaDevice device = await Discover.ConnectAsync(_explicitConfiguration).ConfigureAwait(false);
   }

   [Benchmark]
   public async Task ConnectWithSavedTransportWithoutInitialUpdateAsync()
   {
      using KasaDevice device = await Discover.ConnectAsync(_explicitConfiguration, updateState: false).ConfigureAwait(false);
   }

   [Benchmark]
   public Task WarmCommandRoundTripAsync() => _warmDevice.ExecuteCommandAsync(CreateSmartCommandPayload(true));

   [Benchmark]
   public Task WarmSingleUpdateAsync() => _warmDevice.UpdateAsync();

   [Benchmark]
   public async Task WarmTripleUpdateAsync()
   {
      await _warmDevice.UpdateAsync().ConfigureAwait(false);
      await _warmDevice.UpdateAsync().ConfigureAwait(false);
      await _warmDevice.UpdateAsync().ConfigureAwait(false);
   }

   private static BenchmarkConnectionProfile LoadProfile()
   {
      BenchmarkConnectionProfile? profile = LoadImplicitProfile();
      if (profile is null)
      {
         string? host = LoadRecentHost();
         profile = LoadNamedProfileByHost(host);
      }

      return profile ?? throw new InvalidOperationException("No console connection profile was found. Run a console command against the target Tapo device first so the benchmark can reuse the saved host, transport, and credentials.");
   }

   private static async Task<DeviceConfiguration> CreateResolvedConfigurationAsync(BenchmarkConnectionProfile profile)
   {
      IReadOnlyList<DiscoveryResult> discoveryResults = await Discover.DiscoverAsync(TimeSpan.FromSeconds(5), profile.Host).ConfigureAwait(false);
      foreach (DiscoveryResult discoveryResult in discoveryResults)
      {
         if (string.Equals(discoveryResult.Host, profile.Host, StringComparison.OrdinalIgnoreCase))
         {
            return CreateExplicitConfiguration(profile, discoveryResult);
         }
      }

      throw new TimeoutException($"Discovery did not return a device configuration for '{profile.Host}'.");
   }

   private static DeviceConfiguration CreateExplicitConfiguration(BenchmarkConnectionProfile profile, DiscoveryResult discoveryResult)
   {
      DeviceConnectionParameters connectionParameters = GetInternalProperty<DeviceConnectionParameters>(discoveryResult, "ConnectionParameters")
         ?? throw new InvalidOperationException($"Discovery for '{profile.Host}' did not return connection parameters required for the explicit transport benchmark.");
      DeviceTransportKind transportKind = MapTransportKind(connectionParameters.EncryptionKind);
      bool supportsHttps = GetInternalProperty<bool>(discoveryResult, "SupportsHttps");
      int? discoveredPort = GetInternalProperty<int?>(discoveryResult, "Port");
      bool useSsl = transportKind == DeviceTransportKind.HttpToken && supportsHttps;
      int port = discoveredPort ?? (transportKind == DeviceTransportKind.HttpToken
         ? (useSsl ? 443 : 80)
         : 9999);
      DeviceCredentials? credentials = string.IsNullOrWhiteSpace(profile.UserName) && string.IsNullOrWhiteSpace(profile.Password)
         ? null
         : new DeviceCredentials(profile.UserName, profile.Password);
      var connectionOptions = new DeviceConnectionOptions(
         transportKind,
         connectionParameters: connectionParameters,
         useSsl: useSsl,
         useDefaultCredentials: false,
         defaultCredentialProfile: DefaultCredentialProfile.None,
         applicationPath: "/app",
         useSecurePassthrough: transportKind == DeviceTransportKind.HttpToken);
      return new DeviceConfiguration(discoveryResult.Host, port, credentials, connectionOptions);
   }

   private static DeviceTransportKind MapTransportKind(DeviceEncryptionKind encryptionKind) => encryptionKind switch
   {
      DeviceEncryptionKind.Xor => DeviceTransportKind.LegacyXor,
      DeviceEncryptionKind.Aes => DeviceTransportKind.HttpToken,
      DeviceEncryptionKind.Klap => DeviceTransportKind.HttpToken,
      DeviceEncryptionKind.Tpap => DeviceTransportKind.HttpToken,
      _ => DeviceTransportKind.HttpToken,
   };

   private static T GetInternalProperty<T>(object instance, string propertyName)
   {
      PropertyInfo property = instance.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
         ?? throw new InvalidOperationException($"Property '{propertyName}' was not found on '{instance.GetType().FullName}'.");
      object? value = property.GetValue(instance);
      return value is null ? default! : (T)value;
   }

   private static DeviceConfiguration CreateAutoConfiguration(BenchmarkConnectionProfile profile)
   {
      DeviceCredentials? credentials = string.IsNullOrWhiteSpace(profile.UserName) && string.IsNullOrWhiteSpace(profile.Password)
         ? null
         : new DeviceCredentials(profile.UserName, profile.Password);
      var connectionOptions = new DeviceConnectionOptions(
         DeviceTransportKind.Auto,
         connectionParameters: null,
         useSsl: false,
         useDefaultCredentials: profile.DefaultCredentialProfile != DefaultCredentialProfile.None,
         defaultCredentialProfile: profile.DefaultCredentialProfile,
         applicationPath: "/app",
         useSecurePassthrough: true);
      return new DeviceConfiguration(profile.Host, 9999, credentials, connectionOptions);
   }

   private static string CreateSmartCommandPayload(bool isOn)
   {
      var request = new JObject
      {
         ["method"] = "set_device_info",
         ["request_time_milis"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
         ["terminal_uuid"] = Convert.ToBase64String(Guid.NewGuid().ToByteArray()),
         ["params"] = new JObject
         {
            ["device_on"] = isOn,
         },
      };

      return request.ToString(Formatting.None);
   }

   private static BenchmarkConnectionProfile? LoadImplicitProfile()
   {
      string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "KasaTapoClient", "console-implicit-profile.json");
      if (!File.Exists(path))
      {
         return null;
      }

      return JsonConvert.DeserializeObject<BenchmarkConnectionProfile>(File.ReadAllText(path));
   }

   private static string? LoadRecentHost()
   {
      string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "KasaTapoClient", "console-recent-host.txt");
      if (!File.Exists(path))
      {
         return null;
      }

      string host = File.ReadAllText(path).Trim();
      return string.IsNullOrWhiteSpace(host) ? null : host;
   }

   private static BenchmarkConnectionProfile? LoadNamedProfileByHost(string? host)
   {
      if (string.IsNullOrWhiteSpace(host))
      {
         return null;
      }

      string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "KasaTapoClient", "console-profiles.json");
      if (!File.Exists(path))
      {
         return null;
      }

      Dictionary<string, BenchmarkConnectionProfile>? profiles = JsonConvert.DeserializeObject<Dictionary<string, BenchmarkConnectionProfile>>(File.ReadAllText(path));
      if (profiles is null)
      {
         return null;
      }

      foreach (BenchmarkConnectionProfile profile in profiles.Values)
      {
         if (string.Equals(profile.Host, host, StringComparison.OrdinalIgnoreCase))
         {
            return profile;
         }
      }

      return null;
   }

   private sealed class BenchmarkConnectionProfile
   {
      public string Name { get; set; } = string.Empty;
      public string Host { get; set; } = string.Empty;
      public DeviceTransportKind TransportKind { get; set; }
      public int Port { get; set; }
      public bool UseSsl { get; set; }
      public string? UserName { get; set; }
      public string? Password { get; set; }
      public DefaultCredentialProfile DefaultCredentialProfile { get; set; }
      public string ApplicationPath { get; set; } = "/app";
      public bool UseSecurePassthrough { get; set; }
   }
}
