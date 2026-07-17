using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

const int DiscoveryPort = 20002;
int responseCount = GetIntArgument (args, "--count", 1, 1, 20);
int responseBytes = GetIntArgument (args, "--bytes", 353, 200, 60000);
int delayMilliseconds = GetIntArgument (args, "--delay", 0, 0, 5000);
bool relay = args.Any (argument => string.Equals (argument, "--relay", StringComparison.OrdinalIgnoreCase));

using var client = new UdpClient (AddressFamily.InterNetwork);
client.Client.SetSocketOption (SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
client.Client.Bind (new IPEndPoint (IPAddress.Any, DiscoveryPort));

Console.WriteLine ($"Tapo bulb discovery simulator listening on 0.0.0.0:{DiscoveryPort}.");
Console.WriteLine (relay ? $"Mode: relay CP4-R requests through Windows; forwarding delay={delayMilliseconds}ms." : $"Response profile: count={responseCount}, bytes={responseBytes}, delay={delayMilliseconds}ms.");
Console.WriteLine ("Press Ctrl+C to stop.");

using var cancellation = new CancellationTokenSource ();
Console.CancelKeyPress += (_, eventArgs) =>
	{
	eventArgs.Cancel = true;
	cancellation.Cancel ();
	};

while (!cancellation.IsCancellationRequested)
	{
	try
		{
		UdpReceiveResult request = await client.ReceiveAsync (cancellation.Token).ConfigureAwait (false);
		DateTimeOffset receivedAt = DateTimeOffset.Now;
		Console.WriteLine ($"{receivedAt:O} RX {request.Buffer.Length} bytes from {request.RemoteEndPoint}; header={Convert.ToHexString (request.Buffer.AsSpan (0, Math.Min (16, request.Buffer.Length)))}");
		if (!TapoDiscoveryProtocol.IsDiscoveryRequest (request.Buffer))
			{
			Console.WriteLine ("  Ignored: not a Tapo discovery request.");
			continue;
			}
		if (relay && request.RemoteEndPoint.Address.Equals (GetLocalIPv4Address ()))
			{
			Console.WriteLine ("  Ignored: relay's own broadcast.");
			continue;
			}
		if (relay)
			{
			await RelayDiscoveryAsync (request, delayMilliseconds, cancellation.Token).ConfigureAwait (false);
			continue;
			}

		for (int responseIndex = 1; responseIndex <= responseCount; responseIndex++)
			{
			byte[] response = TapoDiscoveryProtocol.CreateBulbResponse (request.Buffer, responseBytes, responseIndex);
			int sent = await client.SendAsync (response, request.RemoteEndPoint, cancellation.Token).ConfigureAwait (false);
			Console.WriteLine ($"{DateTimeOffset.Now:O} TX {sent} bytes to {request.RemoteEndPoint}; response={responseIndex}/{responseCount} device=windows-tapo-simulator-{responseIndex} model=L530");
			if (delayMilliseconds > 0 && responseIndex < responseCount) await Task.Delay (delayMilliseconds, cancellation.Token).ConfigureAwait (false);
			}

static async Task RelayDiscoveryAsync (UdpReceiveResult originalRequest, int forwardingDelayMilliseconds, CancellationToken cancellationToken)
	{
	using var relayClient = new UdpClient (AddressFamily.InterNetwork) { EnableBroadcast = true };
	relayClient.Client.Bind (new IPEndPoint (IPAddress.Any, 0));
	var destination = new IPEndPoint (IPAddress.Broadcast, DiscoveryPort);
	await relayClient.SendAsync (originalRequest.Buffer, originalRequest.Buffer.Length, destination).ConfigureAwait (false);
	Console.WriteLine ($"{DateTimeOffset.Now:O} RELAY TX {originalRequest.Buffer.Length} bytes from {relayClient.Client.LocalEndPoint} to {destination} for {originalRequest.RemoteEndPoint}");
	var forwardedEndpoints = new HashSet<string> (StringComparer.OrdinalIgnoreCase);
	DateTime expires = DateTime.UtcNow.AddSeconds (3);
	while (DateTime.UtcNow < expires)
		{
		cancellationToken.ThrowIfCancellationRequested ();
		if (relayClient.Available == 0)
			{
			await Task.Delay (10, cancellationToken).ConfigureAwait (false);
			continue;
			}
		UdpReceiveResult response;
		try
			{
			response = await relayClient.ReceiveAsync (cancellationToken).ConfigureAwait (false);
			}
		catch (SocketException ex) when (ex.SocketErrorCode == SocketError.ConnectionReset)
			{
			continue;
			}
		if (response.RemoteEndPoint.Address.Equals (GetLocalIPv4Address ())) continue;
		if (forwardingDelayMilliseconds > 0) await Task.Delay (forwardingDelayMilliseconds, cancellationToken).ConfigureAwait (false);
		await relayClient.SendAsync (response.Buffer, originalRequest.RemoteEndPoint, cancellationToken).ConfigureAwait (false);
		forwardedEndpoints.Add (response.RemoteEndPoint.ToString ());
		Console.WriteLine ($"{DateTimeOffset.Now:O} RELAY RX/TX {response.Buffer.Length} bytes {response.RemoteEndPoint} -> {originalRequest.RemoteEndPoint}");
		}
	Console.WriteLine ($"Relay completed for {originalRequest.RemoteEndPoint}: [{(forwardedEndpoints.Count == 0 ? "none" : string.Join (", ", forwardedEndpoints))}]");
	}

static IPAddress GetLocalIPv4Address ()
	{
	using var socket = new Socket (AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
	socket.Connect (new IPEndPoint (IPAddress.Parse ("192.168.8.1"), 9));
	return ((IPEndPoint)socket.LocalEndPoint!).Address;
	}
		}
	catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
		{
		break;
		}
	}

static int GetIntArgument (string[] arguments, string name, int fallback, int minimum, int maximum)
	{
	int index = Array.FindIndex (arguments, argument => string.Equals (argument, name, StringComparison.OrdinalIgnoreCase));
	if (index < 0) return fallback;
	if (index + 1 >= arguments.Length || !int.TryParse (arguments[index + 1], out int value) || value < minimum || value > maximum)
		{
		throw new ArgumentException ($"{name} must be between {minimum} and {maximum}.");
		}
	return value;
	}

internal static class TapoDiscoveryProtocol
	{
	public static bool IsDiscoveryRequest (byte[] packet)
		{
		if (packet.Length <= 16 || packet[0] != 2 || packet[1] != 0 || packet[6] != 17)
			{
			return false;
			}

		int payloadLength = (packet[4] << 8) | packet[5];
		if (payloadLength != packet.Length - 16)
			{
			return false;
			}

		try
			{
			using JsonDocument document = JsonDocument.Parse (packet.AsMemory (16, payloadLength));
			return document.RootElement.TryGetProperty ("params", out JsonElement parameters)
				&& parameters.TryGetProperty ("rsa_key", out JsonElement rsaKey)
				&& !string.IsNullOrWhiteSpace (rsaKey.GetString ());
			}
		catch (JsonException)
			{
			return false;
			}
		}

	public static byte[] CreateBulbResponse (byte[] request, int targetBytes, int responseIndex)
		{
		int paddingLength = Math.Max (0, targetBytes - 370);
		var payload = new
			{
			error_code = 0,
			result = new
				{
				device_id = "windows-tapo-simulator-" + responseIndex,
				device_model = "L530",
				model = "L530",
				type = "SMART.TAPOBULB",
				device_type = "SMART.TAPOBULB",
				nickname = Convert.ToBase64String (Encoding.UTF8.GetBytes ("Windows Tapo Simulator")),
				ip = GetLocalIPv4Address ().ToString (),
				mac = "02-00-00-00-20-" + responseIndex.ToString ("X2"),
				padding = new string ('x', paddingLength),
				mgt_encrypt_schm = new
					{
					is_support_https = false,
					http_port = 80,
					encrypt_type = "AES",
					lv = 2,
					},
				},
			};
		byte[] payloadBytes = JsonSerializer.SerializeToUtf8Bytes (payload);
		var response = new byte[16 + payloadBytes.Length];
		response[0] = 2;
		response[1] = 0;
		response[2] = request.Length > 2 ? request[2] : (byte)0;
		response[3] = request.Length > 3 ? request[3] : (byte)1;
		response[4] = (byte)(payloadBytes.Length >> 8);
		response[5] = (byte)payloadBytes.Length;
		response[6] = 17;
		response[7] = 0;
		if (request.Length >= 12) Buffer.BlockCopy (request, 8, response, 8, 4);
		response[12] = 0x5A;
		response[13] = 0x6B;
		response[14] = 0x7C;
		response[15] = 0x8D;
		Buffer.BlockCopy (payloadBytes, 0, response, 16, payloadBytes.Length);
		uint crc = ComputeCrc32 (response);
		response[12] = (byte)(crc >> 24);
		response[13] = (byte)(crc >> 16);
		response[14] = (byte)(crc >> 8);
		response[15] = (byte)crc;
		return response;
		}

	private static IPAddress GetLocalIPv4Address ()
		{
		using var socket = new Socket (AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
		socket.Connect (new IPEndPoint (IPAddress.Parse ("192.168.8.1"), 9));
		return ((IPEndPoint)socket.LocalEndPoint!).Address;
		}

	private static uint ComputeCrc32 (byte[] data)
		{
		uint crc = 0xFFFFFFFF;
		foreach (byte value in data)
			{
			crc ^= value;
			for (int bit = 0; bit < 8; bit++) crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320u : crc >> 1;
			}
		return ~crc;
		}
	}
