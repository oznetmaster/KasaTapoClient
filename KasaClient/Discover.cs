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
	// Coordinates concurrent ConnectAsync calls for the same device identity so that only one
	// physical connection attempt is in flight at a time; concurrent callers await and share the
	// same resulting KasaDevice instance instead of each opening an independent TCP connection.
	private static readonly ConcurrentDictionary<string, Task<KasaDevice>> _pendingConnects = new (StringComparer.OrdinalIgnoreCase);

	// Backs GetOrConnectSharedAsync only. This is an explicit, opt-in cache of long-lived shared
	// KasaDevice instances keyed by device identity (host/port); ConnectAsync never reads from or
	// writes to it. See the remarks on GetOrConnectSharedAsync for the ownership/disposal contract.
	private static readonly ConcurrentDictionary<string, KasaDevice> _sharedDevices = new (StringComparer.OrdinalIgnoreCase);

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
	/// See the remarks on <see cref="ConnectAsync(DeviceConfiguration, bool, CancellationToken)"/>
	/// for details on in-flight connect coalescing.
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
	/// If a connect for this device identity (host/port) is already in flight from another
	/// concurrent caller, this call joins that same in-flight attempt and returns its resulting
	/// <see cref="KasaDevice"/> instance instead of opening a second, redundant connection. This
	/// coalescing only applies while a connect is actively in progress; it does not cache or
	/// share device instances across separate, non-overlapping calls; "updateState" only
	/// applies to the caller that actually initiates the underlying connect. Each call that
	/// does not overlap an in-flight connect for the same identity receives its own new,
	/// independently owned <see cref="KasaDevice"/> instance, which the caller is responsible
	/// for disposing when done.
	/// </remarks>
	public static Task<KasaDevice> ConnectAsync (DeviceConfiguration configuration, bool updateState, CancellationToken cancellationToken = default)
		{
		string key = CreateConnectKey (configuration);

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
			// Only the connect's own entry is removed, and only once, so a fresh connect is
			// attempted on the next call after this one completes (success or failure) - the
			// dictionary exists purely to de-duplicate concurrent in-flight connects, not to
			// cache devices indefinitely (that remains the caller's own responsibility).
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
	/// Returns a long-lived <see cref="KasaDevice"/> instance shared by every caller that requests
	/// the same device identity (host/port), connecting only if no live shared instance already
	/// exists for that identity.
	/// </summary>
	/// <param name="configuration">The device configuration.</param>
	/// <param name="updateState"><see langword="true"/> to load device state when a new connection is created; otherwise, <see langword="false"/>.</param>
	/// <param name="cancellationToken">The cancellation token for the operation.</param>
	/// <returns>A shared device instance.</returns>
	/// <remarks>
	/// <para>
	/// Unlike <see cref="ConnectAsync(DeviceConfiguration, bool, CancellationToken)"/>, which always
	/// returns an instance newly and exclusively owned by the calling code (except while a connect
	/// is actively in flight), this method is an explicit, opt-in way for multiple, independent call
	/// sites that are known to target the same device to reuse a single connection instead of each
	/// opening their own. This matters for devices that reject or reset additional concurrent
	/// sessions.
	/// </para>
	/// <para>
	/// <b>The returned instance is shared. Do not call <see cref="KasaDevice.Dispose"/> on it unless
	/// you are certain no other caller still depends on it</b> - disposing a shared instance affects
	/// every other holder immediately. If you need an instance that only you own and control the
	/// lifetime of, use <see cref="ConnectAsync(DeviceConfiguration, bool, CancellationToken)"/>
	/// instead.
	/// </para>
	/// <para>
	/// If the previously cached instance for this identity has been disposed (by any holder), this
	/// method transparently creates and caches a fresh replacement rather than returning a dead
	/// instance - the same recovery model <c>LegacyTransport</c> already uses for stale/idle
	/// connections. There is no reference counting; callers are responsible for coordinating who, if
	/// anyone, disposes the shared instance and when.
	/// </para>
	/// </remarks>
	public static async Task<KasaDevice> GetOrConnectSharedAsync (DeviceConfiguration configuration, bool updateState = true, CancellationToken cancellationToken = default)
		{
		string key = CreateConnectKey (configuration);

		if (_sharedDevices.TryGetValue (key, out KasaDevice? existingDevice) && !existingDevice.IsDisposed)
			{
			return existingDevice;
			}

		KasaDevice device = await ConnectAsync (configuration, updateState, cancellationToken).ConfigureAwait (false);

		// Last-writer-wins: if two callers race here, both connect independently (still coalesced by
		// ConnectAsync's in-flight de-dup when truly concurrent), and whichever publishes last is the
		// instance future GetOrConnectSharedAsync callers observe.
		_sharedDevices[key] = device;
		return device;
		}

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
