// Copyright © 2026 Neil Colvin.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using KasaTapoClient.Internal;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Newtonsoft.Json.Linq;

namespace KasaClient.Tests;

[TestClass]
public sealed class ReportIntervalCommandTests
	{
	private const string CHILD_DEVICE_ID = "802E8EB23E40CFD539830AD2F094E7E125094204";

	[TestMethod]
	public void CreateSmartChildRequest_ForSetReportInterval_ProducesControlChildEnvelope ()
		{
		string request = KasaCommands.CreateSmartChildRequest (
			CHILD_DEVICE_ID,
			"set_device_info",
			new JObject { ["report_interval"] = 45 });

		var parsed = JObject.Parse (request);

		Assert.AreEqual ("control_child", (string?) parsed["method"]);
		Assert.AreEqual (CHILD_DEVICE_ID, (string?) parsed["params"]?["device_id"]);
		Assert.AreEqual ("set_device_info", (string?) parsed["params"]?["requestData"]?["method"]);
		Assert.AreEqual (45, (int?) parsed["params"]?["requestData"]?["params"]?["report_interval"]);
		}

	[TestMethod]
	public void CreateSmartChildRequest_ForGetReportMode_OmitsParams ()
		{
		string request = KasaCommands.CreateSmartChildRequest (
			CHILD_DEVICE_ID,
			KasaCommands.SMART_GET_REPORT_MODE_METHOD);

		var parsed = JObject.Parse (request);

		Assert.AreEqual ("get_report_mode", (string?) parsed["params"]?["requestData"]?["method"]);
		Assert.IsNull (parsed["params"]?["requestData"]?["params"]);
		}
	}
