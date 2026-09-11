// Copyright © 2026 Neil Colvin.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using NUnit.Framework;

// Preserve the original per-test fixture isolation when running on either host.
[assembly: FixtureLifeCycle (LifeCycle.InstancePerTestCase)]