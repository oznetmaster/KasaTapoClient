// Copyright © 2026 Neil Colvin.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using KasaTapoClient.Internal;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Newtonsoft.Json.Linq;

namespace KasaClient.Tests;

[TestClass]
public sealed class DoubleClickCommandTests
	{
	private const string CHILD_DEVICE_ID = "802E3B2CD5CE50468F34279EC01425D72525D3F8";

	[TestMethod]
	public void CreateSmartChildRequest_ForDoubleClickEnable_ProducesControlChildEnvelope ()
		{
		string request = KasaCommands.CreateSmartChildRequest (
			CHILD_DEVICE_ID,
			KasaCommands.SMART_SET_DOUBLE_CLICK_INFO_METHOD,
			new JObject { ["enable"] = true });

		var parsed = JObject.Parse (request);

		Assert.AreEqual ("control_child", (string?) parsed["method"]);
		Assert.AreEqual (CHILD_DEVICE_ID, (string?) parsed["params"]?["device_id"]);
		Assert.AreEqual ("set_double_click_info", (string?) parsed["params"]?["requestData"]?["method"]);
		Assert.AreEqual (true, (bool?) parsed["params"]?["requestData"]?["params"]?["enable"]);
		}

	[TestMethod]
	public void CreateSmartChildRequest_ForDoubleClickDisable_SendsEnableFalse ()
		{
		string request = KasaCommands.CreateSmartChildRequest (
			CHILD_DEVICE_ID,
			KasaCommands.SMART_SET_DOUBLE_CLICK_INFO_METHOD,
			new JObject { ["enable"] = false });

		var parsed = JObject.Parse (request);

		Assert.AreEqual (false, (bool?) parsed["params"]?["requestData"]?["params"]?["enable"]);
		}

	[TestMethod]
	public void CreateSmartChildRequest_ForDoubleClickRead_OmitsParams ()
		{
		string request = KasaCommands.CreateSmartChildRequest (
			CHILD_DEVICE_ID,
			KasaCommands.SMART_GET_DOUBLE_CLICK_INFO_METHOD);

		var parsed = JObject.Parse (request);

		Assert.AreEqual ("get_double_click_info", (string?) parsed["params"]?["requestData"]?["method"]);
		Assert.IsNull (parsed["params"]?["requestData"]?["params"]);
		}
	}
