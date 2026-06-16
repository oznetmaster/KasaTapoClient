// Copyright © 2026 Neil Colvin.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Threading.Tasks;

using KasaTapoClient;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace KasaClient.Tests;

[TestClass]
[TestCategory("Live")]
[DoNotParallelize]
public sealed class LiveHubIntegrationTests
	{
	[TestMethod]
	[DynamicData (nameof (LiveTestSupport.HubDevices), typeof (LiveTestSupport), DynamicDataSourceType.Property)]
	public async Task Hub_GetScannedChildDevices_ReturnsResultWithoutMutatingState (string host)
		{
		using KasaDevice device = await LiveTestSupport.ConnectAsync ("hub", host).ConfigureAwait (false);

		Assert.AreEqual (DeviceType.Hub, device.DeviceType, "Configured hub device should resolve as a hub.");
		ChildSetupScanResult result = await device.GetScannedChildDevicesAsync ().ConfigureAwait (false);

		Assert.IsNotNull (result);
		Assert.IsNotNull (result.SupportedCategories);
		Assert.IsNotNull (result.DetectedDevices);
		}

	[TestMethod]
	[DynamicData (nameof (LiveTestSupport.HubDevices), typeof (LiveTestSupport), DynamicDataSourceType.Property)]
	public async Task Hub_ConfiguredChild_IsPresentAndExposesReportedMetadata (string host)
		{
		using KasaDevice device = await LiveTestSupport.ConnectAsync ("hub", host).ConfigureAwait (false);
		string childDeviceId = LiveTestSupport.GetRequiredSetting ("KASA_LIVE_HUB_CHILD_ID", host);
		ChildDeviceInfo? child = device.GetChild (childDeviceId);
		Assert.IsNotNull (child, $"Hub child '{childDeviceId}' was not found after refresh.");
		Assert.AreEqual (childDeviceId, child.Id, "Hub child id should match the configured child id.");
		Assert.IsFalse (string.IsNullOrWhiteSpace (child.Model), "Hub child should report a model.");
		Assert.IsFalse (string.IsNullOrWhiteSpace (child.RawJson), "Hub child should expose raw state payload data.");
		Assert.IsTrue (child.Features.Count > 0, "Hub child should expose at least one reported feature.");
		}
	}
