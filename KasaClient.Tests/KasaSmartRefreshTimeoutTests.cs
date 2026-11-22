// Copyright © 2026 Neil Colvin.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

using KasaTapoClient;

using NUnit.Framework;

namespace KasaClient.Tests;

[TestFixture]
public sealed class KasaSmartRefreshTimeoutTests
	{
	private const string DEVICE_INFO = """{"error_code":0,"result":{"model":"P300","type":"SMART.TAPOPLUG","device_id":"strip"}}""";
	private const string COMPONENTS = """{"error_code":0,"result":{"component_list":[{"id":"child_device","ver_code":1}]}}""";
	private const string INITIAL_BATCH = """
		{"error_code":0,"result":{"responses":[
			{"method":"get_device_info","error_code":0,"result":{"model":"P300","type":"SMART.TAPOPLUG","device_id":"strip"}},
			{"method":"component_nego","error_code":0,"result":{"component_list":[{"id":"child_device","ver_code":1}]}}
		]}}
		""";
	private const string FIRST_PAGE = """{"error_code":0,"result":{"sum":2,"start_index":0,"child_device_list":[{"device_id":"outlet-1","category":"plug.powerstrip.sub-plug"}]}}""";
	private const string LAST_PAGE = """{"error_code":0,"result":{"sum":2,"start_index":1,"child_device_list":[{"device_id":"outlet-2","category":"plug.powerstrip.sub-plug"}]}}""";
	private const string CHILD_COMPONENTS = """{"error_code":0,"result":{"child_component_list":[]}}""";

	[TestCase ("timeout")]
	[TestCase ("transport cancellation")]
	[TestCase ("WebException timeout")]
	public async Task CoreBatchTimeout_RetriesIndividualReadsAndKeepsBothPages (string failure)
		{
		Exception timeout = failure switch
			{
			"timeout" => new TimeoutException (),
			"transport cancellation" => new TaskCanceledException (),
			"WebException timeout" => new WebException ("Timed out", WebExceptionStatus.Timeout),
			_ => throw new ArgumentOutOfRangeException (nameof (failure)),
			};
		var responses = new Queue<string> ([INITIAL_BATCH, FIRST_PAGE, CHILD_COMPONENTS, LAST_PAGE,
			DEVICE_INFO, COMPONENTS, FIRST_PAGE, CHILD_COMPONENTS, LAST_PAGE]);
		var transport = new FakeDeviceTransport (sendHandler: (request, _) => IsChildListBatch (request)
			? Task.FromException<string> (timeout)
			: Task.FromResult (responses.Dequeue ()));
		using var device = CreateDevice (transport);

		await device.UpdateAsync ();
		Assert.That (device.GetChild ("outlet-1"), Is.Not.Null);
		Assert.That (device.GetChild ("outlet-2"), Is.Not.Null);
		Assert.That (device.DeviceType, Is.EqualTo (DeviceType.Strip));
		Assert.That (transport.SentCommands, Has.Count.EqualTo (5));
		Assert.That (transport.SentCommands[1], Does.Contain ("\"multipleRequest\"")
			.And.Contain ("\"get_child_device_list\"").And.Contain ("\"get_child_device_component_list\""));
		Assert.That (transport.SentCommands[4], Does.Contain ("\"start_index\":1"));

		await device.UpdateAsync ();
		Assert.That (device.Children, Has.Count.EqualTo (2));
		Assert.That (transport.SentCommands, Has.Count.EqualTo (10));
		Assert.That (transport.SentCommands[5], Does.Contain ("\"method\":\"get_device_info\""));
		}

	[Test]
	public async Task CallerCancellation_DoesNotSendAnyFallbackRequests ()
		{
		using var cancellation = new CancellationTokenSource ();
		var transport = new FakeDeviceTransport (sendHandler: (_, token) =>
			{
			cancellation.Cancel ();
			return Task.FromCanceled<string> (token);
			});
		using var device = CreateDevice (transport);

		await Assert.ThatAsync (() => device.UpdateAsync (cancellation.Token), Throws.InstanceOf<OperationCanceledException> ());
		Assert.That (transport.SentCommands, Has.Count.EqualTo (1));
		Assert.That (device.SystemInfo, Is.Null);
		}

	[Test]
	public async Task IncompleteFallbackPage_PreservesPreviouslyEnumeratedChildren ()
		{
		const string emptyPage = """{"error_code":0,"result":{"sum":2,"start_index":1,"child_device_list":[]}}""";
		var responses = new Queue<string> ([INITIAL_BATCH, FIRST_PAGE, CHILD_COMPONENTS, LAST_PAGE,
			DEVICE_INFO, COMPONENTS, FIRST_PAGE, CHILD_COMPONENTS, emptyPage]);
		var transport = new FakeDeviceTransport (sendHandler: (request, _) => IsChildListBatch (request)
			? Task.FromException<string> (new TimeoutException ())
			: Task.FromResult (responses.Dequeue ()));
		using var device = CreateDevice (transport);
		await device.UpdateAsync ();
		ChildDeviceInfo? previousChild = device.GetChild ("outlet-2");
		Assert.That (previousChild, Is.Not.Null);

		await Assert.ThatAsync (() => device.UpdateAsync (), Throws.TypeOf<InvalidDataException> ());
		Assert.That (device.Children, Has.Count.EqualTo (2));
		Assert.That (device.GetChild ("outlet-2"), Is.SameAs (previousChild));
		}

	private static bool IsChildListBatch (string request) => request.Contains ("\"multipleRequest\"")
		&& request.Contains ("\"get_child_device_list\"");

	private static KasaDevice CreateDevice (FakeDeviceTransport transport) => new (
		new DeviceConfiguration ("127.0.0.1", connectionOptions: new DeviceConnectionOptions (
			connectionParameters: new DeviceConnectionParameters (DeviceFamilyKind.SmartTapoPlug, DeviceEncryptionKind.Aes))), transport);
	}
