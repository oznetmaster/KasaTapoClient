// Copyright © 2026 Neil Colvin.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

using Org.BouncyCastle.Asn1.Sec;
using Org.BouncyCastle.Asn1.X9;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Macs;
using Org.BouncyCastle.Crypto.Modes;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Security;
using BigIntegers = Org.BouncyCastle.Utilities.BigIntegers;

namespace KasaTapoClient.Internal;

internal sealed class TpapTransport : IDisposableDeviceTransport
	{
	private const int DEFAULT_HTTP_PORT = 80;
	private const int DEFAULT_HTTPS_PORT = 443;
	private const int TAG_LENGTH_BYTES = 16;
	private const int NONCE_LENGTH_BYTES = 12;
	private const int ERROR_CODE_SUCCESS = 0;
	private const int ERROR_CODE_SESSION_TIMEOUT = 9999;
	private const int ERROR_CODE_TRANSPORT_NOT_AVAILABLE = 1002;
	private const int ERROR_CODE_LOGIN = -1501;
	private const int ERROR_CODE_SESSION_EXPIRED = -40401;
	private const int ERROR_CODE_INVALID_NONCE = -40413;
	private const int ERROR_CODE_UNKNOWN = -100000;
	private static readonly TimeSpan DEFAULT_KEEPALIVE_INTERVAL = TimeSpan.FromSeconds (45);
	private static readonly byte[] PAKE_CONTEXT_TAG = Encoding.ASCII.GetBytes ("PAKE V1");
	private static readonly HashSet<DeviceFamilyKind> CAMERA_AUTH_DEVICE_FAMILIES = new ()
		{
		DeviceFamilyKind.SmartIpCamera,
		DeviceFamilyKind.SmartTapoDoorbell,
		DeviceFamilyKind.SmartTapoHub,
		};
	private static readonly Dictionary<string, CipherParameters> CIPHER_PARAMETERS = new (StringComparer.OrdinalIgnoreCase)
		{
		["aes_128_ccm"] = new CipherParameters (
			Encoding.ASCII.GetBytes ("tp-kdf-salt-aes128-key"),
			Encoding.ASCII.GetBytes ("tp-kdf-info-aes128-key"),
			Encoding.ASCII.GetBytes ("tp-kdf-salt-aes128-iv"),
			Encoding.ASCII.GetBytes ("tp-kdf-info-aes128-iv"),
			16),
		["aes_256_ccm"] = new CipherParameters (
			Encoding.ASCII.GetBytes ("tp-kdf-salt-aes256-key"),
			Encoding.ASCII.GetBytes ("tp-kdf-info-aes256-key"),
			Encoding.ASCII.GetBytes ("tp-kdf-salt-aes256-iv"),
			Encoding.ASCII.GetBytes ("tp-kdf-info-aes256-iv"),
			32),
		};
	// SecureRandom's parameterless constructor triggers BouncyCastle's default seeding path, which on
	// some platforms (notably slower/embedded CPUs such as Crestron processors) performs an expensive
	// entropy-gathering routine (e.g. a counter/timing-based generator) that can take a long time on
	// first use. Since this field is static, that cost is paid exactly once per process, right when the
	// very first TPAP handshake runs during driver startup - this matches the observed multi-minute
	// delay that only happens once per driver (re)load and never again afterward. Using the platform's
	// cryptographically secure RNG (RNGCryptoServiceProvider on .NET Framework, RandomNumberGenerator on
	// modern .NET) to seed a FixedPointCombinedRandomGenerator-free SecureRandom via SecureRandom.GetInstance
	// avoids BouncyCastle's slow default entropy-gathering estimator while still producing cryptographically
	// secure randomness suitable for the SPAKE2+ handshake's random scalar generation.
	private static readonly SecureRandom RANDOM = CreateSecureRandom ();

	internal static SecureRandom CreateSecureRandom ()
		{
		var random = new SecureRandom (new Org.BouncyCastle.Crypto.Prng.CryptoApiRandomGenerator ());
		random.SetSeed (random.GenerateSeed (32));
		return random;
		}
	private readonly DeviceConfiguration _configuration;
	private static readonly HttpClient HTTP_CLIENT = CreateHttpClient ();
	private readonly Uri _bootstrapUri;
	private readonly object _handshakeLockOwner = new ();
	private readonly object _sendLockOwner = new ();
	private readonly TimeSpan? _keepAliveInterval;
	private Uri _appUri;
	private string _knownDeviceMac = string.Empty;
	private int? _knownTpapTls;
	private int? _knownTpapPort;
	private bool _knownTpapDac;
	private List<int> _knownTpapPake = new ();
	private int? _knownTpapUserHashType;
	private SemaphoreSlim? _handshakeLock;
	private SemaphoreSlim? _sendLock;
	private string _deviceMac = string.Empty;
	private int? _tpapTls;
	private int? _tpapPort;
	private bool _tpapDac;
	private List<int> _tpapPake = new ();
	private int? _tpapUserHashType;
	private string? _sessionId;
	private int? _sequence;
	private Uri? _dsUri;
	private string _cipherId = "aes_128_ccm";
	private string _hkdfHash = "SHA256";
	private byte[]? _key;
	private byte[]? _baseNonce;
	private byte[]? _sharedKey;
	private string? _expectedDevConfirm;
	private string? _dacNonceBase64;
	private string? _userRandom;
	private DateTimeOffset? _lastActivityUtc;
	private bool _keepAliveInProgress;

	public TpapTransport (DeviceConfiguration configuration)
		{
		_configuration = configuration;
		_bootstrapUri = BuildAppUri (configuration.ConnectionOptions.UseSsl ? 1 : 0, configuration.Port);
		_appUri = _bootstrapUri;
		_knownTpapTls = configuration.ConnectionOptions.UseSsl ? 1 : 0;
		_knownTpapPort = configuration.Port;
		_keepAliveInterval = ResolveKeepAliveInterval (configuration.ConnectionOptions.TpapKeepAliveInterval);
		Reset ();
		}

	public async Task<string> SendAsync (string commandJson, CancellationToken cancellationToken)
		{
		try
			{
			return await SendOnceAsync (commandJson, cancellationToken).ConfigureAwait (false);
			}
		catch (Exception ex) when (!cancellationToken.IsCancellationRequested && ShouldRetryLiveSession (ex))
			{
			// Only retry when the failure was NOT caused by the caller's own CancellationToken.
			// ShouldRetryLiveSession() treats TaskCanceledException/OperationCanceledException as
			// retryable because SendOnceAsync uses an internal per-request timeout CancellationTokenSource
			// linked to the caller's token; without this guard, a genuine external cancellation would be
			// swallowed and silently retried (including a full Reset()+handshake) instead of propagating
			// to the caller immediately.
			Reset ();
			return await SendOnceAsync (commandJson, cancellationToken).ConfigureAwait (false);
			}
		}

	public async Task<string> SendManyAsync (IReadOnlyList<string> commandJsonPayloads, CancellationToken cancellationToken)
		{
		if (commandJsonPayloads.Count == 0)
			{
			throw new ArgumentException ("At least one command payload is required.", nameof (commandJsonPayloads));
			}

		var merged = new JsonObject ();
		foreach (string payload in commandJsonPayloads)
			{
			string responseJson = await SendAsync (payload, cancellationToken).ConfigureAwait (false);
			JsonSupport.MergeObjects (merged, JsonSupport.ParseObject (responseJson));
			}

		return merged.ToJsonString (JsonSupport.COMPACT_JSON);
		}

	public void Dispose ()
		{
		// HTTP_CLIENT is shared/static across all TpapTransport instances and must not be disposed here.
		}

	private async Task<string> SendOnceAsync (string commandJson, CancellationToken cancellationToken)
		{
		using CancellationTokenSource? timeoutSource = CreateOperationTimeoutSource (GetSecureRequestTimeout (commandJson), cancellationToken);
		CancellationToken operationCancellationToken = timeoutSource?.Token ?? cancellationToken;
		await SendKeepAliveIfNeededAsync (operationCancellationToken).ConfigureAwait (false);
		await EnsureHandshakeAsync (operationCancellationToken).ConfigureAwait (false);

		SemaphoreSlim sendLock;
		lock (_sendLockOwner)
			{
			sendLock = _sendLock ??= new SemaphoreSlim (1, 1);
			}

		await sendLock.WaitAsync (operationCancellationToken).ConfigureAwait (false);
		try
			{
			(Uri dsUri, byte[] key, byte[] baseNonce, string cipherId, int sequence) = RequireEstablishedSession ();
			byte[] plaintext = Encoding.UTF8.GetBytes (commandJson);
			byte[] encrypted = EncryptPayload (cipherId, key, baseNonce, plaintext, sequence);
			byte[] requestPayload = Combine (GetBigEndian (sequence), encrypted);
			_sequence = sequence + 1;

			using var request = new HttpRequestMessage (HttpMethod.Post, dsUri)
				{
				Content = new ByteArrayContent (requestPayload),
				};
			request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue ("application/octet-stream");

			byte[] body;
			try
				{
				using HttpResponseMessage response = await SendHttpAsync (request, operationCancellationToken).ConfigureAwait (false);
				if ((int)response.StatusCode != 200)
					{
					throw new InvalidOperationException ($"TPAP secure request failed for '{_configuration.Host}': status {(int)response.StatusCode}.");
					}

				body = await ReadBytesAsync (response, operationCancellationToken).ConfigureAwait (false);
				}
			catch (Exception ex) when (ex is not OperationCanceledException && operationCancellationToken.IsCancellationRequested)
				{
				throw ToCancellationException (ex, operationCancellationToken);
				}

			if (LooksLikeJson (body))
				{
				string jsonResponse = Encoding.UTF8.GetString (body);
				JsonObject root = JsonSupport.ParseObject (jsonResponse);
				HandleResponseErrorCode (root, "request");
				RecordActivity ();
				return jsonResponse;
				}

			byte[] decrypted = DecryptPayloadEnvelope (cipherId, key, baseNonce, body, sequence);
			string responseJson = Encoding.UTF8.GetString (decrypted);
			JsonSupport.ParseObject (responseJson);
			RecordActivity ();
			return responseJson;
			}
		finally
			{
			sendLock.Release ();
			}
		}

	// When the internal per-request timeout (CreateOperationTimeoutSource) elapses, SocketsHttpHandler
	// aborts the underlying socket and throws IOException/HttpRequestException (commonly with a message
	// like "operation has been aborted"), NOT an OperationCanceledException, even though the failure is
	// really a timeout. ShouldRetryLiveSession() must not misclassify that as a genuine transport reset,
	// so any such exception observed while operationCancellationToken is already cancelled is translated
	// into an OperationCanceledException here before it can reach the retry policy.
	internal static OperationCanceledException ToCancellationException (Exception exception, CancellationToken operationCancellationToken)
		{
		return new OperationCanceledException ("The TPAP request was aborted because the request timeout elapsed.", exception, operationCancellationToken);
		}

	private async Task EnsureHandshakeAsync (CancellationToken cancellationToken)
		{
		if (IsEstablished ())
			{
			return;
			}

		SemaphoreSlim handshakeLock;
		lock (_handshakeLockOwner)
			{
			handshakeLock = _handshakeLock ??= new SemaphoreSlim (1, 1);
			}

		await handshakeLock.WaitAsync (cancellationToken).ConfigureAwait (false);
		try
			{
			if (IsEstablished ())
				{
				return;
				}

			Debug.WriteLine ($"[KasaTapoClient.Tpap] '{_configuration.Host}' handshake starting.");
			Reset ();
			Debug.WriteLine ($"[KasaTapoClient.Tpap] '{_configuration.Host}' handshake: calling DiscoverAsync.");
			await DiscoverAsync (cancellationToken).ConfigureAwait (false);
			Debug.WriteLine ($"[KasaTapoClient.Tpap] '{_configuration.Host}' handshake: DiscoverAsync completed; calling PerformAuthHandshakeAsync.");
			await PerformAuthHandshakeAsync (cancellationToken).ConfigureAwait (false);
			Debug.WriteLine ($"[KasaTapoClient.Tpap] '{_configuration.Host}' handshake: PerformAuthHandshakeAsync completed.");
			RecordActivity ();
			}
		finally
			{
			handshakeLock.Release ();
			}
		}

	private async Task DiscoverAsync (CancellationToken cancellationToken)
		{
		var body = new JsonObject
			{
			["method"] = "login",
			["params"] = new JsonObject
				{
				["sub_method"] = "discover",
				},
			};
		Debug.WriteLine ($"[KasaTapoClient.Tpap] '{_configuration.Host}' discover: posting login/discover to '{_appUri}'.");
		JsonObject response = await PostLoginAsync (body, "discover", cancellationToken).ConfigureAwait (false);
		Debug.WriteLine ($"[KasaTapoClient.Tpap] '{_configuration.Host}' discover: login/discover responded.");
		JsonObject result = RequireResultObject (response);
		if (result["tpap"] is not JsonObject tpap)
			{
			throw new InvalidDataException ("The TPAP discover response did not contain a tpap object.");
			}

		_deviceMac = result["mac"]?.GetValue<string?> () ?? string.Empty;
		_tpapTls = GetOptionalInt (tpap["tls"]);
		_tpapPort = GetOptionalInt (tpap["port"]);
		int? dacValue = GetOptionalInt (tpap["dac"]);
		_tpapDac = dacValue == 1;
		_tpapPake = ReadIntArray (tpap["pake"]);
		_tpapUserHashType = GetOptionalInt (tpap["user_hash_type"]);

		_knownDeviceMac = _deviceMac;
		_knownTpapTls = _tpapTls;
		_knownTpapPort = _tpapPort;
		_knownTpapDac = _tpapDac;
		_knownTpapPake = new List<int> (_tpapPake);
		_knownTpapUserHashType = _tpapUserHashType;
		_appUri = BuildAppUri (_tpapTls, _tpapPort);
		}

	private async Task PerformAuthHandshakeAsync (CancellationToken cancellationToken)
		{
		string? passcodeType = GetPasscodeType ();
		if (passcodeType is null || passcodeType.Length == 0)
			{
			throw new UnauthorizedAccessException ($"TPAP: no supported passcode type for '{_configuration.Host}'.");
			}

		string resolvedPasscodeType = passcodeType;
		List<string> candidateSecrets = GetCandidateSecrets (resolvedPasscodeType);
		if (candidateSecrets.Count == 0)
			{
			throw new UnauthorizedAccessException ($"TPAP: no credential candidates available for '{_configuration.Host}'.");
			}

		string registerUserName = GetRegisterUserName ();
		Exception? lastError = null;
		foreach (string candidateSecret in candidateSecrets)
			{
				_sharedKey = null;
				_expectedDevConfirm = null;
				_dacNonceBase64 = null;
				_userRandom = Convert.ToBase64String (CreateRandomBytes (32));

				var registerParams = new JsonObject
					{
					["sub_method"] = "pake_register",
					["username"] = registerUserName,
					["user_random"] = _userRandom,
					["cipher_suites"] = new JsonArray (1),
					["encryption"] = new JsonArray ("aes_128_ccm"),
					["passcode_type"] = resolvedPasscodeType,
					["stok"] = null,
					};

				try
					{
					Debug.WriteLine ($"[KasaTapoClient.Tpap] '{_configuration.Host}' handshake: calling pake_register.");
					JsonObject registerResult = await LoginAsync (registerParams, "pake_register", cancellationToken).ConfigureAwait (false);
					Debug.WriteLine ($"[KasaTapoClient.Tpap] '{_configuration.Host}' handshake: pake_register responded; resolving credentials.");
					string credentialsString = ResolveCredentialsString (registerResult, candidateSecret, resolvedPasscodeType);
					Debug.WriteLine ($"[KasaTapoClient.Tpap] '{_configuration.Host}' handshake: credentials resolved; building share params (PBKDF2/EC math).");
					JsonObject shareParams = BuildShareParamsFromRegister (registerResult, credentialsString);
					Debug.WriteLine ($"[KasaTapoClient.Tpap] '{_configuration.Host}' handshake: share params built.");
					if (UseDacCertification ())
						{
						_dacNonceBase64 = Convert.ToBase64String (CreateRandomBytes (16));
						shareParams["dac_nonce"] = _dacNonceBase64;
						}

					Debug.WriteLine ($"[KasaTapoClient.Tpap] '{_configuration.Host}' handshake: calling pake_share.");
					JsonObject shareResult = await LoginAsync (shareParams, "pake_share", cancellationToken).ConfigureAwait (false);
					Debug.WriteLine ($"[KasaTapoClient.Tpap] '{_configuration.Host}' handshake: pake_share responded; establishing session.");
					EstablishSessionFromShareResult (shareResult);
					Debug.WriteLine ($"[KasaTapoClient.Tpap] '{_configuration.Host}' handshake: session established.");
					return;
					}
				catch (Exception ex) when (ex is not OperationCanceledException)
					{
					Debug.WriteLine ($"[KasaTapoClient.Tpap] '{_configuration.Host}' handshake candidate failed: {ex.GetType ().Name}: {ex.Message}");
					lastError = ex;
					}
			}

		throw lastError ?? new InvalidOperationException ("TPAP handshake did not produce a session.");
		}

	private async Task<JsonObject> LoginAsync (JsonObject parameters, string stepName, CancellationToken cancellationToken)
		{
		var body = new JsonObject
			{
			["method"] = "login",
			["params"] = parameters,
			};
		JsonObject response = await PostLoginAsync (body, stepName, cancellationToken).ConfigureAwait (false);
		return RequireResultObject (response);
		}

	private async Task<JsonObject> PostLoginAsync (JsonObject body, string stepName, CancellationToken cancellationToken)
		{
		using CancellationTokenSource? timeoutSource = CreateOperationTimeoutSource (_configuration.Timeout, cancellationToken);
		CancellationToken operationCancellationToken = timeoutSource?.Token ?? cancellationToken;
		using var request = new HttpRequestMessage (HttpMethod.Post, new Uri (_appUri, "/"))
			{
			Content = new StringContent (body.ToJsonString (JsonSupport.COMPACT_JSON), Encoding.UTF8, "application/json"),
			};
		string responseText;
		try
			{
			using HttpResponseMessage response = await SendHttpAsync (request, operationCancellationToken).ConfigureAwait (false);
			responseText = await ReadStringAsync (response, operationCancellationToken).ConfigureAwait (false);
			if ((int)response.StatusCode != 200)
				{
				throw new InvalidOperationException ($"TPAP {stepName} failed for '{_configuration.Host}': {(int)response.StatusCode}.");
				}
			}
		catch (Exception ex) when (ex is not OperationCanceledException && operationCancellationToken.IsCancellationRequested)
			{
			throw ToCancellationException (ex, operationCancellationToken);
			}

		JsonObject root = JsonSupport.ParseObject (responseText);
		HandleResponseErrorCode (root, stepName);
		return root;
		}

	private JsonObject BuildShareParamsFromRegister (JsonObject registerResult, string credentialsString)
		{
		if (string.IsNullOrWhiteSpace (_userRandom))
			{
			throw new InvalidOperationException ("TPAP user random was not initialized.");
			}

		string devRandom = registerResult["dev_random"]?.GetValue<string?> () ?? string.Empty;
		string devSalt = registerResult["dev_salt"]?.GetValue<string?> () ?? string.Empty;
		string devShare = registerResult["dev_share"]?.GetValue<string?> () ?? string.Empty;
		if (string.IsNullOrWhiteSpace (devRandom) || string.IsNullOrWhiteSpace (devSalt) || string.IsNullOrWhiteSpace (devShare))
			{
			throw new InvalidDataException ("The TPAP register response was missing required SPAKE2+ fields.");
			}

		int suiteType = GetRequiredInt (registerResult, "cipher_suites");
		int iterations = GetRequiredInt (registerResult, "iterations");
		if (iterations <= 0)
			{
			throw new InvalidDataException ("The TPAP register response reported an invalid iteration count.");
			}

		string encryption = registerResult["encryption"]?.GetValue<string?> () ?? string.Empty;
		if (string.IsNullOrWhiteSpace (encryption))
			{
			throw new InvalidDataException ("The TPAP register response did not include a session cipher.");
			}

		_cipherId = NormalizeCipherId (encryption);
		_hkdfHash = SuiteHashName (suiteType);
		CipherSuiteParameters suite = GetCipherSuiteParameters (suiteType);

		(byte[] leftBytes, byte[] rightBytes) = DeriveAb (Encoding.UTF8.GetBytes (credentialsString), Convert.FromBase64String (devSalt), iterations, 32);
		BigInteger order = suite.CurveParameters.N;
		BigInteger wValue = new (1, leftBytes);
		BigInteger hValue = new (1, rightBytes);
		wValue = wValue.Mod (order);
		hValue = hValue.Mod (order);
		BigInteger xValue = BigIntegers.CreateRandomInRange (BigInteger.One, order.Subtract (BigInteger.One), RANDOM);

		var mPoint = suite.CurveParameters.Curve.DecodePoint (suite.MPointEncoded).Normalize ();
		var nPoint = suite.CurveParameters.Curve.DecodePoint (suite.NPointEncoded).Normalize ();
		var generator = suite.CurveParameters.G.Normalize ();
		var lPoint = generator.Multiply (xValue).Add (mPoint.Multiply (wValue)).Normalize ();
		byte[] lEncoded = lPoint.GetEncoded (false);

		var rPoint = suite.CurveParameters.Curve.DecodePoint (Convert.FromBase64String (devShare)).Normalize ();
		byte[] rEncoded = rPoint.GetEncoded (false);
		var rPrime = rPoint.Subtract (nPoint.Multiply (wValue)).Normalize ();
		var zPoint = rPrime.Multiply (xValue).Normalize ();
		var vPoint = rPrime.Multiply (hValue.Mod (order)).Normalize ();

		byte[] contextHash = ComputeHash (_hkdfHash, Combine (PAKE_CONTEXT_TAG, Convert.FromBase64String (_userRandom), Convert.FromBase64String (devRandom)));
		byte[] wEncoded = EncodeW (wValue);
		byte[] transcript = Combine (
			EncodeLen8Le (contextHash),
			EncodeLen8Le (Array.Empty<byte> ()),
			EncodeLen8Le (Array.Empty<byte> ()),
			EncodeLen8Le (mPoint.GetEncoded (false)),
			EncodeLen8Le (nPoint.GetEncoded (false)),
			EncodeLen8Le (lEncoded),
			EncodeLen8Le (rEncoded),
			EncodeLen8Le (zPoint.GetEncoded (false)),
			EncodeLen8Le (vPoint.GetEncoded (false)),
			EncodeLen8Le (wEncoded));

		byte[] transcriptHash = ComputeHash (_hkdfHash, transcript);
		int digestLength = string.Equals (_hkdfHash, "SHA512", StringComparison.OrdinalIgnoreCase) ? 64 : 32;
		int macLength = suite.UseCmac ? 16 : 32;
		byte[] confirmationKeys = HkdfExpand ("ConfirmationKeys", transcriptHash, macLength * 2, _hkdfHash);
		byte[] keyConfirmA = confirmationKeys.Take (macLength).ToArray ();
		byte[] keyConfirmB = confirmationKeys.Skip (macLength).Take (macLength).ToArray ();
		_sharedKey = HkdfExpand ("SharedKey", transcriptHash, digestLength, _hkdfHash);
		byte[] userConfirm = suite.UseCmac
			? ComputeCmacAes (keyConfirmA, rEncoded)
			: ComputeHmac (_hkdfHash, keyConfirmA, rEncoded);
		byte[] expectedDeviceConfirm = suite.UseCmac
			? ComputeCmacAes (keyConfirmB, lEncoded)
			: ComputeHmac (_hkdfHash, keyConfirmB, lEncoded);
		_expectedDevConfirm = Convert.ToBase64String (expectedDeviceConfirm);

		return new JsonObject
			{
			["sub_method"] = "pake_share",
			["user_share"] = Convert.ToBase64String (lEncoded),
			["user_confirm"] = Convert.ToBase64String (userConfirm),
			};
		}

	private void EstablishSessionFromShareResult (JsonObject shareResult)
		{
		string devConfirm = shareResult["dev_confirm"]?.GetValue<string?> () ?? string.Empty;
		if (string.IsNullOrWhiteSpace (devConfirm))
			{
			throw new InvalidDataException ("The TPAP share response did not include dev_confirm.");
			}

		if (!string.Equals (devConfirm, _expectedDevConfirm, StringComparison.OrdinalIgnoreCase))
			{
			throw new InvalidDataException ("The TPAP confirmation value did not match the expected device confirm.");
			}

		string sessionId = shareResult["sessionId"]?.GetValue<string?> ()
			?? shareResult["stok"]?.GetValue<string?> ()
			?? string.Empty;
		if (string.IsNullOrWhiteSpace (sessionId))
			{
			throw new InvalidDataException ("The TPAP share response did not include a session identifier.");
			}

		if (_sharedKey is null)
			{
			throw new InvalidOperationException ("The TPAP shared key was not derived.");
			}

		int startSequence = GetRequiredInt (shareResult, "start_seq");
		(CipherParameters cipherParameters, byte[] key, byte[] baseNonce) = DeriveSessionKeyMaterial (_sharedKey, _cipherId, _hkdfHash);
		_sessionId = sessionId;
		_sequence = startSequence;
		_key = key;
		_baseNonce = baseNonce;
		_dsUri = new Uri (_appUri, $"stok={Uri.EscapeDataString (sessionId)}/ds");
		}

	private void Reset ()
		{
		_appUri = _bootstrapUri;
		_deviceMac = _knownDeviceMac;
		_tpapTls = _knownTpapTls;
		_tpapPort = _knownTpapPort;
		_tpapDac = _knownTpapDac;
		_tpapPake = new List<int> (_knownTpapPake);
		_tpapUserHashType = _knownTpapUserHashType;
		InvalidateSession ();
		}

	private void InvalidateSession ()
		{
		_sessionId = null;
		_sequence = null;
		_dsUri = null;
		_cipherId = "aes_128_ccm";
		_hkdfHash = "SHA256";
		_key = null;
		_baseNonce = null;
		_sharedKey = null;
		_expectedDevConfirm = null;
		_dacNonceBase64 = null;
		_userRandom = null;
		}

	private bool IsEstablished () => _sessionId is not null
		&& _sequence is not null
		&& _dsUri is not null
		&& _key is not null
		&& _baseNonce is not null;

	private (Uri DsUri, byte[] Key, byte[] BaseNonce, string CipherId, int Sequence) RequireEstablishedSession ()
		{
		if (!IsEstablished () || _key is null || _baseNonce is null || _sequence is null || _dsUri is null)
			{
			throw new InvalidOperationException ("The TPAP transport is not established.");
			}

		return (_dsUri, _key, _baseNonce, _cipherId, _sequence.Value);
		}

	private static HttpClient CreateHttpClient ()
		{
		var handler = new HttpClientHandler
			{
			AllowAutoRedirect = false,
			ServerCertificateCustomValidationCallback = (_, _, _, _) => true,
			};
		return new HttpClient (handler)
			{
			Timeout = Timeout.InfiniteTimeSpan,
			};
		}

	// On .NET Framework, HttpClientHandler is backed by HttpWebRequest, whose in-flight
	// GetRequestStreamAsync/GetResponseAsync calls do not reliably unblock when the supplied
	// CancellationToken is cancelled (see the identical, previously diagnosed issue in
	// KlapTransport.PostBytesNetFrameworkAsync). Left unaddressed, cancelling
	// operationCancellationToken here (e.g. via CreateOperationTimeoutSource's 8-second startup
	// connect timeout) does not actually abort the request; the call instead blocks until a much
	// longer OS/runtime-level socket timeout elapses (observed as ~2 minute stalls in production
	// logs for TPAP devices such as the Tapo L900, instead of the configured timeout). Route
	// through an HttpWebRequest-based path with an explicit Abort() registration on .NET Framework
	// to guarantee prompt cancellation, matching the proven KlapTransport approach.
	private static Task<HttpResponseMessage> SendHttpAsync (HttpRequestMessage request, CancellationToken cancellationToken)
		{
		#if NETFRAMEWORK
		return SendHttpNetFrameworkAsync (request, cancellationToken);
		#else
		return HTTP_CLIENT.SendAsync (request, HttpCompletionOption.ResponseContentRead, cancellationToken);
		#endif
		}

	#if NETFRAMEWORK
	private static async Task<HttpResponseMessage> SendHttpNetFrameworkAsync (HttpRequestMessage request, CancellationToken cancellationToken)
		{
		cancellationToken.ThrowIfCancellationRequested ();

		byte[] payload = request.Content is null
			? Array.Empty<byte> ()
			: await request.Content.ReadAsByteArrayAsync ().ConfigureAwait (false);

		var webRequest = (HttpWebRequest)WebRequest.Create (request.RequestUri);
		webRequest.Method = request.Method.Method;
		webRequest.AllowAutoRedirect = false;
		webRequest.KeepAlive = false;
		webRequest.ServicePoint.Expect100Continue = false;
		// Matches the HttpClientHandler.ServerCertificateCustomValidationCallback behavior used by
		// the non-.NET Framework HttpClient path above: these are local-network TPAP devices that
		// commonly present self-signed certificates, so certificate identity is intentionally not
		// validated here.
		#pragma warning disable CA5359
		webRequest.ServerCertificateValidationCallback = (_, _, _, _) => true;
		#pragma warning restore CA5359

		string? contentType = request.Content?.Headers.ContentType?.ToString ();
		if (!string.IsNullOrWhiteSpace (contentType))
			{
			webRequest.ContentType = contentType;
			}

		webRequest.ContentLength = payload.Length;

		using (cancellationToken.Register (static state => ((HttpWebRequest)state!).Abort (), webRequest))
			{
			if (payload.Length > 0 || string.Equals (request.Method.Method, "POST", StringComparison.OrdinalIgnoreCase))
				{
				using Stream requestStream = await WaitWithCancellationAsync (webRequest.GetRequestStreamAsync (), cancellationToken).ConfigureAwait (false);
				await requestStream.WriteAsync (payload, 0, payload.Length, cancellationToken).ConfigureAwait (false);
				}

			HttpWebResponse webResponse;
			try
				{
				webResponse = (HttpWebResponse)await WaitWithCancellationAsync (webRequest.GetResponseAsync (), cancellationToken).ConfigureAwait (false);
				}
			catch (WebException ex) when (ex.Response is HttpWebResponse errorResponse)
				{
				webResponse = errorResponse;
				}
			catch (WebException ex) when (ex.Status == WebExceptionStatus.RequestCanceled && cancellationToken.IsCancellationRequested)
				{
				throw new OperationCanceledException ("The TPAP request was canceled.", ex, cancellationToken);
				}

			using (webResponse)
				{
				byte[] responseBytes;
				using (Stream responseStream = webResponse.GetResponseStream () ?? Stream.Null)
					{
					using var memoryStream = new MemoryStream ();
					await responseStream.CopyToAsync (memoryStream).ConfigureAwait (false);
					responseBytes = memoryStream.ToArray ();
					}

				var message = new HttpResponseMessage (webResponse.StatusCode)
					{
					Content = new ByteArrayContent (responseBytes),
					};

				foreach (string? headerName in webResponse.Headers.AllKeys)
					{
					if (string.IsNullOrWhiteSpace (headerName))
						{
						continue;
						}

					string[]? headerValues = webResponse.Headers.GetValues (headerName);
					if (headerValues is null || headerValues.Length == 0)
						{
						continue;
						}

					if (!message.Headers.TryAddWithoutValidation (headerName, headerValues))
						{
						message.Content.Headers.TryAddWithoutValidation (headerName, headerValues);
						}
					}

				return message;
				}
			}
		}

	// HttpWebRequest.Abort() (registered against the CancellationToken above) does not always
	// promptly unblock an in-flight GetRequestStreamAsync/GetResponseAsync call on .NET Framework;
	// the underlying task can remain pending until the request's own Timeout elapses. Racing the
	// task against the cancellation token here ensures the caller observes cancellation as soon as
	// the token fires, rather than waiting for Abort() to take effect on the original task.
	private static async Task<T> WaitWithCancellationAsync<T> (Task<T> task, CancellationToken cancellationToken)
		{
		if (task.IsCompleted || !cancellationToken.CanBeCanceled)
			{
			return await task.ConfigureAwait (false);
			}

		var cancellationCompletionSource = new TaskCompletionSource<bool> (TaskCreationOptions.RunContinuationsAsynchronously);
		using (cancellationToken.Register (static state => ((TaskCompletionSource<bool>)state!).TrySetResult (true), cancellationCompletionSource))
			{
			Task completedTask = await Task.WhenAny (task, cancellationCompletionSource.Task).ConfigureAwait (false);
			if (completedTask == cancellationCompletionSource.Task)
				{
				cancellationToken.ThrowIfCancellationRequested ();
				}

			return await task.ConfigureAwait (false);
			}
		}
	#endif

	private TimeSpan GetSecureRequestTimeout (string commandJson)
		{
		if (IsLongRunningLightingEffectRequest (commandJson) && _configuration.Timeout < TimeSpan.FromSeconds (45))
			{
			return TimeSpan.FromSeconds (45);
			}

		if (IsLongRunningLightStateRequest (commandJson) && _configuration.Timeout < TimeSpan.FromSeconds (45))
			{
			return TimeSpan.FromSeconds (45);
			}

		return _configuration.Timeout;
		}

	private static bool IsLongRunningLightingEffectRequest (string commandJson)
		{
		if (string.IsNullOrWhiteSpace (commandJson))
			{
			return false;
			}

		try
			{
				JsonObject root = JsonSupport.ParseObject (commandJson);
				string? method = root["method"]?.GetValue<string?> ();
				return string.Equals (method, "set_lighting_effect", StringComparison.Ordinal);
			}
		catch (Exception)
			{
			return false;
			}
		}

	private static bool IsLongRunningLightStateRequest (string commandJson)
		{
		if (string.IsNullOrWhiteSpace (commandJson))
			{
			return false;
			}

		try
			{
				JsonObject root = JsonSupport.ParseObject (commandJson);
				string? method = root["method"]?.GetValue<string?> ();
				if (!string.Equals (method, "set_device_info", StringComparison.Ordinal))
					{
					return false;
					}

				JsonObject? parameters = root["params"] as JsonObject;
				if (parameters is null)
					{
					return false;
					}

				return parameters.ContainsKey ("color_temp")
					|| parameters.ContainsKey ("brightness")
					|| parameters.ContainsKey ("hue")
					|| parameters.ContainsKey ("saturation");
			}
		catch (Exception)
			{
			return false;
			}
		}

	private static CancellationTokenSource? CreateOperationTimeoutSource (TimeSpan timeout, CancellationToken cancellationToken)
		{
		if (timeout == Timeout.InfiniteTimeSpan)
			{
			return null;
			}

		CancellationTokenSource timeoutSource = CancellationTokenSource.CreateLinkedTokenSource (cancellationToken);
		timeoutSource.CancelAfter (timeout);
		return timeoutSource;
		}

	private static async Task<byte[]> ReadBytesAsync (HttpResponseMessage response, CancellationToken cancellationToken)
		{
		#if NET10_0_OR_GREATER
		return await response.Content.ReadAsByteArrayAsync (cancellationToken).ConfigureAwait (false);
		#else
		#pragma warning disable CA2016
		return await response.Content.ReadAsByteArrayAsync ().ConfigureAwait (false);
		#pragma warning restore CA2016
		#endif
		}

	private static async Task<string> ReadStringAsync (HttpResponseMessage response, CancellationToken cancellationToken)
		{
		#if NET10_0_OR_GREATER
		return await response.Content.ReadAsStringAsync (cancellationToken).ConfigureAwait (false);
		#else
		#pragma warning disable CA2016
		return await response.Content.ReadAsStringAsync ().ConfigureAwait (false);
		#pragma warning restore CA2016
		#endif
		}

	private Uri BuildAppUri (int? tlsMode, int? port)
		{
		string scheme = tlsMode is 1 or 2 ? Uri.UriSchemeHttps : Uri.UriSchemeHttp;
		int resolvedPort = port is > 0
			? port.Value
			: scheme == Uri.UriSchemeHttps
				? DEFAULT_HTTPS_PORT
				: _configuration.Port > 0
					? _configuration.Port
					: DEFAULT_HTTP_PORT;
		var builder = new UriBuilder
			{
			Scheme = scheme,
			Host = _configuration.Host,
			Port = resolvedPort,
			Path = "/",
			};
		return builder.Uri;
		}

	private static bool LooksLikeJson (byte[] payload)
		{
		for (int index = 0; index < payload.Length; index++)
			{
			byte value = payload[index];
			if (value is (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n')
				{
				continue;
				}

			return value is (byte)'{' or (byte)'[';
			}

		return false;
		}

	private static TimeSpan? ResolveKeepAliveInterval (TimeSpan? configuredInterval)
		{
		if (configuredInterval == TimeSpan.Zero)
			{
			return null;
			}

		return configuredInterval ?? DEFAULT_KEEPALIVE_INTERVAL;
		}

	private void RecordActivity () => _lastActivityUtc = DateTimeOffset.UtcNow;

	private async Task SendKeepAliveIfNeededAsync (CancellationToken cancellationToken)
		{
		if (_keepAliveInterval is not TimeSpan keepAliveInterval
			|| keepAliveInterval <= TimeSpan.Zero
			|| !IsEstablished ()
			|| _keepAliveInProgress
			|| _lastActivityUtc is not DateTimeOffset lastActivityUtc)
			{
			return;
			}

		if (DateTimeOffset.UtcNow - lastActivityUtc < keepAliveInterval)
			{
			return;
			}

		_keepAliveInProgress = true;
		try
			{
			await SendKeepAliveCoreAsync (cancellationToken).ConfigureAwait (false);
			}
		finally
			{
			_keepAliveInProgress = false;
			}
		}

	private async Task SendKeepAliveCoreAsync (CancellationToken cancellationToken)
		{
		SemaphoreSlim sendLock;
		lock (_sendLockOwner)
			{
			sendLock = _sendLock ??= new SemaphoreSlim (1, 1);
			}

		await sendLock.WaitAsync (cancellationToken).ConfigureAwait (false);
		try
			{
			if (!IsEstablished () || _lastActivityUtc is not DateTimeOffset lastActivityUtc)
				{
				return;
				}

			if (DateTimeOffset.UtcNow - lastActivityUtc < _keepAliveInterval)
				{
				return;
				}

			(Uri dsUri, byte[] key, byte[] baseNonce, string cipherId, int sequence) = RequireEstablishedSession ();
			byte[] plaintext = Encoding.UTF8.GetBytes (KasaCommands.CreateSmartRequest (KasaCommands.SMART_GET_DEVICE_INFO_METHOD));
			byte[] encrypted = EncryptPayload (cipherId, key, baseNonce, plaintext, sequence);
			byte[] requestPayload = Combine (GetBigEndian (sequence), encrypted);
			_sequence = sequence + 1;

			using CancellationTokenSource? timeoutSource = CreateOperationTimeoutSource (_configuration.Timeout, cancellationToken);
			CancellationToken operationCancellationToken = timeoutSource?.Token ?? cancellationToken;

			using var request = new HttpRequestMessage (HttpMethod.Post, dsUri)
				{
				Content = new ByteArrayContent (requestPayload),
				};
			request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue ("application/octet-stream");

			byte[] body;
			try
				{
				using HttpResponseMessage response = await SendHttpAsync (request, operationCancellationToken).ConfigureAwait (false);
				if ((int)response.StatusCode != 200)
					{
					throw new InvalidOperationException ($"TPAP keepalive failed for '{_configuration.Host}': status {(int)response.StatusCode}.");
					}

				body = await ReadBytesAsync (response, operationCancellationToken).ConfigureAwait (false);
				}
			catch (Exception ex) when (ex is not OperationCanceledException && operationCancellationToken.IsCancellationRequested)
				{
				throw ToCancellationException (ex, operationCancellationToken);
				}

			if (LooksLikeJson (body))
				{
				JsonObject root = JsonSupport.ParseObject (Encoding.UTF8.GetString (body));
				HandleResponseErrorCode (root, "keepalive");
				}
			else
				{
				string responseJson = Encoding.UTF8.GetString (DecryptPayloadEnvelope (cipherId, key, baseNonce, body, sequence));
				JsonSupport.ParseObject (responseJson);
				}

			RecordActivity ();
			}
		catch (Exception ex) when (!cancellationToken.IsCancellationRequested && ShouldRetryLiveSession (ex))
			{
			Reset ();
			}
		finally
			{
			sendLock.Release ();
			}
		}

	#pragma warning disable CA2249
	internal static bool ShouldRetryLiveSession (Exception exception)
		{
		// Note: TaskCanceledException/OperationCanceledException are deliberately NOT treated as
		// retryable here. SendOnceAsync/SendKeepAliveCoreAsync race the request against an internal
		// per-request timeout (CreateOperationTimeoutSource) that is separate from the caller's outer
		// CancellationToken. If that internal timeout fires first, retrying here would wipe the session
		// and perform a full, non-cancellable PAKE handshake (PBKDF2/EC math checks no CancellationToken)
		// before the caller's own deadline is ever observed again, potentially blocking far longer than
		// the caller requested. A timeout or cancellation should propagate immediately instead.
		if (exception is IOException ioException)
			{
			string ioMessage = ioException.Message;
			if (!string.IsNullOrEmpty (ioMessage)
				&& (ioMessage.IndexOf ("transport connection", StringComparison.OrdinalIgnoreCase) >= 0
					|| ioMessage.IndexOf ("operation has been aborted", StringComparison.OrdinalIgnoreCase) >= 0))
				{
				return true;
				}
			}

		#pragma warning disable CA2249
		if (exception is HttpRequestException requestException)
			{
			string message = requestException.Message;
			if (!string.IsNullOrEmpty (message)
				&& (message.IndexOf ("Connection reset", StringComparison.OrdinalIgnoreCase) >= 0
					|| message.IndexOf ("operation has been aborted", StringComparison.OrdinalIgnoreCase) >= 0))
				{
				return true;
				}
			}

		return exception is TpapProtocolException protocolException
			&& protocolException.ErrorCode is ERROR_CODE_SESSION_TIMEOUT
				or ERROR_CODE_SESSION_EXPIRED
				or ERROR_CODE_INVALID_NONCE
				or ERROR_CODE_TRANSPORT_NOT_AVAILABLE;
		#pragma warning restore CA2249
		}
	#pragma warning restore CA2249

	private void HandleResponseErrorCode (JsonObject response, string action)
		{
		int errorCode = GetOptionalInt (response["error_code"]) ?? ERROR_CODE_UNKNOWN;
		if (errorCode == ERROR_CODE_SUCCESS)
			{
			return;
			}

		string message = $"TPAP {action} failed for '{_configuration.Host}': {errorCode}.";
		bool retryable = errorCode is ERROR_CODE_SESSION_TIMEOUT
			or ERROR_CODE_TRANSPORT_NOT_AVAILABLE
			or ERROR_CODE_SESSION_EXPIRED
			or ERROR_CODE_INVALID_NONCE;
		bool authentication = errorCode == ERROR_CODE_LOGIN;
		if (authentication)
			{
			InvalidateSession ();
			}

		throw new TpapProtocolException (message, errorCode, retryable, authentication);
		}

	private static JsonObject RequireResultObject (JsonObject response)
		{
		if (response["result"] is not JsonObject result)
			{
			throw new InvalidDataException ("The TPAP response did not contain a result object.");
			}

		return result;
		}

	private string? GetPasscodeType ()
		{
		if (_tpapPake.Contains (0))
			{
			return "default_userpw";
			}

		if (_tpapPake.Contains (3))
			{
			return "shared_token";
			}

		if (_tpapPake.Contains (1) || _tpapPake.Contains (2) || _tpapPake.Contains (5))
			{
			return "userpw";
			}

		return UsesCameraAuth () ? null : "default_userpw";
		}

	private List<string> GetCandidateSecrets (string passcodeType)
		{
		if (string.Equals (passcodeType, "default_userpw", StringComparison.Ordinal))
			{
			return string.IsNullOrWhiteSpace (_deviceMac)
				? new List<string> ()
				: new List<string> { MacPassFromDeviceMac (_deviceMac) };
			}

		DeviceCredentials credentials = ResolveCredentials ();
		string password = credentials.Password ?? string.Empty;
		if (!UsesCameraAuth ())
			{
			return new List<string> { password };
			}

		if (string.Equals (passcodeType, "shared_token", StringComparison.Ordinal))
			{
			return new List<string> { ComputeMd5Hex (password) };
			}

		if (!_tpapPake.Contains (2))
			{
			return new List<string> { password };
			}

		return new List<string>
			{
			ComputeMd5Hex (password),
			ComputeSha256HexUpper (password),
			}
			.Distinct (StringComparer.Ordinal)
			.ToList ();
		}

	private DeviceCredentials ResolveCredentials ()
		{
		DeviceCredentials? credentials = _configuration.Credentials;
		if (!string.IsNullOrWhiteSpace (credentials?.UserName) || !string.IsNullOrWhiteSpace (credentials?.Password))
			{
			return new DeviceCredentials (credentials?.UserName, credentials?.Password);
			}

		if (_configuration.ConnectionOptions.UseDefaultCredentials)
			{
			DefaultCredentialProfile profile = _configuration.ConnectionOptions.DefaultCredentialProfile == DefaultCredentialProfile.None
				? DefaultCredentialProfile.Tapo
				: _configuration.ConnectionOptions.DefaultCredentialProfile;
			return DeviceCredentials.FromDefault (profile);
			}

		return new DeviceCredentials (string.Empty, string.Empty);
		}

	private string ResolveCredentialsString (JsonObject registerResult, string candidateSecret, string passcodeType)
		{
		if (string.Equals (passcodeType, "default_userpw", StringComparison.Ordinal))
			{
			return candidateSecret;
			}

		JsonObject? extraCrypt = registerResult["extra_crypt"] as JsonObject;
		if (UsesCameraAuth () && extraCrypt is null)
			{
			return candidateSecret;
			}

		DeviceCredentials credentials = ResolveCredentials ();
		string userName = UsesCameraAuth () ? string.Empty : credentials.UserName ?? string.Empty;
		string macNoColon = _deviceMac.Replace (":", string.Empty).Replace ("-", string.Empty);
		return BuildCredentials (extraCrypt, userName, candidateSecret, macNoColon);
		}

	private string GetRegisterUserName () => _tpapUserHashType == 1
		? ComputeSha256HexUpper ("admin")
		: ComputeMd5Hex ("admin");

	private bool UsesCameraAuth () => CAMERA_AUTH_DEVICE_FAMILIES.Contains (_configuration.ConnectionOptions.ConnectionParameters?.DeviceFamily ?? DeviceFamilyKind.Unknown);

	private bool UseDacCertification () => _tpapTls == 0 && _tpapDac;

	private static string BuildCredentials (JsonObject? extraCrypt, string userName, string passcode, string macNoColon)
		{
		if (extraCrypt is null)
			{
			return string.IsNullOrWhiteSpace (userName) ? passcode : userName + "/" + passcode;
			}

		string cryptType = extraCrypt["type"]?.GetValue<string?> ()?.ToLowerInvariant () ?? string.Empty;
		JsonObject paramsObject = extraCrypt["params"] as JsonObject ?? new JsonObject ();
		if (cryptType == "password_shadow")
			{
			int passwdId = GetOptionalInt (paramsObject["passwd_id"]) ?? 0;
			if (passwdId == 2)
				{
				return ComputeSha1Hex (passcode);
				}

			if (passwdId == 3)
				{
				return ComputeSha1UserNameMacShadow (userName, macNoColon, passcode);
				}

			return passcode;
			}

		if (cryptType == "password_authkey")
			{
			string tmpKey = paramsObject["authkey_tmpkey"]?.GetValue<string?> () ?? string.Empty;
			string dictionary = paramsObject["authkey_dictionary"]?.GetValue<string?> () ?? string.Empty;
			return !string.IsNullOrWhiteSpace (tmpKey) && !string.IsNullOrWhiteSpace (dictionary)
				? ApplyAuthKeyMask (passcode, tmpKey, dictionary)
				: passcode;
			}

		if (cryptType == "password_sha_with_salt")
			{
			int? shaName = GetOptionalInt (paramsObject["sha_name"]);
			string shaSaltBase64 = paramsObject["sha_salt"]?.GetValue<string?> () ?? string.Empty;
			if (shaName is null || string.IsNullOrWhiteSpace (shaSaltBase64))
				{
				return passcode;
				}

			try
				{
				string decodedSalt = Encoding.UTF8.GetString (Convert.FromBase64String (shaSaltBase64));
				string userNameHint = shaName == 0 ? "admin" : "user";
				return ComputeSha256Hex (userNameHint + decodedSalt + passcode);
				}
			catch (FormatException)
				{
				return passcode;
				}
			}

		return string.IsNullOrWhiteSpace (userName) ? passcode : userName + "/" + passcode;
		}

	#pragma warning disable CA1846
	private static string ComputeSha1UserNameMacShadow (string userName, string macNoColon, string password)
		{
		if (string.IsNullOrWhiteSpace (userName) || macNoColon.Length != 12 || !macNoColon.All (IsHexChar))
			{
			return password;
			}

		string mac = string.Join (":", Enumerable.Range (0, 6).Select (index => macNoColon.AsSpan (index * 2, 2).ToString ())).ToUpperInvariant ();
		return ComputeSha1Hex (ComputeMd5Hex (userName) + "_" + mac);
		}
	#pragma warning restore CA1846

	private static string ApplyAuthKeyMask (string passcode, string tmpKey, string dictionary)
		{
		var builder = new StringBuilder (Math.Max (passcode.Length, tmpKey.Length));
		int maxLength = Math.Max (passcode.Length, tmpKey.Length);
		for (int index = 0; index < maxLength; index++)
			{
			int left = index < passcode.Length ? passcode[index] : 0xBB;
			int right = index < tmpKey.Length ? tmpKey[index] : 0xBB;
			builder.Append (dictionary[(left ^ right) % dictionary.Length]);
			}

		return builder.ToString ();
		}

	private static string MacPassFromDeviceMac (string mac)
		{
		string macHex = mac.Replace (":", string.Empty).Replace ("-", string.Empty);
		if (macHex.Length < 12)
			{
			throw new InvalidDataException ("The device MAC address is too short for TPAP default passcode derivation.");
			}

		byte[] macBytes = HexToBytes (macHex);
		byte[] seed = Encoding.ASCII.GetBytes ("GqY5o136oa4i6VprTlMW2DpVXxmfW8");
		byte[] ikm = Combine (seed, macBytes.Skip (3).Take (3).ToArray (), macBytes.Take (3).ToArray ());
		byte[] passcode = Hkdf (ikm, Encoding.ASCII.GetBytes ("tp-kdf-salt-default-passcode"), Encoding.ASCII.GetBytes ("tp-kdf-info-default-passcode"), 32, "SHA256");
		return BytesToHex (passcode).ToUpperInvariant ();
		}

	private static (byte[] Left, byte[] Right) DeriveAb (byte[] credentials, byte[] salt, int iterations, int hashLength)
		{
		int length = 2 * (hashLength + 8);
		byte[] derived = Pbkdf2Sha256 (credentials, salt, iterations, length);
		int sliceLength = hashLength + 8;
		return (derived.Take (sliceLength).ToArray (), derived.Skip (sliceLength).Take (sliceLength).ToArray ());
		}

	private static CipherSuiteParameters GetCipherSuiteParameters (int suiteType)
		{
		if (suiteType is 1 or 2 or 8 or 9)
			{
			return new CipherSuiteParameters (
				HexToBytes ("02886e2f97ace46e55ba9dd7242579f2993b64e16ef3dcab95afd497333d8fa12f"),
				HexToBytes ("03d8bbd6c639c62937b04d997f38c3770719c629d7014d49a24b4f98baa1292b49"),
				RequireCurveParameters ("secp256r1"),
				suiteType is 8 or 9);
			}

		if (suiteType is 3 or 4)
			{
			return new CipherSuiteParameters (
				HexToBytes ("030ff0895ae5ebf6187080a82d82b42e2765e3b2f8749c7e05eba366434b363d3dc36f15314739074d2eb8613fceec2853"),
				HexToBytes ("02c72cf2e390853a1c1c4ad816a62fd15824f56078918f43f922ca21518f9c543bb252c5490214cf9aa3f0baab4b665c10"),
				RequireCurveParameters ("secp384r1"),
				false);
			}

		if (suiteType == 5)
			{
			return new CipherSuiteParameters (
				HexToBytes ("02003f06f38131b2ba2600791e82488e8d20ab889af753a41806c5db18d37d85608cfae06b82e4a72cd744c719193562a653ea1f119eef9356907edc9b56979962d7aa"),
				HexToBytes ("0200c7924b9ec017f3094562894336a53c50167ba8c5963876880542bc669e494b2532d76c5b53dfb349fdf69154b9e0048c58a42e8ed04cef052a3bc349d95575cd25"),
				RequireCurveParameters ("secp521r1"),
				false);
			}

		throw new NotSupportedException ($"Unsupported TPAP suite type '{suiteType}'.");
		}

	private static X9ECParameters RequireCurveParameters (string curveName) => SecNamedCurves.GetByName (curveName)
		?? throw new InvalidOperationException ($"The elliptic curve '{curveName}' is not available.");

	private static string SuiteHashName (int suiteType) => suiteType is 2 or 4 or 5 or 7 or 9 ? "SHA512" : "SHA256";

	private static string NormalizeCipherId (string cipherId) => cipherId.ToLowerInvariant ().Replace ('-', '_');

	private static (CipherParameters Parameters, byte[] Key, byte[] BaseNonce) DeriveSessionKeyMaterial (byte[] sharedKey, string cipherId, string hkdfHash)
		{
		string normalized = NormalizeCipherId (cipherId);
		if (!CIPHER_PARAMETERS.TryGetValue (normalized, out CipherParameters? parameters))
			{
			throw new NotSupportedException ($"Unsupported TPAP session cipher '{cipherId}'.");
			}

		byte[] key = Hkdf (sharedKey, parameters.KeySalt, parameters.KeyInfo, parameters.KeyLength, hkdfHash);
		byte[] baseNonce = Hkdf (sharedKey, parameters.NonceSalt, parameters.NonceInfo, NONCE_LENGTH_BYTES, hkdfHash);
		return (parameters, key, baseNonce);
		}

	private static byte[] EncryptPayload (string cipherId, byte[] key, byte[] baseNonce, byte[] plaintext, int sequence)
		{
		string normalized = NormalizeCipherId (cipherId);
		if (normalized != "aes_128_ccm" && normalized != "aes_256_ccm")
			{
			throw new NotSupportedException ($"Unsupported TPAP session cipher '{cipherId}'.");
			}

		byte[] nonce = NonceFromBase (baseNonce, sequence);
		var cipher = new CcmBlockCipher (new AesEngine ());
		cipher.Init (true, new AeadParameters (new KeyParameter (key), TAG_LENGTH_BYTES * 8, nonce));
		var output = new byte[cipher.GetOutputSize (plaintext.Length)];
		int written = cipher.ProcessBytes (plaintext, 0, plaintext.Length, output, 0);
		written += cipher.DoFinal (output, written);
		return written == output.Length ? output : output.Take (written).ToArray ();
		}

	private static byte[] DecryptPayloadEnvelope (string cipherId, byte[] key, byte[] baseNonce, byte[] payload, int requestSequence)
		{
		if (payload.Length < 4 + TAG_LENGTH_BYTES)
			{
			throw new InvalidDataException ("The TPAP response payload was too short.");
			}

		int responseSequence = ReadBigEndian (payload, 0);
		byte[] encrypted = payload.Skip (4).ToArray ();
		string normalized = NormalizeCipherId (cipherId);
		if (normalized != "aes_128_ccm" && normalized != "aes_256_ccm")
			{
			throw new NotSupportedException ($"Unsupported TPAP session cipher '{cipherId}'.");
			}

		byte[] nonce = NonceFromBase (baseNonce, responseSequence == 0 ? requestSequence : responseSequence);
		var cipher = new CcmBlockCipher (new AesEngine ());
		cipher.Init (false, new AeadParameters (new KeyParameter (key), TAG_LENGTH_BYTES * 8, nonce));
		var output = new byte[cipher.GetOutputSize (encrypted.Length)];
		int written = cipher.ProcessBytes (encrypted, 0, encrypted.Length, output, 0);
		written += cipher.DoFinal (output, written);
		return written == output.Length ? output : output.Take (written).ToArray ();
		}

	private static byte[] NonceFromBase (byte[] baseNonce, int sequence)
		{
		if (baseNonce.Length < 4)
			{
			throw new InvalidDataException ("The TPAP base nonce was too short.");
			}

		byte[] nonce = new byte[baseNonce.Length];
		Buffer.BlockCopy (baseNonce, 0, nonce, 0, baseNonce.Length - 4);
		Buffer.BlockCopy (GetBigEndian (sequence), 0, nonce, baseNonce.Length - 4, 4);
		return nonce;
		}

	private static byte[] ComputeHash (string algorithm, byte[] data)
		{
		if (string.Equals (algorithm, "SHA512", StringComparison.OrdinalIgnoreCase))
			{
			#if NET10_0_OR_GREATER
			return SHA512.HashData (data);
			#else
			using SHA512 sha512 = SHA512.Create ();
			return sha512.ComputeHash (data);
			#endif
			}

		#if NET10_0_OR_GREATER
		return SHA256.HashData (data);
		#else
		using SHA256 sha256 = SHA256.Create ();
		return sha256.ComputeHash (data);
		#endif
		}

	private static byte[] ComputeHmac (string algorithm, byte[] key, byte[] data)
		{
		if (string.Equals (algorithm, "SHA512", StringComparison.OrdinalIgnoreCase))
			{
			using var hmac = new HMACSHA512 (key);
			return hmac.ComputeHash (data);
			}

		using var hmacSha256 = new HMACSHA256 (key);
		return hmacSha256.ComputeHash (data);
		}

	private static byte[] ComputeCmacAes (byte[] key, byte[] data)
		{
		var cmac = new CMac (new AesEngine ());
		cmac.Init (new KeyParameter (key));
		cmac.BlockUpdate (data, 0, data.Length);
		var output = new byte[cmac.GetMacSize ()];
		cmac.DoFinal (output, 0);
		return output;
		}

	private static byte[] HkdfExpand (string label, byte[] pseudoRandomKey, int length, string algorithm)
		{
		int digestLength = string.Equals (algorithm, "SHA512", StringComparison.OrdinalIgnoreCase) ? 64 : 32;
		return Hkdf (pseudoRandomKey, new byte[digestLength], Encoding.ASCII.GetBytes (label), length, algorithm);
		}

	private static byte[] Hkdf (byte[] inputKeyMaterial, byte[] salt, byte[] info, int length, string algorithm)
		{
		int digestLength = string.Equals (algorithm, "SHA512", StringComparison.OrdinalIgnoreCase) ? 64 : 32;
		byte[] effectiveSalt = salt.Length == 0 ? new byte[digestLength] : salt;
		byte[] pseudoRandomKey = ComputeHmac (algorithm, effectiveSalt, inputKeyMaterial);
		var output = new byte[length];
		byte[] block = Array.Empty<byte> ();
		int offset = 0;
		byte counter = 1;
		while (offset < length)
			{
			block = ComputeHmac (algorithm, pseudoRandomKey, Combine (block, info, new byte[] { counter }));
			int toCopy = Math.Min (block.Length, length - offset);
			Buffer.BlockCopy (block, 0, output, offset, toCopy);
			offset += toCopy;
			counter++;
			}

		return output;
		}

	private static byte[] Pbkdf2Sha256 (byte[] password, byte[] salt, int iterations, int length)
		{
		if (iterations <= 0)
			{
			throw new ArgumentOutOfRangeException (nameof (iterations), iterations, "PBKDF2 iterations must be positive.");
			}

		int hashLength = 32;
		int blockCount = (int)Math.Ceiling (length / (double)hashLength);
		var output = new byte[length];
		int destinationOffset = 0;
		using var hmac = new HMACSHA256 (password);
		for (int blockIndex = 1; blockIndex <= blockCount; blockIndex++)
			{
			byte[] blockInput = Combine (salt, GetBigEndian (blockIndex));
			byte[] u = hmac.ComputeHash (blockInput);
			byte[] t = (byte[])u.Clone ();
			for (int iteration = 1; iteration < iterations; iteration++)
				{
				u = hmac.ComputeHash (u);
				for (int index = 0; index < t.Length; index++)
					{
					t[index] ^= u[index];
					}
				}

			int toCopy = Math.Min (t.Length, length - destinationOffset);
			Buffer.BlockCopy (t, 0, output, destinationOffset, toCopy);
			destinationOffset += toCopy;
			}

		return output;
		}

	private static byte[] EncodeLen8Le (byte[] value) => Combine (BitConverter.GetBytes ((long)value.Length), value);

	private static byte[] EncodeW (BigInteger value)
		{
		byte[] unsigned = value.SignValue == 0 ? new byte[] { 0 } : value.ToByteArrayUnsigned ();
		if (unsigned.Length % 2 == 0)
			{
			return unsigned;
			}

		if ((unsigned[0] & 0x80) != 0)
			{
			return Combine (new byte[] { 0 }, unsigned);
			}

		return unsigned;
		}

	private static bool IsHexChar (char value) => value is >= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F';

	private static byte[] HexToBytes (string hex)
		{
		#pragma warning disable CA1846
		if (hex.Length % 2 != 0)
			{
			throw new FormatException ("Hex strings must contain an even number of characters.");
			}

		var output = new byte[hex.Length / 2];
		for (int index = 0; index < output.Length; index++)
			{
			output[index] = byte.Parse (hex.Substring (index * 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
			}

		#pragma warning restore CA1846
		return output;
		}

	private static string BytesToHex (byte[] bytes)
		{
		var builder = new StringBuilder (bytes.Length * 2);
		foreach (byte value in bytes)
			{
			builder.Append (value.ToString ("x2", CultureInfo.InvariantCulture));
			}

		return builder.ToString ();
		}

	private static byte[] GetBigEndian (int value)
		{
		byte[] bytes = BitConverter.GetBytes (value);
		if (BitConverter.IsLittleEndian)
			{
			Array.Reverse (bytes);
			}

		return bytes;
		}

	private static int ReadBigEndian (byte[] bytes, int offset)
		{
		var buffer = new byte[4];
		Buffer.BlockCopy (bytes, offset, buffer, 0, 4);
		if (BitConverter.IsLittleEndian)
			{
			Array.Reverse (buffer);
			}

		return BitConverter.ToInt32 (buffer, 0);
		}

	private static byte[] Combine (params byte[][] arrays)
		{
		int totalLength = arrays.Sum (array => array.Length);
		var output = new byte[totalLength];
		int offset = 0;
		foreach (byte[] array in arrays)
			{
			Buffer.BlockCopy (array, 0, output, offset, array.Length);
			offset += array.Length;
			}

		return output;
		}

	private static byte[] CreateRandomBytes (int length)
		{
		var bytes = new byte[length];
		RANDOM.NextBytes (bytes);
		return bytes;
		}

	private static int? GetOptionalInt (JsonNode? node)
		{
		if (node is null)
			{
			return null;
			}

		if (node is JsonValue jsonValue)
			{
			if (jsonValue.TryGetValue<int> (out int intValue))
				{
				return intValue;
				}

			if (jsonValue.TryGetValue<string> (out string? stringValue)
				&& int.TryParse (stringValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed))
				{
				return parsed;
				}
			}

		return null;
		}

	private static int GetRequiredInt (JsonObject source, string propertyName) => GetOptionalInt (source[propertyName])
		?? throw new InvalidDataException ($"The TPAP response field '{propertyName}' was missing or invalid.");

	private static List<int> ReadIntArray (JsonNode? node)
		{
		if (node is not JsonArray array)
			{
			return new List<int> ();
			}

		var values = new List<int> (array.Count);
		foreach (JsonNode? item in array)
			{
			int? value = GetOptionalInt (item);
			if (value is not null)
				{
				values.Add (value.Value);
				}
			}

		return values;
		}

	#pragma warning disable CA5351
	private static string ComputeMd5Hex (string value)
		{
#if NET10_0_OR_GREATER
	return BytesToHex (MD5.HashData (Encoding.UTF8.GetBytes (value)));
#else
	using MD5 md5 = MD5.Create ();
	return BytesToHex (md5.ComputeHash (Encoding.UTF8.GetBytes (value)));
#endif
		}
	#pragma warning restore CA5351

	#pragma warning disable CA5350
	private static string ComputeSha1Hex (string value)
		{
#if NET10_0_OR_GREATER
	return BytesToHex (SHA1.HashData (Encoding.UTF8.GetBytes (value)));
#else
	using SHA1 sha1 = SHA1.Create ();
	return BytesToHex (sha1.ComputeHash (Encoding.UTF8.GetBytes (value)));
#endif
		}
	#pragma warning restore CA5350

	private static string ComputeSha256Hex (string value)
		{
		#if NET10_0_OR_GREATER
		return BytesToHex (SHA256.HashData (Encoding.UTF8.GetBytes (value)));
		#else
		using SHA256 sha256 = SHA256.Create ();
		return BytesToHex (sha256.ComputeHash (Encoding.UTF8.GetBytes (value)));
		#endif
		}

	private static string ComputeSha256HexUpper (string value) => ComputeSha256Hex (value).ToUpperInvariant ();

	private sealed class CipherParameters
		{
		public CipherParameters (byte[] keySalt, byte[] keyInfo, byte[] nonceSalt, byte[] nonceInfo, int keyLength)
			{
			KeySalt = keySalt;
			KeyInfo = keyInfo;
			NonceSalt = nonceSalt;
			NonceInfo = nonceInfo;
			KeyLength = keyLength;
			}

		public byte[] KeySalt
			{
			get;
			}

		public byte[] KeyInfo
			{
			get;
			}

		public byte[] NonceSalt
			{
			get;
			}

		public byte[] NonceInfo
			{
			get;
			}

		public int KeyLength
			{
			get;
			}
		}

	private sealed class CipherSuiteParameters
		{
		public CipherSuiteParameters (byte[] mPointEncoded, byte[] nPointEncoded, X9ECParameters curveParameters, bool useCmac)
			{
			MPointEncoded = mPointEncoded;
			NPointEncoded = nPointEncoded;
			CurveParameters = curveParameters;
			UseCmac = useCmac;
			}

		public byte[] MPointEncoded
			{
			get;
			}

		public byte[] NPointEncoded
			{
			get;
			}

		public X9ECParameters CurveParameters
			{
			get;
			}

		public bool UseCmac
			{
			get;
			}
		}

	internal sealed class TpapProtocolException : InvalidOperationException
		{
		internal TpapProtocolException (string message, int errorCode, bool retryable, bool authentication)
			: base (message)
			{
			ErrorCode = errorCode;
			Retryable = retryable;
			Authentication = authentication;
			}

		public int ErrorCode
			{
			get;
			}

		public bool Retryable
			{
			get;
			}

		public bool Authentication
			{
			get;
			}
		}
	}
