// Copyright © 2026 Neil Colvin.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using KasaTapoClient;

using NUnit.Framework;

namespace KasaClient.Tests;

[TestFixture]
public sealed class LiveDiscoveryCacheTests
	{
	[Test]
	public async Task DifferentDevices_ShareOneScan ()
		{
		int scans = 0;
		var cache = new LiveDiscoveryCache (_ =>
			{
				++scans;
				return Results (Device ("one", "192.0.2.1"), Device ("two", "192.0.2.2"));
			});
		Assert.That ((await cache.ResolveAsync (Target ("one"), TimeSpan.Zero)).FromCache, Is.False);
		Assert.That ((await cache.ResolveAsync (Target ("two"), TimeSpan.Zero)).FromCache, Is.True);
		Assert.That (scans, Is.EqualTo (1));
		}

	[Test]
	public async Task DeviceMissingFromSnapshot_TriggersRediscovery ()
		{
		int scans = 0;
		var cache = new LiveDiscoveryCache (_ => ++scans == 1
			? Results (Device ("one", "192.0.2.1"))
			: Results (Device ("two", "192.0.2.2")));
		await cache.ResolveAsync (Target ("one"), TimeSpan.Zero);
		Assert.That ((await cache.ResolveAsync (Target ("two"), TimeSpan.Zero)).Device.Host, Is.EqualTo ("192.0.2.2"));
		Assert.That (scans, Is.EqualTo (2));
		}

	[Test]
	public async Task FailedConnectionRefresh_FollowsAddressChange ()
		{
		int scans = 0;
		var cache = new LiveDiscoveryCache (_ => Results (Device ("one", ++scans == 1 ? "192.0.2.1" : "192.0.2.2")));
		await cache.ResolveAsync (Target ("one"), TimeSpan.Zero);
		Assert.That ((await cache.ResolveAsync (Target ("one"), TimeSpan.Zero, refresh: true)).Device.Host, Is.EqualTo ("192.0.2.2"));
		Assert.That (scans, Is.EqualTo (2));
		}

	[Test]
	public async Task FailedRefresh_DoesNotReuseOldSnapshot ()
		{
		int scans = 0;
		var cache = new LiveDiscoveryCache (_ => ++scans == 2
			? Task.FromException<IReadOnlyList<DiscoveryResult>> (new TimeoutException ())
			: Results (Device ("one", scans == 1 ? "192.0.2.1" : "192.0.2.3")));
		await cache.ResolveAsync (Target ("one"), TimeSpan.Zero);
		Assert.ThrowsAsync<TimeoutException> (async () => await cache.ResolveAsync (Target ("one"), TimeSpan.Zero, refresh: true));
		Assert.That ((await cache.ResolveAsync (Target ("one"), TimeSpan.Zero)).Device.Host, Is.EqualTo ("192.0.2.3"));
		Assert.That (scans, Is.EqualTo (3));
		}

	[Test]
	public async Task ConcurrentRequests_ShareThePendingScan ()
		{
		int scans = 0;
		var reply = new TaskCompletionSource<IReadOnlyList<DiscoveryResult>> (TaskCreationOptions.RunContinuationsAsynchronously);
		var cache = new LiveDiscoveryCache (_ => { ++scans; return reply.Task; });
		var first = cache.ResolveAsync (Target ("one"), TimeSpan.Zero);
		var second = cache.ResolveAsync (Target ("two"), TimeSpan.Zero);
		reply.SetResult ([Device ("one", "192.0.2.1"), Device ("two", "192.0.2.2")]);
		await Task.WhenAll (first, second);
		Assert.That (scans, Is.EqualTo (1));
		Assert.That (second.Result.FromCache, Is.True);
		}

	private static LiveDeviceTarget Target (string id) => new (id, null, null);
	private static Task<IReadOnlyList<DiscoveryResult>> Results (params DiscoveryResult[] devices) => Task.FromResult<IReadOnlyList<DiscoveryResult>> (devices);
	private static DiscoveryResult Device (string id, string host) => new (host, DeviceType.Plug, null, "Synthetic", id, "{}", DeviceTransportKind.LegacyXor, false, 9999, null);
	}