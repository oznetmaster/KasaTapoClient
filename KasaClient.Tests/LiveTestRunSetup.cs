// Copyright © 2026 Neil Colvin.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using NUnit.Framework;

namespace KasaClient.Tests;

// NUnit executes this scope for every run, including a filtered Run selection.
[SetUpFixture]
public sealed class LiveTestRunSetup
	{
	[OneTimeSetUp]
	public static void BeginRun () => LiveTestSupport.BeginRun ();

	[OneTimeTearDown]
	public static void EndRun () => LiveTestSupport.EndRun ();
	}