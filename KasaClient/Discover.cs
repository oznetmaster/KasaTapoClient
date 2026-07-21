// Copyright © 2026 Neil Colvin.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// Behavior modeled after the independent python-kasa project (https://github.com/python-kasa/python-kasa)
// for protocol/compatibility reference only; no python-kasa source was copied. See ATTRIBUTIONS.md.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using KasaTapoClient.Internal;

namespace KasaTapoClient;

/// <summary>
/// Provides discovery helpers similar to python-kasa's <c>Discover</c> entry point.
/// </summary>
public static class Discover
	{
	private sealed class CachedDeviceEntry
		{
		public CachedDeviceEntry (KasaDevice device)
			{
			Device = device;
			}

		public KasaDevice Device { get; }
		}

	// Persistent cache: one live KasaDevice per Host:Port, shared by every caller/module for the
	// life of that connection. Entries are only replaced (never explicitly removed) once the
	// cached device is found to be disposed/unusable on a later access - there is no reference
	// counting and no explicit invalidation API. Any caller may Dispose() the shared instance;
	// the next caller to ask for that key simply notices IsDisposed and transparently connects
	// and caches a fresh replacement, mirroring the existing stale/idle-connection recovery model
	// already used internally by LegacyTransport.
	private static readonly ConcurrentDictionary<string, CachedDeviceEntry> _connectedDevices = new (StringComparer.OrdinalIgnoreCase);

	// Coordinates concurrent ConnectAsync calls for the same device identity so that only one
	// physical connection attempt is in flight at a time; concurrent callers await and share the
	// same resulting KasaDevice instance instead of each opening an independent TCP connection.
	private static readonly ConcurrentDictionary<string, Task<KasaDevice>> _pendingConnects = new (StringComparer.OrdinalIgnoreCase);

	private static string CreateConnectKey (DeviceConfiguration configuration) =>
		$"{configuration.Host}:{configuration.Port}";
	/// <summary>
	/// Broadcasts a discovery request and returns all responses collected within the timeout window.
	/// </summary>
	/// <param name="timeout">The discovery timeout. If <see langword="null" />, a three second timeout is used.</param>
	/// <param name="target">The discovery target address. The default is the IPv4 broadcast address.</param>
	/// <param name="cancellationToken">The cancellation token for the operation.</param>
	/// <returns>A read-only collection of discovery responses.</returns>
	/// <exception cref="OperationCanceledException">Thrown when the discovery operation is canceled.</exception>
	public static async Task<IReadOnlyList<DiscoveryResult>> DiscoverAsync (
		TimeSpan? timeout = null,
		string target = "255.255.255.255",
		CancellationToken cancellationToken = default)
		{
		var client = new DiscoveryClient (timeout ?? TimeSpan.FromSeconds (3));
		return await client.DiscoverAsync (target, cancellationToken).ConfigureAwait (false);
		}

	/// <summary>
	/// Broadcasts only the legacy UDP discovery request on port 9999.
	/// </summary>
	public static async Task<IReadOnlyList<DiscoveryResult>> DiscoverLegacyAsync (
		TimeSpan? timeout = null,
		string target = "255.255.255.255",
		CancellationToken cancellationToken = default)
		{
		var client = new DiscoveryClient (timeout ?? TimeSpan.FromSeconds (3));
		return await client.DiscoverLegacyAsync (target, cancellationToken).ConfigureAwait (false);
		}

	/// <summary>
	/// Connects to a single device host and returns a ready-to-use device instance.
	/// </summary>
	/// <param name="host">The device host name or IP address.</param>
	/// <param name="port">The device control port.</param>
	/// <param name="credentials">Optional credentials used by newer authenticated devices.</param>
	/// <param name="connectionOptions">Transport-specific connection options used to select the device protocol.</param>
	/// <param name="timeout">The per-operation timeout. If <see langword="null" />, a five second timeout is used.</param>
	/// <param name="cancellationToken">The cancellation token for the operation.</param>
	/// <returns>A device instance with current system information loaded.</returns>
	/// <exception cref="TimeoutException">Thrown when automatic transport resolution cannot obtain a matching discovery result for <paramref name="host" />.</exception>
	public static async Task<KasaDevice> DiscoverSingleAsync (
		string host,
		int port = 9999,
		DeviceCredentials? credentials = null,
		DeviceConnectionOptions? connectionOptions = null,
		TimeSpan? timeout = null,
		CancellationToken cancellationToken = default)
		{
		DeviceConnectionOptions resolvedOptions = connectionOptions ?? new DeviceConnectionOptions ();
		if (resolvedOptions.TransportKind != DeviceTransportKind.Auto)
			{
			return await ConnectAsync (new DeviceConfiguration (host, port, credentials, resolvedOptions, timeout), cancellationToken).ConfigureAwait (false);
			}

		try
			{
				IReadOnlyList<DiscoveryResult> discoveryResults = await DiscoverAsync (timeout, host, cancellationToken).ConfigureAwait (false);
				foreach (DiscoveryResult discoveryResult in discoveryResults)
					{
					if (!string.Equals (discoveryResult.Host, host, StringComparison.OrdinalIgnoreCase))
						{
						continue;
						}

					DeviceConfiguration discoveredConfiguration = CreateConfigurationFromDiscoveryResult (discoveryResult, credentials, timeout);
					return await ConnectAsync (discoveredConfiguration, cancellationToken).ConfigureAwait (false);
					}
				}
		catch
			{
				throw;
				}

		throw new TimeoutException ($"Discovery did not return a device configuration for '{host}'. Automatic fallback to legacy transport was suppressed to avoid forcing an incorrect 9999 connection.");
		}

	/// <summary>
	/// Connects using a complete device configuration and returns a ready-to-use device instance.
	/// </summary>
	/// <param name="configuration">The device configuration.</param>
	/// <param name="cancellationToken">The cancellation token for the operation.</param>
	/// <returns>A device instance with current system information loaded.</returns>
	/// <exception cref="TimeoutException">Thrown when automatic transport resolution cannot obtain a matching discovery result for the configured host.</exception>
	/// <remarks>
	/// The returned instance may be shared with other callers for the same host/port; see the
	/// remarks on <see cref="ConnectAsync(DeviceConfiguration, bool, CancellationToken)"/> for details.
	/// </remarks>
	public static async Task<KasaDevice> ConnectAsync (DeviceConfiguration configuration, CancellationToken cancellationToken = default)
		=> await ConnectAsync (configuration, updateState: true, cancellationToken).ConfigureAwait (false);

	/// <summary>
	/// Connects using a complete device configuration and optionally loads the initial device state.
	/// </summary>
	/// <param name="configuration">The device configuration.</param>
	/// <param name="updateState"><see langword="true" /> to load device state before returning; otherwise, <see langword="false" />.</param>
	/// <param name="cancellationToken">The cancellation token for the operation.</param>
	/// <returns>A device instance ready for direct commands.</returns>
	/// <exception cref="TimeoutException">Thrown when automatic transport resolution cannot obtain a matching discovery result for the configured host.</exception>
	/// <remarks>
	/// There is only ever one live device instance per host/port at a time: if a device for this
	/// identity is already connected (or is currently being connected by another concurrent call),
	/// this returns that same shared <see cref="KasaDevice"/> instance instead of opening a new
	/// connection. In that case <paramref name="updateState"/> has no effect - the existing
	/// instance's state is not refreshed as part of this call. Because the instance is shared,
	/// disposing it affects every other caller holding the same reference; the next call to
	/// <see cref="ConnectAsync(DeviceConfiguration, bool, CancellationToken)"/> for that identity
	/// detects the disposed instance and transparently connects and caches a fresh replacement.
	/// </remarks>
	public static Task<KasaDevice> ConnectAsync (DeviceConfiguration configuration, bool updateState, CancellationToken cancellationToken = default)
		{
		string key = CreateConnectKey (configuration);

		// Fastest path: reuse the existing live shared device for this identity, if one exists and
		// has not been disposed. This is what makes the cache persistent across non-concurrent
		// calls, not just calls that happen to race each other.
		if (_connectedDevices.TryGetValue (key, out CachedDeviceEntry? cachedEntry) && !cachedEntry.Device.IsDisposed)
			{
			return Task.FromResult (cachedEntry.Device);
			}

		// Fast path: join an in-flight connect for the same device identity instead of starting
		// a second, independent one.
		if (_pendingConnects.TryGetValue (key, out Task<KasaDevice>? existingConnect))
			{
			return AwaitSharedConnectAsync (existingConnect, cancellationToken);
			}

		var connectCompletionSource = new TaskCompletionSource<KasaDevice> (TaskCreationOptions.RunContinuationsAsynchronously);
		Task<KasaDevice> registeredConnect = _pendingConnects.GetOrAdd (key, connectCompletionSource.Task);

		if (!ReferenceEquals (registeredConnect, connectCompletionSource.Task))
			{
			// Another thread registered first between our TryGetValue and GetOrAdd; join theirs.
			return AwaitSharedConnectAsync (registeredConnect, cancellationToken);
			}

		return ConnectAndPublishAsync (configuration, updateState, key, connectCompletionSource, cancellationToken);
		}

	private static async Task<KasaDevice> ConnectAndPublishAsync (
		DeviceConfiguration configuration,
		bool updateState,
		string key,
		TaskCompletionSource<KasaDevice> connectCompletionSource,
		CancellationToken cancellationToken)
		{
		try
			{
			KasaDevice device = await ConnectCoreAsync (configuration, updateState, cancellationToken).ConfigureAwait (false);

			// Populate the persistent cache so future (non-concurrent) callers for this identity
			// reuse this instance instead of connecting again.
			_connectedDevices[key] = new CachedDeviceEntry (device);

			connectCompletionSource.TrySetResult (device);
			return device;
			}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
			{
			// A caller-initiated cancellation should not fault the shared task for OTHER
			// concurrent callers who did not cancel; only propagate to this caller.
			connectCompletionSource.TrySetCanceled (cancellationToken);
			throw;
			}
		catch (Exception ex)
			{
			connectCompletionSource.TrySetException (ex);
			throw;
			}
		finally
			{
			// Only the connect's own entry is removed, and only once, so a fresh reconnect is
			// attempted on the next call after this one completes (success or failure) - the
			// cache exists purely to de-duplicate concurrent in-flight connects, not to cache
			// devices indefinitely (that remains the caller's responsibility, as today).
			((ICollection<KeyValuePair<string, Task<KasaDevice>>>) _pendingConnects).Remove (
				new KeyValuePair<string, Task<KasaDevice>> (key, connectCompletionSource.Task));
			}
		}

	// Awaits a shared in-flight connect while allowing this caller's own cancellationToken to
	// stop *this* caller's wait without canceling the underlying shared connect (which other,
	// still-waiting callers may depend on). Implemented manually (rather than via
	// Task<T>.WaitAsync) since this library targets .NET Framework 4.7.2, where that BCL
	// extension is unavailable.
	private static async Task<KasaDevice> AwaitSharedConnectAsync (Task<KasaDevice> sharedConnect, CancellationToken cancellationToken)
		{
		if (!cancellationToken.CanBeCanceled)
			{
			return await sharedConnect.ConfigureAwait (false);
			}

		var cancellationCompletionSource = new TaskCompletionSource<bool> (TaskCreationOptions.RunContinuationsAsynchronously);
		using (cancellationToken.Register (static state => ((TaskCompletionSource<bool>) state!).TrySetResult (true), cancellationCompletionSource))
			{
			Task completedTask = await Task.WhenAny (sharedConnect, cancellationCompletionSource.Task).ConfigureAwait (false);
			if (completedTask == cancellationCompletionSource.Task)
				{
				cancellationToken.ThrowIfCancellationRequested ();
				}

			return await sharedConnect.ConfigureAwait (false);
			}
		}

	private static async Task<KasaDevice> ConnectCoreAsync (DeviceConfiguration configuration, bool updateState, CancellationToken cancellationToken)
		{
		if (configuration.ConnectionOptions.TransportKind == DeviceTransportKind.Auto)
			{
			DeviceConfiguration resolvedConfiguration = await ResolveAutoConfigurationAsync (configuration, cancellationToken).ConfigureAwait (false);
			var resolvedDevice = new KasaDevice (resolvedConfiguration);
			if (updateState)
				{
				await resolvedDevice.UpdateAsync (cancellationToken).ConfigureAwait (false);
				}
			return resolvedDevice;
			}

		var device = new KasaDevice (configuration);
		if (updateState)
			{
			await device.UpdateAsync (cancellationToken).ConfigureAwait (false);
			}
		return device;
		}

	/// <summary>
	/// Connects to a discovered device using the connection parameters parsed from its discovery result.
	/// </summary>
	/// <param name="discoveryResult">The discovery result to connect with.</param>
	/// <param name="updateState"><see langword="true" /> to load device state before returning; otherwise, <see langword="false" />.</param>
	/// <param name="credentials">Optional credentials used by newer authenticated devices.</param>
	/// <param name="timeout">The per-operation timeout. If <see langword="null" />, a five second timeout is used.</param>
	/// <param name="cancellationToken">The cancellation token for the operation.</param>
	/// <returns>A connected device instance.</returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="discoveryResult" /> is <see langword="null" />.</exception>
	#pragma warning disable CA1510
	public static Task<KasaDevice> ConnectAsync (
		DiscoveryResult discoveryResult,
		bool updateState = true,
		DeviceCredentials? credentials = null,
		TimeSpan? timeout = null,
		CancellationToken cancellationToken = default)
		{
		if (discoveryResult is null)
			{
			throw new ArgumentNullException (nameof (discoveryResult));
			}

		DeviceConfiguration configuration = CreateConfigurationFromDiscoveryResult (discoveryResult, credentials, timeout);
		return ConnectAsync (configuration, updateState, cancellationToken);
		}
	#pragma warning restore CA1510

	/// <summary>
	/// Creates a device configuration directly from a discovery result.
	/// </summary>
	/// <param name="discoveryResult">The discovery result to convert.</param>
	/// <param name="credentials">Optional credentials used by newer authenticated devices.</param>
	/// <param name="timeout">The per-operation timeout. If <see langword="null" />, a five second timeout is used.</param>
	/// <returns>A device configuration derived from the discovery metadata.</returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="discoveryResult" /> is <see langword="null" />.</exception>
	#pragma warning disable CA1510
	public static DeviceConfiguration CreateConfiguration (DiscoveryResult discoveryResult, DeviceCredentials? credentials = null, TimeSpan? timeout = null)
		{
		if (discoveryResult is null)
			{
			throw new ArgumentNullException (nameof (discoveryResult));
			}

		return CreateConfigurationFromDiscoveryResult (discoveryResult, credentials, timeout);
		}
	#pragma warning restore CA1510

	private static async Task<DeviceConfiguration> ResolveAutoConfigurationAsync (DeviceConfiguration configuration, CancellationToken cancellationToken)
		{
		IReadOnlyList<DiscoveryResult> discoveryResults = await DiscoverAsync (configuration.Timeout, configuration.Host, cancellationToken).ConfigureAwait (false);
		foreach (DiscoveryResult discoveryResult in discoveryResults)
			{
				if (string.Equals (discoveryResult.Host, configuration.Host, StringComparison.OrdinalIgnoreCase))
					{
					return CreateConfigurationFromDiscoveryResult (discoveryResult, configuration.Credentials, configuration.Timeout);
					}
			}

		throw new TimeoutException ($"Discovery did not return a device configuration for '{configuration.Host}'. Automatic fallback to legacy transport was suppressed to avoid forcing an incorrect 9999 connection.");
		}

	private static DeviceConfiguration CreateConfigurationFromDiscoveryResult (DiscoveryResult discoveryResult, DeviceCredentials? credentials, TimeSpan? timeout)
		{
		DeviceConnectionParameters? connectionParameters = discoveryResult.ConnectionParameters;
		DeviceTransportKind transportKind = connectionParameters?.TransportKind ?? discoveryResult.TransportKind;
		bool isTpap = connectionParameters?.EncryptionKind == DeviceEncryptionKind.Tpap;
		bool useSsl = isTpap
			? discoveryResult.TpapMetadata?.Tls is int tlsMode && tlsMode > 0
			: transportKind == DeviceTransportKind.HttpToken && discoveryResult.SupportsHttps;
		int? discoveredPort = isTpap
			? discoveryResult.TpapMetadata?.Port ?? (useSsl ? 443 : 80)
			: discoveryResult.Port;
		int port = discoveredPort ?? (transportKind == DeviceTransportKind.HttpToken
			? (useSsl ? 443 : 80)
			: 9999);
		var connectionOptions = new DeviceConnectionOptions (
			transportKind,
			connectionParameters: connectionParameters,
			useSsl: useSsl,
			useDefaultCredentials: false,
			defaultCredentialProfile: DefaultCredentialProfile.None,
			applicationPath: isTpap ? "/" : "/app",
			useSecurePassthrough: transportKind == DeviceTransportKind.HttpToken);
		return new DeviceConfiguration (discoveryResult.Host, port, credentials, connectionOptions, timeout);
		}
	}
