// Copyright © 2026 Neil Colvin.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using System;

namespace KasaTapoClient.Internal;

internal sealed class DiscoveryPayloadDto
	{
	[System.Text.Json.Serialization.JsonPropertyName ("result")]
	public DiscoveryPayloadDto? Result { get; set; }
	[System.Text.Json.Serialization.JsonPropertyName ("params")]
	public DiscoveryPayloadDto? Parameters { get; set; }
	[System.Text.Json.Serialization.JsonPropertyName ("model")]
	public string? Model { get; set; }
	[System.Text.Json.Serialization.JsonPropertyName ("device_model")]
	public string? DeviceModel { get; set; }
	[System.Text.Json.Serialization.JsonPropertyName ("device_model_name")]
	public string? DeviceModelName { get; set; }
	[System.Text.Json.Serialization.JsonPropertyName ("nickname")]
	public string? Nickname { get; set; }
	[System.Text.Json.Serialization.JsonPropertyName ("alias")]
	public string? Alias { get; set; }
	[System.Text.Json.Serialization.JsonPropertyName ("device_name")]
	public string? DeviceName { get; set; }
	[System.Text.Json.Serialization.JsonPropertyName ("device_id")]
	public string? DeviceId { get; set; }
	[System.Text.Json.Serialization.JsonPropertyName ("deviceId")]
	public string? LegacyDeviceId { get; set; }
	[System.Text.Json.Serialization.JsonPropertyName ("type")]
	public string? Type { get; set; }
	[System.Text.Json.Serialization.JsonPropertyName ("device_type")]
	public string? DeviceType { get; set; }
	[System.Text.Json.Serialization.JsonPropertyName ("protocol_version")]
	public int? ProtocolVersion { get; set; }
	[System.Text.Json.Serialization.JsonPropertyName ("tpap_preferred")]
	public bool? TpapPreferred { get; set; }
	[System.Text.Json.Serialization.JsonPropertyName ("mgt_encrypt_schm")]
	public DiscoveryEncryptionSchemeDto? EncryptionScheme { get; set; }
	[System.Text.Json.Serialization.JsonPropertyName ("encrypt_type")]
	public DiscoveryEncryptionType? EncryptionType { get; set; }
	[System.Text.Json.Serialization.JsonPropertyName ("encrypt_info")]
	public DiscoveryEncryptionInfoDto? EncryptionInfo { get; set; }
	[System.Text.Json.Serialization.JsonPropertyName ("is_support_https")]
	public bool? SupportsHttps { get; set; }
	[System.Text.Json.Serialization.JsonPropertyName ("tpap")]
	public TpapDiscoveryDto? Tpap { get; set; }
	}

internal sealed class DiscoveryEncryptionSchemeDto
	{
	[System.Text.Json.Serialization.JsonPropertyName ("is_support_https")]
	public bool? SupportsHttps { get; set; }
	[System.Text.Json.Serialization.JsonPropertyName ("http_port")]
	public int? HttpPort { get; set; }
	[System.Text.Json.Serialization.JsonPropertyName ("encrypt_type")]
	public string? EncryptionType { get; set; }
	[System.Text.Json.Serialization.JsonPropertyName ("lv")]
	public int? LoginVersion { get; set; }
	}

internal sealed class DiscoveryEncryptionInfoDto
	{
	[System.Text.Json.Serialization.JsonPropertyName ("sym_schm")]
	public string? SymmetricScheme { get; set; }
	}

internal sealed class TpapDiscoveryDto
	{
	[System.Text.Json.Serialization.JsonPropertyName ("port")]
	public int? Port { get; set; }
	[System.Text.Json.Serialization.JsonPropertyName ("tls")]
	public int? Tls { get; set; }
	[System.Text.Json.Serialization.JsonPropertyName ("dac")]
	public int? Dac { get; set; }
	[System.Text.Json.Serialization.JsonPropertyName ("noc")]
	public int? Noc { get; set; }
	[System.Text.Json.Serialization.JsonPropertyName ("user_hash_type")]
	public int? UserHashType { get; set; }
	[System.Text.Json.Serialization.JsonPropertyName ("pake")]
	public List<int?>? Pake { get; set; }
	}

internal sealed class DiscoveryQueryDto
	{
	[System.Text.Json.Serialization.JsonPropertyName ("params")]
	public DiscoveryQueryParametersDto? Parameters { get; set; }
	}

internal sealed class DiscoveryQueryParametersDto
	{
	[System.Text.Json.Serialization.JsonPropertyName ("rsa_key")]
	public string? RsaKey { get; set; }
	}

[JsonConverter (typeof (DiscoveryEncryptionTypeConverter))]
internal sealed class DiscoveryEncryptionType
	{
	internal string? Name { get; set; }
	internal List<int?>? Versions { get; set; }
	}

internal sealed class DiscoveryEncryptionTypeConverter : JsonConverter<DiscoveryEncryptionType>
	{
	public override DiscoveryEncryptionType Read (ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
		if (reader.TokenType == JsonTokenType.String) return new DiscoveryEncryptionType { Name = reader.GetString () };
		if (reader.TokenType == JsonTokenType.StartArray) return new DiscoveryEncryptionType { Versions = JsonSerializer.Deserialize<List<int?>> (ref reader, options) };
		throw new JsonException ("Expected an encryption name or login-version array.");
		}
	public override void Write (Utf8JsonWriter writer, DiscoveryEncryptionType value, JsonSerializerOptions options)
		{
		if (value.Versions is not null) JsonSerializer.Serialize (writer, value.Versions, options);
		else writer.WriteStringValue (value.Name);
		}
	}
