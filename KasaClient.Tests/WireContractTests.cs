// Copyright © 2026 Neil Colvin.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using KasaTapoClient;
using KasaTapoClient.Internal;
using NUnit.Framework;

namespace KasaClient.Tests;

[TestFixture]
public sealed class WireContractTests
	{
	[TestCase("12", 12)]
	[TestCase("\"12\"", 12)]
	[TestCase("12.5", 13)]
	[TestCase("-12.5", -13)]
	[TestCase("\"12.5\"", 13)]
	[TestCase("true", 1)]
	[TestCase("false", 0)]
	[TestCase("null", null)]
	[TestCase("\"not a number\"", null)]
	[TestCase("2147483648", null)]
	[TestCase("{}", null)]
	[TestCase("[1,2]", null)]
	public void SensorWarnings_AcceptKnownFirmwareRepresentations (string value, int? expected)
		{
		var child = WireJson.Read<KasaResponseParser.SmartChildDeviceDto> ("{\"current_temp_exception\":" + value + ",\"device_id\":\"after-warning\"}");
		Assert.That (child.CurrentTemperatureException, Is.EqualTo (expected));
		Assert.That (child.DeviceId, Is.EqualTo ("after-warning"));
		}

	[Test]
	public void SmartResults_AcceptResultBeforeMethodAndIgnoreUnknownMethods ()
		{
		const string json = """{"result":{"responses":[{"result":{"current_power":12500},"error_code":0,"method":"get_energy_usage"},{"method":"future_method","result":[{"future":true}]}]}}""";
		var results = KasaResponseParser.ParseSmartModuleResults (json);
		Assert.That (results.Keys, Is.EquivalentTo (new[] { "get_energy_usage" }));
		Assert.That (KasaResponseParser.ParseSmartEnergyUsage (results)?.CurrentPowerWatts, Is.EqualTo (12.5));
		}

	[Test]
	public void FailedSmartMethods_DoNotOverwriteStateWithTheirResult ()
		{
		const string json = """{"result":{"responses":[{"method":"get_energy_usage","error_code":-1002,"result":{"current_power":1000}}]}}""";
		Assert.That (KasaResponseParser.ParseSmartModuleResults (json), Is.Empty);
		Assert.That (KasaResponseParser.ParseSmartModuleResult (json, "get_energy_usage", out int? code), Is.Null);
		Assert.That (code, Is.EqualTo (-1002));
		}

	[TestCase("null")]
	[TestCase("[]")]
	[TestCase("{}")]
	public void SmartResponses_RejectMissingEnvelope (string json)
		{
		Assert.That (() => KasaResponseParser.ParseSmartResponse (json), Throws.Exception);
		}

	[Test]
	public void LegacyMerge_PreservesOtherMethodsInTheSameModule ()
		{
		var response = WireJson.Read<KasaResponseParser.LegacyResponseDto> ("""{"emeter":{"get_realtime":{"power_mw":1250}}} """);
		response.Merge (WireJson.Read<KasaResponseParser.LegacyResponseDto> ("""{"emeter":{"get_daystat":{"day_list":[{"day":22,"energy_wh":450}]}}} """));
		response.Merge (WireJson.Read<KasaResponseParser.LegacyResponseDto> ("""{"emeter":{"get_monthstat":{"month_list":[{"month":9,"energy_wh":900}]}}} """));
		using JsonDocument json = JsonDocument.Parse (WireJson.Serialize (response));
		JsonElement emeter = json.RootElement.GetProperty ("emeter");
		Assert.That (emeter.GetProperty ("get_realtime").GetProperty ("power_mw").GetDouble (), Is.EqualTo (1250));
		Assert.That (emeter.GetProperty ("get_daystat").GetProperty ("day_list").GetArrayLength (), Is.EqualTo (1));
		Assert.That (emeter.GetProperty ("get_monthstat").GetProperty ("month_list").GetArrayLength (), Is.EqualTo (1));
		}

	[Test]
	public void ChildOverlay_PreservesIdentityAndAcceptsFalseAndZero ()
		{
		var child = WireJson.Read<KasaResponseParser.SmartChildDeviceDto> ("""{"device_id":"child","model":"S200B","device_on":true,"report_interval":60}""");
		var updated = child.Overlay (WireJson.Read<KasaResponseParser.SmartChildDeviceDto> ("""{"device_on":false,"report_interval":0}"""));
		Assert.That (updated.DeviceId, Is.EqualTo ("child"));
		Assert.That (updated.Model, Is.EqualTo ("S200B"));
		Assert.That (updated.DeviceOn, Is.False);
		Assert.That (updated.ReportInterval, Is.Zero);
		Assert.That (child.DeviceOn, Is.True);
		}

	[Test]
	public void DiscoveryEncryption_AcceptsNameAndVersionList ()
		{
		var named = WireJson.Read<DiscoveryPayloadDto> ("""{"encrypt_type":"AES"}""");
		var versions = WireJson.Read<DiscoveryPayloadDto> ("""{"encrypt_type":[1,"2",null],"mgt_encrypt_schm":{"encrypt_type":"KLAP","lv":"2"}}""");
		Assert.That (named.EncryptionType?.Name, Is.EqualTo ("AES"));
		Assert.That (versions.EncryptionType?.Versions, Is.EqualTo (new int?[] { 1, 2, null }));
		Assert.That (versions.EncryptionScheme?.LoginVersion, Is.EqualTo (2));
		}

	[Test]
	public void Authentication_MapsSessionAndLockoutFields ()
		{
		var response = WireJson.Read<AuthenticationResponseDto> ("""{"error_code":"-1501","error_info":{"failedAttempts":2,"remainAttempts":3,"lockedMinute":1},"result":{"sessionId":"test-session","start_seq":"42","extra_crypt":{"type":"password_sha_with_salt","params":{"sha_name":2,"sha_salt":"YWJj"}}}}""");
		Assert.That (response.ErrorCode, Is.EqualTo (-1501));
		Assert.That (response.ErrorInfo?.RemainingAttempts, Is.EqualTo (3));
		Assert.That (response.Result?.StartSequence, Is.EqualTo (42));
		Assert.That (response.Result?.ExtraCrypt?.Parameters?.ShaSalt, Is.EqualTo ("YWJj"));
		}

	[Test]
	public void TpapRegistration_EmitsNullStokAndIntegerCipherSuite ()
		{
		string payload = WireJson.Serialize (new WireRequest<TpapLoginParametersDto> { Method = "login", Parameters = new TpapRegisterParametersDto { SubMethod = "pake_register", CipherSuites = new[] { 1 }, Encryption = new[] { "aes_128_ccm" } } });
		using JsonDocument json = JsonDocument.Parse (payload);
		JsonElement parameters = json.RootElement.GetProperty ("params");
		Assert.That (parameters.TryGetProperty ("stok", out JsonElement stok), Is.True);
		Assert.That (stok.ValueKind, Is.EqualTo (JsonValueKind.Null));
		Assert.That (parameters.GetProperty ("cipher_suites")[0].GetInt32 (), Is.EqualTo (1));
		Assert.That (parameters.TryGetProperty ("dac_nonce", out _), Is.False);
		}

	[Test]
	public void SmartCommand_EscapesDeviceIdentifiersAndOmitsUnusedParameters ()
		{
		const string id = "child\"\\\n雪";
		using JsonDocument json = JsonDocument.Parse (KasaCommands.CreateSmartChildRequest (id, "get_device_info"));
		JsonElement parameters = json.RootElement.GetProperty ("params");
		Assert.That (parameters.GetProperty ("device_id").GetString (), Is.EqualTo (id));
		Assert.That (parameters.GetProperty ("requestData").TryGetProperty ("params", out _), Is.False);
		}

	[Test]
	public void BuiltInEffects_ReturnIndependentTypedInstances ()
		{
		var first = KasaResponseParser.CreateSmartLightStripEffectPayload ("Aurora");
		first.Sequence![0][0] = 999;
		Assert.That (KasaResponseParser.CreateSmartLightStripEffectPayload ("Aurora").Sequence![0][0], Is.EqualTo (120));
		}

	[Test]
	public void PublicApi_DoesNotExposeRawPayloadsOrJsonLibraries ()
		{
		Type[] types = typeof (KasaDevice).Assembly.GetExportedTypes ();
		Assert.That (types.SelectMany (type => type.GetProperties ()).Any (property => property.Name == "RawJson"), Is.False);
		Assert.That (typeof (KasaDevice).GetMethod ("ExecuteCommandAsync", BindingFlags.Public | BindingFlags.Instance), Is.Null);
		Assert.That (typeof (KasaDevice).GetMethod ("ExecuteSmartCommandAsync", BindingFlags.Public | BindingFlags.Instance), Is.Null);
		Assert.That (typeof (KasaDevice).Assembly.GetReferencedAssemblies ().Select (assembly => assembly.Name), Does.Not.Contain ("Newtonsoft.Json").And.Not.Contain ("log4net"));
		}
	[Test]
	public void OptionalModuleRefresh_PropagatesCancellation ()
		{
		int calls = 0;
		using var cancelled = new System.Threading.CancellationTokenSource ();
		var transport = new FakeDeviceTransport (sendHandler: (_, token) =>
			{
			if (++calls == 1) return System.Threading.Tasks.Task.FromResult ("""{"result":{"responses":[{"method":"get_device_info","result":{"model":"P110","type":"SMART.TAPOPLUG","device_id":"unit-test"}},{"method":"component_nego","result":{"component_list":[{"id":"energy_monitoring","ver_code":1}]}}]}}""");
			cancelled.Cancel ();
			throw new OperationCanceledException (cancelled.Token);
			});
		using var device = new KasaDevice (new DeviceConfiguration ("127.0.0.1", connectionOptions: new DeviceConnectionOptions (connectionParameters: new DeviceConnectionParameters (DeviceFamilyKind.SmartTapoPlug, DeviceEncryptionKind.Aes))), transport);
		Assert.ThrowsAsync<OperationCanceledException> (async () => await device.UpdateAsync (cancelled.Token).ConfigureAwait (false));
		Assert.That (calls, Is.EqualTo (2));
		Assert.That (device.SystemInfo, Is.Null, "A cancelled refresh must not publish a partial snapshot.");
		}

	}
