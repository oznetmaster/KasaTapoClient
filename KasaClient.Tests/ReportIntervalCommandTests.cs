// Copyright © 2026 Neil Colvin.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using KasaTapoClient.Internal;

using NUnit.Framework;

using Newtonsoft.Json.Linq;

namespace KasaClient.Tests;

[TestFixture]
public sealed class ReportIntervalCommandTests
	{
	private const string CHILD_DEVICE_ID = "802E8EB23E40CFD539830AD2F094E7E125094204";

	[Test]
	public void CreateSmartChildRequest_ForSetReportInterval_ProducesControlChildEnvelope ()
		{
		string request = KasaCommands.CreateSmartChildRequest (
			CHILD_DEVICE_ID,
			"set_device_info",
			new JObject { ["report_interval"] = 45 });

		var parsed = JObject.Parse (request);

		Assert.That ((string?)parsed["method"], Is.EqualTo ("control_child"));
		Assert.That ((string?)parsed["params"]?["device_id"], Is.EqualTo (CHILD_DEVICE_ID));
		Assert.That ((string?)parsed["params"]?["requestData"]?["method"], Is.EqualTo ("set_device_info"));
		Assert.That ((int?)parsed["params"]?["requestData"]?["params"]?["report_interval"], Is.EqualTo (45));
		}

	[Test]
	public void CreateSmartChildRequest_ForGetReportMode_OmitsParams ()
		{
		string request = KasaCommands.CreateSmartChildRequest (
			CHILD_DEVICE_ID,
			KasaCommands.SMART_GET_REPORT_MODE_METHOD);

		var parsed = JObject.Parse (request);

		Assert.That ((string?)parsed["params"]?["requestData"]?["method"], Is.EqualTo ("get_report_mode"));
		Assert.That (parsed["params"]?["requestData"]?["params"], Is.Null);
		}
	}