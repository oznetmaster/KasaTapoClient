// Copyright © 2026 Neil Colvin.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System;

using KasaTapoClient;

using NUnit.Framework;

namespace KasaClient.Tests;

[TestFixture]
public sealed class LiveDeviceTargetTests
	{
	[Test]
	public void DeviceId_FollowsAddressChange_WithoutChangingTestIdentity ()
		{
		var target = new LiveDeviceTarget ("fixture-id", "Old alias", "192.0.2.10");
		string? key = target.Key;
		Assert.That (target.Select ([Device ("192.0.2.11", "fixture-id", "New alias")]).Host, Is.EqualTo ("192.0.2.11"));
		Assert.That (target.Select ([Device ("192.0.2.12", "fixture-id", "Another alias")]).Host, Is.EqualTo ("192.0.2.12"));
		Assert.That (target.Key, Is.EqualTo (key));
		}

	[Test]
	public void DeviceId_DoesNotFallBackToOldAddressOrAlias ()
		{
		var target = new LiveDeviceTarget ("wanted-id", "Demo", "192.0.2.10");
		Assert.Throws<TimeoutException> (() => target.Select ([Device ("192.0.2.10", "different-id", "Demo")]));
		}

	[Test]
	public void Alias_SelectsTheCurrentAddress ()
		{
		var target = new LiveDeviceTarget (null, "Demo light", "192.0.2.10");
		Assert.That (target.Select ([Device ("192.0.2.20", "light-id", "Demo light")]).Host, Is.EqualTo ("192.0.2.20"));
		Assert.That (target.Key, Is.EqualTo ("alias:Demo light"));
		}

	[Test]
	public void Alias_RejectsAmbiguousDevices ()
		{
		var target = new LiveDeviceTarget (null, "Demo", null);
		Assert.Throws<InvalidOperationException> (() => target.Select ([Device ("192.0.2.10", "one", "Demo"), Device ("192.0.2.11", "two", "Demo")]));
		}

	[Test]
	public void DeviceId_RejectsAmbiguousAddresses ()
		{
		var target = new LiveDeviceTarget ("same-id", null, null);
		Assert.Throws<InvalidOperationException> (() => target.Select ([Device ("192.0.2.10", "same-id", "Demo"), Device ("192.0.2.11", "same-id", "Demo")]));
		}

	[Test]
	public void MultipleProtocolReplies_PreserveTpapPreference ()
		{
		var target = new LiveDeviceTarget ("hub-id", null, null);
		DiscoveryResult preferred = Device ("192.0.2.10", "hub-id", "Hub", true);
		Assert.That (target.Select ([Device ("192.0.2.10", "hub-id", "Hub"), preferred]), Is.SameAs (preferred));
		}

	[Test]
	public void HostOnlySettings_KeepTheirExistingTestIdentity ()
		{
		var target = new LiveDeviceTarget (null, null, "192.0.2.10");
		Assert.That (target.Key, Is.EqualTo ("192.0.2.10"));
		Assert.That (target.UsesDiscovery, Is.False);
		}

	[Test]
	public void EmptySettings_DoNotCreateATestTarget ()
		{
		var target = new LiveDeviceTarget (" ", " ", " ");
		Assert.That (target.Key, Is.Null);
		Assert.That (target.UsesDiscovery, Is.False);
		}

	[Test]
	public void DeviceId_MatchesWithoutCaseSensitivity ()
		{
		var target = new LiveDeviceTarget ("  DEVICE-ID  ", null, null);
		Assert.That (target.Select ([Device ("192.0.2.10", "device-id", "Demo")]).Host, Is.EqualTo ("192.0.2.10"));
		}

	private static DiscoveryResult Device (string host, string deviceId, string alias, bool preferred = false) =>
		 new (host, DeviceType.Bulb, alias, "Synthetic", deviceId, "{}", DeviceTransportKind.LegacyXor, false, 9999, null, tpapPreferred: preferred);
	}