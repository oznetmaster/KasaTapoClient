// Copyright © 2026 Neil Colvin.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// Behavior modeled after the independent python-kasa project (https://github.com/python-kasa/python-kasa)
// for protocol/compatibility reference only; no python-kasa source was copied. See ATTRIBUTIONS.md.

using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using Newtonsoft.Json.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace KasaTapoClient.Internal;

internal sealed class LegacyTransport : IDisposableDeviceTransport
	{
	private const int HEADER_SIZE = 4;
	private const int MAX_RESPONSE_BYTES = 1024 * 1024;
	private static readonly TimeSpan IDLE_CONNECTION_LIFETIME = TimeSpan.FromSeconds (10);
	private readonly DeviceConfiguration _configuration;
	private readonly TimeSpan _timeout;
	private readonly SemaphoreSlim _connectionLock = new (1, 1);
	private TcpClient? _client;
	private NetworkStream? _stream;
	private DateTime _lastActivityUtc;
	private bool _disposed;

	public LegacyTransport (DeviceConfiguration configuration)
		{
		_configuration = configuration;
		_timeout = configuration.Timeout;
		}

	public async Task<string> SendAsync (string payload, CancellationToken cancellationToken)
		{
		if (_disposed)
			{
#if NET7_0_OR_GREATER
			ObjectDisposedException.ThrowIf (true, this);
#else
			throw new ObjectDisposedException (nameof (LegacyTransport));
#endif
			}

		await _connectionLock.WaitAsync (cancellationToken).ConfigureAwait (false);
		try
			{
			byte[] requestBytes = KasaCipher.EncryptWithHeader (payload);

			// Stale legacy connections are handled with two complementary safeguards. First,
			// EnsureConnectedAsync proactively drops any connection that has been idle longer than
			// IDLE_CONNECTION_LIFETIME, since the device may have already closed its end. Second, a
			// legacy device is free to close an idle connection at any time (even within that window),
			// so a reused connection failing on its first write/read is a normal, expected "idle
			// timeout on the far end" condition -- not a real offline/failure signal. When that happens
			// on a *reused* connection we transparently reconnect and retry once before giving up. Only
			// a failure against a freshly established connection indicates the device is genuinely
			// unreachable/offline.
			bool isReusedConnection = _client is { Connected: true } && _stream is not null
				&& DateTime.UtcNow - _lastActivityUtc <= IDLE_CONNECTION_LIFETIME;

			try
				{
				NetworkStream stream = await EnsureConnectedAsync (cancellationToken).ConfigureAwait (false);
				return await SendOverStreamAsync (stream, requestBytes, cancellationToken).ConfigureAwait (false);
				}
			catch (Exception) when (isReusedConnection)
				{
				// The reused connection was likely closed by the device while idle. Drop it and retry
				// once against a freshly established connection before treating this as a real failure.
				ResetConnection ();
				NetworkStream stream = await EnsureConnectedAsync (cancellationToken).ConfigureAwait (false);
				return await SendOverStreamAsync (stream, requestBytes, cancellationToken).ConfigureAwait (false);
				}
			}
		finally
			{
			_connectionLock.Release ();
			}
		}

	private async Task<string> SendOverStreamAsync (NetworkStream stream, byte[] requestBytes, CancellationToken cancellationToken)
		{
		try
			{
			await WriteAsync (stream, requestBytes, cancellationToken).ConfigureAwait (false);

			byte[] header = await ReadExactAsync (stream, HEADER_SIZE, cancellationToken).ConfigureAwait (false);
			int responseLength = ReadLength (header);
			if (responseLength is < 0 or > MAX_RESPONSE_BYTES)
				{
				throw new InvalidDataException ($"The device returned an invalid payload length of {responseLength} bytes.");
				}

			byte[] body = await ReadExactAsync (stream, responseLength, cancellationToken).ConfigureAwait (false);
			_lastActivityUtc = DateTime.UtcNow;
			return KasaCipher.Decrypt (body);
			}
		catch
			{
			// Any failure on an established connection (reset, timeout, protocol error) may leave the
			// socket in an unusable state. Drop it so the next call reconnects from scratch.
			ResetConnection ();
			throw;
			}
		}

	private async Task<NetworkStream> EnsureConnectedAsync (CancellationToken cancellationToken)
		{
		if (_client is { Connected: true } && _stream is not null)
			{
			if (DateTime.UtcNow - _lastActivityUtc <= IDLE_CONNECTION_LIFETIME)
				{
				return _stream;
				}

			// The connection has been idle long enough that the device may have closed it on its
			// end already. Proactively close and reconnect rather than risk a failed write/read
			// against a half-open socket.
			ResetConnection ();
			}

		ResetConnection ();
		var client = new TcpClient ();
		try
			{
			await ConnectAsync (client, _configuration.Host, _configuration.Port, cancellationToken).ConfigureAwait (false);
			}
		catch
			{
			client.Dispose ();
			throw;
			}

		_client = client;
		_stream = client.GetStream ();
		_lastActivityUtc = DateTime.UtcNow;
		return _stream;
		}

	private void ResetConnection ()
		{
		_stream?.Dispose ();
		_stream = null;
		_client?.Dispose ();
		_client = null;
		}

	public void Dispose ()
		{
		if (_disposed)
			{
			return;
			}

		_disposed = true;
		_connectionLock.Wait ();
		try
			{
			ResetConnection ();
			}
		finally
			{
			_connectionLock.Release ();
			}

		_connectionLock.Dispose ();
		}

	public Task<string> SendManyAsync (IReadOnlyList<string> commandJsonPayloads, CancellationToken cancellationToken)
		{
		if (commandJsonPayloads.Count == 0)
			{
			throw new ArgumentException ("At least one command payload is required.", nameof (commandJsonPayloads));
			}

		var merged = new JObject ();
		foreach (string payload in commandJsonPayloads)
			{
			JsonSupport.MergeObjects (merged, JsonSupport.ParseObject (payload));
			}

		return SendAsync (merged.ToJsonString (JsonSupport.COMPACT_JSON), cancellationToken);
		}

	private async Task ConnectAsync (TcpClient client, string host, int port, CancellationToken cancellationToken)
		{
#if NET10_0_OR_GREATER
		using CancellationTokenSource timeoutSource = CancellationTokenSource.CreateLinkedTokenSource (cancellationToken);
		timeoutSource.CancelAfter (_timeout);
		try
			{
			await client.ConnectAsync (host, port, timeoutSource.Token).ConfigureAwait (false);
			}
		catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
			{
			throw new TimeoutException ($"Timed out while connecting to {host}:{port}.");
			}
#else
		// TcpClient.ConnectAsync(string, int) performs synchronous DNS resolution on the calling
		// thread before the returned Task even exists, so a slow/hanging DNS lookup would block this
		// method (and starve the Task.WhenAny race below) before the timeout/cancellation could ever
		// take effect. Running the call on a background thread ensures the DNS phase cannot bypass
		// the timeout race.
		Task connectTask = Task.Run (() => client.ConnectAsync (host, port), CancellationToken.None);
		Task completedTask = await Task.WhenAny (connectTask, Task.Delay (_timeout, cancellationToken)).ConfigureAwait (false);
		if (completedTask != connectTask)
			{
			cancellationToken.ThrowIfCancellationRequested ();
			throw new TimeoutException ($"Timed out while connecting to {host}:{port}.");
			}

		await connectTask.ConfigureAwait (false);
#endif
		}

	private async Task WriteAsync (NetworkStream stream, byte[] buffer, CancellationToken cancellationToken)
		{
		using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource (cancellationToken);
		timeoutSource.CancelAfter (_timeout);
#if NET10_0_OR_GREATER
		await stream.WriteAsync(buffer.AsMemory(), timeoutSource.Token).ConfigureAwait(false);
#else
		using (timeoutSource.Token.Register (() => ResetConnection ()))
			{
			await stream.WriteAsync (buffer, 0, buffer.Length, timeoutSource.Token).ConfigureAwait (false);
			}
#endif
		}

	private async Task<byte[]> ReadExactAsync (NetworkStream stream, int length, CancellationToken cancellationToken)
		{
		var buffer = new byte[length];
		int offset = 0;
		while (offset < length)
			{
			using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource (cancellationToken);
			timeoutSource.CancelAfter (_timeout);
#if NET10_0_OR_GREATER
			int bytesRead = await stream.ReadAsync(buffer.AsMemory(offset, length - offset), timeoutSource.Token).ConfigureAwait(false);
#else
			int bytesRead;
			using (timeoutSource.Token.Register (() => ResetConnection ()))
				{
				bytesRead = await stream.ReadAsync (buffer, offset, length - offset, timeoutSource.Token).ConfigureAwait (false);
				}
#endif
			if (bytesRead == 0)
				{
				throw new EndOfStreamException ("The device closed the connection before the full response was received.");
				}

			offset += bytesRead;
			}

		return buffer;
		}

	private static int ReadLength (byte[] header)
		{
		if (header.Length != HEADER_SIZE)
			{
			throw new InvalidDataException ($"Expected a {HEADER_SIZE}-byte response header but received {header.Length} bytes.");
			}

		return (header[0] << 24)
			| (header[1] << 16)
			| (header[2] << 8)
			| header[3];
		}
	}
