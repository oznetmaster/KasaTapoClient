// Copyright © 2026 Neil Colvin.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

using KasaTapoClient;

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
	}
