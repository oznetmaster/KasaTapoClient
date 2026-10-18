// Copyright © 2026 Neil Colvin.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Reflection;

using KasaTapoClient;
using KasaTapoClient.Internal;

using NUnit.Framework;

namespace KasaClient.Tests;

/// <summary>
/// Checks version selection against fixed KLAP challenge vectors without contacting a device.
/// </summary>
[TestFixture]
public sealed class KlapTransportTests
	{
	// Independently generated from the protocol's MD5/SHA1/SHA256 definitions, not KlapAuthHash.
	private const string V1_AUTH = "NtBmSxshaVdiElNF+SyjFw==";
	private const string V2_AUTH = "lnTN/HNm8fgs//Xs0dGvQRR+ctmcqp88lw/47tlmpM0=";
	private const string V1_CHALLENGE = "wg/sJA74zXlyHFbIlUtsyDC7UYu30lkTZQIpMhb+oPk=";
	private const string V2_CHALLENGE = "afk8gL3cN3eSNPReFJxk6qPbe4NTtsm6rChZHWpLOvU=";

	/// <summary>
	/// Legacy v1 remains the default; advertised v2 or later selects v2, while SMART remains v2.
	/// </summary>
	[TestCase (DeviceFamilyKind.IotSmartPlugSwitch, null, false)]
	[TestCase (DeviceFamilyKind.IotSmartPlugSwitch, 0, false)]
	[TestCase (DeviceFamilyKind.IotSmartPlugSwitch, 1, false)]
	[TestCase (DeviceFamilyKind.IotSmartPlugSwitch, 2, true)]
	[TestCase (DeviceFamilyKind.IotSmartPlugSwitch, 3, true)]
	[TestCase (DeviceFamilyKind.IotSmartBulb, null, false)]
	[TestCase (DeviceFamilyKind.IotSmartBulb, 1, false)]
	[TestCase (DeviceFamilyKind.IotSmartBulb, 2, true)]
	[TestCase (DeviceFamilyKind.IotSmartBulb, 3, true)]
	[TestCase (DeviceFamilyKind.IotIpCamera, null, false)]
	[TestCase (DeviceFamilyKind.IotIpCamera, 1, false)]
	[TestCase (DeviceFamilyKind.IotIpCamera, 2, true)]
	[TestCase (DeviceFamilyKind.IotIpCamera, 3, true)]
	[TestCase (DeviceFamilyKind.SmartTapoHub, null, true)]
	[TestCase (DeviceFamilyKind.SmartTapoHub, 1, true)]
	[TestCase (DeviceFamilyKind.SmartTapoHub, 2, true)]
	[TestCase (null, null, true)]
	public void ResolveHandshakeAuthHash_UsesAdvertisedVersion (DeviceFamilyKind? family, int? loginVersion, bool expectedV2)
		{
		using KlapTransport transport = CreateTransport (family, loginVersion);
		(byte[] authHash, bool v2) = ResolveChallenge (transport, Convert.FromBase64String (expectedV2 ? V2_CHALLENGE : V1_CHALLENGE));
		Assert.That (v2, Is.EqualTo (expectedV2));
		Assert.That (authHash, Is.EqualTo (Convert.FromBase64String (expectedV2 ? V2_AUTH : V1_AUTH)));
		}

	/// <summary>
	/// Version selection must never bypass verification of the server challenge.
	/// </summary>
	[TestCase (1)]
	[TestCase (2)]
	public void ResolveHandshakeAuthHash_RejectsInvalidSignature (int loginVersion)
		{
		using KlapTransport transport = CreateTransport (DeviceFamilyKind.IotSmartPlugSwitch, loginVersion);
		byte[] challenge = Convert.FromBase64String (loginVersion == 2 ? V2_CHALLENGE : V1_CHALLENGE);
		challenge[0] ^= 1;
		TargetInvocationException? exception = Assert.Throws<TargetInvocationException> (() => ResolveChallenge (transport, challenge));
		Assert.That (exception!.InnerException, Is.TypeOf<InvalidDataException> ());
		}

	private static KlapTransport CreateTransport (DeviceFamilyKind? family, int? loginVersion) => new (
		new DeviceConfiguration (
			"127.0.0.1",
			credentials: new DeviceCredentials ("offline@example.invalid", "synthetic-audit-password"),
			connectionOptions: new DeviceConnectionOptions (connectionParameters: family is DeviceFamilyKind kind
				? new DeviceConnectionParameters (kind, DeviceEncryptionKind.Klap, loginVersion)
				: null)));

	private static (byte[] AuthHash, bool V2) ResolveChallenge (KlapTransport transport, byte[] challenge)
		{
		byte[] localSeed = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16];
		byte[] remoteSeed = [16, 15, 14, 13, 12, 11, 10, 9, 8, 7, 6, 5, 4, 3, 2, 1];
		// Exercise the real resolver without adding a test-only transport API or using the LAN.
		MethodInfo resolver = typeof (KlapTransport).GetMethod ("ResolveHandshakeAuthHash", BindingFlags.NonPublic | BindingFlags.Instance)!;
		return ((byte[] AuthHash, bool V2))resolver.Invoke (transport, [localSeed, remoteSeed, challenge])!;
		}
	}