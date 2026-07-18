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

[SimpleJob(launchCount: 1, warmupCount: 0, iterationCount: 3)]
[CPUUsageDiagnoser]
public class TpapIdleSessionBenchmarks
{
    private static readonly string GetDeviceInfoPayload = CreateSmartRequest("get_device_info", null);
    private BenchmarkConnectionProfile _profile = null!;
    private DeviceConfiguration _configuration = null!;
    private KasaDevice _device = null!;

    [GlobalSetup]
    public async Task GlobalSetupAsync()
    {
        _profile = LoadProfile();
        _configuration = await CreateResolvedConfigurationAsync(_profile).ConfigureAwait(false);
        _device = await Discover.ConnectAsync(_configuration, updateState: false).ConfigureAwait(false);
    }

    [Benchmark]
    public Task QueryAfter5SecondsIdleAsync() => _device.ExecuteCommandAsync(GetDeviceInfoPayload);

    [IterationSetup(Target = nameof(QueryAfter5SecondsIdleAsync))]
    public void Setup5SecondsIdleAsync() => PrimeAndWaitAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();

    [Benchmark]
    public Task QueryAfter30SecondsIdleAsync() => _device.ExecuteCommandAsync(GetDeviceInfoPayload);

    [IterationSetup(Target = nameof(QueryAfter30SecondsIdleAsync))]
    public void Setup30SecondsIdleAsync() => PrimeAndWaitAsync(TimeSpan.FromSeconds(30)).GetAwaiter().GetResult();

    [Benchmark]
    public Task QueryAfter60SecondsIdleAsync() => _device.ExecuteCommandAsync(GetDeviceInfoPayload);

    [IterationSetup(Target = nameof(QueryAfter60SecondsIdleAsync))]
    public void Setup60SecondsIdleAsync() => PrimeAndWaitAsync(TimeSpan.FromSeconds(60)).GetAwaiter().GetResult();

    [Benchmark]
    public Task QueryAfter120SecondsIdleAsync() => _device.ExecuteCommandAsync(GetDeviceInfoPayload);

    [IterationSetup(Target = nameof(QueryAfter120SecondsIdleAsync))]
    public void Setup120SecondsIdleAsync() => PrimeAndWaitAsync(TimeSpan.FromSeconds(120)).GetAwaiter().GetResult();

    [GlobalCleanup]
    public void GlobalCleanup()
    {
        _device?.Dispose();
    }

    private async Task PrimeAndWaitAsync(TimeSpan idleDuration)
    {
        await _device.ExecuteCommandAsync(GetDeviceInfoPayload).ConfigureAwait(false);
        await Task.Delay(idleDuration).ConfigureAwait(false);
    }

    private static BenchmarkConnectionProfile LoadProfile()
    {
        BenchmarkConnectionProfile? profile = LoadImplicitProfile();
        if (profile is null)
        {
            string? host = LoadRecentHost();
            profile = LoadNamedProfileByHost(host);
        }

        return profile ?? throw new InvalidOperationException("No console connection profile was found. Run a console command against the target device first so the benchmark can reuse the saved host, transport, and credentials.");
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

    private static Dictionary<string, BenchmarkConnectionProfile>? LoadNamedProfiles()
    {
        string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "KasaTapoClient", "console-profiles.json");
        if (!File.Exists(path))
        {
            return null;
        }

        return JsonConvert.DeserializeObject<Dictionary<string, BenchmarkConnectionProfile>>(File.ReadAllText(path));
    }

    private static BenchmarkConnectionProfile? LoadNamedProfileByHost(string? host)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            return null;
        }

        Dictionary<string, BenchmarkConnectionProfile>? profiles = LoadNamedProfiles();
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

    private static BenchmarkConnectionProfile LoadImplicitProfile()
    {
        string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "KasaTapoClient", "console-implicit-profile.json");
        if (!File.Exists(path))
        {
            return null!;
        }

        return JsonConvert.DeserializeObject<BenchmarkConnectionProfile>(File.ReadAllText(path))!;
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
        DeviceConnectionParameters connectionParameters = GetInternalProperty<DeviceConnectionParameters>(discoveryResult, "ConnectionParameters") ?? throw new InvalidOperationException($"Discovery for '{profile.Host}' did not return connection parameters required for the explicit transport benchmark.");
        DeviceTransportKind transportKind = MapTransportKind(connectionParameters.EncryptionKind);
        bool supportsHttps = GetInternalProperty<bool>(discoveryResult, "SupportsHttps");
        int? discoveredPort = GetInternalProperty<int?>(discoveryResult, "Port");
        bool useSsl = transportKind == DeviceTransportKind.HttpToken && supportsHttps;
        int port = discoveredPort ?? (transportKind == DeviceTransportKind.HttpToken ? (useSsl ? 443 : 80) : 9999);
        DeviceCredentials? credentials = string.IsNullOrWhiteSpace(profile.UserName) && string.IsNullOrWhiteSpace(profile.Password) ? null : new DeviceCredentials(profile.UserName, profile.Password);
        var connectionOptions = new DeviceConnectionOptions(transportKind, connectionParameters: connectionParameters, useSsl: useSsl, useDefaultCredentials: false, defaultCredentialProfile: DefaultCredentialProfile.None, applicationPath: "/app", useSecurePassthrough: transportKind == DeviceTransportKind.HttpToken);
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
        PropertyInfo property = instance.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) ?? throw new InvalidOperationException($"Property '{propertyName}' was not found on '{instance.GetType().FullName}'.");
        object? value = property.GetValue(instance);
        return value is null ? default! : (T)value;
    }

    private static string CreateSmartRequest(string method, JObject? parameters)
    {
        var request = new JObject
        {
            ["method"] = method,
            ["request_time_milis"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            ["terminal_uuid"] = Convert.ToBase64String(Guid.NewGuid().ToByteArray())
        };
        if (parameters is not null)
        {
            request["params"] = parameters;
        }

        return request.ToString(Formatting.None);
    }

    private sealed class BenchmarkConnectionProfile
    {
        public string Name { get; set; } = string.Empty;
        public string Host { get; set; } = string.Empty;
        public DeviceTransportKind TransportKind { get; set; }
        public int Port { get; set; }
        public bool UseSsl { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public DefaultCredentialProfile DefaultCredentialProfile { get; set; }
        public string ApplicationPath { get; set; } = "/app";
        public bool UseSecurePassthrough { get; set; }
    }
}
