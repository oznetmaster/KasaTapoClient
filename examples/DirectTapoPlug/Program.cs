// Copyright © 2026 Neil Colvin.
// Licensed under the MIT License. See LICENSE file in the repository root.

using System;
using System.Threading.Tasks;

using KasaTapoClient;

namespace DirectTapoPlug;

internal static class Program
	{
	private static async Task<int> Main (string[] args)
		{
		if (args.Length is < 4 or > 5)
			{
			Console.WriteLine ("Usage: DirectTapoPlug <host-or-ip> <tpap|aes|klap> <port> <http|https> [state|on|off]");
			Console.WriteLine ("Supply the device's known protocol settings. Set TAPO_USERNAME and TAPO_PASSWORD in the process environment. Default action: state.");
			return 2;
			}

		string action = args.Length > 4 ? args[4].ToLowerInvariant () : "state";
		string protocol = args[1].ToLowerInvariant ();
		string scheme = args[3].ToLowerInvariant ();
		if (action is not ("state" or "on" or "off") || protocol is not ("tpap" or "aes" or "klap")
			|| scheme is not ("http" or "https") || !int.TryParse (args[2], out int port) || port is < 1 or > 65535)
			{
			Console.Error.WriteLine ("Supply a known protocol (tpap, aes or klap), port 1-65535, http or https, and state, on or off.");
			return 2;
			}

		string? username = Environment.GetEnvironmentVariable ("TAPO_USERNAME");
		string? password = Environment.GetEnvironmentVariable ("TAPO_PASSWORD");
		if (string.IsNullOrWhiteSpace (username) || string.IsNullOrWhiteSpace (password))
			{
			Console.Error.WriteLine ("Set TAPO_USERNAME and TAPO_PASSWORD to the TP-Link account credentials used by the plug.");
			return 2;
			}

		var credentials = new DeviceCredentials (username, password);
		bool useHttps = scheme == "https";
		var parameters = new DeviceConnectionParameters (
			deviceFamily: DeviceFamilyKind.SmartTapoPlug,
			encryptionKind: protocol switch
				{
				"tpap" => DeviceEncryptionKind.Tpap,
				"aes" => DeviceEncryptionKind.Aes,
				_ => DeviceEncryptionKind.Klap,
				},
			loginVersion: null,
			useHttps: useHttps,
			httpPort: port);
		var options = new DeviceConnectionOptions (
			transportKind: DeviceTransportKind.HttpToken,
			connectionParameters: parameters,
			useSsl: useHttps,
			useDefaultCredentials: false,
			defaultCredentialProfile: DefaultCredentialProfile.None,
			applicationPath: protocol == "tpap" ? "/" : "/app",
			useSecurePassthrough: true,
			tpapKeepAliveInterval: null);
		var configuration = new DeviceConfiguration (
			host: args[0],
			port: port,
			credentials: credentials,
			connectionOptions: options,
			timeout: TimeSpan.FromSeconds (10));

		try
			{
			using KasaDevice device = await Discover.ConnectAsync (configuration);
			// ConnectAsync refreshes state by default. Refresh again after an explicit control request.
			if (action == "on")
				await device.TurnOnAsync ();
			else if (action == "off")
				await device.TurnOffAsync ();
			if (action != "state")
				await device.UpdateAsync ();

			Console.WriteLine ($"Device: {device.Alias}");
			Console.WriteLine ($"Is on: {device.IsOn}");
			return 0;
			}
		catch (Exception exception)
			{
			Console.Error.WriteLine ($"Connection/control failed ({exception.GetType ().Name}). Check reachability, credentials and protocol settings.");
			return 1;
			}
		}
	}