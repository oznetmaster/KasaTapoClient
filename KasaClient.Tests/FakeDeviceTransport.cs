// Copyright © 2026 Neil Colvin.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using KasaTapoClient.Internal;

namespace KasaClient.Tests;

internal sealed class FakeDeviceTransport : IDeviceTransport
	{
	private readonly Queue<string> _sendResponses;
	private readonly Queue<string> _sendManyResponses;

	internal FakeDeviceTransport(IEnumerable<string>? sendResponses = null, IEnumerable<string>? sendManyResponses = null)
		{
		_sendResponses = new Queue<string> (sendResponses ?? Enumerable.Empty<string> ());
		_sendManyResponses = new Queue<string> (sendManyResponses ?? Enumerable.Empty<string> ());
		SentCommands = new List<string> ();
		SentManyCommands = new List<IReadOnlyList<string>> ();
		}

	internal List<string> SentCommands { get; }
	internal List<IReadOnlyList<string>> SentManyCommands { get; }

	public Task<string> SendAsync (string commandJson, CancellationToken cancellationToken)
		{
		cancellationToken.ThrowIfCancellationRequested ();
		SentCommands.Add (commandJson);
		if (_sendResponses.Count == 0)
			{
			throw new InvalidOperationException ("No queued SendAsync response is available for the fake transport.");
			}

		return Task.FromResult (_sendResponses.Dequeue ());
		}

	public Task<string> SendManyAsync (IReadOnlyList<string> commandJsonPayloads, CancellationToken cancellationToken)
		{
		cancellationToken.ThrowIfCancellationRequested ();
		SentManyCommands.Add (commandJsonPayloads.ToArray ());
		if (_sendManyResponses.Count == 0)
			{
			throw new InvalidOperationException ("No queued SendManyAsync response is available for the fake transport.");
			}

		return Task.FromResult (_sendManyResponses.Dequeue ());
		}
	}
