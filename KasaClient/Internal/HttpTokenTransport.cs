// Copyright © 2026 Neil Colvin.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// Adapted from python-kasa (https://github.com/python-kasa/python-kasa)
// Original work Copyright (c) python-kasa contributors, MIT License

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace KasaTapoClient.Internal;

internal sealed class HttpTokenTransport : IDeviceTransport
	{
	private static readonly HttpClient HTTP_CLIENT = new ();
	private const int AUTHENTICATION_ERROR_CODE = -1501;
	private const string PASSTHROUGH_METHOD = "securePassthrough";
	private const string HANDSHAKE_METHOD = "handshake";
	private const string AES_SESSION_COOKIE_NAME = "TP_SESSIONID";
	private const string AES_TIMEOUT_COOKIE_NAME = "TIMEOUT";
	private const int AES_DEFAULT_SESSION_TIMEOUT_SECONDS = 24 * 60 * 60;
	private const int AES_SESSION_TIMEOUT_BUFFER_SECONDS = 20 * 60;
	private const int AES_HANDSHAKE_CONTENT_LENGTH = 314;
	private readonly DeviceConfiguration _configuration;
	private readonly Uri _applicationUri;
	private readonly CookieContainer _cookies = new ();
	private readonly DeviceConnectionParameters? _connectionParameters;
	private AesAuthState _authState = AesAuthState.HandshakeRequired;
	private DateTimeOffset? _sessionExpiresAtUtc;
	private RsaKeyPair? _rsaKeyPair;
	private AesEncryptionSession? _aesSession;
	private string? _token;

	public HttpTokenTransport (DeviceConfiguration configuration)
		{
		_configuration = configuration;
		_applicationUri = CreateApplicationUri (configuration);
		_connectionParameters = configuration.ConnectionOptions.ConnectionParameters;
		}

	public async Task<string> SendAsync (string commandJson, CancellationToken cancellationToken)
		{
		await EnsureAuthenticatedAsync (cancellationToken).ConfigureAwait (false);
		return await SendAuthenticatedAsync (commandJson, cancellationToken).ConfigureAwait (false);
		}

	public async Task<string> SendManyAsync (IReadOnlyList<string> commandJsonPayloads, CancellationToken cancellationToken)
		{
		if (commandJsonPayloads.Count == 0)
			{
			throw new ArgumentException ("At least one command payload is required.", nameof (commandJsonPayloads));
			}

		await EnsureAuthenticatedAsync (cancellationToken).ConfigureAwait (false);
		var mergedResponse = new JsonObject ();
		foreach (string payload in commandJsonPayloads)
			{
			string responseJson = await SendAuthenticatedAsync (payload, cancellationToken).ConfigureAwait (false);
			JsonSupport.MergeObjects (mergedResponse, JsonSupport.ParseObject (responseJson));
			}

		return mergedResponse.ToJsonString (JsonSupport.COMPACT_JSON);
		}

	private async Task EnsureAuthenticatedAsync (CancellationToken cancellationToken)
		{
		if (_authState == AesAuthState.Established && !IsSessionExpired ())
			{
			return;
			}

		DeviceCredentials credentials = ResolveCredentials ();
		if (_authState == AesAuthState.HandshakeRequired || IsSessionExpired ())
			{
			await PerformHandshakeAsync (cancellationToken).ConfigureAwait (false);
			}

		if (_authState == AesAuthState.LoginRequired)
			{
			await PerformLoginAsync (credentials, cancellationToken).ConfigureAwait (false);
			}
		}

	private async Task<string> SendAuthenticatedAsync (string commandJson, CancellationToken cancellationToken)
		{
		if (_aesSession is not null)
			{
			string aesResponseJson = await SendEncryptedPassthroughAsync (commandJson, cancellationToken).ConfigureAwait (false);
			if (RequiresReauthentication (aesResponseJson))
				{
				ResetAuthenticationState ();
				await EnsureAuthenticatedAsync (cancellationToken).ConfigureAwait (false);
				aesResponseJson = await SendEncryptedPassthroughAsync (commandJson, cancellationToken).ConfigureAwait (false);
				}

			return aesResponseJson;
			}

		Uri requestUri = CreateAuthenticatedUri ();
		string payload = WrapCommandPayload (commandJson);
		string responseJson = await PostJsonAsync (requestUri, payload, cancellationToken).ConfigureAwait (false);
		if (RequiresReauthentication (responseJson))
			{
			ResetAuthenticationState ();
			await EnsureAuthenticatedAsync (cancellationToken).ConfigureAwait (false);
			responseJson = await PostJsonAsync (CreateAuthenticatedUri (), payload, cancellationToken).ConfigureAwait (false);
			}

		return UnwrapCommandResponse (responseJson);
		}

	private async Task PerformHandshakeAsync (CancellationToken cancellationToken)
		{
		ResetHandshakeState ();
		RsaKeyPair keyPair = _rsaKeyPair ??= RsaKeyPair.Create ();
		string requestJson = CreateHandshakeRequest (keyPair.PublicKeyPem);
		string handshakeResponse = await PostJsonAsync (
			_applicationUri,
			requestJson,
			cancellationToken,
			AES_HANDSHAKE_CONTENT_LENGTH).ConfigureAwait (false);
		JsonObject root = JsonSupport.ParseObject (handshakeResponse);
		EnsureSuccess (root, $"Unable to complete handshake for '{_configuration.Host}'");
		string handshakeKey = root["result"]?["key"]?.GetValue<string?> ()
			?? throw new InvalidDataException ($"The handshake response for '{_configuration.Host}' did not include a key.");
		CaptureAesSessionCookies ();
		_aesSession = AesEncryptionSession.CreateFromHandshakeKey (handshakeKey, keyPair);
		_sessionExpiresAtUtc = ResolveSessionExpiryUtc ();
		_authState = AesAuthState.LoginRequired;
		}

	private async Task PerformLoginAsync (DeviceCredentials credentials, CancellationToken cancellationToken)
		{
		LoginAttemptResult result = await TryAesLoginAsync (CreateAesLoginRequest (credentials), cancellationToken).ConfigureAwait (false);
		if (string.IsNullOrWhiteSpace (result.Token) && IsLoginError (result))
			{
			DeviceCredentials defaultCredentials = DeviceCredentials.FromDefault (DefaultCredentialProfile.Tapo);
			if (!AreSameCredentials (credentials, defaultCredentials))
				{
				result = await RetryWithDefaultTapoCredentialsAsync (defaultCredentials, cancellationToken).ConfigureAwait (false);
				}
			}

		if (string.IsNullOrWhiteSpace (result.Token))
			{
			throw new InvalidDataException ($"The device login failed for '{_configuration.Host}'. encrypted login_device: {DescribeLoginFailure (result)}");
			}

		_token = result.Token;
		_authState = AesAuthState.Established;
		}

	private async Task<LoginAttemptResult> TryAesLoginAsync (JsonObject loginRequest, CancellationToken cancellationToken)
		{
		string requestJson = loginRequest.ToJsonString (JsonSupport.COMPACT_JSON);
		string responseJson = await SendEncryptedPassthroughAsync (requestJson, cancellationToken).ConfigureAwait (false);
		JsonObject root = JsonSupport.ParseObject (responseJson);
		return new LoginAttemptResult (ExtractToken (root), GetErrorCode (root), responseJson);
		}

	private async Task<LoginAttemptResult> RetryWithDefaultTapoCredentialsAsync (DeviceCredentials defaultCredentials, CancellationToken cancellationToken)
		{
		await PerformHandshakeAsync (cancellationToken).ConfigureAwait (false);
		return await TryAesLoginAsync (CreateAesLoginRequest (defaultCredentials), cancellationToken).ConfigureAwait (false);
		}

	private JsonObject CreateAesLoginRequest (DeviceCredentials credentials)
		{
		bool loginVersion2 = _connectionParameters?.LoginVersion == 2;
		(string userName, string password) = HashAesCredentials (credentials, loginVersion2);
		string passwordFieldName = loginVersion2 ? "password2" : "password";
		return new JsonObject
			{
			["method"] = "login_device",
			["params"] = new JsonObject
				{
				["username"] = userName,
				[passwordFieldName] = password,
				},
			["request_time_milis"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds (),
			};
		}

	private static (string UserName, string Password) HashAesCredentials (DeviceCredentials credentials, bool loginVersion2)
		{
		string userName = credentials.UserName ?? throw new InvalidOperationException ("A user name is required for AES login.");
		string password = credentials.Password ?? throw new InvalidOperationException ("A password is required for AES login.");
		string hashedUserName = Convert.ToBase64String (Encoding.UTF8.GetBytes (ComputeSha1Hex (userName)));
		string passwordValue = loginVersion2
			? ComputeSha1Hex (password)
			: password;
		string hashedPassword = Convert.ToBase64String (Encoding.UTF8.GetBytes (passwordValue));
		return (hashedUserName, hashedPassword);
		}

	#pragma warning disable CA5350
	#pragma warning disable CA1850
	private static string ComputeSha1Hex (string value)
		{
		using SHA1 sha1 = SHA1.Create ();
		byte[] hash = sha1.ComputeHash (Encoding.UTF8.GetBytes (value));
		var builder = new StringBuilder (hash.Length * 2);
		for (int i = 0; i < hash.Length; i++)
			{
			builder.Append (hash[i].ToString ("x2", System.Globalization.CultureInfo.InvariantCulture));
			}

		return builder.ToString ();
		}
	#pragma warning restore CA1850
	#pragma warning restore CA5350

	private static string CreateHandshakeRequest (string publicKeyPem)
		{
		return new JsonObject
			{
			["method"] = HANDSHAKE_METHOD,
			["params"] = new JsonObject
				{
				["key"] = publicKeyPem,
				},
			}.ToJsonString (JsonSupport.COMPACT_JSON);
		}

	private static JsonObject CreateLoginRequest (DeviceCredentials credentials)
		{
		return new JsonObject
			{
			["method"] = "login",
			["params"] = new JsonObject
				{
				["username"] = credentials.UserName,
				["password"] = credentials.Password,
				},
			};
		}

	private static JsonObject CreateLoginDeviceRequest (DeviceCredentials credentials)
		{
		return new JsonObject
			{
			["method"] = "login_device",
			["params"] = new JsonObject
				{
				["username"] = credentials.UserName,
				["password"] = credentials.Password,
				},
			["request_time_milis"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds (),
			};
		}

	private static JsonObject CreateSecurePassthroughLoginRequest (JsonObject loginDeviceRequest)
		{
		string loginRequestJson = loginDeviceRequest.ToJsonString (JsonSupport.COMPACT_JSON);
		return new JsonObject
			{
			["method"] = PASSTHROUGH_METHOD,
			["params"] = new JsonObject
				{
				["request"] = loginRequestJson,
				},
			};
		}

	private Uri CreateAuthenticatedUri ()
		{
		if (string.IsNullOrWhiteSpace (_token))
			{
			throw new InvalidOperationException ("The HTTP transport is not authenticated.");
			}

		var builder = new UriBuilder (_applicationUri)
			{
			Query = $"token={Uri.EscapeDataString (_token)}",
			};
		return builder.Uri;
		}

	private async Task<string> PostJsonAsync (Uri uri, string payload, CancellationToken cancellationToken, int? overrideContentLength = null)
		{
		byte[] payloadBytes = Encoding.UTF8.GetBytes (payload);
		using var request = new HttpRequestMessage (HttpMethod.Post, uri)
			{
			Content = new ByteArrayContent (payloadBytes),
			};
		request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue ("application/json")
			{
			CharSet = Encoding.UTF8.WebName,
			};
		request.Headers.Add ("requestByApp", "true");
		request.Headers.Accept.ParseAdd ("application/json");
		if (overrideContentLength is int contentLength)
			{
			if (contentLength != payloadBytes.Length)
				{
				throw new InvalidOperationException ($"The explicit content length {contentLength} did not match the serialized payload length {payloadBytes.Length} for '{uri}'.");
				}
			request.Content.Headers.ContentLength = contentLength;
			}
		string cookieHeader = _cookies.GetCookieHeader (uri);
		if (!string.IsNullOrWhiteSpace (cookieHeader))
			{
			request.Headers.Add ("Cookie", cookieHeader);
			}

		using HttpResponseMessage response = await HTTP_CLIENT.SendAsync (request, HttpCompletionOption.ResponseContentRead, cancellationToken).ConfigureAwait (false);
		CaptureCookies (uri, response);
		response.EnsureSuccessStatusCode ();
#if NET10_0_OR_GREATER
		return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
#else
#pragma warning disable CA2016
		return await response.Content.ReadAsStringAsync ().ConfigureAwait (false);
#pragma warning restore CA2016
#endif
		}

	private DeviceCredentials ResolveCredentials ()
		{
		DeviceCredentials? credentials = _configuration.Credentials;
		string? userName = credentials?.UserName;
		string? password = credentials?.Password;
		if (!string.IsNullOrWhiteSpace (userName)
			&& !string.IsNullOrWhiteSpace (password))
			{
			return new DeviceCredentials (userName, password);
			}

		if (_configuration.ConnectionOptions.UseDefaultCredentials)
			{
			DefaultCredentialProfile profile = _configuration.ConnectionOptions.DefaultCredentialProfile == DefaultCredentialProfile.None
				? DefaultCredentialProfile.Tapo
				: _configuration.ConnectionOptions.DefaultCredentialProfile;
			return DeviceCredentials.FromDefault (profile);
			}

		throw new InvalidOperationException ($"Credentials are required to authenticate to '{_configuration.Host}'.");
		}

	private static Uri CreateApplicationUri (DeviceConfiguration configuration)
		{
		string path = configuration.ConnectionOptions.ApplicationPath.Length > 0
			&& configuration.ConnectionOptions.ApplicationPath[0] == '/'
			? configuration.ConnectionOptions.ApplicationPath
			: "/" + configuration.ConnectionOptions.ApplicationPath;
		var builder = new UriBuilder
			{
			Scheme = configuration.ConnectionOptions.UseSsl ? Uri.UriSchemeHttps : Uri.UriSchemeHttp,
			Host = configuration.Host,
			Port = configuration.Port,
			Path = path,
			};
		return builder.Uri;
		}

	private void CaptureCookies (Uri uri, HttpResponseMessage response)
		{
		if (!response.Headers.TryGetValues ("Set-Cookie", out IEnumerable<string>? cookieValues))
			{
			return;
			}

		foreach (string cookieValue in cookieValues)
			{
			_cookies.SetCookies (uri, cookieValue);
			}
		}

	private string WrapCommandPayload (string commandJson)
		{
		if (!_configuration.ConnectionOptions.UseSecurePassthrough)
			{
			return commandJson;
			}

		return new JsonObject
			{
			["method"] = PASSTHROUGH_METHOD,
			["params"] = new JsonObject
				{
				["request"] = commandJson,
				},
			}.ToJsonString (JsonSupport.COMPACT_JSON);
		}

	private string UnwrapCommandResponse (string responseJson)
		{
		if (!_configuration.ConnectionOptions.UseSecurePassthrough)
			{
			return responseJson;
			}

		JsonObject root = JsonSupport.ParseObject (responseJson);
		if (GetErrorCode (root) != 0)
			{
			return responseJson;
			}

		if (root["result"] is not JsonObject result)
			{
			return responseJson;
			}

		string? response = result["response"]?.GetValue<string?> (); 
		return string.IsNullOrWhiteSpace (response) ? responseJson : response ?? responseJson;
		}

	private static JsonObject ParseLoginResponse (string responseJson)
		{
		JsonObject root = JsonSupport.ParseObject (responseJson);
		if (root["result"] is not JsonObject result)
			{
			return root;
			}

		string? nestedResponse = result["response"]?.GetValue<string?> ();
		if (string.IsNullOrWhiteSpace (nestedResponse))
			{
			return root;
			}

		return JsonSupport.ParseObject (nestedResponse!);
		}

	private static string? ExtractToken (JsonObject response)
		{
		if (response["result"] is not JsonObject result)
			{
			return response["token"]?.GetValue<string?> () ?? response["stok"]?.GetValue<string?> ();
			}

		return result["token"]?.GetValue<string?> ()
			?? result["stok"]?.GetValue<string?> ()
			?? result["stok_token"]?.GetValue<string?> ()
			?? result["session_token"]?.GetValue<string?> ();
		}

	private static bool RequiresReauthentication (string responseJson)
		{
		JsonObject response = JsonSupport.ParseObject (responseJson);
		return GetErrorCode (response) == AUTHENTICATION_ERROR_CODE;
		}

	private async Task<string> SendEncryptedPassthroughAsync (string requestJson, CancellationToken cancellationToken)
		{
		if (_aesSession is null)
			{
			throw new InvalidOperationException ("The AES session has not been established.");
			}

		string encryptedRequest = _aesSession.Encrypt (requestJson);
		string payload = new JsonObject
			{
			["method"] = PASSTHROUGH_METHOD,
			["params"] = new JsonObject
				{
				["request"] = encryptedRequest,
				},
			}.ToJsonString (JsonSupport.COMPACT_JSON);
		string responseJson = await PostJsonAsync (ResolveAesRequestUri (), payload, cancellationToken).ConfigureAwait (false);
		JsonObject root = JsonSupport.ParseObject (responseJson);
		EnsureSuccess (root, $"Error sending secure_passthrough message to '{_configuration.Host}'");
		string? rawResponse = root["result"]?["response"]?.GetValue<string?> ();
		if (string.IsNullOrWhiteSpace (rawResponse))
			{
			return responseJson;
			}

		try
			{
			return _aesSession.Decrypt (rawResponse!);
			}
		catch
			{
			return rawResponse!;
			}
		}

	private Uri ResolveAesRequestUri ()
		{
		if (string.IsNullOrWhiteSpace (_token))
			{
			return _applicationUri;
			}

		return CreateAuthenticatedUri ();
		}

	private void CaptureAesSessionCookies ()
		{
		string cookieHeader = _cookies.GetCookieHeader (_applicationUri);
		if (string.IsNullOrWhiteSpace (cookieHeader))
			{
			return;
			}

		if (!ContainsOrdinalIgnoreCase (cookieHeader, AES_SESSION_COOKIE_NAME)
			&& ContainsOrdinalIgnoreCase (cookieHeader, "SESSIONID"))
			{
			string? sessionId = GetCookieValue (_applicationUri, "SESSIONID");
			if (!string.IsNullOrWhiteSpace (sessionId))
				{
				_cookies.SetCookies (_applicationUri, AES_SESSION_COOKIE_NAME + "=" + sessionId);
				}
			}
		}

	private DateTimeOffset ResolveSessionExpiryUtc ()
		{
		string? timeoutValue = GetCookieValue (_applicationUri, AES_TIMEOUT_COOKIE_NAME);
		int timeoutSeconds = int.TryParse (timeoutValue, out int parsedTimeout) ? parsedTimeout : AES_DEFAULT_SESSION_TIMEOUT_SECONDS;
		return DateTimeOffset.UtcNow.AddSeconds (Math.Max (0, timeoutSeconds - AES_SESSION_TIMEOUT_BUFFER_SECONDS));
		}

	private string? GetCookieValue (Uri uri, string cookieName)
		{
		foreach (Cookie cookie in _cookies.GetCookies (uri))
			{
			if (string.Equals (cookie.Name, cookieName, StringComparison.OrdinalIgnoreCase))
				{
				return cookie.Value;
				}
			}

		return null;
		}

	#pragma warning disable CA2249
	private static bool ContainsOrdinalIgnoreCase (string value, string substring) =>
		value.IndexOf (substring, StringComparison.OrdinalIgnoreCase) >= 0;
	#pragma warning restore CA2249

	private bool IsSessionExpired () => _sessionExpiresAtUtc is not DateTimeOffset expiresAtUtc || expiresAtUtc <= DateTimeOffset.UtcNow;

	private void ResetHandshakeState ()
		{
		_token = null;
		_aesSession = null;
		_sessionExpiresAtUtc = null;
		_authState = AesAuthState.HandshakeRequired;
		}

	private void ResetAuthenticationState () => ResetHandshakeState ();

	private static string DescribeLoginFailure (LoginAttemptResult result)
		{
		if (result.ErrorCode != 0)
			{
			return $"device returned error code {result.ErrorCode}";
			}

		return $"device returned no token. Raw response: {TrimResponse (result.RawResponse)}";
		}

	private static bool IsLoginError (LoginAttemptResult result) => result.ErrorCode == 1111;

	private static bool AreSameCredentials (DeviceCredentials left, DeviceCredentials right) =>
		string.Equals (left.UserName, right.UserName, StringComparison.Ordinal)
		&& string.Equals (left.Password, right.Password, StringComparison.Ordinal);

#pragma warning disable CA1845
	private static string TrimResponse (string response) => response.Length <= 240 ? response : response.Substring (0, 240) + "...";
#pragma warning restore CA1845

	private static int GetErrorCode (JsonObject response) =>
		response["error_code"]?.GetValue<int?> ()
		?? response["errorCode"]?.GetValue<int?> ()
		?? 0;

	private sealed class LoginAttemptResult
		{
		public LoginAttemptResult (string? token, int errorCode, string rawResponse)
			{
			Token = token;
			ErrorCode = errorCode;
			RawResponse = rawResponse;
			}

		public string? Token
			{
			get;
			}

		public int ErrorCode
			{
			get;
			}

		public string RawResponse
			{
			get;
			}
		}

	private enum AesAuthState
		{
		HandshakeRequired,
		LoginRequired,
		Established,
		}

	private sealed class AesEncryptionSession
		{
		private readonly byte[] _key;
		private readonly byte[] _iv;

		private AesEncryptionSession (byte[] key, byte[] iv)
			{
			_key = key;
			_iv = iv;
			}

		public static AesEncryptionSession CreateFromHandshakeKey (string handshakeKey, RsaKeyPair keyPair)
			{
			byte[] encryptedKey = Convert.FromBase64String (handshakeKey);
			byte[] keyAndIv = keyPair.DecryptHandshakeKey (encryptedKey);
			if (keyAndIv.Length < 32)
				{
				throw new InvalidDataException ("The AES handshake key payload was shorter than expected.");
				}

			byte[] key = new byte[16];
			byte[] iv = new byte[16];
			Buffer.BlockCopy (keyAndIv, 0, key, 0, 16);
			Buffer.BlockCopy (keyAndIv, 16, iv, 0, 16);
			return new AesEncryptionSession (key, iv);
			}

		public string Encrypt (string request)
			{
			byte[] requestBytes = Encoding.UTF8.GetBytes (request);
			byte[] encrypted = Transform (requestBytes, encrypt: true);
			return Convert.ToBase64String (encrypted);
			}

		public string Decrypt (string response)
			{
			byte[] encryptedBytes = Convert.FromBase64String (response);
			byte[] decrypted = Transform (encryptedBytes, encrypt: false);
			return Encoding.UTF8.GetString (decrypted);
			}

		private byte[] Transform (byte[] data, bool encrypt)
			{
			using Aes aes = Aes.Create ();
			aes.Mode = CipherMode.CBC;
			aes.Padding = PaddingMode.PKCS7;
			aes.Key = _key;
			aes.IV = _iv;
			using ICryptoTransform transform = encrypt ? aes.CreateEncryptor () : aes.CreateDecryptor ();
			return transform.TransformFinalBlock (data, 0, data.Length);
			}
		}

	private sealed class RsaKeyPair
		{
		private readonly RSA _privateKey;
		private readonly string _publicKeyDerBase64;

		private RsaKeyPair (RSA privateKey, string publicKeyDerBase64)
			{
			_privateKey = privateKey;
			_publicKeyDerBase64 = publicKeyDerBase64;
			}

		public static RsaKeyPair Create ()
			{
			RSA privateKey = RSA.Create (1024);
			string publicKeyDerBase64 = ExportPublicKeyDerBase64 (privateKey);
			return new RsaKeyPair (privateKey, publicKeyDerBase64);
			}

		public string PublicKeyPem => "-----BEGIN PUBLIC KEY-----\n" + _publicKeyDerBase64 + "\n-----END PUBLIC KEY-----\n";

		public byte[] DecryptHandshakeKey (byte[] encryptedKey) => _privateKey.Decrypt (encryptedKey, RSAEncryptionPadding.Pkcs1);
		}

	private static string ExportPublicKeyDerBase64 (RSA rsa)
		{
#if NET10_0_OR_GREATER
		return Convert.ToBase64String (rsa.ExportSubjectPublicKeyInfo ());
#else
		RSAParameters parameters = rsa.ExportParameters (false);
		return Convert.ToBase64String (CreateSubjectPublicKeyInfo (parameters));
#endif
		}

	private static byte[] CreateSubjectPublicKeyInfo (RSAParameters parameters)
		{
		if (parameters.Modulus is null || parameters.Exponent is null)
			{
			throw new InvalidOperationException ("Unable to export the RSA public key for HTTP AES handshake.");
			}

		byte[] rsaPublicKey = EncodeSequence (
			EncodeInteger (parameters.Modulus),
			EncodeInteger (parameters.Exponent));
		return EncodeSequence (
			EncodeSequence (
				EncodeObjectIdentifier (new byte[] { 0x2A, 0x86, 0x48, 0x86, 0xF7, 0x0D, 0x01, 0x01, 0x01 }),
				EncodeNull ()),
			EncodeBitString (rsaPublicKey));
		}

	private static byte[] EncodeSequence (params byte[][] values) => EncodeAsn1 (0x30, Combine (values));

	private static byte[] EncodeInteger (byte[] value)
		{
		int start = 0;
		while (start < value.Length - 1 && value[start] == 0)
			{
			start++;
			}

		int length = value.Length - start;
		bool prependZero = (value[start] & 0x80) != 0;
		var normalized = new byte[length + (prependZero ? 1 : 0)];
		if (prependZero)
			{
			normalized[0] = 0;
			}

		Buffer.BlockCopy (value, start, normalized, prependZero ? 1 : 0, length);
		return EncodeAsn1 (0x02, normalized);
		}

	private static byte[] EncodeBitString (byte[] value)
		{
		var content = new byte[value.Length + 1];
		content[0] = 0;
		Buffer.BlockCopy (value, 0, content, 1, value.Length);
		return EncodeAsn1 (0x03, content);
		}

	private static byte[] EncodeObjectIdentifier (byte[] encodedOid) => EncodeAsn1 (0x06, encodedOid);

	private static byte[] EncodeNull () => new byte[] { 0x05, 0x00 };

	private static byte[] EncodeAsn1 (byte tag, byte[] value)
		{
		byte[] length = EncodeLength (value.Length);
		var encoded = new byte[1 + length.Length + value.Length];
		encoded[0] = tag;
		Buffer.BlockCopy (length, 0, encoded, 1, length.Length);
		Buffer.BlockCopy (value, 0, encoded, 1 + length.Length, value.Length);
		return encoded;
		}

	private static byte[] EncodeLength (int value)
		{
		if (value < 0x80)
			{
			return [unchecked ((byte)value)];
			}

		var bytes = new List<byte> (4);
		int remaining = value;
		while (remaining > 0)
			{
			bytes.Insert (0, unchecked ((byte)(remaining & 0xFF)));
			remaining >>= 8;
			}

		bytes.Insert (0, unchecked ((byte)(0x80 | bytes.Count)));
		return [.. bytes];
		}

	private static byte[] Combine (params byte[][] values)
		{
		int length = 0;
		for (int i = 0; i < values.Length; i++)
			{
			length += values[i].Length;
			}

		var combined = new byte[length];
		int offset = 0;
		for (int i = 0; i < values.Length; i++)
			{
			Buffer.BlockCopy (values[i], 0, combined, offset, values[i].Length);
			offset += values[i].Length;
			}

		return combined;
		}

	private static void EnsureSuccess (JsonObject response, string message)
		{
		int errorCode = GetErrorCode (response);
		if (errorCode != 0)
			{
			throw new InvalidOperationException ($"{message}: device returned error code {errorCode}.");
			}
		}
	}
