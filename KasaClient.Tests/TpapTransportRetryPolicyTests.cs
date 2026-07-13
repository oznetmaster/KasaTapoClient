// Copyright © 2026 Neil Colvin.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Net.Http;
using System.Threading;

using KasaTapoClient.Internal;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace KasaClient.Tests;

[TestClass]
public sealed class TpapTransportRetryPolicyTests
	{
	[TestMethod]
	public void ShouldRetryLiveSession_TaskCanceledException_IsNotRetryable ()
		{
		var exception = new TaskCanceledException ("A task was canceled.");

		Assert.IsFalse (TpapTransport.ShouldRetryLiveSession (exception));
		}

	[TestMethod]
	public void ShouldRetryLiveSession_OperationCanceledException_IsNotRetryable ()
		{
		var exception = new OperationCanceledException ("The operation was canceled.");

		Assert.IsFalse (TpapTransport.ShouldRetryLiveSession (exception));
		}

	[TestMethod]
	public void ShouldRetryLiveSession_HttpRequestException_TaskWasCanceledMessage_IsNotRetryable ()
		{
		var exception = new HttpRequestException ("A task was canceled.");

		Assert.IsFalse (TpapTransport.ShouldRetryLiveSession (exception));
		}

	[TestMethod]
	public void ShouldRetryLiveSession_IOException_OperationAbortedMessage_IsRetryable ()
		{
		// A genuine socket abort (for example the remote device resetting the connection) must still be
		// retryable so a live session recovers from real transport failures. Distinguishing this from an
		// internal-timeout-triggered abort is handled by translating that specific case to
		// OperationCanceledException via ToCancellationException before ShouldRetryLiveSession is consulted.
		var exception = new IOException ("The I/O operation has been aborted because of either a thread exit or an application request.");

		Assert.IsTrue (TpapTransport.ShouldRetryLiveSession (exception));
		}

	[TestMethod]
	public void ShouldRetryLiveSession_HttpRequestException_ConnectionResetMessage_IsRetryable ()
		{
		var exception = new HttpRequestException ("Connection reset by peer.");

		Assert.IsTrue (TpapTransport.ShouldRetryLiveSession (exception));
		}

	[TestMethod]
	public void ShouldRetryLiveSession_HttpRequestException_OperationAbortedMessage_IsRetryable ()
		{
		var exception = new HttpRequestException ("The operation has been aborted.");

		Assert.IsTrue (TpapTransport.ShouldRetryLiveSession (exception));
		}

	[TestMethod]
	public void ShouldRetryLiveSession_UnrelatedException_IsNotRetryable ()
		{
		var exception = new InvalidOperationException ("Some unrelated failure.");

		Assert.IsFalse (TpapTransport.ShouldRetryLiveSession (exception));
		}

	[TestMethod]
	public void ToCancellationException_WrapsInnerExceptionAndPreservesToken ()
		{
		using var cts = new CancellationTokenSource ();
		cts.Cancel ();
		var inner = new IOException ("The I/O operation has been aborted because of either a thread exit or an application request.");

		OperationCanceledException result = TpapTransport.ToCancellationException (inner, cts.Token);

		Assert.AreSame (inner, result.InnerException);
		Assert.AreEqual (cts.Token, result.CancellationToken);
		}

	[TestMethod]
	public void ShouldRetryLiveSession_TranslatedInternalTimeoutAbort_IsNotRetryable ()
		{
		// This reproduces the scenario that caused the ~130s stall for TPAP devices: SendOnceAsync races a
		// request against an internal per-request timeout separate from the caller's token. When that internal
		// timeout fires, SocketsHttpHandler throws an IOException/HttpRequestException with an
		// "operation has been aborted" style message rather than an OperationCanceledException. Before reaching
		// ShouldRetryLiveSession, that exception must be translated via ToCancellationException so it is never
		// misclassified as a retryable transport reset (which would otherwise trigger Reset() plus a full,
		// non-cancellable PAKE handshake).
		using var cts = new CancellationTokenSource ();
		cts.Cancel ();
		var abortedException = new IOException ("The I/O operation has been aborted because of either a thread exit or an application request.");

		OperationCanceledException translated = TpapTransport.ToCancellationException (abortedException, cts.Token);

		Assert.IsFalse (TpapTransport.ShouldRetryLiveSession (translated));
		}
	}
