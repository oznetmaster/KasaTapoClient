// Copyright © 2026 Neil Colvin.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using KasaTapoClient;

namespace KasaClient.Tests;

internal sealed class LiveDiscoveryCache
	{
	private readonly Func<TimeSpan, Task<IReadOnlyList<DiscoveryResult>>> _discover;
	private readonly SemaphoreSlim _gate = new (1, 1);
	private IReadOnlyList<DiscoveryResult>? _results;

	internal LiveDiscoveryCache (Func<TimeSpan, Task<IReadOnlyList<DiscoveryResult>>> discover) => _discover = discover;

	internal async Task<(DiscoveryResult Device, bool FromCache)> ResolveAsync (LiveDeviceTarget target, TimeSpan timeout, bool refresh = false)
		{
		await _gate.WaitAsync ().ConfigureAwait (false);
		try
			{
			if (!refresh && _results != null)
				{
				try
					{
					return (target.Select (_results), true);
					}
				catch (TimeoutException)
					{
					// A device missing from the first scan may have come online since then.
					}
				}
			// Do not retain an old snapshot if rediscovery fails.
			_results = null;
			_results = await _discover (timeout).ConfigureAwait (false);
			return (target.Select (_results), false);
			}
		finally
			{
			_gate.Release ();
			}
		}
	}