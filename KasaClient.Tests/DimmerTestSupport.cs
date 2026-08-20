// Copyright © 2026 Neil Colvin.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Threading.Tasks;

using KasaTapoClient;

namespace KasaClient.Tests;

/// <summary>
/// Shared fixtures for dimmer coverage.
///
/// No device response here is copied from any external project: the SMART envelopes and
/// legacy sysinfo payloads below are hand-written to the minimum shape the parsers require.
/// </summary>
internal static class DimmerTestSupport
	{
	internal const string PENDING_CATEGORY = "PendingDimmerSupport";

	internal const string DIMMER_COMPONENTS =
		"""{ "id": "device", "ver_code": 2 }, { "id": "brightness", "ver_code": 1 }, { "id": "dimmer_calibration", "ver_code": 1 }""";

	internal const string DIMMER_WITH_CHILDREN_COMPONENTS =
		"""{ "id": "device", "ver_code": 2 }, { "id": "brightness", "ver_code": 1 }, { "id": "dimmer_calibration", "ver_code": 1 }, { "id": "child_device", "ver_code": 1 }""";

	internal const string LEGACY_MODULE_RESPONSE =
		"{\"emeter\":{\"err_code\":-1},\"time\":{\"get_time\":{\"year\":2025,\"month\":1,\"mday\":2,\"hour\":3,\"min\":4,\"sec\":5}},\"cnCloud\":{\"get_info\":{\"binded\":1,\"cld_connection\":1}},\"count_down\":{\"get_rules\":{\"rule_list\":[]}},\"schedule\":{\"get_rules\":{\"rule_list\":[]}},\"anti_theft\":{\"get_rules\":{\"rule_list\":[]}}}";

	/// <summary>
	/// A legacy dimmer sysinfo. The type string is forced to IOT.DIMMER: a real HS220 reports
	/// IOT.SMARTPLUGSWITCH and is identified as a dimmer only by dev_name, which this library
	/// does not parse, so DetermineDeviceType matches "plug" first and classifies it as Plug.
	/// These fixtures exercise the DeviceType.Dimmer branch that already exists in the legacy
	/// path - the branch that becomes reachable the moment dev_name detection is added.
	/// </summary>
	internal static string LegacyDimmerSysInfo (int relayState, int brightness) =>
		"{\"system\":{\"get_sysinfo\":{\"alias\":\"Hall Dimmer\",\"model\":\"HS220\",\"deviceId\":\"dimmer-1\",\"type\":\"IOT.DIMMER\",\"relay_state\":"
		+ relayState + ",\"brightness\":" + brightness + "}}}";

	/// <summary>
	/// One envelope answers every SMART SendAsync. ParseSmartResponse requires get_device_info
	/// and component_nego in each response it parses and ignores any method it did not ask for,
	/// so a single envelope satisfies the initial update, the module refresh and the
	/// post-command refresh alike.
	/// </summary>
	internal static string SmartEnvelope (string model, string type, string nicknameBase64, string components) => $$"""
		{
		  "result": {
			 "responses": [
				{
				  "method": "get_device_info",
				  "result": {
					 "model": "{{model}}",
					 "type": "{{type}}",
					 "device_id": "dimmer-1",
					 "nickname": "{{nicknameBase64}}",
					 "device_on": true,
					 "brightness": 25,
					 "fw_ver": "1.0.0",
					 "hw_ver": "1.0"
				  }
				},
				{
				  "method": "component_nego",
				  "result": { "component_list": [{{components}}] }
				},
				{ "method": "get_child_device_list", "result": { "child_device_list": [] } },
				{ "method": "set_device_info", "result": { } }
			 ]
		  }
		}
		""";

	internal static KasaDevice CreateSmartDevice (string model, string type, string nicknameBase64, string components, out FakeDeviceTransport transport)
		{
		string envelope = SmartEnvelope (model, type, nicknameBase64, components);
		transport = new FakeDeviceTransport (sendHandler: (_, _) => Task.FromResult (envelope));
		DeviceConfiguration configuration = new (
			"127.0.0.1",
			connectionOptions: new DeviceConnectionOptions (
				connectionParameters: new DeviceConnectionParameters (
					type.Contains ("PLUG") ? DeviceFamilyKind.SmartTapoPlug : DeviceFamilyKind.SmartKasaSwitch,
					DeviceEncryptionKind.Aes)));
		return new KasaDevice (configuration, transport);
		}

	/// <summary>A KS225-shaped SMART dimmer: brightness plus dimmer_calibration, no children.</summary>
	internal static KasaDevice CreateSmartDimmer (out FakeDeviceTransport transport) =>
		CreateSmartDevice ("KS225", "SMART.KASASWITCH", "SGFsbCBEaW1tZXI=", DIMMER_COMPONENTS, out transport);

	internal static string? FindCommand (FakeDeviceTransport transport, int startIndex, string needle)
		{
		for (int index = startIndex; index < transport.SentCommands.Count; index++)
			{
			if (transport.SentCommands[index].Contains (needle))
				{
				return transport.SentCommands[index];
				}
			}

		return null;
		}
	}
