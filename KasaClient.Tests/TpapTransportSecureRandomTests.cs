// Copyright © 2026 Neil Colvin.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;

using KasaTapoClient.Internal;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace KasaClient.Tests;

[TestClass]
public sealed class TpapTransportSecureRandomTests
	{
	[TestMethod]
	public void CreateSecureRandom_CompletesQuickly ()
		{
		// Guards against a regression to SecureRandom's default parameterless constructor, whose
		// timing-based entropy estimator can take a very long time on slower/embedded CPUs. Seeding
		// via CryptoApiRandomGenerator should complete effectively instantly on any platform.
		var stopwatch = Stopwatch.StartNew ();

		var random = TpapTransport.CreateSecureRandom ();

		stopwatch.Stop ();

		Assert.IsNotNull (random);
		Assert.IsTrue (stopwatch.Elapsed < TimeSpan.FromSeconds (2), $"CreateSecureRandom took {stopwatch.Elapsed}, which suggests it is no longer using a fast seeding path.");
		}

	[TestMethod]
	public void CreateSecureRandom_ProducesNonZeroRandomBytes ()
		{
		var random = TpapTransport.CreateSecureRandom ();

		byte[] bytes = new byte[32];
		random.NextBytes (bytes);

		Assert.IsTrue (Array.Exists (bytes, b => b != 0), "Generated random bytes were all zero.");
		}

	[TestMethod]
	public void CreateSecureRandom_ReturnsDistinctInstancesWithDifferentOutput ()
		{
		var first = TpapTransport.CreateSecureRandom ();
		var second = TpapTransport.CreateSecureRandom ();

		byte[] firstBytes = new byte[32];
		byte[] secondBytes = new byte[32];
		first.NextBytes (firstBytes);
		second.NextBytes (secondBytes);

		CollectionAssert.AreNotEqual (firstBytes, secondBytes);
		}
	}
