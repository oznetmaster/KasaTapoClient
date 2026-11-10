// Copyright (c) 2026 Neil Colvin.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using KasaTapoClient;
using KasaTapoClient.Internal;

using NUnit.Framework;

namespace KasaClient.Tests;

[TestFixture]
public sealed class DeviceLifetimeTests
	{
	[Test]
	public async Task DisposalCancelsQueuedWorkAndRejectsLateStateBeforeTransportCleanup ()
		{
		var transport = new DelayedTransport ();
		var device = new KasaDevice (new DeviceConfiguration ("audit.invalid", connectionOptions: new (DeviceTransportKind.LegacyXor)), transport);
		Task update = device.UpdateAsync ();
		try
			{
			await transport.Started.Task;
			Task queued = device.ReadOutletStateAsync ();
			device.Dispose ();
			device.Dispose ();
			Assert.That (device.IsDisposed, Is.True);
			Assert.That (transport.Disposals, Is.Zero, "Active operations still own the transport.");
			_ = transport.Release.TrySetResult (true);
			Exception? activeError = await Failure (update);
			Exception? queuedError = await Failure (queued);
			Assert.That (activeError, Is.InstanceOf<OperationCanceledException> ().Or.InstanceOf<ObjectDisposedException> ());
			Assert.That (queuedError, Is.InstanceOf<OperationCanceledException> ().Or.InstanceOf<ObjectDisposedException> ());
			Assert.That (device.SystemInfo, Is.Null);
			Assert.That (transport.Disposals, Is.EqualTo (1));
			await Assert.ThrowsAsync<ObjectDisposedException> (async () => await device.UpdateAsync ());
			}
		finally { _ = transport.Release.TrySetResult (true); device.Dispose (); }
		}
	private static async Task<Exception?> Failure (Task task)
		{
		if (await Task.WhenAny (task, Task.Delay (TimeSpan.FromSeconds (10))) != task)
			throw new TimeoutException ("The fake device operation did not drain.");
		try
			{
			await task;
			return null;
			}
		catch (Exception error) { return error; }
		}
	private sealed class DelayedTransport : IDisposableDeviceTransport
		{
		internal int Disposals;
		internal TaskCompletionSource<bool> Started { get; } = new (TaskCreationOptions.RunContinuationsAsynchronously);
		internal TaskCompletionSource<bool> Release { get; } = new (TaskCreationOptions.RunContinuationsAsynchronously);
		public async Task<string> SendAsync (string json, CancellationToken cancellationToken)
			{
			_ = Started.TrySetResult (true);
			await Release.Task; // Deliberately ignore cancellation to test the publication barrier.
			return "{\"system\":{\"get_sysinfo\":{\"model\":\"HS100\",\"alias\":\"fake\",\"deviceId\":\"fake\",\"relay_state\":1,\"err_code\":0}}}";
			}
		public Task<string> SendManyAsync (IReadOnlyList<string> json, CancellationToken cancellationToken) => Task.FromResult ("{}");
		public void Dispose () => Interlocked.Increment (ref Disposals);
		}
	}