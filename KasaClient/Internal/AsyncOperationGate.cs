// Copyright © 2026 Neil Colvin.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace KasaTapoClient.Internal;

// Admission, stop and publication share a Monitor on every runtime. Cancellation
// and resource disposal run outside it: callbacks may reenter the owning object.
// Count waiters as well as holders so neither CTS nor semaphore is disposed early.
internal sealed class AsyncOperationGate (Type ownerType, Action? cleanup = null) : IDisposable
	{
#pragma warning disable IDE0330 // Monitor-based locking is shared with net472 and processor Mono.
	private readonly object _sync = new ();
#pragma warning restore IDE0330
	private readonly SemaphoreSlim _serial = new (1, 1);
	private readonly CancellationTokenSource _stop = new ();
	private int _disposed, _active;
	private bool _cancellationComplete, _cleaned;
	internal bool IsDisposed => Volatile.Read (ref _disposed) != 0;
	internal void ThrowIfDisposed ()
		{
#if NETFRAMEWORK
		if (IsDisposed) throw new ObjectDisposedException (ownerType.FullName);
#else
		ObjectDisposedException.ThrowIf (IsDisposed, ownerType);
#endif
		}
	internal async Task<Operation> EnterAsync (CancellationToken cancellationToken)
		{
		lock (_sync)
			{
			ThrowIfDisposed ();
			_active++;
			}

		CancellationTokenSource? linked = null;
		bool acquired = false;
		try
			{
			linked = CancellationTokenSource.CreateLinkedTokenSource (cancellationToken, _stop.Token);
			await _serial.WaitAsync (linked.Token).ConfigureAwait (false);
			acquired = true;
			linked.Token.ThrowIfCancellationRequested ();
			ThrowIfDisposed ();
			return new (this, linked);
			}
		catch
			{
			if (acquired)
				_ = _serial.Release ();
			linked?.Dispose ();
			Exit ();
			throw;
			}
		}
	internal T Commit<T> (Func<T> action)
		{
		lock (_sync)
			{
			ThrowIfDisposed ();
			return action ();
			}
		}
	internal void Commit (Action action)
		{
		lock (_sync)
			{
			ThrowIfDisposed ();
			action ();
			}
		}
	internal void Stop (Action? onStopping = null)
		{
		lock (_sync)
			{
			if (IsDisposed)
				return;
			Volatile.Write (ref _disposed, 1);
			onStopping?.Invoke ();
			}

		try
			{
			_stop.Cancel ();
			}
		finally
			{
			bool release;
			lock (_sync)
				{
				_cancellationComplete = true;
				release = ClaimCleanup ();
				}

			if (release)
				Cleanup ();
			}
		}
	private bool ClaimCleanup ()
		{
		if (!IsDisposed || !_cancellationComplete || _active != 0 || _cleaned)
			return false;
		_cleaned = true;
		return true;
		}
	private void Exit ()
		{
		bool release;
		lock (_sync)
			{
			_active--;
			release = ClaimCleanup ();
			}

		if (release)
			Cleanup ();
		}
	private void Cleanup ()
		{
		try
			{
			cleanup?.Invoke ();
			}
		finally
			{
			_serial.Dispose ();
			_stop.Dispose ();
			}
		}
	public void Dispose () => Stop ();
	internal sealed class Operation (AsyncOperationGate owner, CancellationTokenSource linked) : IDisposable
		{
		private AsyncOperationGate? _owner = owner;
		internal CancellationToken Token { get; } = linked.Token;
		public void Dispose ()
			{
			AsyncOperationGate? active = Interlocked.Exchange (ref _owner, null);
			if (active is null)
				return;
			try
				{
				linked.Dispose ();
				}
			finally
				{
				_ = active._serial.Release ();
				active.Exit ();
				}
			}
		}
	}