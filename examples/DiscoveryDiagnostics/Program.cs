// Copyright © 2026 Neil Colvin.
// Licensed under the MIT License. See LICENSE file in the repository root.

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading.Tasks;

using KasaTapoClient;

namespace DiscoveryDiagnostics;

internal static class Program
	{
	private static async Task<int> Main (string[] args)
		{
		if (args.Length is < 1 or > 2 || !IPAddress.TryParse (args[0], out IPAddress? deviceAddress)
			|| deviceAddress.AddressFamily != AddressFamily.InterNetwork)
			{
			Console.WriteLine ("Usage: DiscoveryDiagnostics <device-ipv4> [broadcast-ipv4]");
			Console.WriteLine ("No credentials or device control. Output includes local network addresses; review before sharing.");
			return 2;
			}
		string broadcast = args.Length == 2 ? args[1] : "255.255.255.255";
		if (!IPAddress.TryParse (broadcast, out IPAddress? broadcastAddress) || broadcastAddress.AddressFamily != AddressFamily.InterNetwork)
			{
			Console.Error.WriteLine ("The broadcast target must be an IPv4 address.");
			return 2;
			}

		Console.WriteLine ($"OS: {Environment.OSVersion}; runtime: {Environment.Version}; package: KasaTapoClient 1.8.1");
		foreach (NetworkInterface adapter in NetworkInterface.GetAllNetworkInterfaces ())
			{
			if (adapter.OperationalStatus != OperationalStatus.Up)
				continue;
			foreach (UnicastIPAddressInformation address in adapter.GetIPProperties ().UnicastAddresses)
				if (address.Address.AddressFamily == AddressFamily.InterNetwork)
					Console.WriteLine ($"Adapter: {adapter.Name}; IPv4: {address.Address}; mask: {address.IPv4Mask}");
			}

		int failures = 0;
		failures += await ProbeAsync ("Broadcast", () => Discover.DiscoverAsync (TimeSpan.FromSeconds (5), target: broadcast));
		failures += await ProbeAsync ("Targeted", () => Discover.DiscoverAsync (TimeSpan.FromSeconds (5), target: args[0]));
		failures += await ProbeAsync ("Targeted legacy-only", () => Discover.DiscoverLegacyAsync (TimeSpan.FromSeconds (5), target: args[0]));
		return failures == 0 ? 0 : 1;
		}

	private static async Task<int> ProbeAsync (string name, Func<Task<IReadOnlyList<DiscoveryResult>>> discover)
		{
		try
			{
			IReadOnlyList<DiscoveryResult> devices = await discover ();
			Console.WriteLine ($"{name}: {devices.Count} result(s)");
			foreach (DiscoveryResult device in devices)
				{
				DeviceConfiguration configuration = Discover.CreateConfiguration (device);
				DeviceConnectionParameters? parameters = configuration.ConnectionOptions.ConnectionParameters;
				Console.WriteLine ($"  {device.Host}: {device.Model}; transport={configuration.ConnectionOptions.TransportKind}; encryption={parameters?.EncryptionKind}; port={configuration.Port}; HTTPS={configuration.ConnectionOptions.UseSsl}");
				}
			return 0;
			}
		catch (Exception exception)
			{
			Console.Error.WriteLine ($"{name}: {exception.GetType ().Name}");
			return 1;
			}
		}
	}