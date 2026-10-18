// Copyright © 2026 Neil Colvin.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using KasaTapoClient;

using NUnit.Framework;

namespace KasaClient.Tests;

/// <summary>
/// Verifies typed lighting configuration, wire contracts and unsupported-device behavior.
/// </summary>
[TestFixture]
public sealed class LightConfigurationTests
	{
	/// <summary>
	/// The strip count is queried only when advertised and is preserved at the supported boundaries.
	/// </summary>
	[TestCase (0)]
	[TestCase (47)]
	[TestCase (50)]
	public async Task UpdateAsync_ProjectsAdvertisedSegmentCount (int count)
		{
		var transport = new FakeDeviceTransport (sendResponses: [Core (), Modules (count, false)]);
		using KasaDevice device = CreateDevice (transport);
		await device.UpdateAsync ().ConfigureAwait (false);
		Assert.That (device.LightStripSegment.IsAvailable, Is.True);
		Assert.That (device.LightStripSegment.SegmentCount, Is.EqualTo (count));
		Assert.That (transport.SentCommands[1], Does.Contain ("get_device_segment"));
		Assert.That (device.Features, Has.Some.Property ("Id").EqualTo ("strip_segments"));
		}

	/// <summary>
	/// Setting physical length uses the singular segment field and refreshes the typed snapshot.
	/// </summary>
	[TestCase (0)]
	[TestCase (50)]
	public async Task SetSegmentCountAsync_WritesOnlySegmentAndRefreshes (int count)
		{
		var transport = new FakeDeviceTransport (sendResponses: [Core (), Modules (47, false), "{}", Core (), Modules (count, false)]);
		using KasaDevice device = CreateDevice (transport);
		await device.UpdateAsync ().ConfigureAwait (false);
		await device.LightStripSegment.SetSegmentCountAsync (count).ConfigureAwait (false);
		using JsonDocument command = JsonDocument.Parse (transport.SentCommands[2]);
		Assert.That (command.RootElement.GetProperty ("method").GetString (), Is.EqualTo ("set_device_segment"));
		Assert.That (command.RootElement.GetProperty ("params").GetRawText (), Is.EqualTo ($"{{\"segment\":{count}}}"));
		Assert.That (device.LightStripSegment.SegmentCount, Is.EqualTo (count));
		Assert.That (device.LightTransition.ChangeTransitionEnabled, Is.False);
		}

	/// <summary>
	/// Invalid counts are rejected before any network operation.
	/// </summary>
	[TestCase (-1)]
	[TestCase (51)]
	public void SetSegmentCountAsync_RejectsOutOfRangeWithoutSending (int count)
		{
		var transport = new FakeDeviceTransport ();
		using KasaDevice device = CreateDevice (transport);
		Assert.Throws<ArgumentOutOfRangeException> (() => device.LightStripSegment.SetSegmentCountAsync (count));
		Assert.That (transport.SentCommands, Is.Empty);
		}

	/// <summary>
	/// Existing devices gain no segment query or writable capability without advertising support.
	/// </summary>
	[Test]
	public async Task UpdateAsync_WithoutOptionalComponents_PreservesUnavailableState ()
		{
		var transport = new FakeDeviceTransport (sendResponses: [Core (false, false)]);
		using KasaDevice device = CreateDevice (transport);
		await device.UpdateAsync ().ConfigureAwait (false);
		Assert.That (transport.SentCommands.Count, Is.EqualTo (1));
		Assert.That (device.LightStripSegment.IsAvailable, Is.False);
		Assert.That (device.LightTransition.SupportsChangeTransition, Is.False);
		Assert.That (device.LightTransition.ChangeTransitionEnabled, Is.Null);
		await Assert.ThrowsAsync<NotSupportedException> (async () => await device.LightStripSegment.SetSegmentCountAsync (25).ConfigureAwait (false)).ConfigureAwait (false);
		await Assert.ThrowsAsync<NotSupportedException> (async () => await device.LightTransition.SetChangeTransitionEnabledAsync (true).ConfigureAwait (false)).ConfigureAwait (false);
		Assert.That (transport.SentCommands.Count, Is.EqualTo (1));
		}

	/// <summary>
	/// Optional getter errors or empty results must not manufacture a writable state.
	/// </summary>
	[TestCase ("{\"error_code\":0,\"result\":{}}")]
	[TestCase ("{\"error_code\":-1002}")]
	public async Task UpdateAsync_WithoutOptionalState_DisablesNewSetters (string result)
		{
		string response = "{\"result\":{\"responses\":[{\"method\":\"get_device_segment\"," + result.Substring (1) + "]}}";
		var transport = new FakeDeviceTransport (sendResponses: [Core (), response]);
		using KasaDevice device = CreateDevice (transport);
		await device.UpdateAsync ().ConfigureAwait (false);
		Assert.That (device.LightStripSegment.IsAvailable, Is.False);
		Assert.That (device.LightTransition.SupportsChangeTransition, Is.False);
		await Assert.ThrowsAsync<NotSupportedException> (async () => await device.LightStripSegment.SetSegmentCountAsync (25).ConfigureAwait (false)).ConfigureAwait (false);
		Assert.That (transport.SentCommands.Count, Is.EqualTo (2));
		}

	/// <summary>
	/// On/off durations are unaffected when the separate change-transition setting is toggled.
	/// </summary>
	[TestCase (true)]
	[TestCase (false)]
	public async Task SetChangeTransitionEnabledAsync_WritesOnlyChangeStateAndRefreshes (bool enabled)
		{
		var transport = new FakeDeviceTransport (sendResponses: [Core (), Modules (47, !enabled), "{}", Core (), Modules (47, enabled)]);
		using KasaDevice device = CreateDevice (transport);
		await device.UpdateAsync ().ConfigureAwait (false);
		await device.LightTransition.SetChangeTransitionEnabledAsync (enabled).ConfigureAwait (false);
		using JsonDocument command = JsonDocument.Parse (transport.SentCommands[2]);
		Assert.That (command.RootElement.GetProperty ("method").GetString (), Is.EqualTo ("set_on_off_gradually_info"));
		Assert.That (command.RootElement.GetProperty ("params").GetRawText (), Is.EqualTo ("{\"change_state\":{\"enable\":" + (enabled ? "true" : "false") + "}}"));
		Assert.That (device.LightTransition.ChangeTransitionEnabled, Is.EqualTo (enabled));
		Assert.That (device.LightTransition.State!.TransitionOnDurationSeconds, Is.EqualTo (12));
		Assert.That (device.LightTransition.State.TransitionOffDurationSeconds, Is.EqualTo (8));
		Assert.That (device.LightStripSegment.SegmentCount, Is.EqualTo (47));
		}

	/// <summary>
	/// Older gradual-transition devices retain on/off state without gaining a change-state setter.
	/// </summary>
	[Test]
	public async Task UpdateAsync_OlderTransitionsKeepNullChangeState ()
		{
		var transport = new FakeDeviceTransport (sendResponses: [Core (), Modules (47, null)]);
		using KasaDevice device = CreateDevice (transport);
		await device.UpdateAsync ().ConfigureAwait (false);
		Assert.That (device.LightTransition.State!.TransitionOnSeconds, Is.EqualTo (12));
		Assert.That (device.LightTransition.ChangeTransitionEnabled, Is.Null);
		await Assert.ThrowsAsync<NotSupportedException> (async () => await device.LightTransition.SetChangeTransitionEnabledAsync (true).ConfigureAwait (false)).ConfigureAwait (false);
		Assert.That (transport.SentCommands.Count, Is.EqualTo (2));
		}

	/// <summary>
	/// Caller cancellation prevents either new setter from sending a command.
	/// </summary>
	[Test]
	public async Task Setters_WithCanceledToken_DoNotSend ()
		{
		var transport = new FakeDeviceTransport (sendResponses: [Core (), Modules (47, false)]);
		using KasaDevice device = CreateDevice (transport);
		await device.UpdateAsync ().ConfigureAwait (false);
		using var cancellation = new CancellationTokenSource ();
		cancellation.Cancel ();
		await Assert.CatchAsync<OperationCanceledException> (async () => await device.LightStripSegment.SetSegmentCountAsync (25, cancellation.Token).ConfigureAwait (false)).ConfigureAwait (false);
		await Assert.CatchAsync<OperationCanceledException> (async () => await device.LightTransition.SetChangeTransitionEnabledAsync (true, cancellation.Token).ConfigureAwait (false)).ConfigureAwait (false);
		Assert.That (transport.SentCommands.Count, Is.EqualTo (2));
		}

	/// <summary>
	/// Device rejection must propagate without claiming a new value or issuing a refresh.
	/// </summary>
	[TestCase (true)]
	[TestCase (false)]
	public async Task Setters_RejectDeviceErrorsWithoutChangingSnapshot (bool segment)
		{
		var transport = new FakeDeviceTransport (sendResponses: [Core (), Modules (47, false), "{\"error_code\":-1002}"]);
		using KasaDevice device = CreateDevice (transport);
		await device.UpdateAsync ().ConfigureAwait (false);
		await Assert.ThrowsAsync<InvalidOperationException> (async () =>
			{
				if (segment)
					{
					await device.LightStripSegment.SetSegmentCountAsync (25).ConfigureAwait (false);
					}
				else
					{
					await device.LightTransition.SetChangeTransitionEnabledAsync (true).ConfigureAwait (false);
					}
			}).ConfigureAwait (false);
		Assert.That (device.LightStripSegment.SegmentCount, Is.EqualTo (47));
		Assert.That (device.LightTransition.ChangeTransitionEnabled, Is.False);
		Assert.That (transport.SentCommands.Count, Is.EqualTo (3));
		}

	/// <summary>
	/// A response alone cannot grant an unadvertised strip-length capability.
	/// </summary>
	[Test]
	public async Task UpdateAsync_UnadvertisedSegmentResult_DoesNotEnableSetter ()
		{
		string core = Core (false, false).Replace ("\"responses\":[", "\"responses\":[{\"method\":\"get_device_segment\",\"result\":{\"segment\":47}},");
		var transport = new FakeDeviceTransport (sendResponses: [core]);
		using KasaDevice device = CreateDevice (transport);
		await device.UpdateAsync ().ConfigureAwait (false);
		Assert.That (device.LightStripSegment.IsAvailable, Is.False);
		await Assert.ThrowsAsync<NotSupportedException> (async () => await device.LightStripSegment.SetSegmentCountAsync (25).ConfigureAwait (false)).ConfigureAwait (false);
		Assert.That (transport.SentCommands.Count, Is.EqualTo (1));
		}

	/// <summary>
	/// Firmware may report a change flag independently of directional transition fields.
	/// </summary>
	[Test]
	public async Task UpdateAsync_ChangeOnlyResult_PreservesReportedFalse ()
		{
		var transport = new FakeDeviceTransport (sendResponses: [Core (false, true), "{\"result\":{\"responses\":[{\"method\":\"get_on_off_gradually_info\",\"result\":{\"change_state\":{\"enable\":false}}}]}}"]);
		using KasaDevice device = CreateDevice (transport);
		await device.UpdateAsync ().ConfigureAwait (false);
		Assert.That (device.LightTransition.SupportsChangeTransition, Is.True);
		Assert.That (device.LightTransition.ChangeTransitionEnabled, Is.False);
		Assert.That (device.LightTransition.TransitionOnSeconds, Is.Null);
		}

	private static KasaDevice CreateDevice (FakeDeviceTransport transport) => new (
		new DeviceConfiguration ("127.0.0.1", connectionOptions: new DeviceConnectionOptions (
			connectionParameters: new DeviceConnectionParameters (DeviceFamilyKind.SmartTapoBulb, DeviceEncryptionKind.Aes))), transport);

	private static string Core (bool segment = true, bool transition = true)
		{
		string components = segment ? "{\"id\":\"segment\",\"ver_code\":1}" : string.Empty;
		if (transition)
			{
			components += (segment ? "," : string.Empty) + "{\"id\":\"on_off_gradually\",\"ver_code\":4}";
			}
		return "{\"result\":{\"responses\":[{\"method\":\"get_device_info\",\"result\":{\"model\":\"L930\",\"device_id\":\"offline-strip\",\"type\":\"SMART.TAPOBULB\"}},{\"method\":\"component_nego\",\"result\":{\"component_list\":[" + components + "]}}]}}";
		}

	private static string Modules (int count, bool? changeEnabled) =>
		"{\"result\":{\"responses\":[{\"method\":\"get_device_segment\",\"result\":{\"segment\":" + count + "}},{\"method\":\"get_on_off_gradually_info\",\"result\":{\"on_state\":{\"enable\":true,\"duration\":12},\"off_state\":{\"enable\":true,\"duration\":8}" +
		(changeEnabled is bool enabled ? ",\"change_state\":{\"enable\":" + (enabled ? "true" : "false") + "}" : string.Empty) + "}}]}}";
	}