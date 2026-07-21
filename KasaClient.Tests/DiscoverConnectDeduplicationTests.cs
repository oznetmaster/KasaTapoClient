// Copyright © 2026 Neil Colvin.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

using KasaTapoClient;
using KasaTapoClient.Internal;

namespace KasaClient.Tests;

[TestClass]
public sealed class DiscoverConnectDeduplicationTests
	{
	private static async Task<bool> ConnectAndExpectFailureAsync (Task<KasaDevice> connectTask)
		{
		try
			{
			await connectTask.ConfigureAwait (false);
			return false;
			}
		catch
			{
			return true;
			}
		}

	[TestMethod]
	public async Task ConnectAsync_ConcurrentCallsForSameDevice_ShareSingleConnectionAttempt ()
		{
		var listener = new TcpListener (IPAddress.Loopback, 0);
		listener.Start ();
		int port = ((IPEndPoint) listener.LocalEndpoint).Port;

		int acceptedConnectionCount = 0;
		using var acceptCancellation = new CancellationTokenSource ();

		Task acceptLoop = Task.Run (async () =>
			{
			try
				{
				while (!acceptCancellation.IsCancellationRequested)
					{
					TcpClient client = await listener.AcceptTcpClientAsync ().ConfigureAwait (false);
					Interlocked.Increment (ref acceptedConnectionCount);

					// Never respond so the transport read eventually times out on its own; just
					// keep the socket open briefly before dropping it so it does not look like an
					// immediate reset to the caller.
					_ = Task.Delay (TimeSpan.FromMilliseconds (200)).ContinueWith (_ => client.Dispose (), TaskScheduler.Default);
					}
				}
			catch (ObjectDisposedException)
				{
				}
			catch (SocketException)
				{
				}
			});

		try
			{
			var connectionOptions = new DeviceConnectionOptions (DeviceTransportKind.LegacyXor);
			var configuration = new DeviceConfiguration (
				"127.0.0.1",
				port,
				credentials: null,
				connectionOptions: connectionOptions,
				timeout: TimeSpan.FromMilliseconds (300));

			// Neither call is awaited before the second is issued, so both run against the exact
			// same dictionary state: the first call registers the in-flight connect synchronously
			// before yielding on real I/O, and the second call must observe that registration and
			// join it instead of dialing its own connection.
			Task<KasaDevice> firstConnect = Discover.ConnectAsync (configuration, updateState: true);
			Task<KasaDevice> secondConnect = Discover.ConnectAsync (configuration, updateState: true);

			bool firstFailed = await ConnectAndExpectFailureAsync (firstConnect).ConfigureAwait (false);
			bool secondFailed = await ConnectAndExpectFailureAsync (secondConnect).ConfigureAwait (false);

			Assert.IsTrue (firstFailed, "The first connect was expected to fail against the fake, non-responsive listener.");
			Assert.IsTrue (secondFailed, "The second connect was expected to fail against the fake, non-responsive listener.");
			}
		finally
			{
			acceptCancellation.Cancel ();
			listener.Stop ();
			try
				{
				await acceptLoop.ConfigureAwait (false);
				}
			catch
				{
				}
			}

		Assert.AreEqual (1, acceptedConnectionCount, "Concurrent ConnectAsync calls for the same device identity should share a single physical connection attempt.");
		}

	[TestMethod]
	public async Task ConnectAsync_SequentialCallsForSameDevice_EachOpenOwnConnection ()
		{
		var listener = new TcpListener (IPAddress.Loopback, 0);
		listener.Start ();
		int port = ((IPEndPoint) listener.LocalEndpoint).Port;

		int acceptedConnectionCount = 0;
		using var acceptCancellation = new CancellationTokenSource ();

		Task acceptLoop = Task.Run (async () =>
			{
			try
				{
				while (!acceptCancellation.IsCancellationRequested)
					{
					TcpClient client = await listener.AcceptTcpClientAsync ().ConfigureAwait (false);
					Interlocked.Increment (ref acceptedConnectionCount);
					client.Dispose ();
					}
				}
			catch (ObjectDisposedException)
				{
				}
			catch (SocketException)
				{
				}
			});

		try
			{
			var connectionOptions = new DeviceConnectionOptions (DeviceTransportKind.LegacyXor);
			var configuration = new DeviceConfiguration (
				"127.0.0.1",
				port,
				credentials: null,
				connectionOptions: connectionOptions,
				timeout: TimeSpan.FromMilliseconds (300));

			// Once the first ConnectAsync has fully completed (successfully or not), its
			// dictionary entry is removed, so a later, non-overlapping call must perform its own
			// connection attempt rather than incorrectly reusing a stale cache entry.
			bool firstFailed = await ConnectAndExpectFailureAsync (Discover.ConnectAsync (configuration, updateState: true)).ConfigureAwait (false);
			bool secondFailed = await ConnectAndExpectFailureAsync (Discover.ConnectAsync (configuration, updateState: true)).ConfigureAwait (false);

			Assert.IsTrue (firstFailed, "The first connect was expected to fail against the fake, non-responsive listener.");
			Assert.IsTrue (secondFailed, "The second connect was expected to fail against the fake, non-responsive listener.");
			}
		finally
			{
			acceptCancellation.Cancel ();
			listener.Stop ();
			try
				{
				await acceptLoop.ConfigureAwait (false);
				}
			catch
				{
				}
			}

		Assert.AreEqual (2, acceptedConnectionCount, "Non-overlapping ConnectAsync calls for the same device identity should each open their own connection.");
		}

	[TestMethod]
	public async Task GetOrConnectSharedAsync_RepeatedCallsForSameDevice_ReuseSharedInstanceUntilDisposed ()
		{
		var listener = new TcpListener (IPAddress.Loopback, 0);
		listener.Start ();
		int port = ((IPEndPoint) listener.LocalEndpoint).Port;

		int acceptedConnectionCount = 0;
		using var acceptCancellation = new CancellationTokenSource ();

		const string sysInfoResponse = "{\"system\":{\"get_sysinfo\":{\"alias\":\"Test Plug\",\"model\":\"HS100\",\"deviceId\":\"device-1\",\"relay_state\":1,\"on_time\":120}}}";

		Task acceptLoop = Task.Run (async () =>
			{
			try
				{
				while (!acceptCancellation.IsCancellationRequested)
					{
					TcpClient client = await listener.AcceptTcpClientAsync ().ConfigureAwait (false);
					Interlocked.Increment (ref acceptedConnectionCount);
					_ = ServeLegacyRequestsAsync (client, sysInfoResponse, acceptCancellation.Token);
					}
				}
			catch (ObjectDisposedException)
				{
				}
			catch (SocketException)
				{
				}
			});

		try
			{
			var connectionOptions = new DeviceConnectionOptions (DeviceTransportKind.LegacyXor);
			var configuration = new DeviceConfiguration (
				"127.0.0.1",
				port,
				credentials: null,
				connectionOptions: connectionOptions,
				timeout: TimeSpan.FromSeconds (2));

			KasaDevice firstDevice = await Discover.GetOrConnectSharedAsync (configuration, updateState: true).ConfigureAwait (false);
			KasaDevice secondDevice = await Discover.GetOrConnectSharedAsync (configuration, updateState: true).ConfigureAwait (false);

			Assert.AreSame (firstDevice, secondDevice, "Non-overlapping GetOrConnectSharedAsync calls for an already-connected device identity should reuse the same shared instance.");
			Assert.AreEqual (1, acceptedConnectionCount, "Reusing the cached shared device should not open a second connection.");

			firstDevice.Dispose ();

			KasaDevice thirdDevice = await Discover.GetOrConnectSharedAsync (configuration, updateState: true).ConfigureAwait (false);

			Assert.AreNotSame (firstDevice, thirdDevice, "Once the shared instance is disposed, the next GetOrConnectSharedAsync call should replace it with a fresh instance.");
			Assert.AreEqual (2, acceptedConnectionCount, "A fresh connection should be opened once the previously cached shared device was disposed.");

			thirdDevice.Dispose ();
			}
		finally
			{
			acceptCancellation.Cancel ();
			listener.Stop ();
			try
				{
				await acceptLoop.ConfigureAwait (false);
				}
			catch
				{
				}
			}
		}

	[TestMethod]
	public async Task ConnectAsync_ConcurrentCallsWithMismatchedConfigurations_EachOpenOwnConnection ()
		{
		var listener = new TcpListener (IPAddress.Loopback, 0);
		listener.Start ();
		int port = ((IPEndPoint) listener.LocalEndpoint).Port;

		int acceptedConnectionCount = 0;
		using var acceptCancellation = new CancellationTokenSource ();

		Task acceptLoop = Task.Run (async () =>
			{
			try
				{
				while (!acceptCancellation.IsCancellationRequested)
					{
					TcpClient client = await listener.AcceptTcpClientAsync ().ConfigureAwait (false);
					Interlocked.Increment (ref acceptedConnectionCount);

					// Never respond so the transport read eventually times out on its own; just
					// keep the socket open briefly before dropping it so it does not look like an
					// immediate reset to the caller.
					_ = Task.Delay (TimeSpan.FromMilliseconds (200)).ContinueWith (_ => client.Dispose (), TaskScheduler.Default);
					}
				}
			catch (ObjectDisposedException)
				{
				}
			catch (SocketException)
				{
				}
			});

		try
			{
			var connectionOptions = new DeviceConnectionOptions (DeviceTransportKind.LegacyXor);

			// Same host/port identity, but materially different configuration (credentials).
			// These must NOT be coalesced into a single connection attempt even though both
			// calls are issued concurrently for the same device identity.
			var firstConfiguration = new DeviceConfiguration (
				"127.0.0.1",
				port,
				credentials: new DeviceCredentials ("user-one", "password-one"),
				connectionOptions: connectionOptions,
				timeout: TimeSpan.FromMilliseconds (300));
			var secondConfiguration = new DeviceConfiguration (
				"127.0.0.1",
				port,
				credentials: new DeviceCredentials ("user-two", "password-two"),
				connectionOptions: connectionOptions,
				timeout: TimeSpan.FromMilliseconds (300));

			Task<KasaDevice> firstConnect = Discover.ConnectAsync (firstConfiguration, updateState: true);
			Task<KasaDevice> secondConnect = Discover.ConnectAsync (secondConfiguration, updateState: true);

			bool firstFailed = await ConnectAndExpectFailureAsync (firstConnect).ConfigureAwait (false);
			bool secondFailed = await ConnectAndExpectFailureAsync (secondConnect).ConfigureAwait (false);

			Assert.IsTrue (firstFailed, "The first connect was expected to fail against the fake, non-responsive listener.");
			Assert.IsTrue (secondFailed, "The second connect was expected to fail against the fake, non-responsive listener.");
			}
		finally
			{
			acceptCancellation.Cancel ();
			listener.Stop ();
			try
				{
				await acceptLoop.ConfigureAwait (false);
				}
			catch
				{
				}
			}

		Assert.AreEqual (2, acceptedConnectionCount, "Concurrent ConnectAsync calls for the same device identity but with mismatched configurations should each open their own connection.");
		}

	private static async Task ServeLegacyRequestsAsync (TcpClient client, string responseJson, CancellationToken cancellationToken)
		{
		using (client)
			{
			try
				{
				NetworkStream stream = client.GetStream ();
				byte[] responseBytes = KasaCipher.EncryptWithHeader (responseJson);

				while (!cancellationToken.IsCancellationRequested)
					{
					byte[]? header = await ReadExactAsync (stream, 4, cancellationToken).ConfigureAwait (false);
					if (header is null)
						{
						return;
						}

					int requestLength = (header[0] << 24) | (header[1] << 16) | (header[2] << 8) | header[3];
					byte[]? requestBody = await ReadExactAsync (stream, requestLength, cancellationToken).ConfigureAwait (false);
					if (requestBody is null)
						{
						return;
						}

					await stream.WriteAsync (responseBytes, 0, responseBytes.Length, cancellationToken).ConfigureAwait (false);
					}
				}
			catch
				{
				}
			}
		}

	private static async Task<byte[]?> ReadExactAsync (NetworkStream stream, int count, CancellationToken cancellationToken)
		{
		var buffer = new byte[count];
		int offset = 0;
		while (offset < count)
			{
			int read = await stream.ReadAsync (buffer, offset, count - offset, cancellationToken).ConfigureAwait (false);
			if (read == 0)
				{
				return null;
				}

			offset += read;
			}

		return buffer;
		}
	}
