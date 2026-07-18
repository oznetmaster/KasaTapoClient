// Copyright © 2026 Neil Colvin.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// Behavior modeled after the independent python-kasa project (https://github.com/python-kasa/python-kasa)
// for protocol/compatibility reference only; no python-kasa source was copied. See ATTRIBUTIONS.md.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json.Linq;
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
	#if DEBUG
	private static int _nextDiagnosticSessionId;
	#endif
	private readonly TimeSpan _timeout;

	public DiscoveryClient (TimeSpan timeout)
		{
		_timeout = timeout;
		}

	public async Task<IReadOnlyList<DiscoveryResult>> DiscoverAsync (string target, CancellationToken cancellationToken)
		=> await DiscoverAsync (target, includeSmart: true, cancellationToken).ConfigureAwait (false);

	public async Task<IReadOnlyList<DiscoveryResult>> DiscoverLegacyAsync (string target, CancellationToken cancellationToken)
		=> await DiscoverAsync (target, includeSmart: false, cancellationToken).ConfigureAwait (false);

	private async Task<IReadOnlyList<DiscoveryResult>> DiscoverAsync (string target, bool includeSmart, CancellationToken cancellationToken)
		{
		#if DEBUG
		int diagnosticSessionId = Interlocked.Increment (ref _nextDiagnosticSessionId);
		#else
		const int diagnosticSessionId = 0;
		#endif
		int receivedPacketCount = 0;
		int parseSuccessCount = 0;
		int parseFailureCount = 0;
		int ignoredSocketExceptionCount = 0;
		IPAddress targetAddress = ResolveTarget (target);
		using var kasaClient = new UdpClient (AddressFamily.InterNetwork)
			{
			EnableBroadcast = true,
			};
		kasaClient.Client.ReceiveBufferSize = UDP_RECEIVE_BUFFER_SIZE;
		kasaClient.Client.Bind (new IPEndPoint (IPAddress.Any, 0));
		DisableUdpConnectionReset (kasaClient.Client);
		using UdpClient? smartClient = includeSmart ? new UdpClient (AddressFamily.InterNetwork) : null;
		if (smartClient is not null)
			{
			smartClient.EnableBroadcast = true;
			smartClient.Client.ReceiveBufferSize = UDP_RECEIVE_BUFFER_SIZE;
			smartClient.Client.Bind (new IPEndPoint (IPAddress.Any, 0));
			DisableUdpConnectionReset (smartClient.Client);
			}

		byte[] kasaRequest = KasaCipher.Encrypt (KasaCommands.GET_SYSTEM_INFO);
		byte[]? smartRequest = includeSmart ? CreateNewDiscoveryQuery () : null;
		var kasaEndpoint = new IPEndPoint (targetAddress, KASA_DISCOVERY_PORT);
		IPEndPoint? tapoEndpoint = includeSmart ? new IPEndPoint (targetAddress, TAPO_DISCOVERY_PORT) : null;

		var results = new Dictionary<(string Host, DeviceTransportKind TransportKind), DiscoveryResult> ();
		DateTimeOffset expiresAt = DateTimeOffset.UtcNow.Add (_timeout);
		Task<UdpReceiveResult>? kasaReceiveTask = null;
		Task<UdpReceiveResult>? smartReceiveTask = null;
		LogDiagnostic ($"[KasaTapoClient.Discovery:{diagnosticSessionId}] target={targetAddress} timeout={_timeout} buffer={UDP_RECEIVE_BUFFER_SIZE} smart={(smartClient is not null ? "enabled" : "disabled")}");
		Task broadcastTask = BroadcastAsync (diagnosticSessionId, kasaClient, smartClient, kasaRequest, smartRequest, kasaEndpoint, tapoEndpoint, cancellationToken);
		try
			{
			while (DateTimeOffset.UtcNow < expiresAt)
				{
				DateTimeOffset now = DateTimeOffset.UtcNow;
				TimeSpan remaining = expiresAt - now;
				kasaReceiveTask ??= ReceiveAsync (kasaClient, cancellationToken);
				if (smartClient is not null)
					{
					smartReceiveTask ??= ReceiveAsync (smartClient, cancellationToken);
					}
				Task completedTask = smartReceiveTask is null
					? await Task.WhenAny (kasaReceiveTask, Task.Delay (remaining, cancellationToken)).ConfigureAwait (false)
					: await Task.WhenAny (kasaReceiveTask, smartReceiveTask, Task.Delay (remaining, cancellationToken)).ConfigureAwait (false);
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
						#if DEBUG
						receivedPacketCount++;
						#endif
						string response = KasaCipher.Decrypt (packet.Buffer);
						KasaResponseParser.ParsedResponse parsedResponse = KasaResponseParser.ParseResponse (response);
						if (KasaResponseParser.TryParseDiscoveryResult (parsedResponse, packet.RemoteEndPoint, out DiscoveryResult? result)
							&& result is not null)
							{
							StorePreferredDiscoveryResult (results, result);
							#if DEBUG
							parseSuccessCount++;
							#endif
							LogDiagnostic ($"[KasaTapoClient.Discovery:{diagnosticSessionId}] legacy packet parsed host={packet.RemoteEndPoint} bytes={packet.Buffer.Length} deviceId={result.DeviceId ?? "<null>"} alias={result.Alias ?? "<null>"}");
							}
						else
							{
							#if DEBUG
							parseFailureCount++;
							#endif
							LogDiagnostic ($"[KasaTapoClient.Discovery:{diagnosticSessionId}] legacy packet failed to parse host={packet.RemoteEndPoint} bytes={packet.Buffer.Length}");
							}
						}
					catch (OperationCanceledException)
						{
						throw;
						}
					catch (SocketException ex) when (IsTransientDiscoverySocketException (ex.SocketErrorCode))
						{
						#if DEBUG
						ignoredSocketExceptionCount++;
						#endif
						LogDiagnostic ($"[KasaTapoClient.Discovery:{diagnosticSessionId}] ignored legacy receive socket error={ex.SocketErrorCode}");
						}
					catch (Exception ex)
						{
						#if DEBUG
						parseFailureCount++;
						#endif
						LogDiagnostic ($"[KasaTapoClient.Discovery:{diagnosticSessionId}] legacy receive exception={ex.GetType ().Name}: {ex.Message}");
						}
					kasaReceiveTask = null;
					}
				else if (completedTask == smartReceiveTask)
					{
					try
						{
						UdpReceiveResult packet = await smartReceiveTask.ConfigureAwait (false);
						#if DEBUG
						receivedPacketCount++;
						#endif
						if (TryParseTapoDiscoveryResult (packet, out DiscoveryResult? result)
							&& result is not null)
							{
							StorePreferredDiscoveryResult (results, result);
							#if DEBUG
							parseSuccessCount++;
							#endif
							LogDiagnostic ($"[KasaTapoClient.Discovery:{diagnosticSessionId}] smart packet parsed host={packet.RemoteEndPoint} bytes={packet.Buffer.Length} deviceId={result.DeviceId ?? "<null>"} alias={result.Alias ?? "<null>"}");
							}
						else
							{
							#if DEBUG
							parseFailureCount++;
							#endif
							LogDiagnostic ($"[KasaTapoClient.Discovery:{diagnosticSessionId}] smart packet failed to parse host={packet.RemoteEndPoint} bytes={packet.Buffer.Length}");
							}
						}
					catch (OperationCanceledException)
						{
						throw;
						}
					catch (SocketException ex) when (IsTransientDiscoverySocketException (ex.SocketErrorCode))
						{
						#if DEBUG
						ignoredSocketExceptionCount++;
						#endif
						LogDiagnostic ($"[KasaTapoClient.Discovery:{diagnosticSessionId}] ignored smart receive socket error={ex.SocketErrorCode}");
						}
					catch (Exception ex)
						{
						#if DEBUG
						parseFailureCount++;
						#endif
						LogDiagnostic ($"[KasaTapoClient.Discovery:{diagnosticSessionId}] smart receive exception={ex.GetType ().Name}: {ex.Message}");
						}
					smartReceiveTask = null;
					}
				}

			await broadcastTask.ConfigureAwait (false);
			}
		finally
			{
			LogDiagnostic ($"[KasaTapoClient.Discovery:{diagnosticSessionId}] cleanup starting legacyReceive={GetTaskStatus (kasaReceiveTask)} smartReceive={GetTaskStatus (smartReceiveTask)} broadcast={broadcastTask.Status}");
			kasaClient.Close ();
			smartClient?.Close ();
			await ObserveReceiveTaskAsync (diagnosticSessionId, "legacy", kasaReceiveTask).ConfigureAwait (false);
			await ObserveReceiveTaskAsync (diagnosticSessionId, "smart", smartReceiveTask).ConfigureAwait (false);
			LogDiagnostic ($"[KasaTapoClient.Discovery:{diagnosticSessionId}] cleanup completed legacyReceive={GetTaskStatus (kasaReceiveTask)} smartReceive={GetTaskStatus (smartReceiveTask)}");
			}

		LogDiagnostic ($"[KasaTapoClient.Discovery:{diagnosticSessionId}] completed target={targetAddress} timeout={_timeout} packets={receivedPacketCount} parseSuccess={parseSuccessCount} parseFailure={parseFailureCount} ignoredSocketExceptions={ignoredSocketExceptionCount} resultCount={results.Count}");

		return results.Values.OrderBy (static result => result.Host, StringComparer.OrdinalIgnoreCase).ToArray ();
		}

	[Conditional ("DEBUG")]
	private static void LogDiagnostic (string message)
		{
		Debug.WriteLine (message);
		}

	private static string GetTaskStatus (Task? task) => task?.Status.ToString () ?? "none";

	private static async Task ObserveReceiveTaskAsync (int diagnosticSessionId, string protocol, Task<UdpReceiveResult>? receiveTask)
		{
		if (receiveTask is null)
			{
			return;
			}

		Task completedTask = await Task.WhenAny (receiveTask, Task.Delay (TimeSpan.FromSeconds (1))).ConfigureAwait (false);
		if (completedTask != receiveTask)
			{
			LogDiagnostic ($"[KasaTapoClient.Discovery:{diagnosticSessionId}] cleanup {protocol} receive remained pending after socket close");
			return;
			}

		try
			{
			await receiveTask.ConfigureAwait (false);
			LogDiagnostic ($"[KasaTapoClient.Discovery:{diagnosticSessionId}] cleanup {protocol} receive completed with a packet");
			}
		catch (Exception ex)
			{
			LogDiagnostic ($"[KasaTapoClient.Discovery:{diagnosticSessionId}] cleanup {protocol} receive terminated with {ex.GetType ().Name}: {ex.Message}");
			}
		}

	private static void StorePreferredDiscoveryResult (Dictionary<(string Host, DeviceTransportKind TransportKind), DiscoveryResult> results, DiscoveryResult candidate)
		{
		var key = (candidate.Host, candidate.TransportKind);
		if (!results.ContainsKey (key))
			{
			results[key] = candidate;
			}
		}

	private async Task BroadcastAsync (
		int diagnosticSessionId,
		UdpClient kasaClient,
		UdpClient? smartClient,
		byte[] kasaRequest,
		byte[]? smartRequest,
		IPEndPoint kasaEndpoint,
		IPEndPoint? tapoEndpoint,
		CancellationToken cancellationToken)
		{
		TimeSpan delay = TimeSpan.FromTicks (_timeout.Ticks / DISCOVERY_PACKET_COUNT);
		for (int attempt = 0; attempt < DISCOVERY_PACKET_COUNT; attempt++)
			{
			int kasaBytes = await kasaClient.SendAsync (kasaRequest, kasaRequest.Length, kasaEndpoint).ConfigureAwait (false);
			LogDiagnostic ($"[KasaTapoClient.Discovery:{diagnosticSessionId}] send attempt={attempt + 1} protocol=legacy remote={kasaEndpoint} bytes={kasaBytes}");
			if (smartClient is not null && smartRequest is not null && tapoEndpoint is not null)
				{
				int tapoBytes = await smartClient.SendAsync (smartRequest, smartRequest.Length, tapoEndpoint).ConfigureAwait (false);
				LogDiagnostic ($"[KasaTapoClient.Discovery:{diagnosticSessionId}] send attempt={attempt + 1} protocol=tapo remote={tapoEndpoint} bytes={tapoBytes}");
				}

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
				JObject root = JsonSupport.ParseObject (response);
				JObject? data = root["result"] as JObject
					?? root["params"] as JObject
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

	private static DiscoveryTransportMetadata GetDiscoveryTransportMetadata (JObject data, string? model, int responsePort)
		{
		if (data["mgt_encrypt_schm"] is JObject encryptionScheme)
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

		if (data["encrypt_type"] is not null || data["encrypt_info"] is JObject)
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

	private static string? ResolveDiscoveryEncryptType (JObject data, JObject encryptionScheme)
		{
		string? encryptTypeText = encryptionScheme["encrypt_type"]?.GetValue<string?> ();
		if (!string.IsNullOrWhiteSpace (encryptTypeText))
			{
			return encryptTypeText;
			}

		return data["encrypt_info"] is JObject encryptInfo
			? encryptInfo["sym_schm"]?.GetValue<string?> ()
			: null;
		}

	private static int? ResolveDiscoveryLoginVersion (JObject data, JObject encryptionScheme)
		{
		int? loginVersion = encryptionScheme["lv"]?.GetValue<int?> ();
		if (loginVersion is not null)
			{
			return loginVersion;
			}

		if (data["encrypt_type"] is not JArray encryptTypes)
			{
			return null;
			}

		int? maxLoginVersion = null;
		foreach (JToken? encryptType in encryptTypes)
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

	private static TpapDiscoveryMetadata? TryParseTpapDiscoveryMetadata (JObject data)
		{
		if (data["tpap"] is not JObject tpap)
			{
			return null;
			}

		List<int>? pakeModes = null;
		if (tpap["pake"] is JArray pakeArray)
			{
			pakeModes = [];
			foreach (JToken? item in pakeArray)
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
		string payload = new JObject
			{
			["params"] = new JObject
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
