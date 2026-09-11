// Copyright © 2026 Neil Colvin.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using KasaTapoClient.Internal;

using NUnit.Framework;

using Newtonsoft.Json.Linq;

namespace KasaClient.Tests;

[TestFixture]
public sealed class DoubleClickCommandTests
	{
	private const string CHILD_DEVICE_ID = "802E3B2CD5CE50468F34279EC01425D72525D3F8";

	[Test]
	public void CreateSmartChildRequest_ForDoubleClickEnable_ProducesControlChildEnvelope ()
		{
		string request = KasaCommands.CreateSmartChildRequest (
			CHILD_DEVICE_ID,
			KasaCommands.SMART_SET_DOUBLE_CLICK_INFO_METHOD,
			new JObject { ["enable"] = true });

		var parsed = JObject.Parse (request);

		Assert.That ((string?)parsed["method"], Is.EqualTo ("control_child"));
		Assert.That ((string?)parsed["params"]?["device_id"], Is.EqualTo (CHILD_DEVICE_ID));
		Assert.That ((string?)parsed["params"]?["requestData"]?["method"], Is.EqualTo ("set_double_click_info"));
		Assert.That ((bool?)parsed["params"]?["requestData"]?["params"]?["enable"], Is.EqualTo (true));
		}

	[Test]
	public void CreateSmartChildRequest_ForDoubleClickDisable_SendsEnableFalse ()
		{
		string request = KasaCommands.CreateSmartChildRequest (
			CHILD_DEVICE_ID,
			KasaCommands.SMART_SET_DOUBLE_CLICK_INFO_METHOD,
			new JObject { ["enable"] = false });

		var parsed = JObject.Parse (request);

		Assert.That ((bool?)parsed["params"]?["requestData"]?["params"]?["enable"], Is.EqualTo (false));
		}

	[Test]
	public void CreateSmartChildRequest_ForDoubleClickRead_OmitsParams ()
		{
		string request = KasaCommands.CreateSmartChildRequest (
			CHILD_DEVICE_ID,
			KasaCommands.SMART_GET_DOUBLE_CLICK_INFO_METHOD);

		var parsed = JObject.Parse (request);

		Assert.That ((string?)parsed["params"]?["requestData"]?["method"], Is.EqualTo ("get_double_click_info"));
		Assert.That (parsed["params"]?["requestData"]?["params"], Is.Null);
		}
	}