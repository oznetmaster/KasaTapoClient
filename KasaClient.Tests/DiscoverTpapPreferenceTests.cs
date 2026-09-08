// Copyright © 2026 Neil Colvin.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using KasaTapoClient;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace KasaClient.Tests;

[TestClass]
public sealed class DiscoverTpapPreferenceTests
	{
	private static DiscoveryResult CreateDiscoveryResult (
		DeviceConnectionParameters? connectionParameters,
		bool? tpapPreferred = null,
		TpapDiscoveryMetadata? tpapMetadata = null) =>
		new (
			host: "192.168.8.164",
			deviceType: DeviceType.Hub,
			alias: "Test Hub",
			model: "H100",
			deviceId: "TEST-DEVICE-ID",
			rawJson: "{}",
			transportKind: DeviceTransportKind.HttpToken,
			supportsHttps: false,
			port: 80,
			connectionParameters: connectionParameters,
			tpapMetadata: tpapMetadata,
			protocolVersion: 2,
			tpapPreferred: tpapPreferred);

	[TestMethod]
	public void PreferTpap_ReturnsNull_WhenNoConnectionParameters ()
		{
		var discoveryResult = CreateDiscoveryResult (connectionParameters: null, tpapPreferred: true);

		var result = Discover.PreferTpapConnectionParameters (discoveryResult);

		Assert.IsNull (result);
		}

	[TestMethod]
	public void PreferTpap_LeavesParametersUnchanged_WhenDeviceDoesNotAdvertiseTpap ()
		{
		// A stale KLAP profile on a device with no TPAP signal must not be rewritten.
		var connectionParameters = new DeviceConnectionParameters (DeviceFamilyKind.SmartTapoHub, DeviceEncryptionKind.Klap);
		var discoveryResult = CreateDiscoveryResult (connectionParameters);

		var result = Discover.PreferTpapConnectionParameters (discoveryResult);

		Assert.AreSame (connectionParameters, result);
		}

	[TestMethod]
	public void PreferTpap_PromotesHub_WhenTpapPreferredIsAdvertised ()
		{
		// Regression guard for the H100: discovery advertises tpap_preferred, but a cached profile
		// pinned the hub to KLAP, which caused handshake failures against an endpoint it never served.
		var connectionParameters = new DeviceConnectionParameters (DeviceFamilyKind.SmartTapoHub, DeviceEncryptionKind.Klap);
		var discoveryResult = CreateDiscoveryResult (connectionParameters, tpapPreferred: true);

		var result = Discover.PreferTpapConnectionParameters (discoveryResult);

		Assert.IsNotNull (result);
		Assert.AreEqual (DeviceEncryptionKind.Tpap, result!.EncryptionKind);
		Assert.AreEqual (DeviceFamilyKind.SmartTapoHub, result.DeviceFamily);
		}

	[TestMethod]
	public void PreferTpap_PromotesDevice_WhenOnlyTpapMetadataIsPresent ()
		{
		var connectionParameters = new DeviceConnectionParameters (DeviceFamilyKind.SmartTapoPlug, DeviceEncryptionKind.Klap);
		var tpapMetadata = new TpapDiscoveryMetadata (port: 80, pakeModes: new[] { 2 }, tls: 0, dac: 1, noc: 1);
		var discoveryResult = CreateDiscoveryResult (connectionParameters, tpapMetadata: tpapMetadata);

		var result = Discover.PreferTpapConnectionParameters (discoveryResult);

		Assert.IsNotNull (result);
		Assert.AreEqual (DeviceEncryptionKind.Tpap, result!.EncryptionKind);
		}

	[TestMethod]
	public void PreferTpap_UsesAdvertisedPortAndPlaintext_WhenTlsIsZero ()
		{
		var connectionParameters = new DeviceConnectionParameters (DeviceFamilyKind.SmartTapoHub, DeviceEncryptionKind.Klap);
		var tpapMetadata = new TpapDiscoveryMetadata (port: 80, pakeModes: new[] { 2 }, tls: 0, dac: 1, noc: 1);
		var discoveryResult = CreateDiscoveryResult (connectionParameters, tpapPreferred: true, tpapMetadata: tpapMetadata);

		var result = Discover.PreferTpapConnectionParameters (discoveryResult);

		Assert.IsNotNull (result);
		Assert.IsFalse (result!.UseHttps);
		Assert.AreEqual (80, result.HttpPort);
		}

	[TestMethod]
	public void PreferTpap_UsesHttpsAndPort443_WhenTlsIsEnabledWithoutAdvertisedPort ()
		{
		var connectionParameters = new DeviceConnectionParameters (DeviceFamilyKind.SmartTapoHub, DeviceEncryptionKind.Klap);
		var tpapMetadata = new TpapDiscoveryMetadata (port: null, pakeModes: new[] { 2 }, tls: 1, dac: 0, noc: 0);
		var discoveryResult = CreateDiscoveryResult (connectionParameters, tpapPreferred: true, tpapMetadata: tpapMetadata);

		var result = Discover.PreferTpapConnectionParameters (discoveryResult);

		Assert.IsNotNull (result);
		Assert.IsTrue (result!.UseHttps);
		Assert.AreEqual (443, result.HttpPort);
		}

	[TestMethod]
	public void PreferTpap_PreservesLoginVersion_WhenPromoting ()
		{
		var connectionParameters = new DeviceConnectionParameters (DeviceFamilyKind.SmartTapoHub, DeviceEncryptionKind.Klap, loginVersion: 2);
		var discoveryResult = CreateDiscoveryResult (connectionParameters, tpapPreferred: true);

		var result = Discover.PreferTpapConnectionParameters (discoveryResult);

		Assert.IsNotNull (result);
		Assert.AreEqual (2, result!.LoginVersion);
		}

	[TestMethod]
	public void PreferTpap_ReturnsExistingInstance_WhenAlreadyTpap ()
		{
		var connectionParameters = new DeviceConnectionParameters (DeviceFamilyKind.SmartTapoHub, DeviceEncryptionKind.Tpap);
		var discoveryResult = CreateDiscoveryResult (connectionParameters, tpapPreferred: true);

		var result = Discover.PreferTpapConnectionParameters (discoveryResult);

		Assert.AreSame (connectionParameters, result);
		}

	[TestMethod]
	[DataRow (DeviceFamilyKind.IotIpCamera, DeviceEncryptionKind.Xor)]
	[DataRow (DeviceFamilyKind.SmartIpCamera, DeviceEncryptionKind.Aes)]
	[DataRow (DeviceFamilyKind.SmartTapoDoorbell, DeviceEncryptionKind.Aes)]
	[DataRow (DeviceFamilyKind.SmartTapoRobovac, DeviceEncryptionKind.Aes)]
	public void PreferTpap_DoesNotPromoteStrictEncryptionFamilies (DeviceFamilyKind deviceFamily, DeviceEncryptionKind encryptionKind)
		{
		// DeviceTransportFactory pins these families to a required encryption kind. Promoting them
		// to TPAP would trade a working connection for a NotSupportedException.
		var connectionParameters = new DeviceConnectionParameters (deviceFamily, encryptionKind);
		var tpapMetadata = new TpapDiscoveryMetadata (port: 80, pakeModes: new[] { 2 }, tls: 0, dac: 1, noc: 1);
		var discoveryResult = CreateDiscoveryResult (connectionParameters, tpapPreferred: true, tpapMetadata: tpapMetadata);

		var result = Discover.PreferTpapConnectionParameters (discoveryResult);

		Assert.AreSame (connectionParameters, result);
		Assert.AreEqual (encryptionKind, result!.EncryptionKind);
		}
	}
