// Copyright © 2026 Neil Colvin.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// Adapted from python-kasa (https://github.com/python-kasa/python-kasa)
// Original work Copyright (c) python-kasa contributors, MIT License

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace KasaTapoClient.Internal;

internal sealed class KlapTransport : IDisposableDeviceTransport
	{
	private const int DEFAULT_HTTP_PORT = 80;
	private const int DEFAULT_HTTPS_PORT = 4433;
	private const int SESSION_EXPIRE_BUFFER_SECONDS = 20 * 60;
	private const string SESSION_COOKIE_NAME = "TP_SESSIONID";
	private const string TIMEOUT_COOKIE_NAME = "TIMEOUT";
	private readonly DeviceConfiguration _configuration;
	private readonly Uri _appUri;
	private readonly Uri _requestUri;
	private readonly CookieContainer _cookies = new ();
	private static readonly HttpClient HTTP_CLIENT = CreateHttpClient ();
	private readonly object _handshakeLockOwner = new ();
	private SemaphoreSlim? _handshakeLock;
	private KlapEncryptionSession? _session;
	private DateTimeOffset? _sessionExpiresAt;
	private string? _sessionCookieValue;

	public KlapTransport (DeviceConfiguration configuration)
		{
		_configuration = configuration;
		_appUri = CreateApplicationUri (configuration);
		_requestUri = new Uri (_appUri, "request");
		}

	private static HttpClient CreateHttpClient ()
		{
		var handler = new HttpClientHandler
			{
			AllowAutoRedirect = false,
			UseCookies = false,
			};
		return new HttpClient (handler);
		}

	public async Task<string> SendAsync (string commandJson, CancellationToken cancellationToken)
		{
		await EnsureHandshakeAsync (cancellationToken).ConfigureAwait (false);
		return await SendEncryptedAsync (commandJson, cancellationToken).ConfigureAwait (false);
		}

	public async Task<string> SendManyAsync (IReadOnlyList<string> commandJsonPayloads, CancellationToken cancellationToken)
		{
		if (commandJsonPayloads.Count == 0)
			{
			throw new ArgumentException ("At least one command payload is required.", nameof (commandJsonPayloads));
			}

		await EnsureHandshakeAsync (cancellationToken).ConfigureAwait (false);
		var mergedResponse = new JsonObject ();
		foreach (string payload in commandJsonPayloads)
			{
				string responseJson = await SendEncryptedAsync (payload, cancellationToken).ConfigureAwait (false);
				JsonSupport.MergeObjects (mergedResponse, JsonSupport.ParseObject (responseJson));
			}

		return mergedResponse.ToJsonString (JsonSupport.COMPACT_JSON);
		}

	public void Dispose ()
		{
		// HTTP_CLIENT is shared/static across all KlapTransport instances and must not be disposed here.
		}

	private async Task EnsureHandshakeAsync (CancellationToken cancellationToken)
		{
		if (_session is not null && !IsSessionExpired ())
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
			if (_session is not null && !IsSessionExpired ())
				{
				return;
				}

			await PerformHandshakeAsync (cancellationToken).ConfigureAwait (false);
			}
		finally
			{
			handshakeLock.Release ();
			}
		}

	private async Task PerformHandshakeAsync (CancellationToken cancellationToken)
		{
		_session = null;
		_sessionExpiresAt = null;
		_sessionCookieValue = null;
		byte[] localSeed = CreateSeed ();
		(byte[] remoteSeed, byte[] serverHash, int timeoutSeconds) = await PerformHandshake1Async (localSeed, cancellationToken).ConfigureAwait (false);
		(byte[] authHash, bool v2) = ResolveHandshakeAuthHash (localSeed, remoteSeed, serverHash);
		await PerformHandshake2Async (localSeed, remoteSeed, authHash, v2, cancellationToken).ConfigureAwait (false);

		_session = new KlapEncryptionSession (localSeed, remoteSeed, authHash);
		_sessionExpiresAt = DateTimeOffset.UtcNow.AddSeconds (timeoutSeconds <= SESSION_EXPIRE_BUFFER_SECONDS ? timeoutSeconds : timeoutSeconds - SESSION_EXPIRE_BUFFER_SECONDS);
		}

	private async Task<(byte[] RemoteSeed, byte[] ServerHash, int TimeoutSeconds)> PerformHandshake1Async (byte[] localSeed, CancellationToken cancellationToken)
		{
		Uri handshakeUri = new (_appUri, "handshake1");
		using HttpResponseMessage response = await PostBytesAsync (handshakeUri, localSeed, cancellationToken).ConfigureAwait (false);
		if (response.StatusCode != HttpStatusCode.OK)
			{
			throw new InvalidOperationException ($"Device '{_configuration.Host}' responded with status {(int)response.StatusCode} to KLAP handshake1.");
			}
		byte[] payload = await ReadBytesAsync (response, cancellationToken).ConfigureAwait (false);
		if (payload.Length != 48)
			{
			throw new InvalidDataException ($"The KLAP handshake1 response from '{_configuration.Host}' was {payload.Length} bytes instead of 48 bytes.");
			}

		CaptureCookies (handshakeUri, response);
		byte[] remoteSeed = new byte[16];
		byte[] serverHash = new byte[32];
		Buffer.BlockCopy (payload, 0, remoteSeed, 0, remoteSeed.Length);
		Buffer.BlockCopy (payload, 16, serverHash, 0, serverHash.Length);
		int timeoutSeconds = ReadTimeoutSeconds (response);
		return (remoteSeed, serverHash, timeoutSeconds);
		}

	private async Task PerformHandshake2Async (byte[] localSeed, byte[] remoteSeed, byte[] authHash, bool v2, CancellationToken cancellationToken)
		{
		Uri handshakeUri = new (_appUri, "handshake2");
		byte[] payload = v2
			? KlapAuthHash.CreateHandshake2HashV2 (localSeed, remoteSeed, authHash)
			: KlapAuthHash.CreateHandshake2HashV1 (localSeed, remoteSeed, authHash);
		using HttpResponseMessage response = await PostBytesAsync (handshakeUri, payload, cancellationToken).ConfigureAwait (false);
		CaptureCookies (handshakeUri, response);
		if (response.StatusCode != HttpStatusCode.OK)
			{
			throw new InvalidOperationException ($"Device '{_configuration.Host}' responded with status {(int)response.StatusCode} to KLAP handshake2.");
			}
		}

	private async Task<string> SendEncryptedAsync (string commandJson, CancellationToken cancellationToken)
		{
		KlapEncryptionSession session = _session ?? throw new InvalidOperationException ("The KLAP session is not initialized.");
		KlapEncryptedRequest encryptedRequest = session.Encrypt (commandJson);
		Uri requestUri = CreateRequestUri (encryptedRequest.Sequence);
		using HttpResponseMessage response = await PostBytesAsync (requestUri, encryptedRequest.Payload, cancellationToken).ConfigureAwait (false);
		CaptureCookies (_appUri, response);
		if (response.StatusCode == HttpStatusCode.Forbidden)
			{
			_session = null;
			_sessionExpiresAt = null;
			throw new UnauthorizedAccessException ($"The KLAP session for '{_configuration.Host}' was rejected and must be re-established.");
			}

		response.EnsureSuccessStatusCode ();
		byte[] payload = await ReadBytesAsync (response, cancellationToken).ConfigureAwait (false);
		return session.Decrypt (payload);
		}

	private Uri CreateRequestUri (int sequence)
		{
		var builder = new UriBuilder (_requestUri)
			{
			Query = "seq=" + sequence.ToString (System.Globalization.CultureInfo.InvariantCulture),
			};
		return builder.Uri;
		}

	private DeviceCredentials ResolveCredentials ()
		{
		DeviceCredentials? credentials = _configuration.Credentials;
		string? userName = credentials?.UserName;
		string? password = credentials?.Password;
		if (!string.IsNullOrWhiteSpace (userName) && !string.IsNullOrWhiteSpace (password))
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

		return new DeviceCredentials (string.Empty, string.Empty);
		}

	private static Uri CreateApplicationUri (DeviceConfiguration configuration)
		{
		int port = configuration.Port;
		if (port == 9999)
			{
			port = configuration.ConnectionOptions.UseSsl ? DEFAULT_HTTPS_PORT : DEFAULT_HTTP_PORT;
			}

		var builder = new UriBuilder
			{
			Scheme = configuration.ConnectionOptions.UseSsl ? Uri.UriSchemeHttps : Uri.UriSchemeHttp,
			Host = configuration.Host,
			Port = port,
			Path = "/app/",
			};
		return builder.Uri;
		}

	private async Task<HttpResponseMessage> PostBytesAsync (Uri uri, byte[] payload, CancellationToken cancellationToken)
		{
		#if NETFRAMEWORK
		return await PostBytesNetFrameworkAsync (uri, payload, cancellationToken).ConfigureAwait (false);
		#else
		using var request = new HttpRequestMessage (HttpMethod.Post, uri)
			{
			Version = HttpVersion.Version11,
			Content = new ByteArrayContent (payload),
			};
		string? cookieHeader = GetSessionCookieHeader ();
		if (!string.IsNullOrWhiteSpace (cookieHeader))
			{
			request.Headers.TryAddWithoutValidation ("Cookie", cookieHeader);
			}

		return await HTTP_CLIENT.SendAsync (request, HttpCompletionOption.ResponseContentRead, cancellationToken).ConfigureAwait (false);
		#endif
		}

	#if NETFRAMEWORK
	private async Task<HttpResponseMessage> PostBytesNetFrameworkAsync (Uri uri, byte[] payload, CancellationToken cancellationToken)
		{
		cancellationToken.ThrowIfCancellationRequested ();
		HttpWebRequest request = (HttpWebRequest)WebRequest.Create (uri);
		request.Method = "POST";
		request.ProtocolVersion = HttpVersion.Version11;
		request.AllowAutoRedirect = false;
		request.ContentType = "application/octet-stream";
		request.ContentLength = payload.Length;
		request.CookieContainer = _cookies;
		// KeepAlive must be false to avoid a documented Crestron/Mono HttpWebRequest leak
		// (a Timer/DelayPromise object that is never released when KeepAlive is true).
		request.KeepAlive = false;
		request.ServicePoint.Expect100Continue = false;

		if (!string.IsNullOrWhiteSpace (_sessionCookieValue))
			{
			request.CookieContainer.Add (uri, new Cookie (SESSION_COOKIE_NAME, _sessionCookieValue));
			}

		using (Stream requestStream = await request.GetRequestStreamAsync ().ConfigureAwait (false))
			{
				await requestStream.WriteAsync (payload, 0, payload.Length, cancellationToken).ConfigureAwait (false);
			}

		HttpWebResponse response;
		try
			{
				response = (HttpWebResponse)await request.GetResponseAsync ().ConfigureAwait (false);
			}
		catch (WebException ex) when (ex.Response is HttpWebResponse errorResponse)
			{
				response = errorResponse;
			}

		using (response)
			{
				byte[] responseBytes = await ReadResponseBytesAsync (response).ConfigureAwait (false);
				var message = new HttpResponseMessage (response.StatusCode)
					{
					Content = new ByteArrayContent (responseBytes),
					};

				foreach (string? headerName in response.Headers.AllKeys)
					{
						if (string.IsNullOrWhiteSpace (headerName))
							{
							continue;
							}

						string[]? headerValues = response.Headers.GetValues (headerName);
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

	private static async Task<byte[]> ReadResponseBytesAsync (HttpWebResponse response)
		{
		using Stream responseStream = response.GetResponseStream () ?? Stream.Null;
		using var memoryStream = new MemoryStream ();
		await responseStream.CopyToAsync (memoryStream).ConfigureAwait (false);
		return memoryStream.ToArray ();
		}
	#endif

	private static async Task<byte[]> ReadBytesAsync (HttpResponseMessage response, CancellationToken cancellationToken)
		{
#if NET10_0_OR_GREATER
		return await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
#else
#pragma warning disable CA2016
		return await response.Content.ReadAsByteArrayAsync ().ConfigureAwait (false);
#pragma warning restore CA2016
#endif
		}

	private void CaptureCookies (Uri uri, HttpResponseMessage response)
		{
		if (!response.Headers.TryGetValues ("Set-Cookie", out IEnumerable<string>? cookieValues))
			{
			return;
			}

		foreach (string cookieValue in cookieValues)
			{
				CaptureSessionCookieFromHeader (cookieValue);
			_cookies.SetCookies (uri, cookieValue);
			}

		CookieCollection cookies = _cookies.GetCookies (_appUri);
		Cookie? sessionCookie = cookies[SESSION_COOKIE_NAME] ?? cookies["SESSIONID"];
		if (sessionCookie is not null && !string.IsNullOrWhiteSpace (sessionCookie.Value))
			{
			_sessionCookieValue = sessionCookie.Value;
			}
		}

	private int ReadTimeoutSeconds (HttpResponseMessage response)
		{
		CookieCollection cookies = _cookies.GetCookies (_appUri);
		Cookie? timeoutCookie = cookies[TIMEOUT_COOKIE_NAME];
		if (timeoutCookie is not null && int.TryParse (timeoutCookie.Value, out int seconds) && seconds > 0)
			{
			return seconds;
			}

		return 24 * 60 * 60;
		}

	private bool IsSessionExpired () => _sessionExpiresAt is null || _sessionExpiresAt <= DateTimeOffset.UtcNow;

	private string? GetSessionCookieHeader () => string.IsNullOrWhiteSpace (_sessionCookieValue)
		? null
		: SESSION_COOKIE_NAME + "=" + _sessionCookieValue;

	private void CaptureSessionCookieFromHeader (string cookieHeader)
		{
		if (string.IsNullOrWhiteSpace (cookieHeader))
			{
			return;
			}

		string[] segments = cookieHeader.Split (';');
		foreach (string segment in segments)
			{
				int separatorIndex = segment.IndexOf ('=');
				if (separatorIndex <= 0)
					{
					continue;
					}

				string name = segment.Substring (0, separatorIndex).Trim ();
				if (!string.Equals (name, SESSION_COOKIE_NAME, StringComparison.OrdinalIgnoreCase)
					&& !string.Equals (name, "SESSIONID", StringComparison.OrdinalIgnoreCase))
					{
					continue;
					}

				string value = segment.Substring (separatorIndex + 1).Trim ();
				if (!string.IsNullOrWhiteSpace (value))
					{
					_sessionCookieValue = value;
					}

				return;
			}
		}

	private (byte[] AuthHash, bool V2) ResolveHandshakeAuthHash (byte[] localSeed, byte[] remoteSeed, byte[] serverHash)
		{
		foreach ((byte[] authHash, bool v2) in GetHandshakeAuthCandidates ())
			{
			byte[] expectedServerHash = v2
				? KlapAuthHash.CreateHandshake1HashV2 (localSeed, remoteSeed, authHash)
				: KlapAuthHash.CreateHandshake1HashV1 (localSeed, remoteSeed, authHash);
			if (FixedTimeEquals (expectedServerHash, serverHash))
				{
				return (authHash, v2);
				}
			}

		throw new InvalidDataException ($"The KLAP handshake1 response from '{_configuration.Host}' did not match the expected challenge response.");
		}

	private IEnumerable<(byte[] AuthHash, bool V2)> GetHandshakeAuthCandidates ()
		{
		DeviceCredentials suppliedCredentials = ResolveCredentials ();
		bool useV2Hashes = ShouldUseV2KlapHashes ();
		yield return (CreateAuthHash (suppliedCredentials, useV2Hashes), useV2Hashes);

		DeviceCredentials defaultCredentials = DeviceCredentials.FromDefault (DefaultCredentialProfile.Tapo);
		if (!AreSameCredentials (suppliedCredentials, defaultCredentials))
			{
			yield return (CreateAuthHash (defaultCredentials, useV2Hashes), useV2Hashes);
			}

		DeviceCredentials blankCredentials = new (string.Empty, string.Empty);
		if (!AreSameCredentials (suppliedCredentials, blankCredentials) && !AreSameCredentials (defaultCredentials, blankCredentials))
			{
			yield return (CreateAuthHash (blankCredentials, useV2Hashes), useV2Hashes);
			}
		}

	private bool ShouldUseV2KlapHashes ()
		{
		DeviceConnectionParameters? connectionParameters = _configuration.ConnectionOptions.ConnectionParameters;
		if (connectionParameters?.DeviceFamily == DeviceFamilyKind.IotSmartPlugSwitch
			|| connectionParameters?.DeviceFamily == DeviceFamilyKind.IotSmartBulb
			|| connectionParameters?.DeviceFamily == DeviceFamilyKind.IotIpCamera)
			{
			return false;
			}

		return true;
		}

	private static byte[] CreateAuthHash (DeviceCredentials credentials, bool useV2Hashes) => useV2Hashes
		? KlapAuthHash.CreateV2 (credentials)
		: KlapAuthHash.CreateV1 (credentials);

	private static bool AreSameCredentials (DeviceCredentials left, DeviceCredentials right) =>
		string.Equals (left.UserName, right.UserName, StringComparison.Ordinal)
		&& string.Equals (left.Password, right.Password, StringComparison.Ordinal);

	private static byte[] CreateSeed ()
		{
		var seed = new byte[16];
		using RandomNumberGenerator randomNumberGenerator = RandomNumberGenerator.Create ();
		randomNumberGenerator.GetBytes (seed);
		return seed;
		}

	private static bool FixedTimeEquals (byte[] left, byte[] right)
		{
		if (left.Length != right.Length)
			{
			return false;
			}

		int diff = 0;
		for (int i = 0; i < left.Length; i++)
			{
			diff |= left[i] ^ right[i];
			}

		return diff == 0;
		}
	}

internal static class KlapAuthHash
	{
	#pragma warning disable CA5351, CA5350
	public static byte[] CreateV1 (DeviceCredentials credentials)
		{
		byte[] userHash = ComputeMd5 (Encoding.UTF8.GetBytes (credentials.UserName ?? string.Empty));
		byte[] passwordHash = ComputeMd5 (Encoding.UTF8.GetBytes (credentials.Password ?? string.Empty));
		return ComputeMd5 (Combine (userHash, passwordHash));
		}

	public static byte[] CreateV2 (DeviceCredentials credentials)
		{
		byte[] userHash = ComputeSha1 (Encoding.UTF8.GetBytes (credentials.UserName ?? string.Empty));
		byte[] passwordHash = ComputeSha1 (Encoding.UTF8.GetBytes (credentials.Password ?? string.Empty));
		return ComputeSha256 (Combine (userHash, passwordHash));
		}

	public static byte[] CreateHandshake1HashV1 (byte[] localSeed, byte[] remoteSeed, byte[] authHash) => ComputeSha256 (Combine (localSeed, authHash));
	public static byte[] CreateHandshake2HashV1 (byte[] localSeed, byte[] remoteSeed, byte[] authHash) => ComputeSha256 (Combine (remoteSeed, authHash));
	public static byte[] CreateHandshake1HashV2 (byte[] localSeed, byte[] remoteSeed, byte[] authHash) => ComputeSha256 (Combine (localSeed, remoteSeed, authHash));
	public static byte[] CreateHandshake2HashV2 (byte[] localSeed, byte[] remoteSeed, byte[] authHash) => ComputeSha256 (Combine (remoteSeed, localSeed, authHash));

	private static byte[] ComputeMd5 (byte[] data)
		{
		#if NET10_0_OR_GREATER
		return MD5.HashData (data);
		#else
		using MD5 md5 = MD5.Create ();
		return md5.ComputeHash (data);
		#endif
		}

	private static byte[] ComputeSha1 (byte[] data)
		{
		#if NET10_0_OR_GREATER
		return SHA1.HashData (data);
		#else
		using SHA1 sha1 = SHA1.Create ();
		return sha1.ComputeHash (data);
		#endif
		}

	internal static byte[] ComputeSha256 (byte[] data)
		{
		#if NET10_0_OR_GREATER
		return SHA256.HashData (data);
		#else
		using SHA256 sha256 = SHA256.Create ();
		return sha256.ComputeHash (data);
		#endif
		}

	internal static byte[] Combine (params byte[][] arrays)
		{
		int totalLength = 0;
		foreach (byte[] array in arrays)
			{
			totalLength += array.Length;
			}

		var combined = new byte[totalLength];
		int offset = 0;
		foreach (byte[] array in arrays)
			{
			Buffer.BlockCopy (array, 0, combined, offset, array.Length);
			offset += array.Length;
			}

		return combined;
		}
	#pragma warning restore CA5351, CA5350
	}

internal readonly struct KlapEncryptedRequest
	{
	public KlapEncryptedRequest (byte[] payload, int sequence)
		{
		Payload = payload;
		Sequence = sequence;
		}

	public byte[] Payload { get; }
	public int Sequence { get; }
	}

internal sealed class KlapEncryptionSession
	{
	private readonly byte[] _key;
	private readonly byte[] _ivPrefix;
	private readonly byte[] _signaturePrefix;
	private int _sequence;

	public KlapEncryptionSession (byte[] localSeed, byte[] remoteSeed, byte[] userHash)
		{
		_key = Slice16 (KlapAuthHash.ComputeSha256 (KlapAuthHash.Combine (Encoding.ASCII.GetBytes ("lsk"), localSeed, remoteSeed, userHash)));
		byte[] fullIv = KlapAuthHash.ComputeSha256 (KlapAuthHash.Combine (Encoding.ASCII.GetBytes ("iv"), localSeed, remoteSeed, userHash));
		_ivPrefix = new byte[12];
		Buffer.BlockCopy (fullIv, 0, _ivPrefix, 0, _ivPrefix.Length);
		_sequence = ReadInt32BigEndian (fullIv, fullIv.Length - 4);
		byte[] sig = KlapAuthHash.ComputeSha256 (KlapAuthHash.Combine (Encoding.ASCII.GetBytes ("ldk"), localSeed, remoteSeed, userHash));
		_signaturePrefix = new byte[28];
		Buffer.BlockCopy (sig, 0, _signaturePrefix, 0, _signaturePrefix.Length);
		}

	public KlapEncryptedRequest Encrypt (string payload)
		{
		_sequence++;
		byte[] sequenceBytes = WriteInt32BigEndian (_sequence);
		byte[] iv = KlapAuthHash.Combine (_ivPrefix, sequenceBytes);
		byte[] plainBytes = Encoding.UTF8.GetBytes (payload);
		byte[] cipherBytes;
		using (Aes aes = Aes.Create ())
			{
			aes.Mode = CipherMode.CBC;
			aes.Padding = PaddingMode.PKCS7;
			aes.Key = _key;
			aes.IV = iv;
			using ICryptoTransform encryptor = aes.CreateEncryptor ();
			cipherBytes = encryptor.TransformFinalBlock (plainBytes, 0, plainBytes.Length);
			}

		byte[] signature = KlapAuthHash.ComputeSha256 (KlapAuthHash.Combine (_signaturePrefix, sequenceBytes, cipherBytes));
		return new KlapEncryptedRequest (KlapAuthHash.Combine (signature, cipherBytes), _sequence);
		}

	public string Decrypt (byte[] payload)
		{
		if (payload.Length < 32)
			{
			throw new InvalidDataException ("The KLAP response payload was too short to contain a signature.");
			}

		byte[] cipherBytes = new byte[payload.Length - 32];
		Buffer.BlockCopy (payload, 32, cipherBytes, 0, cipherBytes.Length);
		byte[] iv = KlapAuthHash.Combine (_ivPrefix, WriteInt32BigEndian (_sequence));
		byte[] plainBytes;
		using (Aes aes = Aes.Create ())
			{
			aes.Mode = CipherMode.CBC;
			aes.Padding = PaddingMode.PKCS7;
			aes.Key = _key;
			aes.IV = iv;
			using ICryptoTransform decryptor = aes.CreateDecryptor ();
			plainBytes = decryptor.TransformFinalBlock (cipherBytes, 0, cipherBytes.Length);
			}

		return Encoding.UTF8.GetString (plainBytes);
		}

	private static byte[] Slice16 (byte[] value)
		{
		var result = new byte[16];
		Buffer.BlockCopy (value, 0, result, 0, result.Length);
		return result;
		}

	private static int ReadInt32BigEndian (byte[] buffer, int offset) =>
		(buffer[offset] << 24)
		| (buffer[offset + 1] << 16)
		| (buffer[offset + 2] << 8)
		| buffer[offset + 3];

	private static byte[] WriteInt32BigEndian (int value) =>
		new byte[]
			{
			(byte)((value >> 24) & 0xFF),
			(byte)((value >> 16) & 0xFF),
			(byte)((value >> 8) & 0xFF),
			(byte)(value & 0xFF),
			};
	}
