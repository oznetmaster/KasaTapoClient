// Copyright © 2026 Neil Colvin.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System;
using System.Threading;
using System.Threading.Tasks;

using KasaTapoClient.Internal;

namespace KasaTapoClient;

public sealed partial class KasaDevice
	{
	/// <summary>
	/// Gets access to the configured physical light-strip length, when supported.
	/// </summary>
	public LightStripSegmentModule LightStripSegment
		{
		get;
		}

	/// <summary>
	/// Gets the last reported strip length, or <see langword="null" /> when unavailable.
	/// </summary>
	public LightStripSegmentState? LightStripSegmentState
		{
		get; private set;
		}

	/// <summary>
	/// Sets the number of 10 cm segments in each strip run and refreshes device state.
	/// </summary>
	/// <param name="segmentCount">The configured count, between 0 and 50 inclusive.</param>
	/// <param name="cancellationToken">The cancellation token for the operation.</param>
	/// <returns>A task that completes after the refreshed state is available.</returns>
	/// <exception cref="ArgumentOutOfRangeException">The count is outside 0 to 50.</exception>
	/// <exception cref="NotSupportedException">The device has not reported segment support and state.</exception>
	public Task SetLightStripSegmentCountAsync (int segmentCount, CancellationToken cancellationToken = default)
		{
		if (segmentCount is < 0 or > 50)
			{
			throw new ArgumentOutOfRangeException (nameof (segmentCount), segmentCount, "Segment count must be between 0 and 50.");
			}

		return RunDeviceOperationAsync (ct => SetLightStripSegmentCountCoreAsync (segmentCount, ct), cancellationToken);
		}

	private async Task SetLightStripSegmentCountCoreAsync (int segmentCount, CancellationToken cancellationToken)
		{
		if (!UsesSmartProtocol () || !_smartComponentVersions.ContainsKey ("segment") || LightStripSegmentState is null)
			{
			throw new NotSupportedException ($"The device '{Host}' does not report light-strip segment control.");
			}

		await ExecuteLightConfigurationCommandAsync (KasaCommands.CreateSetSmartLightStripSegmentCommand (segmentCount), cancellationToken).ConfigureAwait (false);
		}

	/// <summary>
	/// Enables or disables fading between brightness or color settings while the light is on.
	/// </summary>
	/// <param name="enabled">Whether changes should fade between settings.</param>
	/// <param name="cancellationToken">The cancellation token for the operation.</param>
	/// <returns>A task that completes after device state has been refreshed.</returns>
	/// <exception cref="NotSupportedException">The device has not reported change-transition support.</exception>
	public Task SetLightChangeTransitionEnabledAsync (bool enabled, CancellationToken cancellationToken = default) =>
		RunDeviceOperationAsync (ct => SetLightChangeTransitionEnabledCoreAsync (enabled, ct), cancellationToken);

	private async Task SetLightChangeTransitionEnabledCoreAsync (bool enabled, CancellationToken cancellationToken)
		{
		if (!UsesSmartProtocol () || !_smartComponentVersions.ContainsKey ("on_off_gradually") || LightTransitionState?.IsChangeTransitionEnabled is null)
			{
			throw new NotSupportedException ($"The device '{Host}' does not report fading between light settings.");
			}

		await ExecuteLightConfigurationCommandAsync (KasaCommands.CreateSetSmartLightChangeTransitionCommand (enabled), cancellationToken).ConfigureAwait (false);
		}

	private async Task ExecuteLightConfigurationCommandAsync (string command, CancellationToken cancellationToken)
		{
		string response = await ExecuteCommandCoreAsync (command, cancellationToken).ConfigureAwait (false);
		ResponseHeader acknowledgment = WireJson.Read<ResponseHeader> (response);
		if (acknowledgment.ErrorCode is int errorCode && errorCode != 0)
			{
			throw new InvalidOperationException ($"Device '{Host}' rejected the lighting setting with error code {errorCode}.");
			}

		await UpdateCoreAsync (cancellationToken).ConfigureAwait (false);
		}
	}

/// <summary>
/// Represents the configured physical length of each light-strip run.
/// </summary>
public sealed record LightStripSegmentState
	{
	internal LightStripSegmentState (int segmentCount) => SegmentCount = segmentCount;

	/// <summary>
	/// Gets the reported number of 10 cm segments per run, independent of lighting-effect segments.
	/// </summary>
	public int SegmentCount
		{
		get;
		}
	}

/// <summary>
/// Provides typed access to physical strip length on devices advertising the segment component.
/// </summary>
public sealed class LightStripSegmentModule
	{
	private readonly KasaDevice _device;

	internal LightStripSegmentModule (KasaDevice device) => _device = device;

	/// <summary>
	/// Gets the latest reported length, or <see langword="null" /> when unavailable.
	/// </summary>
	public LightStripSegmentState? State => _device.LightStripSegmentState;

	/// <summary>
	/// Gets the reported count of 10 cm segments per run, or <see langword="null" /> when unavailable.
	/// </summary>
	public int? SegmentCount => State?.SegmentCount;

	/// <summary>
	/// Gets whether the device advertises the segment component and has returned segment state.
	/// </summary>
	public bool IsAvailable => State is not null;

	/// <summary>
	/// Refreshes device state, including strip length when the segment component is advertised.
	/// </summary>
	/// <param name="cancellationToken">The cancellation token for the operation.</param>
	/// <returns>A task that completes after device state has been refreshed.</returns>
	public Task UpdateAsync (CancellationToken cancellationToken = default) => _device.UpdateAsync (cancellationToken);

	/// <summary>
	/// Sets the physical strip length; this does not configure colors for individual segments.
	/// </summary>
	/// <param name="segmentCount">The count of 10 cm segments per run, from 0 to 50 inclusive.</param>
	/// <param name="cancellationToken">The cancellation token for the operation.</param>
	/// <returns>A task that completes after device state has been refreshed.</returns>
	/// <exception cref="ArgumentOutOfRangeException">The count is outside 0 to 50.</exception>
	/// <exception cref="NotSupportedException">The device has not reported segment support and state.</exception>
	public Task SetSegmentCountAsync (int segmentCount, CancellationToken cancellationToken = default) =>
		_device.SetLightStripSegmentCountAsync (segmentCount, cancellationToken);
	}