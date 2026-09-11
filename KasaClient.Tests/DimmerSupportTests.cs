// Copyright © 2026 Neil Colvin.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System;
using System.Threading.Tasks;

using KasaTapoClient;

using NUnit.Framework;

namespace KasaClient.Tests;

/// <summary>
/// Coverage for dimmer/brightness capability gating, following review feedback on PR #3
/// ("dimmer support (SupportsLightControl DeviceType.Dimmer)"): brightness must be keyed to the
/// capability a device actually advertises (the "brightness" component on SMART) rather than to
/// DeviceType, since KS240 and P135 both dim but never classify as DeviceType.Dimmer. Color
/// temperature and HSV stay keyed to Bulb/LightStrip, and legacy (IOT) dimmer on/off keeps using
/// system.set_relay_state - the legacy IOT dimmer command surface (smartlife.iot.dimmer.*) is
/// intentionally out of scope here.
///
/// The relay-state and colour-temperature regression guards live in DimmerRegressionTests.cs;
/// this file covers the capability that was actually missing.
/// </summary>
[TestFixture]
public sealed class DimmerSupportTests
	{
	/// <summary>
	/// A dimmer has no color control. Setting HSV should refuse locally rather than send hue/saturation
	/// to hardware that cannot act on them.
	/// </summary>
	[Test]
	public async Task SetHsvAsync_WithSmartDimmer_Throws ()
		{
		KasaDevice device = DimmerTestSupport.CreateSmartDimmer (out FakeDeviceTransport transport);
		await device.UpdateAsync ().ConfigureAwait (false);
		Assert.That (device.DeviceType, Is.EqualTo (DeviceType.Dimmer));

		await Assert.ThatAsync (() => device.SetHsvAsync (120, 50, 40), Throws.TypeOf<InvalidOperationException> ()).ConfigureAwait (false);

		foreach (string command in transport.SentCommands)
			{
			Assert.That (command.Contains ("\"hue\""), Is.False, $"No hue/saturation command should reach a dimmer, but got: {command}");
			}
		}

	/// <summary>
	/// A SMART-protocol dimmer accepts brightness through set_device_info, and nothing else.
	/// </summary>
	[Test]
	public async Task SetBrightnessAsync_WithSmartDimmer_SendsBrightnessOnly ()
		{
		KasaDevice device = DimmerTestSupport.CreateSmartDimmer (out FakeDeviceTransport transport);
		await device.UpdateAsync ().ConfigureAwait (false);
		Assert.That (device.DeviceType, Is.EqualTo (DeviceType.Dimmer));

		int commandsBefore = transport.SentCommands.Count;
		await device.SetBrightnessAsync (40).ConfigureAwait (false);

		string setCommand = DimmerTestSupport.FindCommand (transport, commandsBefore, "set_device_info")
			?? throw new AssertionException ("SetBrightnessAsync should have issued a set_device_info request.");
		Assert.That (setCommand, Does.Contain ("\"brightness\":40"));
		Assert.That (setCommand.Contains ("color_temp"), Is.False, "Brightness-only change must not carry color_temp.");
		Assert.That (setCommand.Contains ("\"hue\""), Is.False, "Brightness-only change must not carry hue.");
		Assert.That (setCommand.Contains ("saturation"), Is.False, "Brightness-only change must not carry saturation.");
		}

	/// <summary>
	/// KS240 is a dimmer + fan combo. It reports SMART.KASASWITCH *with* a child_device component,
	/// so the "SWITCH and child_device" branch claims it before the dimmer_calibration branch is
	/// reached, and it classifies as WallSwitch - not DeviceType.Dimmer. Brightness must still work
	/// because the device advertises the "brightness" component; gating on DeviceType alone can
	/// never reach this device.
	/// </summary>
	[Test]
	public async Task SetBrightnessAsync_WithKs240_SucceedsDespiteWallSwitchClassification ()
		{
		KasaDevice device = DimmerTestSupport.CreateSmartDevice (
			"KS240", "SMART.KASASWITCH", "RmFuIERpbW1lcg==", DimmerTestSupport.DIMMER_WITH_CHILDREN_COMPONENTS, out FakeDeviceTransport transport);
		await device.UpdateAsync ().ConfigureAwait (false);
		Assert.That (device.DeviceType, Is.EqualTo (DeviceType.WallSwitch));

		int commandsBefore = transport.SentCommands.Count;
		await device.SetBrightnessAsync (40).ConfigureAwait (false);

		string setCommand = DimmerTestSupport.FindCommand (transport, commandsBefore, "set_device_info")
			?? throw new AssertionException ("A device advertising the brightness component should accept SetBrightnessAsync.");
		Assert.That (setCommand, Does.Contain ("\"brightness\":40"));
		}

	/// <summary>
	/// P135 is a dimmable plug: it advertises both brightness and dimmer_calibration, but reports
	/// SMART.TAPOPLUG, and the "PLUG" branch is tested first in DetermineSmartDeviceType - so it
	/// classifies as Plug and can never reach the dimmer branch. Brightness must still work via the
	/// advertised component.
	/// </summary>
	[Test]
	public async Task SetBrightnessAsync_WithDimmablePlug_SucceedsDespitePlugClassification ()
		{
		KasaDevice device = DimmerTestSupport.CreateSmartDevice (
			"P135", "SMART.TAPOPLUG", "RGltbWFibGUgUGx1Zw==", DimmerTestSupport.DIMMER_COMPONENTS, out FakeDeviceTransport transport);
		await device.UpdateAsync ().ConfigureAwait (false);
		Assert.That (device.DeviceType, Is.EqualTo (DeviceType.Plug));

		int commandsBefore = transport.SentCommands.Count;
		await device.SetBrightnessAsync (40).ConfigureAwait (false);

		string setCommand = DimmerTestSupport.FindCommand (transport, commandsBefore, "set_device_info")
			?? throw new AssertionException ("A device advertising the brightness component should accept SetBrightnessAsync.");
		Assert.That (setCommand, Does.Contain ("\"brightness\":40"));
		}
	}