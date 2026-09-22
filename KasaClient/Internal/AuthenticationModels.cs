// Copyright © 2026 Neil Colvin.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Text.Json.Serialization;

namespace KasaTapoClient.Internal;

internal sealed class WireRequest<T>
	{
	[JsonPropertyName ("method")]
	[JsonIgnore (Condition = JsonIgnoreCondition.WhenWritingNull)]
	public string? Method { get; set; }
	[JsonPropertyName ("params")]
	[JsonIgnore (Condition = JsonIgnoreCondition.WhenWritingNull)]
	public T? Parameters { get; set; }
	[JsonPropertyName ("request_time_milis")]
	[JsonIgnore (Condition = JsonIgnoreCondition.WhenWritingNull)]
	public long? RequestTimeMilliseconds { get; set; }
	[JsonPropertyName ("terminal_uuid")]
	[JsonIgnore (Condition = JsonIgnoreCondition.WhenWritingNull)]
	public string? TerminalUuid { get; set; }
	}

internal sealed class HttpAuthParametersDto
	{
	[JsonPropertyName ("username")]
	[JsonIgnore (Condition = JsonIgnoreCondition.WhenWritingNull)]
	public string? Username { get; set; }
	[JsonPropertyName ("password")]
	[JsonIgnore (Condition = JsonIgnoreCondition.WhenWritingNull)]
	public string? Password { get; set; }
	[JsonPropertyName ("password2")]
	[JsonIgnore (Condition = JsonIgnoreCondition.WhenWritingNull)]
	public string? Password2 { get; set; }
	[JsonPropertyName ("key")]
	[JsonIgnore (Condition = JsonIgnoreCondition.WhenWritingNull)]
	public string? Key { get; set; }
	[JsonPropertyName ("request")]
	[JsonIgnore (Condition = JsonIgnoreCondition.WhenWritingNull)]
	public string? Request { get; set; }
	}

[JsonDerivedType (typeof (TpapRegisterParametersDto))]
internal class TpapLoginParametersDto
	{
	[JsonPropertyName ("sub_method")]
	[JsonIgnore (Condition = JsonIgnoreCondition.WhenWritingNull)]
	public string? SubMethod { get; set; }
	[JsonPropertyName ("username")]
	[JsonIgnore (Condition = JsonIgnoreCondition.WhenWritingNull)]
	public string? Username { get; set; }
	[JsonPropertyName ("user_random")]
	[JsonIgnore (Condition = JsonIgnoreCondition.WhenWritingNull)]
	public string? UserRandom { get; set; }
	[JsonPropertyName ("cipher_suites")]
	[JsonIgnore (Condition = JsonIgnoreCondition.WhenWritingNull)]
	public int[]? CipherSuites { get; set; }
	[JsonPropertyName ("encryption")]
	[JsonIgnore (Condition = JsonIgnoreCondition.WhenWritingNull)]
	public string[]? Encryption { get; set; }
	[JsonPropertyName ("passcode_type")]
	[JsonIgnore (Condition = JsonIgnoreCondition.WhenWritingNull)]
	public string? PasscodeType { get; set; }
	[JsonPropertyName ("user_share")]
	[JsonIgnore (Condition = JsonIgnoreCondition.WhenWritingNull)]
	public string? UserShare { get; set; }
	[JsonPropertyName ("user_confirm")]
	[JsonIgnore (Condition = JsonIgnoreCondition.WhenWritingNull)]
	public string? UserConfirm { get; set; }
	[JsonPropertyName ("dac_nonce")]
	[JsonIgnore (Condition = JsonIgnoreCondition.WhenWritingNull)]
	public string? DacNonce { get; set; }
	}

internal sealed class TpapRegisterParametersDto : TpapLoginParametersDto
	{
	[JsonPropertyName ("stok")]
	public string? Stok { get; set; }
	}

internal sealed class AuthenticationResponseDto
	{
	[JsonPropertyName ("error_code")]
	public int? ErrorCode { get; set; }
	[JsonPropertyName ("errorCode")]
	public int? LegacyErrorCode { get; set; }
	[JsonPropertyName ("result")]
	public AuthenticationResultDto? Result { get; set; }
	[JsonPropertyName ("error_info")]
	public AuthenticationErrorDto? ErrorInfo { get; set; }
	[JsonPropertyName ("token")]
	public string? Token { get; set; }
	[JsonPropertyName ("stok")]
	public string? Stok { get; set; }
	}

internal sealed class AuthenticationResultDto
	{
	[JsonPropertyName ("key")]
	public string? Key { get; set; }
	[JsonPropertyName ("response")]
	public string? Response { get; set; }
	[JsonPropertyName ("token")]
	public string? Token { get; set; }
	[JsonPropertyName ("stok")]
	public string? Stok { get; set; }
	[JsonPropertyName ("stok_token")]
	public string? StokToken { get; set; }
	[JsonPropertyName ("session_token")]
	public string? SessionToken { get; set; }
	[JsonPropertyName ("mac")]
	public string? Mac { get; set; }
	[JsonPropertyName ("dev_random")]
	public string? DeviceRandom { get; set; }
	[JsonPropertyName ("dev_salt")]
	public string? DeviceSalt { get; set; }
	[JsonPropertyName ("dev_share")]
	public string? DeviceShare { get; set; }
	[JsonPropertyName ("encryption")]
	public string? Encryption { get; set; }
	[JsonPropertyName ("dev_confirm")]
	public string? DeviceConfirm { get; set; }
	[JsonPropertyName ("sessionId")]
	public string? SessionId { get; set; }
	[JsonPropertyName ("cipher_suites")]
	public int? CipherSuite { get; set; }
	[JsonPropertyName ("iterations")]
	public int? Iterations { get; set; }
	[JsonPropertyName ("start_seq")]
	public int? StartSequence { get; set; }
	[JsonPropertyName ("tpap")]
	public TpapDiscoveryDto? Tpap { get; set; }
	[JsonPropertyName ("extra_crypt")]
	public ExtraCryptDto? ExtraCrypt { get; set; }
	}

internal sealed class AuthenticationErrorDto
	{
	[JsonPropertyName ("failedAttempts")]
	public int? FailedAttempts { get; set; }
	[JsonPropertyName ("remainAttempts")]
	public int? RemainingAttempts { get; set; }
	[JsonPropertyName ("lockedMinute")]
	public int? LockedMinutes { get; set; }
	}

internal sealed class ExtraCryptDto
	{
	[JsonPropertyName ("type")]
	public string? Type { get; set; }
	[JsonPropertyName ("params")]
	public ExtraCryptParametersDto? Parameters { get; set; }
	}

internal sealed class ExtraCryptParametersDto
	{
	[JsonPropertyName ("passwd_id")]
	public int? PasswordId { get; set; }
	[JsonPropertyName ("sha_name")]
	public int? ShaName { get; set; }
	[JsonPropertyName ("sha_salt")]
	public string? ShaSalt { get; set; }
	[JsonPropertyName ("authkey_tmpkey")]
	public string? AuthKeyTemporaryKey { get; set; }
	[JsonPropertyName ("authkey_dictionary")]
	public string? AuthKeyDictionary { get; set; }
	}

internal sealed class LightCommandParametersDto
	{
	[JsonPropertyName ("color_temp")]
	[JsonIgnore (Condition = JsonIgnoreCondition.WhenWritingNull)]
	public int? ColorTemperature { get; set; }
	[JsonPropertyName ("brightness")]
	[JsonIgnore (Condition = JsonIgnoreCondition.WhenWritingNull)]
	public int? Brightness { get; set; }
	[JsonPropertyName ("hue")]
	[JsonIgnore (Condition = JsonIgnoreCondition.WhenWritingNull)]
	public int? Hue { get; set; }
	[JsonPropertyName ("saturation")]
	[JsonIgnore (Condition = JsonIgnoreCondition.WhenWritingNull)]
	public int? Saturation { get; set; }
	}

