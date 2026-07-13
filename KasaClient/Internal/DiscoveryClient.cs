// Copyright © 2026 Neil Colvin.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// Adapted from python-kasa (https://github.com/python-kasa/python-kasa)
// Original work Copyright (c) python-kasa contributors, MIT License

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace KasaTapoClient.Internal;

internal sealed class DiscoveryClient
	{
	private const int KASA_DISCOVERY_PORT = 9999;
	private const int TAPO_DISCOVERY_PORT = 20002;
	private const int KLAP_DISCOVERY_PORT = 20004;
	private const int DISCOVERY_PACKET_COUNT = 3;
	private const int UDP_RECEIVE_BUFFER_SIZE = 256 * 1024;
	private static readonly byte[] NEW_DISCOVERY_QUERY = CreateNewDiscoveryQuery ();
	private readonly TimeSpan _timeout;

	public DiscoveryClient (TimeSpan timeout)
		{
		_timeout = timeout;
		}

	public async Task<IReadOnlyList<DiscoveryResult>> DiscoverAsync (string target, CancellationToken cancellationToken)
		{
		IPAddress targetAddress = ResolveTarget (target);
		int receivedPacketCount = 0;
		int parseSuccessCount = 0;
		int parseFailureCount = 0;
		int ignoredSocketExceptionCount = 0;
		using var kasaClient = new UdpClient (AddressFamily.InterNetwork)
			{
			EnableBroadcast = true,
			};
		kasaClient.Client.SetSocketOption (SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
		kasaClient.Client.ReceiveBufferSize = UDP_RECEIVE_BUFFER_SIZE;
		kasaClient.Client.Bind (new IPEndPoint (IPAddress.Any, 0));
		DisableUdpConnectionReset (kasaClient.Client);
		using var smartClient = new UdpClient (AddressFamily.InterNetwork)
			{
			EnableBroadcast = true,
			};
		smartClient.Client.SetSocketOption (SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
		smartClient.Client.ReceiveBufferSize = UDP_RECEIVE_BUFFER_SIZE;
		smartClient.Client.Bind (new IPEndPoint (IPAddress.Any, 0));
		DisableUdpConnectionReset (smartClient.Client);

		byte[] kasaRequest = KasaCipher.Encrypt (KasaCommands.GET_SYSTEM_INFO);
		var kasaEndpoint = new IPEndPoint (targetAddress, KASA_DISCOVERY_PORT);
		var tapoEndpoint = new IPEndPoint (targetAddress, TAPO_DISCOVERY_PORT);
		var klapEndpoint = new IPEndPoint (targetAddress, KLAP_DISCOVERY_PORT);

		var results = new Dictionary<string, DiscoveryResult> (StringComparer.OrdinalIgnoreCase);
		DateTimeOffset expiresAt = DateTimeOffset.UtcNow.Add (_timeout);
		Task<UdpReceiveResult>? kasaReceiveTask = null;
		Task<UdpReceiveResult>? smartReceiveTask = null;
		LogDiagnostic ($"[KasaTapoClient.Discovery] target={targetAddress} timeout={_timeout} buffer={UDP_RECEIVE_BUFFER_SIZE}");
		Task broadcastTask = BroadcastAsync (kasaClient, smartClient, kasaRequest, kasaEndpoint, tapoEndpoint, klapEndpoint, cancellationToken);
		while (DateTimeOffset.UtcNow < expiresAt)
			{
			DateTimeOffset now = DateTimeOffset.UtcNow;
			TimeSpan remaining = expiresAt - now;
			kasaReceiveTask ??= ReceiveAsync (kasaClient, cancellationToken);
			smartReceiveTask ??= ReceiveAsync (smartClient, cancellationToken);
			Task completedTask = await Task.WhenAny (kasaReceiveTask, smartReceiveTask, Task.Delay (remaining, cancellationToken)).ConfigureAwait (false);
			if (completedTask != kasaReceiveTask && completedTask != smartReceiveTask)
				{
				cancellationToken.ThrowIfCancellationRequested ();
				continue;
				}

			if (completedTask == kasaReceiveTask)
				{
				try
					{
					UdpReceiveResult packet = await kasaReceiveTask.ConfigureAwait (false);
					receivedPacketCount++;
					string response = KasaCipher.Decrypt (packet.Buffer);
					KasaResponseParser.ParsedResponse parsedResponse = KasaResponseParser.ParseResponse (response);
					if (KasaResponseParser.TryParseDiscoveryResult (parsedResponse, packet.RemoteEndPoint, out DiscoveryResult? result)
						&& result is not null)
						{
						StorePreferredDiscoveryResult (results, result);
						parseSuccessCount++;
						LogDiagnostic ($"[KasaTapoClient.Discovery] legacy packet parsed host={packet.RemoteEndPoint} deviceId={result.DeviceId ?? "<null>"} alias={result.Alias ?? "<null>"}");
						}
					else
						{
						parseFailureCount++;
						LogDiagnostic ($"[KasaTapoClient.Discovery] legacy packet failed to parse host={packet.RemoteEndPoint}");
						}
					}
				catch (OperationCanceledException)
					{
					throw;
					}
				catch (SocketException ex) when (IsTransientDiscoverySocketException (ex.SocketErrorCode))
					{
					ignoredSocketExceptionCount++;
					LogDiagnostic ($"[KasaTapoClient.Discovery] ignored legacy receive socket error={ex.SocketErrorCode}");
					}
				catch (Exception ex)
					{
					parseFailureCount++;
					LogDiagnostic ($"[KasaTapoClient.Discovery] legacy receive exception={ex.GetType ().Name}: {ex.Message}");
					}
				kasaReceiveTask = null;
				}
			else if (completedTask == smartReceiveTask)
				{
				try
					{
					UdpReceiveResult packet = await smartReceiveTask.ConfigureAwait (false);
					receivedPacketCount++;
					if (TryParseTapoDiscoveryResult (packet, out DiscoveryResult? result)
						&& result is not null)
						{
						StorePreferredDiscoveryResult (results, result);
						parseSuccessCount++;
						LogDiagnostic ($"[KasaTapoClient.Discovery] smart packet parsed host={packet.RemoteEndPoint} deviceId={result.DeviceId ?? "<null>"} alias={result.Alias ?? "<null>"}");
						}
					else
						{
						parseFailureCount++;
						LogDiagnostic ($"[KasaTapoClient.Discovery] smart packet failed to parse host={packet.RemoteEndPoint}");
						}
					}
				catch (OperationCanceledException)
					{
					throw;
					}
				catch (SocketException ex) when (IsTransientDiscoverySocketException (ex.SocketErrorCode))
					{
					ignoredSocketExceptionCount++;
					LogDiagnostic ($"[KasaTapoClient.Discovery] ignored smart receive socket error={ex.SocketErrorCode}");
					}
				catch (Exception ex)
					{
					parseFailureCount++;
					LogDiagnostic ($"[KasaTapoClient.Discovery] smart receive exception={ex.GetType ().Name}: {ex.Message}");
					}
				smartReceiveTask = null;
				}
			}

		await broadcastTask.ConfigureAwait (false);
		LogDiagnostic ($"[KasaTapoClient.Discovery] completed target={targetAddress} timeout={_timeout} packets={receivedPacketCount} parseSuccess={parseSuccessCount} parseFailure={parseFailureCount} ignoredSocketExceptions={ignoredSocketExceptionCount} resultCount={results.Count}");

		return results.Values.OrderBy (static result => result.Host, StringComparer.OrdinalIgnoreCase).ToArray ();
		}

	private static void LogDiagnostic (string message)
		{
		Debug.WriteLine (message);
		}

	private static void StorePreferredDiscoveryResult (Dictionary<string, DiscoveryResult> results, DiscoveryResult candidate)
		{
		if (!results.TryGetValue (candidate.Host, out DiscoveryResult? existing))
			{
			results[candidate.Host] = candidate;
			}
		}

	private async Task BroadcastAsync (
		UdpClient kasaClient,
		UdpClient smartClient,
		byte[] kasaRequest,
		IPEndPoint kasaEndpoint,
		IPEndPoint tapoEndpoint,
		IPEndPoint klapEndpoint,
		CancellationToken cancellationToken)
		{
		TimeSpan delay = TimeSpan.FromTicks (_timeout.Ticks / DISCOVERY_PACKET_COUNT);
		for (int attempt = 0; attempt < DISCOVERY_PACKET_COUNT; attempt++)
			{
			await kasaClient.SendAsync (kasaRequest, kasaRequest.Length, kasaEndpoint).ConfigureAwait (false);
			await smartClient.SendAsync (NEW_DISCOVERY_QUERY, NEW_DISCOVERY_QUERY.Length, tapoEndpoint).ConfigureAwait (false);
			await smartClient.SendAsync (NEW_DISCOVERY_QUERY, NEW_DISCOVERY_QUERY.Length, klapEndpoint).ConfigureAwait (false);

			if (attempt == DISCOVERY_PACKET_COUNT - 1 || delay <= TimeSpan.Zero)
				{
				continue;
				}

			await Task.Delay (delay, cancellationToken).ConfigureAwait (false);
			}
		}

	private static bool TryParseTapoDiscoveryResult (UdpReceiveResult packet, out DiscoveryResult? result)
		{
		result = null;
		try
			{
				if (packet.RemoteEndPoint.Port != TAPO_DISCOVERY_PORT && packet.RemoteEndPoint.Port != KLAP_DISCOVERY_PORT)
					{
					return false;
					}

				if (packet.Buffer.Length <= 16)
					{
					return false;
					}

				string response = Encoding.UTF8.GetString (packet.Buffer, 16, packet.Buffer.Length - 16);
				JsonObject root = JsonSupport.ParseObject (response);
				JsonObject? data = root["result"] as JsonObject
					?? root["params"] as JsonObject
					?? root;
				if (data is null)
					{
					return false;
					}

				string host = packet.RemoteEndPoint.Address.ToString ();
				string? model = data["model"]?.GetValue<string?> () ?? data["device_model"]?.GetValue<string?> () ?? data["device_model_name"]?.GetValue<string?> ();
				string? encodedAlias = data["nickname"]?.GetValue<string?> () ?? data["alias"]?.GetValue<string?> () ?? data["device_name"]?.GetValue<string?> ();
				string? alias = string.IsNullOrWhiteSpace (encodedAlias)
					? encodedAlias
					: KasaResponseParser.DecodeSmartAlias (encodedAlias);
				string? deviceId = data["device_id"]?.GetValue<string?> () ?? data["deviceId"]?.GetValue<string?> ();
				if (string.IsNullOrWhiteSpace (model) && string.IsNullOrWhiteSpace (alias) && string.IsNullOrWhiteSpace (deviceId))
					{
					return false;
					}
				DeviceType deviceType = DetermineTapoDeviceType (model, data["type"]?.GetValue<string?> (), data["device_type"]?.GetValue<string?> ());
				DiscoveryTransportMetadata metadata = GetDiscoveryTransportMetadata (data, model, packet.RemoteEndPoint.Port);
				TpapDiscoveryMetadata? tpapMetadata = TryParseTpapDiscoveryMetadata (data);
				int? protocolVersion = data["protocol_version"]?.GetValue<int?> ();
				bool? tpapPreferred = data["tpap_preferred"]?.GetValue<bool?> ();
				result = new DiscoveryResult (host, deviceType, alias, model, deviceId, response, metadata.TransportKind, metadata.SupportsHttps, metadata.Port, metadata.ConnectionParameters, tpapMetadata, protocolVersion, tpapPreferred);
				return true;
			}
		catch
			{
			return false;
			}
		}

	private static DeviceType DetermineTapoDeviceType (string? model, params string?[] rawTypes)
		{
		string combined = string.Join (" ", rawTypes.Append (model ?? string.Empty)).ToUpperInvariant ();
		if (combined.Contains ("HUB"))
			{
			return DeviceType.Hub;
			}

		string modelText = (model ?? string.Empty).Trim ().ToUpperInvariant ();
		if (modelText is "H100" or "H110" or "H200" or "H500")
			{
			return DeviceType.Hub;
			}

		if (combined.Contains ("PLUG"))
			{
			return DeviceType.Plug;
			}

		if (combined.Contains ("LIGHT_STRIP") || combined.Contains ("LIGHT STRIP"))
			{
			return DeviceType.LightStrip;
			}

		if (combined.Contains ("SWITCH"))
			{
			return DeviceType.WallSwitch;
			}

		if (combined.Contains ("DIMMER"))
			{
			return DeviceType.Dimmer;
			}

		if (combined.Contains ("BULB"))
			{
			return DeviceType.Bulb;
			}

		if (combined.Contains ("SENSOR"))
			{
			return DeviceType.Sensor;
			}

		if (combined.Contains ("ENERGY"))
			{
			return DeviceType.Thermostat;
			}

		if (combined.Contains ("ROBOVAC"))
			{
			return DeviceType.Vacuum;
			}

		if (combined.Contains ("TAPOCHIME"))
			{
			return DeviceType.Chime;
			}

		if (modelText.Length > 0 && modelText[0] == 'L')
			{
			return DeviceType.Bulb;
			}

		if (modelText.Length > 0 && modelText[0] == 'P')
			{
			return DeviceType.Plug;
			}

		return DeviceType.Unknown;
		}

	private static DiscoveryTransportMetadata GetDiscoveryTransportMetadata (JsonObject data, string? model, int responsePort)
		{
		if (data["mgt_encrypt_schm"] is JsonObject encryptionScheme)
			{
				bool supportsHttps = encryptionScheme["is_support_https"]?.GetValue<bool?> () == true;
				int? port = encryptionScheme["http_port"]?.GetValue<int?> ();
				string? encryptTypeText = ResolveDiscoveryEncryptType (data, encryptionScheme);
				string? deviceTypeText = data["device_type"]?.GetValue<string?> () ?? data["type"]?.GetValue<string?> ();
				int? loginVersion = ResolveDiscoveryLoginVersion (data, encryptionScheme);
				if (port is null || port <= 0)
					{
					port = supportsHttps ? 443 : 80;
					}

				return new DiscoveryTransportMetadata (
					DeviceTransportKind.HttpToken,
					supportsHttps,
					port,
					new DeviceConnectionParameters (
						DetermineDeviceFamilyKind (deviceTypeText, model, supportsHttps),
						DetermineEncryptionKind (encryptTypeText, responsePort),
						loginVersion,
						supportsHttps,
						port));
			}

		if (data["encrypt_type"] is not null || data["encrypt_info"] is JsonObject)
			{
				bool supportsHttps = data["is_support_https"]?.GetValue<bool?> () == true || responsePort == KLAP_DISCOVERY_PORT;
				int port = supportsHttps ? 443 : 80;
				string? encryptTypeText = data["encrypt_type"]?.GetValue<string?> ();
				string? deviceTypeText = data["device_type"]?.GetValue<string?> () ?? data["type"]?.GetValue<string?> ();
				return new DiscoveryTransportMetadata (
					DeviceTransportKind.HttpToken,
					supportsHttps,
					port,
					new DeviceConnectionParameters (
						DetermineDeviceFamilyKind (deviceTypeText, model, supportsHttps),
						DetermineEncryptionKind (encryptTypeText, responsePort),
						null,
						supportsHttps,
						port));
			}

		return new DiscoveryTransportMetadata (
			DeviceTransportKind.LegacyXor,
			supportsHttps: false,
			port: 9999,
			new DeviceConnectionParameters (DetermineLegacyDeviceFamilyKind (model), DeviceEncryptionKind.Xor, useHttps: false, httpPort: null));
		}

	private static string? ResolveDiscoveryEncryptType (JsonObject data, JsonObject encryptionScheme)
		{
		string? encryptTypeText = encryptionScheme["encrypt_type"]?.GetValue<string?> ();
		if (!string.IsNullOrWhiteSpace (encryptTypeText))
			{
			return encryptTypeText;
			}

		return data["encrypt_info"] is JsonObject encryptInfo
			? encryptInfo["sym_schm"]?.GetValue<string?> ()
			: null;
		}

	private static int? ResolveDiscoveryLoginVersion (JsonObject data, JsonObject encryptionScheme)
		{
		int? loginVersion = encryptionScheme["lv"]?.GetValue<int?> ();
		if (loginVersion is not null)
			{
			return loginVersion;
			}

		if (data["encrypt_type"] is not JsonArray encryptTypes)
			{
			return null;
			}

		int? maxLoginVersion = null;
		foreach (JsonNode? encryptType in encryptTypes)
			{
			if (encryptType?.GetValue<int?> () is not int candidate)
				{
				continue;
				}

			maxLoginVersion = maxLoginVersion is int existing && existing > candidate
				? existing
				: candidate;
			}

		return maxLoginVersion;
		}

	private static TpapDiscoveryMetadata? TryParseTpapDiscoveryMetadata (JsonObject data)
		{
		if (data["tpap"] is not JsonObject tpap)
			{
			return null;
			}

		List<int>? pakeModes = null;
		if (tpap["pake"] is JsonArray pakeArray)
			{
			pakeModes = [];
			foreach (JsonNode? item in pakeArray)
				{
				if (item?.GetValue<int?> () is int mode)
					{
					pakeModes.Add (mode);
					}
				}
			}

		return new TpapDiscoveryMetadata (
			tpap["port"]?.GetValue<int?> (),
			pakeModes,
			tpap["tls"]?.GetValue<int?> (),
			tpap["dac"]?.GetValue<int?> (),
			tpap["noc"]?.GetValue<int?> ());
		}

	private static DeviceFamilyKind DetermineDeviceFamilyKind (string? deviceType, string? model, bool supportsHttps)
		{
		string value = (deviceType ?? string.Empty).Trim ().ToUpperInvariant ();
		string lookupKey = supportsHttps ? value + ".HTTPS" : value;
		return lookupKey switch
			{
				"SMART.TAPOPLUG" => DeviceFamilyKind.SmartTapoPlug,
				"SMART.TAPOBULB" => DeviceFamilyKind.SmartTapoBulb,
				"SMART.TAPOSWITCH" => DeviceFamilyKind.SmartTapoSwitch,
				"SMART.TAPOHUB" => DeviceFamilyKind.SmartTapoHub,
				"SMART.TAPOHUB.HTTPS" => DeviceFamilyKind.SmartTapoHub,
				"SMART.KASAHUB" => DeviceFamilyKind.SmartKasaHub,
				"SMART.KASAPLUG" => DeviceFamilyKind.SmartKasaPlug,
				"SMART.KASASWITCH" => DeviceFamilyKind.SmartKasaSwitch,
				"SMART.IPCAMERA.HTTPS" => DeviceFamilyKind.SmartIpCamera,
				"SMART.TAPODOORBELL.HTTPS" => DeviceFamilyKind.SmartTapoDoorbell,
				"SMART.TAPOROBOVAC.HTTPS" => DeviceFamilyKind.SmartTapoRobovac,
				_ when value.StartsWith ("SMART.", StringComparison.Ordinal) => DeviceFamilyKind.Unknown,
				_ => DeviceFamilyKind.Unknown,
			};
		}

	private static DeviceFamilyKind DetermineLegacyDeviceFamilyKind (string? model)
		{
		string modelText = (model ?? string.Empty).Trim ().ToUpperInvariant ();
		if (modelText.StartsWith ("LB", StringComparison.Ordinal)
			|| modelText.StartsWith ("KL", StringComparison.Ordinal)
			|| modelText.StartsWith ("L5", StringComparison.Ordinal)
			|| modelText.StartsWith ("L6", StringComparison.Ordinal)
			|| modelText.StartsWith ("L9", StringComparison.Ordinal))
			{
			return DeviceFamilyKind.IotSmartBulb;
			}

		if (modelText.StartsWith ("KC", StringComparison.Ordinal))
			{
			return DeviceFamilyKind.IotIpCamera;
			}

		return DeviceFamilyKind.IotSmartPlugSwitch;
		}

	private static DeviceEncryptionKind DetermineEncryptionKind (string? encryptType, int responsePort)
		{
		string value = (encryptType ?? string.Empty).Trim ().ToUpperInvariant ();
		if (value.Length == 0)
			{
			return responsePort == KLAP_DISCOVERY_PORT ? DeviceEncryptionKind.Klap : DeviceEncryptionKind.Aes;
			}

		return value switch
			{
				"AES" => DeviceEncryptionKind.Aes,
				"TPAP" => DeviceEncryptionKind.Tpap,
				"KLAP" => DeviceEncryptionKind.Klap,
				"XOR" => DeviceEncryptionKind.Xor,
				_ => responsePort == KLAP_DISCOVERY_PORT ? DeviceEncryptionKind.Klap : DeviceEncryptionKind.Aes,
			};
		}

	private static IPAddress ResolveTarget (string target)
		{
		if (string.IsNullOrWhiteSpace (target))
			{
			return IPAddress.Broadcast;
			}

		if (IPAddress.TryParse (target, out IPAddress? address))
			{
			return address;
			}

		IPAddress[] addresses = Dns.GetHostAddresses (target);
		IPAddress? ipv4Address = addresses.FirstOrDefault (static address => address.AddressFamily == AddressFamily.InterNetwork);
		return ipv4Address ?? throw new InvalidOperationException ($"Unable to resolve an IPv4 address for '{target}'.");
		}

	private static Task<UdpReceiveResult> ReceiveAsync (UdpClient client, CancellationToken cancellationToken)
		{
#if NET10_0_OR_GREATER
		return client.ReceiveAsync(cancellationToken).AsTask();
#else
#pragma warning disable CA2016
		return client.ReceiveAsync ();
#pragma warning restore CA2016
#endif
		}

	private static bool IsTransientDiscoverySocketException (SocketError socketError) => socketError is
		SocketError.ConnectionReset
		or SocketError.ConnectionAborted
		or SocketError.Interrupted
		or SocketError.MessageSize
		or SocketError.NetworkReset
		or SocketError.NetworkDown
		or SocketError.NetworkUnreachable
		or SocketError.HostDown
		or SocketError.HostUnreachable
		or SocketError.NotConnected
		or SocketError.Shutdown
		or SocketError.TimedOut
		or SocketError.TryAgain
		or SocketError.WouldBlock;

	private static void DisableUdpConnectionReset (Socket socket)
		{
		PlatformID platform = Environment.OSVersion.Platform;
		if (platform != PlatformID.Win32NT && platform != PlatformID.Win32Windows)
			{
			return;
			}

		try
			{
			const int SIO_UDP_CONNRESET = unchecked((int)0x9800000C);
			socket.IOControl (SIO_UDP_CONNRESET, new byte[] { 0, 0, 0, 0 }, null);
			}
		catch (NotSupportedException)
			{
			}
		catch (SocketException)
			{
			}
		}

	private static byte[] CreateNewDiscoveryQuery ()
		{
		byte[] secret = new byte[4];
		using (RandomNumberGenerator randomNumberGenerator = RandomNumberGenerator.Create ())
			{
			randomNumberGenerator.GetBytes (secret);
			}

		string publicKeyPem = CreateDiscoveryPublicKeyPem ();
		string payload = new JsonObject
			{
			["params"] = new JsonObject
				{
				["rsa_key"] = publicKeyPem,
				},
			}.ToJsonString (JsonSupport.COMPACT_JSON);
		byte[] payloadBytes = Encoding.UTF8.GetBytes (payload);
		var query = new byte[16 + payloadBytes.Length];
		query[0] = 2;
		query[1] = 0;
		WriteUInt16BigEndian (query, 2, 1);
		WriteUInt16BigEndian (query, 4, checked((ushort)payloadBytes.Length));
		query[6] = 17;
		query[7] = 0;
		Buffer.BlockCopy (secret, 0, query, 8, secret.Length);
		WriteUInt32BigEndian (query, 12, 0x5A6B7C8D);
		Buffer.BlockCopy (payloadBytes, 0, query, 16, payloadBytes.Length);
		WriteUInt32BigEndian (query, 12, ComputeCrc32 (query));
		return query;
		}

	private static string CreateDiscoveryPublicKeyPem ()
		{
		using RSA rsa = RSA.Create (2048);
		RSAParameters parameters = rsa.ExportParameters (false);
		byte[] publicKeyInfo = CreateSubjectPublicKeyInfo (parameters);
		string base64 = Convert.ToBase64String (publicKeyInfo, Base64FormattingOptions.InsertLineBreaks);
		return "-----BEGIN PUBLIC KEY-----\n" + base64 + "\n-----END PUBLIC KEY-----\n";
		}

	private static byte[] CreateSubjectPublicKeyInfo (RSAParameters parameters)
		{
		if (parameters.Modulus is null || parameters.Exponent is null)
			{
			throw new InvalidOperationException ("Unable to export the RSA discovery public key.");
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
		if (value < 128)
			{
			return new byte[] { (byte)value };
			}

		var bytes = new List<byte> (4);
		int remaining = value;
		while (remaining > 0)
			{
			bytes.Insert (0, (byte)(remaining & 0xFF));
			remaining >>= 8;
			}

		bytes.Insert (0, (byte)(0x80 | bytes.Count));
		return bytes.ToArray ();
		}

	private static byte[] Combine (params byte[][] values)
		{
		int totalLength = 0;
		foreach (byte[] value in values)
			{
			totalLength += value.Length;
			}

		var combined = new byte[totalLength];
		int offset = 0;
		foreach (byte[] value in values)
			{
			Buffer.BlockCopy (value, 0, combined, offset, value.Length);
			offset += value.Length;
			}

		return combined;
		}

	private static uint ComputeCrc32 (byte[] data)
		{
		uint crc = 0xFFFFFFFF;
		for (int index = 0; index < data.Length; index++)
			{
			crc ^= data[index];
			for (int bit = 0; bit < 8; bit++)
				{
				crc = (crc & 1) != 0
					? (crc >> 1) ^ 0xEDB88320u
					: crc >> 1;
				}
			}

		return ~crc;
		}

	private static void WriteUInt16BigEndian (byte[] buffer, int offset, ushort value)
		{
		buffer[offset] = (byte)(value >> 8);
		buffer[offset + 1] = (byte)value;
		}

	private static void WriteUInt32BigEndian (byte[] buffer, int offset, uint value)
		{
		buffer[offset] = (byte)(value >> 24);
		buffer[offset + 1] = (byte)(value >> 16);
		buffer[offset + 2] = (byte)(value >> 8);
		buffer[offset + 3] = (byte)value;
		}

	private readonly struct DiscoveryTransportMetadata
		{
		public DiscoveryTransportMetadata (DeviceTransportKind transportKind, bool supportsHttps, int? port, DeviceConnectionParameters? connectionParameters)
			{
			TransportKind = transportKind;
			SupportsHttps = supportsHttps;
			Port = port;
			ConnectionParameters = connectionParameters;
			}

		public DeviceTransportKind TransportKind
			{
			get;
			}

		public bool SupportsHttps
			{
			get;
			}

		public int? Port
			{
			get;
			}

		public DeviceConnectionParameters? ConnectionParameters
			{
			get;
			}
		}
	}
