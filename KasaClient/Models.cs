// Copyright © 2026 Neil Colvin.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// Adapted from python-kasa (https://github.com/python-kasa/python-kasa)
// Original work Copyright (c) python-kasa contributors, MIT License

using System;
using System.Collections.Generic;
using System.Text;

namespace KasaTapoClient;

/// <summary>
/// Represents the high-level TP-Link device family inferred from discovery or system information.
/// </summary>
public enum DeviceType
	{
	/// <summary>
	/// The device family could not be determined.
	/// </summary>
	Unknown,

	/// <summary>
	/// A smart plug device.
	/// </summary>
	Plug,

	/// <summary>
	/// A multi-outlet smart strip.
	/// </summary>
	Strip,

	/// <summary>
	/// A bulb device.
	/// </summary>
	Bulb,

	/// <summary>
	/// A light strip device.
	/// </summary>
	LightStrip,

	/// <summary>
	/// A dimmer device.
	/// </summary>
	Dimmer,

	/// <summary>
	/// A wall switch device.
	/// </summary>
	WallSwitch,

	/// <summary>
	/// A camera device.
	/// </summary>
	Camera,

	/// <summary>
	/// A hub device.
	/// </summary>
	Hub,

	/// <summary>
	/// A sensor device.
	/// </summary>
	Sensor,

	/// <summary>
	/// A fan device.
	/// </summary>
	Fan,

	/// <summary>
	/// A thermostat device.
	/// </summary>
	Thermostat,

	/// <summary>
	/// A chime device.
	/// </summary>
	Chime,

	/// <summary>
	/// A doorbell device.
	/// </summary>
	Doorbell,

	/// <summary>
	/// A robot vacuum device.
	/// </summary>
	Vacuum,
	}

/// <summary>
/// Represents optional credentials for devices that require authentication.
/// </summary>
public sealed class DeviceCredentials
	{
	/// <summary>
	/// Initializes a new instance of the <see cref="DeviceCredentials" /> class.
	/// </summary>
	/// <param name="userName">The user name or email address used by the device ecosystem.</param>
	/// <param name="password">The password associated with <paramref name="userName" />.</param>
	public DeviceCredentials (string? userName = null, string? password = null)
		{
		UserName = userName;
		Password = password;
		}

	/// <summary>
	/// Gets the user name or email address.
	/// </summary>
	public string? UserName
		{
		get;
		}

	/// <summary>
	/// Gets the password.
	/// </summary>
	public string? Password
		{
		get;
		}

	/// <summary>
	/// Creates credentials from a known default credential profile.
	/// </summary>
	/// <param name="profile">The default credential profile to decode.</param>
	/// <returns>The decoded credentials.</returns>
	public static DeviceCredentials FromDefault (DefaultCredentialProfile profile)
		{
		return profile switch
			{
				DefaultCredentialProfile.Kasa => CreateFromEncoded ("a2FzYUB0cC1saW5rLm5ldA==", "a2FzYVNldHVw"),
				DefaultCredentialProfile.KasaCamera => CreateFromEncoded ("YWRtaW4=", "MjEyMzJmMjk3YTU3YTVhNzQzODk0YTBlNGE4MDFmYzM="),
				DefaultCredentialProfile.Tapo => CreateFromEncoded ("dGVzdEB0cC1saW5rLm5ldA==", "dGVzdA=="),
				DefaultCredentialProfile.TapoCamera => CreateFromEncoded ("YWRtaW4=", "YWRtaW4="),
				DefaultCredentialProfile.TapoCameraLv3 => CreateFromEncoded ("YWRtaW4=", "VFBMMDc1NTI2NDYwNjAz"),
				_ => throw new ArgumentOutOfRangeException (nameof (profile), profile, "Unknown default credential profile."),
				};
		}

	private static DeviceCredentials CreateFromEncoded (string encodedUserName, string encodedPassword)
		{
		string userName = Encoding.UTF8.GetString (Convert.FromBase64String (encodedUserName));
		string password = Encoding.UTF8.GetString (Convert.FromBase64String (encodedPassword));
		return new DeviceCredentials (userName, password);
		}
	}

/// <summary>
/// Represents the wire transport used to communicate with a device.
/// </summary>
public enum DeviceTransportKind
	{
	/// <summary>
	/// Select the transport automatically from discovery or connection behavior.
	/// </summary>
	Auto,

	/// <summary>
	/// The legacy TP-Link XOR TCP protocol, typically on port 9999.
	/// </summary>
	LegacyXor,

	/// <summary>
	/// The HTTP or HTTPS application API using an authenticated app token.
	/// </summary>
	HttpToken,
	}

/// <summary>
/// Represents python-kasa style device family identifiers used for protocol selection.
/// </summary>
public enum DeviceFamilyKind
	{
	/// <summary>
	/// The device family is unknown.
	/// </summary>
	Unknown,
	/// <summary>
	/// The legacy IOT smart plug or switch family.
	/// </summary>
	IotSmartPlugSwitch,
	/// <summary>
	/// The legacy IOT smart bulb family.
	/// </summary>
	IotSmartBulb,
	/// <summary>
	/// The legacy IOT IP camera family.
	/// </summary>
	IotIpCamera,
	/// <summary>
	/// The SMART Kasa plug family.
	/// </summary>
	SmartKasaPlug,
	/// <summary>
	/// The SMART Kasa switch family.
	/// </summary>
	SmartKasaSwitch,
	/// <summary>
	/// The SMART Tapo plug family.
	/// </summary>
	SmartTapoPlug,
	/// <summary>
	/// The SMART Tapo bulb family.
	/// </summary>
	SmartTapoBulb,
	/// <summary>
	/// The SMART Tapo switch family.
	/// </summary>
	SmartTapoSwitch,
	/// <summary>
	/// The SMART Tapo hub family.
	/// </summary>
	SmartTapoHub,
	/// <summary>
	/// The SMART Kasa hub family.
	/// </summary>
	SmartKasaHub,
	/// <summary>
	/// The SMART IP camera family.
	/// </summary>
	SmartIpCamera,
	/// <summary>
	/// The SMART Tapo robot vacuum family.
	/// </summary>
	SmartTapoRobovac,
	/// <summary>
	/// The SMART Tapo chime family.
	/// </summary>
	SmartTapoChime,
	/// <summary>
	/// The SMART Tapo doorbell family.
	/// </summary>
	SmartTapoDoorbell,
	}

/// <summary>
/// Represents python-kasa style transport encryption identifiers.
/// </summary>
public enum DeviceEncryptionKind
	{
	/// <summary>
	/// The encryption kind is unknown.
	/// </summary>
	Unknown,
	/// <summary>
	/// The legacy XOR transport encryption.
	/// </summary>
	Xor,
	/// <summary>
	/// The AES-based smart transport encryption.
	/// </summary>
	Aes,
	/// <summary>
	/// The KLAP transport encryption.
	/// </summary>
	Klap,
	/// <summary>
	/// The TPAP transport encryption reported by newer discovery responses.
	/// </summary>
	Tpap,
	}

/// <summary>
/// Represents parsed connection metadata similar to python-kasa's DeviceConnectionParameters.
/// </summary>
public sealed class DeviceConnectionParameters
	{
	/// <summary>
	/// Initializes a new instance of the <see cref="DeviceConnectionParameters" /> class.
	/// </summary>
	public DeviceConnectionParameters (
		DeviceFamilyKind deviceFamily,
		DeviceEncryptionKind encryptionKind,
		int? loginVersion = null,
		bool useHttps = false,
		int? httpPort = null)
		{
		DeviceFamily = deviceFamily;
		EncryptionKind = encryptionKind;
		LoginVersion = loginVersion;
		UseHttps = useHttps;
		HttpPort = httpPort;
		}

	/// <summary>
	/// Gets the device family identifier.
	/// </summary>
	public DeviceFamilyKind DeviceFamily
		{
		get;
		}

	/// <summary>
	/// Gets the transport encryption identifier.
	/// </summary>
	public DeviceEncryptionKind EncryptionKind
		{
		get;
		}

	/// <summary>
	/// Gets the optional login version.
	/// </summary>
	public int? LoginVersion
		{
		get;
		}

	/// <summary>
	/// Gets a value indicating whether HTTPS is supported or required.
	/// </summary>
	public bool UseHttps
		{
		get;
		}

	/// <summary>
	/// Gets the HTTP port reported by discovery when available.
	/// </summary>
	public int? HttpPort
		{
		get;
		}

	internal DeviceTransportKind TransportKind => EncryptionKind switch
		{
			DeviceEncryptionKind.Xor => DeviceTransportKind.LegacyXor,
			DeviceEncryptionKind.Aes => DeviceTransportKind.HttpToken,
			DeviceEncryptionKind.Klap => DeviceTransportKind.HttpToken,
			DeviceEncryptionKind.Tpap => DeviceTransportKind.HttpToken,
			_ => DeviceTransportKind.HttpToken,
		};
	}

/// <summary>
/// Represents the known default credential profiles used by python-kasa.
/// </summary>
public enum DefaultCredentialProfile
	{
	/// <summary>
	/// No default profile.
	/// </summary>
	None,

	/// <summary>
	/// Default credentials for Kasa devices.
	/// </summary>
	Kasa,

	/// <summary>
	/// Default credentials for Kasa cameras.
	/// </summary>
	KasaCamera,

	/// <summary>
	/// Default credentials for Tapo devices.
	/// </summary>
	Tapo,

	/// <summary>
	/// Default credentials for Tapo cameras.
	/// </summary>
	TapoCamera,

	/// <summary>
	/// Default credentials for level 3 Tapo cameras.
	/// </summary>
	TapoCameraLv3,
	}

/// <summary>
/// Represents transport-specific connection options.
/// </summary>
public sealed class DeviceConnectionOptions
	{
	/// <summary>
	/// Initializes a new instance of the <see cref="DeviceConnectionOptions" /> class.
	/// </summary>
	/// <param name="transportKind">The transport to use when communicating with the device.</param>
	/// <param name="connectionParameters">The parsed protocol family and encryption metadata, when known.</param>
	/// <param name="useSsl">A value indicating whether HTTPS should be used for HTTP token transport.</param>
	/// <param name="useDefaultCredentials">A value indicating whether a known default credential profile should be used when explicit credentials are not supplied.</param>
	/// <param name="defaultCredentialProfile">The default credential profile to use when <paramref name="useDefaultCredentials" /> is enabled.</param>
	/// <param name="applicationPath">The application API path for HTTP token transport.</param>
	/// <param name="useSecurePassthrough">A value indicating whether the HTTP transport should wrap device requests in the secure passthrough envelope when supported.</param>
	/// <param name="tpapKeepAliveInterval">The optional TPAP keepalive interval. <see langword="null" /> uses the transport default, and <see cref="TimeSpan.Zero" /> disables TPAP keepalive.</param>
	/// <exception cref="ArgumentException">Thrown when <paramref name="applicationPath" /> is empty or whitespace.</exception>
	public DeviceConnectionOptions (
		DeviceTransportKind transportKind = DeviceTransportKind.Auto,
		DeviceConnectionParameters? connectionParameters = null,
		bool useSsl = false,
		bool useDefaultCredentials = false,
		DefaultCredentialProfile defaultCredentialProfile = DefaultCredentialProfile.None,
		string applicationPath = "/app",
		bool useSecurePassthrough = true,
		TimeSpan? tpapKeepAliveInterval = null)
		{
		if (string.IsNullOrWhiteSpace (applicationPath))
			{
			throw new ArgumentException ("An application path is required.", nameof (applicationPath));
			}

		if (tpapKeepAliveInterval is TimeSpan keepAliveInterval && keepAliveInterval < TimeSpan.Zero)
			{
			throw new ArgumentOutOfRangeException (nameof (tpapKeepAliveInterval), keepAliveInterval, "The TPAP keepalive interval must be zero or positive.");
			}

		TransportKind = transportKind;
		ConnectionParameters = connectionParameters;
		UseSsl = useSsl;
		UseDefaultCredentials = useDefaultCredentials;
		DefaultCredentialProfile = defaultCredentialProfile;
		ApplicationPath = applicationPath;
		UseSecurePassthrough = useSecurePassthrough;
		TpapKeepAliveInterval = tpapKeepAliveInterval;
		}

	/// <summary>
	/// Gets the transport kind.
	/// </summary>
	public DeviceTransportKind TransportKind
		{
		get;
		}

	/// <summary>
	/// Gets the parsed connection parameters, when known.
	/// </summary>
	public DeviceConnectionParameters? ConnectionParameters
		{
		get;
		}

	/// <summary>
	/// Gets a value indicating whether HTTPS should be used for HTTP token transport.
	/// </summary>
	public bool UseSsl
		{
		get;
		}

	/// <summary>
	/// Gets a value indicating whether a known default credential profile should be used.
	/// </summary>
	public bool UseDefaultCredentials
		{
		get;
		}

	/// <summary>
	/// Gets the default credential profile.
	/// </summary>
	public DefaultCredentialProfile DefaultCredentialProfile
		{
		get;
		}

	/// <summary>
	/// Gets the application API path.
	/// </summary>
	public string ApplicationPath
		{
		get;
		}

	/// <summary>
	/// Gets a value indicating whether the HTTP transport should use secure passthrough wrapping when available.
	/// </summary>
	public bool UseSecurePassthrough
		{
		get;
		}

	/// <summary>
	/// Gets the optional TPAP keepalive interval.
	/// </summary>
	public TimeSpan? TpapKeepAliveInterval
		{
		get;
		}
	}

/// <summary>
/// Represents the configuration used to connect to a device.
/// </summary>
public sealed class DeviceConfiguration
	{
	/// <summary>
	/// Initializes a new instance of the <see cref="DeviceConfiguration" /> class.
	/// </summary>
	/// <param name="host">The device host name or IP address.</param>
	/// <param name="port">The device control port.</param>
	/// <param name="credentials">Optional credentials required for authenticated devices.</param>
	/// <param name="connectionOptions">Transport-specific connection options.</param>
	/// <param name="timeout">The per-operation timeout. If <see langword="null" />, a five second timeout is used.</param>
	/// <exception cref="ArgumentException">Thrown when <paramref name="host" /> is empty or whitespace.</exception>
	/// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="port" /> is outside the TCP/UDP port range.</exception>
	public DeviceConfiguration (
		string host,
		int port = 9999,
		DeviceCredentials? credentials = null,
		DeviceConnectionOptions? connectionOptions = null,
		TimeSpan? timeout = null)
		{
		if (string.IsNullOrWhiteSpace (host))
			{
			throw new ArgumentException ("A device host is required.", nameof (host));
			}

		if (port is < 1 or > 65535)
			{
			throw new ArgumentOutOfRangeException (nameof (port), port, "The device port must be between 1 and 65535.");
			}

		DeviceConnectionOptions resolvedConnectionOptions = connectionOptions ?? new DeviceConnectionOptions ();
		Host = host;
		Port = ResolvePort (port, resolvedConnectionOptions);
		Credentials = credentials;
		ConnectionOptions = resolvedConnectionOptions;
		Timeout = timeout ?? TimeSpan.FromSeconds (5);
		}

	/// <summary>
	/// Gets the device host name or IP address.
	/// </summary>
	public string Host
		{
		get;
		}

	/// <summary>
	/// Gets the device control port.
	/// </summary>
	public int Port
		{
		get;
		}

	/// <summary>
	/// Gets the optional device credentials.
	/// </summary>
	public DeviceCredentials? Credentials
		{
		get;
		}

	/// <summary>
	/// Gets the transport-specific connection options.
	/// </summary>
	public DeviceConnectionOptions ConnectionOptions
		{
		get;
		}

	/// <summary>
	/// Gets the per-operation timeout.
	/// </summary>
	public TimeSpan Timeout
		{
		get;
		}

	private static int ResolvePort (int port, DeviceConnectionOptions connectionOptions)
		{
		if (connectionOptions.ConnectionParameters?.HttpPort is int discoveredHttpPort
			&& discoveredHttpPort > 0
			&& port == 9999)
			{
			return discoveredHttpPort;
			}

		if (port != 9999)
			{
			return port;
			}

		if (connectionOptions.TransportKind == DeviceTransportKind.Auto)
			{
			DeviceConnectionParameters? connectionParameters = connectionOptions.ConnectionParameters;
			if (connectionParameters is null || connectionParameters.TransportKind != DeviceTransportKind.HttpToken)
				{
				return port;
				}
			}
		else if (connectionOptions.TransportKind != DeviceTransportKind.HttpToken)
			{
			return port;
			}

		return connectionOptions.UseSsl ? 443 : 80;
		}
	}

/// <summary>
/// Represents a single discovery response from a device.
/// </summary>
public sealed class DiscoveryResult
	{
	internal DiscoveryResult (
		string host,
		DeviceType deviceType,
		string? alias,
		string? model,
		string? deviceId,
		string rawJson,
		DeviceTransportKind transportKind,
		bool supportsHttps,
		int? port,
		DeviceConnectionParameters? connectionParameters,
		TpapDiscoveryMetadata? tpapMetadata = null,
		int? protocolVersion = null,
		bool? tpapPreferred = null)
		{
		Host = host;
		DeviceType = deviceType;
		Alias = alias;
		Model = model;
		DeviceId = deviceId;
		RawJson = rawJson;
		TransportKind = transportKind;
		SupportsHttps = supportsHttps;
		Port = port;
		ConnectionParameters = connectionParameters;
		TpapMetadata = tpapMetadata;
		ProtocolVersion = protocolVersion;
		TpapPreferred = tpapPreferred;
		}

	/// <summary>
	/// Gets the device host that produced the discovery response.
	/// </summary>
	public string Host
		{
		get;
		}

	/// <summary>
	/// Gets the inferred device family.
	/// </summary>
	public DeviceType DeviceType
		{
		get;
		}

	/// <summary>
	/// Gets the user-visible device alias, when available.
	/// </summary>
	public string? Alias
		{
		get;
		}

	/// <summary>
	/// Gets the model identifier, when available.
	/// </summary>
	public string? Model
		{
		get;
		}

	/// <summary>
	/// Gets the device identifier, when available.
	/// </summary>
	public string? DeviceId
		{
		get;
		}

	/// <summary>
	/// Gets the raw JSON payload returned by the device.
	/// </summary>
	public string RawJson
		{
		get;
		}

	internal DeviceTransportKind TransportKind
		{
		get;
		}

	internal bool SupportsHttps
		{
		get;
		}

	internal int? Port
		{
		get;
		}

	internal DeviceConnectionParameters? ConnectionParameters
		{
		get;
		}

	/// <summary>
	/// Gets the advertised TPAP metadata, when present.
	/// </summary>
	public TpapDiscoveryMetadata? TpapMetadata
		{
		get;
		}

	/// <summary>
	/// Gets the advertised protocol version, when present.
	/// </summary>
	public int? ProtocolVersion
		{
		get;
		}

	/// <summary>
	/// Gets a value indicating whether discovery marked TPAP as preferred.
	/// </summary>
	public bool? TpapPreferred
		{
		get;
		}
	}

/// <summary>
/// Represents TPAP-specific discovery metadata when present.
/// </summary>
public sealed class TpapDiscoveryMetadata
	{
	/// <summary>
	/// Initializes a new instance of the <see cref="TpapDiscoveryMetadata" /> class.
	/// </summary>
	/// <param name="port">The TPAP-advertised port, when present.</param>
	/// <param name="pakeModes">The PAKE modes advertised by discovery, when present.</param>
	/// <param name="tls">The advertised TLS mode, when present.</param>
	/// <param name="dac">The advertised DAC mode, when present.</param>
	/// <param name="noc">The advertised NOC mode, when present.</param>
	public TpapDiscoveryMetadata (int? port, IReadOnlyList<int>? pakeModes, int? tls, int? dac, int? noc)
		{
		Port = port;
		PakeModes = pakeModes;
		Tls = tls;
		Dac = dac;
		Noc = noc;
		}

	/// <summary>
	/// Gets the TPAP-advertised port.
	/// </summary>
	public int? Port
		{
		get;
		}

	/// <summary>
	/// Gets the advertised PAKE modes.
	/// </summary>
	public IReadOnlyList<int>? PakeModes
		{
		get;
		}

	/// <summary>
	/// Gets the advertised TLS mode.
	/// </summary>
	public int? Tls
		{
		get;
		}

	/// <summary>
	/// Gets the advertised DAC mode.
	/// </summary>
	public int? Dac
		{
		get;
		}

	/// <summary>
	/// Gets the advertised NOC mode.
	/// </summary>
	public int? Noc
		{
		get;
		}
	}

/// <summary>
/// Represents the kind of a feature exposed by a device.
/// </summary>
public enum FeatureKind
	{
	/// <summary>
	/// A read-only informational value.
	/// </summary>
	Info,

	/// <summary>
	/// A boolean on/off style value.
	/// </summary>
	Switch,

	/// <summary>
	/// A numeric value.
	/// </summary>
	Number,

	/// <summary>
	/// A constrained choice value.
	/// </summary>
	Choice,

	/// <summary>
	/// An action that can be invoked.
	/// </summary>
	Action,
	}

/// <summary>
/// Represents a normalized device feature similar to python-kasa's feature interface.
/// </summary>
public sealed class DeviceFeature
	{
	/// <summary>
	/// Initializes a new instance of the <see cref="DeviceFeature" /> class.
	/// </summary>
	/// <param name="id">The stable feature identifier.</param>
	/// <param name="name">The display name of the feature.</param>
	/// <param name="kind">The kind of feature.</param>
	/// <param name="value">The current feature value.</param>
	/// <param name="unit">The optional unit for numeric or informational values.</param>
	/// <param name="isReadOnly">A value indicating whether the feature is read-only.</param>
	/// <param name="minimumValue">The optional minimum numeric value.</param>
	/// <param name="maximumValue">The optional maximum numeric value.</param>
	/// <param name="choices">The optional allowed choice values.</param>
	/// <exception cref="ArgumentException">Thrown when <paramref name="id" /> or <paramref name="name" /> is empty or whitespace.</exception>
	public DeviceFeature (
		string id,
		string name,
		FeatureKind kind,
		object? value,
		string? unit = null,
		bool isReadOnly = true,
		double? minimumValue = null,
		double? maximumValue = null,
		IReadOnlyList<string>? choices = null)
		{
		if (string.IsNullOrWhiteSpace (id))
			{
			throw new ArgumentException ("A feature identifier is required.", nameof (id));
			}

		if (string.IsNullOrWhiteSpace (name))
			{
			throw new ArgumentException ("A feature name is required.", nameof (name));
			}

		Id = id;
		Name = name;
		Kind = kind;
		Value = value;
		Unit = unit;
		IsReadOnly = isReadOnly;
		MinimumValue = minimumValue;
		MaximumValue = maximumValue;
		Choices = choices ?? Array.Empty<string> ();
		}

	/// <summary>
	/// Gets the stable feature identifier.
	/// </summary>
	public string Id
		{
		get;
		}

	/// <summary>
	/// Gets the display name of the feature.
	/// </summary>
	public string Name
		{
		get;
		}

	/// <summary>
	/// Gets the kind of feature.
	/// </summary>
	public FeatureKind Kind
		{
		get;
		}

	/// <summary>
	/// Gets the current feature value.
	/// </summary>
	public object? Value
		{
		get;
		}

	/// <summary>
	/// Gets the optional unit associated with <see cref="Value" />.
	/// </summary>
	public string? Unit
		{
		get;
		}

	/// <summary>
	/// Gets a value indicating whether the feature is read-only.
	/// </summary>
	public bool IsReadOnly
		{
		get;
		}

	/// <summary>
	/// Gets the optional minimum numeric value.
	/// </summary>
	public double? MinimumValue
		{
		get;
		}

	/// <summary>
	/// Gets the optional maximum numeric value.
	/// </summary>
	public double? MaximumValue
		{
		get;
		}

	/// <summary>
	/// Gets the optional allowed choice values.
	/// </summary>
	public IReadOnlyList<string> Choices
		{
		get;
		}
	}

/// <summary>
/// Represents normalized energy metering information from a device.
/// </summary>
public sealed class EnergyUsage
	{
	internal EnergyUsage (
		double? currentPowerWatts,
		double? voltageVolts,
		double? currentAmps,
		double? totalKilowattHours,
		double? todayKilowattHours,
		double? monthKilowattHours,
		string rawJson)
		{
		CurrentPowerWatts = currentPowerWatts;
		VoltageVolts = voltageVolts;
		CurrentAmps = currentAmps;
		TotalKilowattHours = totalKilowattHours;
		TodayKilowattHours = todayKilowattHours;
		MonthKilowattHours = monthKilowattHours;
		RawJson = rawJson;
		}

	/// <summary>
	/// Gets the current power draw in watts.
	/// </summary>
	public double? CurrentPowerWatts
		{
		get;
		}

	/// <summary>
	/// Gets the line voltage in volts.
	/// </summary>
	public double? VoltageVolts
		{
		get;
		}

	/// <summary>
	/// Gets the current draw in amps.
	/// </summary>
	public double? CurrentAmps
		{
		get;
		}

	/// <summary>
	/// Gets the total measured energy in kilowatt-hours.
	/// </summary>
	public double? TotalKilowattHours
		{
		get;
		}

	/// <summary>
	/// Gets today's measured energy in kilowatt-hours when periodic statistics are available.
	/// </summary>
	public double? TodayKilowattHours
		{
		get;
		}

	/// <summary>
	/// Gets this month's measured energy in kilowatt-hours when periodic statistics are available.
	/// </summary>
	public double? MonthKilowattHours
		{
		get;
		}

	/// <summary>
	/// Gets the raw JSON payload returned by the device.
	/// </summary>
	public string RawJson
		{
		get;
		}
	}

/// <summary>
/// Represents a child device exposed by a parent strip, hub, or similar device.
/// </summary>
public sealed class ChildDeviceInfo
	{
	internal ChildDeviceInfo (
		string id,
		string? alias,
		string? model,
		DeviceType deviceType,
		bool? isOn,
		string rawJson,
		string? category = null,
		IReadOnlyList<string>? componentIds = null,
		IReadOnlyList<DeviceFeature>? features = null)
		{
		Id = id;
		Alias = alias;
		Model = model;
		DeviceType = deviceType;
		IsOn = isOn;
		RawJson = rawJson;
		Category = category;
		ComponentIds = componentIds ?? Array.Empty<string> ();
		Features = features ?? Array.Empty<DeviceFeature> ();
		}

	/// <summary>
	/// Gets the child device identifier.
	/// </summary>
	public string Id
		{
		get;
		}

	/// <summary>
	/// Gets the child device alias.
	/// </summary>
	public string? Alias
		{
		get;
		}

	/// <summary>
	/// Gets the child device model.
	/// </summary>
	public string? Model
		{
		get;
		}

	/// <summary>
	/// Gets the inferred child device family.
	/// </summary>
	public DeviceType DeviceType
		{
		get;
		}

	/// <summary>
	/// Gets the python-kasa style child category when reported.
	/// </summary>
	public string? Category
		{
		get;
		}

	/// <summary>
	/// Gets a value indicating whether the child device appears to be on.
	/// </summary>
	public bool? IsOn
		{
		get;
		}

	/// <summary>
	/// Gets the reported child component identifiers.
	/// </summary>
	public IReadOnlyList<string> ComponentIds
		{
		get;
		}

	/// <summary>
	/// Gets the normalized child features derived from the latest payload.
	/// </summary>
	public IReadOnlyList<DeviceFeature> Features
		{
		get;
		}

	/// <summary>
	/// Gets the raw JSON payload returned by the device.
	/// </summary>
	public string RawJson
		{
		get;
		}
	}

/// <summary>
/// Represents trigger log state reported by an event-driven child device.
/// </summary>
public sealed class ChildTriggerLogState
	{
	internal ChildTriggerLogState (IReadOnlyList<ChildTriggerLogEntry> logs) => Logs = logs;

	/// <summary>
	/// Gets the trigger log entries reported for the child device.
	/// </summary>
	public IReadOnlyList<ChildTriggerLogEntry> Logs
		{
		get;
		}
	}

/// <summary>
/// Represents a single trigger log entry reported by a child device.
/// </summary>
public sealed class ChildTriggerLogEntry
	{
	internal ChildTriggerLogEntry (int? id, string? eventId, long? timestamp, string? eventName)
		{
		Id = id;
		EventId = eventId;
		Timestamp = timestamp;
		EventName = eventName;
		}

	/// <summary>
	/// Gets the numeric log identifier, when reported.
	/// </summary>
	public int? Id
		{
		get;
		}

	/// <summary>
	/// Gets the device-reported event identifier.
	/// </summary>
	public string? EventId
		{
		get;
		}

	/// <summary>
	/// Gets the raw event timestamp value, when reported.
	/// </summary>
	public long? Timestamp
		{
		get;
		}

	/// <summary>
	/// Gets the event name such as a button click type.
	/// </summary>
	public string? EventName
		{
		get;
		}
	}

/// <summary>
/// Represents battery-related child sensor state.
/// </summary>
public sealed class ChildBatterySensorState
	{
	internal ChildBatterySensorState (int? batteryLevel, bool? batteryLow)
		{
		BatteryLevel = batteryLevel;
		BatteryLow = batteryLow;
		}

	/// <summary>
	/// Gets the latest battery level percentage, when reported.
	/// </summary>
	public int? BatteryLevel
		{
		get;
		}

	/// <summary>
	/// Gets whether the child sensor reports a low battery condition.
	/// </summary>
	public bool? BatteryLow
		{
		get;
		}
	}

/// <summary>
/// Represents contact sensor state for a child device.
/// </summary>
public sealed class ChildContactSensorState
	{
	internal ChildContactSensorState (bool? isOpen) => IsOpen = isOpen;

	/// <summary>
	/// Gets whether the contact sensor is currently open.
	/// </summary>
	public bool? IsOpen
		{
		get;
		}
	}

/// <summary>
/// Represents motion sensor state for a child device.
/// </summary>
public sealed class ChildMotionSensorState
	{
	internal ChildMotionSensorState (bool? motionDetected) => MotionDetected = motionDetected;

	/// <summary>
	/// Gets whether motion is currently detected.
	/// </summary>
	public bool? MotionDetected
		{
		get;
		}
	}

/// <summary>
/// Represents water leak sensor state for a child device.
/// </summary>
public sealed class ChildWaterLeakSensorState
	{
	internal ChildWaterLeakSensorState (string? status, bool? alert, long? alertTimestamp)
		{
		Status = status;
		Alert = alert;
		AlertTimestamp = alertTimestamp;
		}

	/// <summary>
	/// Gets the reported water leak status string.
	/// </summary>
	public string? Status
		{
		get;
		}

	/// <summary>
	/// Gets whether an alert is currently active.
	/// </summary>
	public bool? Alert
		{
		get;
		}

	/// <summary>
	/// Gets the raw timestamp of the latest alert, when reported.
	/// </summary>
	public long? AlertTimestamp
		{
		get;
		}
	}

/// <summary>
/// Represents temperature sensor state for a child device.
/// </summary>
public sealed class ChildTemperatureSensorState
	{
	internal ChildTemperatureSensorState (double? temperature, bool? warning, string? unit, double? minimumComfortTemperature, double? maximumComfortTemperature)
		{
		Temperature = temperature;
		Warning = warning;
		Unit = unit;
		MinimumComfortTemperature = minimumComfortTemperature;
		MaximumComfortTemperature = maximumComfortTemperature;
		}

	/// <summary>
	/// Gets the latest reported temperature value.
	/// </summary>
	public double? Temperature
		{
		get;
		}

	/// <summary>
	/// Gets whether the child device reports a temperature warning.
	/// </summary>
	public bool? Warning
		{
		get;
		}

	/// <summary>
	/// Gets the temperature unit such as celsius or fahrenheit.
	/// </summary>
	public string? Unit
		{
		get;
		}

	/// <summary>
	/// Gets the minimum comfort temperature, when reported.
	/// </summary>
	public double? MinimumComfortTemperature
		{
		get;
		}

	/// <summary>
	/// Gets the maximum comfort temperature, when reported.
	/// </summary>
	public double? MaximumComfortTemperature
		{
		get;
		}
	}

/// <summary>
/// Represents humidity sensor state for a child device.
/// </summary>
public sealed class ChildHumiditySensorState
	{
	internal ChildHumiditySensorState (int? humidity, bool? warning, double? minimumComfortHumidity, double? maximumComfortHumidity)
		{
		Humidity = humidity;
		Warning = warning;
		MinimumComfortHumidity = minimumComfortHumidity;
		MaximumComfortHumidity = maximumComfortHumidity;
		}

	/// <summary>
	/// Gets the latest reported humidity percentage.
	/// </summary>
	public int? Humidity
		{
		get;
		}

	/// <summary>
	/// Gets whether the child device reports a humidity warning.
	/// </summary>
	public bool? Warning
		{
		get;
		}

	/// <summary>
	/// Gets the minimum comfort humidity, when reported.
	/// </summary>
	public double? MinimumComfortHumidity
		{
		get;
		}

	/// <summary>
	/// Gets the maximum comfort humidity, when reported.
	/// </summary>
	public double? MaximumComfortHumidity
		{
		get;
		}
	}

/// <summary>
/// Represents report-mode state for a child sensor device.
/// </summary>
public sealed class ChildReportModeState
	{
	internal ChildReportModeState (int? reportInterval) => ReportInterval = reportInterval;

	/// <summary>
	/// Gets the sensor report interval in seconds, when reported.
	/// </summary>
	public int? ReportInterval
		{
		get;
		}
	}

/// <summary>
/// Represents double-click state for a child button device.
/// </summary>
public sealed class ChildDoubleClickState
	{
	internal ChildDoubleClickState (bool? enabled) => Enabled = enabled;

	/// <summary>
	/// Gets whether double-click is enabled.
	/// </summary>
	public bool? Enabled
		{
		get;
		}
	}

/// <summary>
/// Represents a detected child device during hub scanning.
/// </summary>
public sealed class DetectedChildDevice
	{
	internal DetectedChildDevice (string deviceId, string? model, string? category, string? rawJson)
		{
		DeviceId = deviceId;
		Model = model;
		Category = category;
		RawJson = rawJson;
		}

	/// <summary>
	/// Gets the detected device identifier.
	/// </summary>
	public string DeviceId
		{
		get;
		}

	/// <summary>
	/// Gets the detected device model.
	/// </summary>
	public string? Model
		{
		get;
		}

	/// <summary>
	/// Gets the detected device category.
	/// </summary>
	public string? Category
		{
		get;
		}

	/// <summary>
	/// Gets the raw JSON for the detected device.
	/// </summary>
	public string? RawJson
		{
		get;
		}
	}

/// <summary>
/// Represents the latest child setup scan result reported by a hub.
/// </summary>
public sealed class ChildSetupScanResult
	{
	internal ChildSetupScanResult (IReadOnlyList<string> supportedCategories, IReadOnlyList<DetectedChildDevice> detectedDevices)
		{
		SupportedCategories = supportedCategories;
		DetectedDevices = detectedDevices;
		}

	/// <summary>
	/// Gets the supported child categories reported by the hub.
	/// </summary>
	public IReadOnlyList<string> SupportedCategories
		{
		get;
		}

	/// <summary>
	/// Gets the detected child devices from the latest scan.
	/// </summary>
	public IReadOnlyList<DetectedChildDevice> DetectedDevices
		{
		get;
		}
	}

/// <summary>
/// Represents frost-protection state for a child thermostat device.
/// </summary>
public sealed class ChildFrostProtectionState
	{
	internal ChildFrostProtectionState (bool? enabled, int? minimumTemperature, string? unit)
		{
		Enabled = enabled;
		MinimumTemperature = minimumTemperature;
		Unit = unit;
		}

	/// <summary>
	/// Gets whether frost protection is currently enabled.
	/// </summary>
	public bool? Enabled
		{
		get;
		}

	/// <summary>
	/// Gets the minimum frost-protection temperature, when reported.
	/// </summary>
	public int? MinimumTemperature
		{
		get;
		}

	/// <summary>
	/// Gets the reported temperature unit.
	/// </summary>
	public string? Unit
		{
		get;
		}
	}

/// <summary>
/// Represents child-protection state for a child thermostat device.
/// </summary>
public sealed class ChildProtectionState
	{
	internal ChildProtectionState (bool? enabled) => Enabled = enabled;

	/// <summary>
	/// Gets whether child protection is enabled.
	/// </summary>
	public bool? Enabled
		{
		get;
		}
	}

/// <summary>
/// Represents temperature-control state for a child thermostat device.
/// </summary>
public sealed class ChildTemperatureControlState
	{
	internal ChildTemperatureControlState (
		bool? enabled,
		double? targetTemperature,
		int? minimumTargetTemperature,
		int? maximumTargetTemperature,
		int? temperatureOffset,
		IReadOnlyList<string> states)
		{
		Enabled = enabled;
		TargetTemperature = targetTemperature;
		MinimumTargetTemperature = minimumTargetTemperature;
		MaximumTargetTemperature = maximumTargetTemperature;
		TemperatureOffset = temperatureOffset;
		States = states;
		}

	/// <summary>
	/// Gets whether temperature control is enabled.
	/// </summary>
	public bool? Enabled
		{
		get;
		}

	/// <summary>
	/// Gets the target temperature, when reported.
	/// </summary>
	public double? TargetTemperature
		{
		get;
		}

	/// <summary>
	/// Gets the minimum supported target temperature, when reported.
	/// </summary>
	public int? MinimumTargetTemperature
		{
		get;
		}

	/// <summary>
	/// Gets the maximum supported target temperature, when reported.
	/// </summary>
	public int? MaximumTargetTemperature
		{
		get;
		}

	/// <summary>
	/// Gets the temperature offset, when reported.
	/// </summary>
	public int? TemperatureOffset
		{
		get;
		}

	/// <summary>
	/// Gets the raw TRV state flags reported by the device.
	/// </summary>
	public IReadOnlyList<string> States
		{
		get;
		}
	}

/// <summary>
/// Represents aggregated thermostat state for a child thermostat device.
/// </summary>
public sealed class ChildThermostatState
	{
	internal ChildThermostatState (
		bool? enabled,
		double? targetTemperature,
		double? currentTemperature,
		string? unit,
		IReadOnlyList<string> states)
		{
		Enabled = enabled;
		TargetTemperature = targetTemperature;
		CurrentTemperature = currentTemperature;
		Unit = unit;
		States = states;
		}

	/// <summary>
	/// Gets whether the thermostat is enabled.
	/// </summary>
	public bool? Enabled
		{
		get;
		}

	/// <summary>
	/// Gets the target temperature, when reported.
	/// </summary>
	public double? TargetTemperature
		{
		get;
		}

	/// <summary>
	/// Gets the current measured temperature, when reported.
	/// </summary>
	public double? CurrentTemperature
		{
		get;
		}

	/// <summary>
	/// Gets the reported temperature unit.
	/// </summary>
	public string? Unit
		{
		get;
		}

	/// <summary>
	/// Gets the raw TRV state flags reported by the device.
	/// </summary>
	public IReadOnlyList<string> States
		{
		get;
		}
	}

/// <summary>
/// Represents normalized rule-related state for Kasa local devices.
/// </summary>
public sealed class RuleModuleState
	{
	internal RuleModuleState (
		CountdownRuleState? countdown,
		IReadOnlyList<ScheduledRule> schedules,
		IReadOnlyList<ScheduledRule> antitheftRules,
		string rawJson)
		{
		Countdown = countdown;
		Schedules = schedules;
		AntitheftRules = antitheftRules;
		RawJson = rawJson;
		}

	/// <summary>
	/// Gets the current countdown timer state, when reported.
	/// </summary>
	public CountdownRuleState? Countdown
		{
		get;
		}

	/// <summary>
	/// Gets the configured schedule rules.
	/// </summary>
	public IReadOnlyList<ScheduledRule> Schedules
		{
		get;
		}

	/// <summary>
	/// Gets the configured antitheft rules.
	/// </summary>
	public IReadOnlyList<ScheduledRule> AntitheftRules
		{
		get;
		}

	/// <summary>
	/// Gets the raw JSON payload returned by the device.
	/// </summary>
	public string RawJson
		{
		get;
		}
	}

/// <summary>
/// Represents a normalized countdown timer state.
/// </summary>
public sealed class CountdownRuleState
	{
	internal CountdownRuleState (bool? isEnabled, bool? isActive, int? delaySeconds, bool? actionTurnsOn, string rawJson)
		{
		IsEnabled = isEnabled;
		IsActive = isActive;
		DelaySeconds = delaySeconds;
		ActionTurnsOn = actionTurnsOn;
		RawJson = rawJson;
		}

	/// <summary>
	/// Gets a value indicating whether the countdown feature is enabled.
	/// </summary>
	public bool? IsEnabled
		{
		get;
		}

	/// <summary>
	/// Gets a value indicating whether a countdown is currently active.
	/// </summary>
	public bool? IsActive
		{
		get;
		}

	/// <summary>
	/// Gets the configured countdown delay in seconds.
	/// </summary>
	public int? DelaySeconds
		{
		get;
		}

	/// <summary>
	/// Gets a value indicating whether the countdown action turns the device on.
	/// </summary>
	public bool? ActionTurnsOn
		{
		get;
		}

	/// <summary>
	/// Gets the raw JSON payload returned by the device.
	/// </summary>
	public string RawJson
		{
		get;
		}
	}

/// <summary>
/// Represents a normalized schedule or antitheft rule.
/// </summary>
public sealed class ScheduledRule
	{
	internal ScheduledRule (string id, string? name, bool? isEnabled, bool? actionTurnsOn, int? startMinute, int? endMinute, string rawJson)
		{
		Id = id;
		Name = name;
		IsEnabled = isEnabled;
		ActionTurnsOn = actionTurnsOn;
		StartMinute = startMinute;
		EndMinute = endMinute;
		RawJson = rawJson;
		}

	/// <summary>
	/// Gets the stable rule identifier.
	/// </summary>
	public string Id
		{
		get;
		}

	/// <summary>
	/// Gets the optional rule name.
	/// </summary>
	public string? Name
		{
		get;
		}

	/// <summary>
	/// Gets a value indicating whether the rule is enabled.
	/// </summary>
	public bool? IsEnabled
		{
		get;
		}

	/// <summary>
	/// Gets a value indicating whether the rule action turns the device on.
	/// </summary>
	public bool? ActionTurnsOn
		{
		get;
		}

	/// <summary>
	/// Gets the start minute-of-day value when reported.
	/// </summary>
	public int? StartMinute
		{
		get;
		}

	/// <summary>
	/// Gets the end minute-of-day value when reported.
	/// </summary>
	public int? EndMinute
		{
		get;
		}

	/// <summary>
	/// Gets the raw JSON payload returned by the device.
	/// </summary>
	public string RawJson
		{
		get;
		}
	}

/// <summary>
/// Represents normalized firmware-related device state.
/// </summary>
public sealed class FirmwareState
	{
	internal FirmwareState (
		string? currentFirmwareVersion,
		string? currentHardwareVersion,
		bool? autoUpdateEnabled,
		string? availableFirmwareVersion,
		bool? updateAvailable,
		string rawJson)
		{
		CurrentFirmwareVersion = currentFirmwareVersion;
		CurrentHardwareVersion = currentHardwareVersion;
		AutoUpdateEnabled = autoUpdateEnabled;
		AvailableFirmwareVersion = availableFirmwareVersion;
		UpdateAvailable = updateAvailable;
		RawJson = rawJson;
		}

	/// <summary>
	/// Gets the current firmware version reported by the device.
	/// </summary>
	public string? CurrentFirmwareVersion
		{
		get;
		}

	/// <summary>
	/// Gets the current hardware version reported by the device.
	/// </summary>
	public string? CurrentHardwareVersion
		{
		get;
		}

	/// <summary>
	/// Gets whether automatic firmware updates are enabled, when reported.
	/// </summary>
	public bool? AutoUpdateEnabled
		{
		get;
		}

	/// <summary>
	/// Gets the latest available firmware version, when requested and reported.
	/// </summary>
	public string? AvailableFirmwareVersion
		{
		get;
		}

	/// <summary>
	/// Gets whether a newer firmware version is available, when known.
	/// </summary>
	public bool? UpdateAvailable
		{
		get;
		}

	/// <summary>
	/// Gets the raw JSON payload returned by the device.
	/// </summary>
	public string RawJson
		{
		get;
		}
	}

/// <summary>
/// Represents normalized cloud-connectivity information.
/// </summary>
public sealed class CloudConnectionState
	{
	internal CloudConnectionState (bool? isConnected, bool? isProvisioned, string? server, string? userName, string rawJson)
		{
		IsConnected = isConnected;
		IsProvisioned = isProvisioned;
		Server = server;
		UserName = userName;
		RawJson = rawJson;
		}

	/// <summary>
	/// Gets whether the device reports an active cloud connection.
	/// </summary>
	public bool? IsConnected
		{
		get;
		}

	/// <summary>
	/// Gets whether the device reports cloud provisioning, when available.
	/// </summary>
	public bool? IsProvisioned
		{
		get;
		}

	/// <summary>
	/// Gets the configured cloud server, when reported.
	/// </summary>
	public string? Server
		{
		get;
		}

	/// <summary>
	/// Gets the cloud account user name, when reported.
	/// </summary>
	public string? UserName
		{
		get;
		}

	/// <summary>
	/// Gets the raw JSON payload returned by the device.
	/// </summary>
	public string RawJson
		{
		get;
		}
	}

/// <summary>
/// Represents normalized device-local time information.
/// </summary>
public sealed class DeviceTimeState
	{
	internal DeviceTimeState (DateTime? localTime, string? region, int? timeDifferenceMinutes, string rawJson)
		{
		LocalTime = localTime;
		Region = region;
		TimeDifferenceMinutes = timeDifferenceMinutes;
		RawJson = rawJson;
		}

	/// <summary>
	/// Gets the local device time, when reported.
	/// </summary>
	public DateTime? LocalTime
		{
		get;
		}

	/// <summary>
	/// Gets the reported timezone region, when available.
	/// </summary>
	public string? Region
		{
		get;
		}

	/// <summary>
	/// Gets the reported UTC offset in minutes, when available.
	/// </summary>
	public int? TimeDifferenceMinutes
		{
		get;
		}

	/// <summary>
	/// Gets the raw JSON payload returned by the device.
	/// </summary>
	public string RawJson
		{
		get;
		}
	}

/// <summary>
/// Represents Matter setup information exposed by a device.
/// </summary>
public sealed class MatterSetupInfo
	{
	internal MatterSetupInfo (string? setupCode, string? setupPayload, string rawJson)
		{
		SetupCode = setupCode;
		SetupPayload = setupPayload;
		RawJson = rawJson;
		}

	/// <summary>
	/// Gets the Matter setup code, when reported.
	/// </summary>
	public string? SetupCode
		{
		get;
		}

	/// <summary>
	/// Gets the Matter setup payload, when reported.
	/// </summary>
	public string? SetupPayload
		{
		get;
		}

	/// <summary>
	/// Gets the raw JSON payload returned by the device.
	/// </summary>
	public string RawJson
		{
		get;
		}
	}

/// <summary>
/// Represents HomeKit setup information exposed by a device.
/// </summary>
public sealed class HomeKitSetupInfo
	{
	internal HomeKitSetupInfo (string? setupCode, string? setupPayload, string rawJson)
		{
		SetupCode = setupCode;
		SetupPayload = setupPayload;
		RawJson = rawJson;
		}

	/// <summary>
	/// Gets the HomeKit setup code, when reported.
	/// </summary>
	public string? SetupCode
		{
		get;
		}

	/// <summary>
	/// Gets the HomeKit setup payload, when reported.
	/// </summary>
	public string? SetupPayload
		{
		get;
		}

	/// <summary>
	/// Gets the raw JSON payload returned by the device.
	/// </summary>
	public string RawJson
		{
		get;
		}
	}

/// <summary>
/// Represents normalized auto-off configuration and timer state.
/// </summary>
public sealed class AutoOffState
	{
	internal AutoOffState (bool? enabled, int? delayMinutes, bool? timerActive, DateTime? autoOffAt, string rawJson)
		{
		Enabled = enabled;
		DelayMinutes = delayMinutes;
		TimerActive = timerActive;
		AutoOffAt = autoOffAt;
		RawJson = rawJson;
		}

	/// <summary>
	/// Gets whether auto-off is enabled.
	/// </summary>
	public bool? Enabled
		{
		get;
		}

	/// <summary>
	/// Gets the configured auto-off delay in minutes, when reported.
	/// </summary>
	public int? DelayMinutes
		{
		get;
		}

	/// <summary>
	/// Gets whether an auto-off timer is currently active, when reported.
	/// </summary>
	public bool? TimerActive
		{
		get;
		}

	/// <summary>
	/// Gets the local time when the device is expected to turn off automatically, when known.
	/// </summary>
	public DateTime? AutoOffAt
		{
		get;
		}

	/// <summary>
	/// Gets the raw JSON payload returned by the device.
	/// </summary>
	public string RawJson
		{
		get;
		}
	}

/// <summary>
/// Represents LED night-mode configuration when the device exposes it.
/// </summary>
public sealed class LedNightModeSettings
	{
	internal LedNightModeSettings (int? startMinute, int? endMinute, string? modeType, int? sunriseOffsetMinutes, int? sunsetOffsetMinutes)
		{
		StartMinute = startMinute;
		EndMinute = endMinute;
		ModeType = modeType;
		SunriseOffsetMinutes = sunriseOffsetMinutes;
		SunsetOffsetMinutes = sunsetOffsetMinutes;
		}

	/// <summary>
	/// Gets the configured night-mode start minute, when reported.
	/// </summary>
	public int? StartMinute
		{
		get;
		}

	/// <summary>
	/// Gets the configured night-mode end minute, when reported.
	/// </summary>
	public int? EndMinute
		{
		get;
		}

	/// <summary>
	/// Gets the night-mode type, such as a scheduled or sunrise/sunset mode.
	/// </summary>
	public string? ModeType
		{
		get;
		}

	/// <summary>
	/// Gets the configured sunrise offset in minutes, when reported.
	/// </summary>
	public int? SunriseOffsetMinutes
		{
		get;
		}

	/// <summary>
	/// Gets the configured sunset offset in minutes, when reported.
	/// </summary>
	public int? SunsetOffsetMinutes
		{
		get;
		}
	}

/// <summary>
/// Represents normalized LED status and mode information.
/// </summary>
public sealed class LedState
	{
	internal LedState (bool? enabled, string? mode, LedNightModeSettings? nightModeSettings, string rawJson)
		{
		Enabled = enabled;
		Mode = mode;
		NightModeSettings = nightModeSettings;
		RawJson = rawJson;
		}

	/// <summary>
	/// Gets whether the device LED is enabled.
	/// </summary>
	public bool? Enabled
		{
		get;
		}

	/// <summary>
	/// Gets the LED mode string, when reported.
	/// </summary>
	public string? Mode
		{
		get;
		}

	/// <summary>
	/// Gets the LED night-mode settings, when reported.
	/// </summary>
	public LedNightModeSettings? NightModeSettings
		{
		get;
		}

	/// <summary>
	/// Gets the raw JSON payload returned by the device.
	/// </summary>
	public string RawJson
		{
		get;
		}
	}

/// <summary>
/// Represents normalized child-lock state for a device.
/// </summary>
public sealed class ChildLockState
	{
	internal ChildLockState (bool? enabled, string rawJson)
		{
		Enabled = enabled;
		RawJson = rawJson;
		}

	/// <summary>
	/// Gets whether child lock is enabled.
	/// </summary>
	public bool? Enabled
		{
		get;
		}

	/// <summary>
	/// Gets the raw JSON payload returned by the device.
	/// </summary>
	public string RawJson
		{
		get;
		}
	}

/// <summary>
/// Represents normalized alarm state for devices such as hubs and chimes.
/// </summary>
public sealed class AlarmState
	{
	internal AlarmState (bool? isActive, string? source, string? sound, string? volume, int? volumeLevel, int? durationSeconds, string rawJson)
		{
		IsActive = isActive;
		Source = source;
		Sound = sound;
		Volume = volume;
		VolumeLevel = volumeLevel;
		DurationSeconds = durationSeconds;
		RawJson = rawJson;
		}

	/// <summary>
	/// Gets whether the alarm is currently active.
	/// </summary>
	public bool? IsActive
		{
		get;
		}

	/// <summary>
	/// Gets the alarm source, when reported.
	/// </summary>
	public string? Source
		{
		get;
		}

	/// <summary>
	/// Gets the selected alarm sound, when reported.
	/// </summary>
	public string? Sound
		{
		get;
		}

	/// <summary>
	/// Gets the selected alarm volume label, when reported.
	/// </summary>
	public string? Volume
		{
		get;
		}

	/// <summary>
	/// Gets the selected alarm volume level, when reported.
	/// </summary>
	public int? VolumeLevel
		{
		get;
		}

	/// <summary>
	/// Gets the configured alarm duration in seconds, when reported.
	/// </summary>
	public int? DurationSeconds
		{
		get;
		}

	/// <summary>
	/// Gets the raw JSON payload returned by the device.
	/// </summary>
	public string RawJson
		{
		get;
		}
	}

/// <summary>
/// Represents normalized overheat protection state.
/// </summary>
public sealed class OverheatProtectionState
	{
	internal OverheatProtectionState (bool? overheated, string rawJson)
		{
		Overheated = overheated;
		RawJson = rawJson;
		}

	/// <summary>
	/// Gets whether the device reports an overheat condition.
	/// </summary>
	public bool? Overheated
		{
		get;
		}

	/// <summary>
	/// Gets the raw JSON payload returned by the device.
	/// </summary>
	public string RawJson
		{
		get;
		}
	}

/// <summary>
/// Represents normalized power protection state.
/// </summary>
public sealed class PowerProtectionState
	{
	internal PowerProtectionState (bool? protectionActive, string rawJson)
		{
		ProtectionActive = protectionActive;
		RawJson = rawJson;
		}

	/// <summary>
	/// Gets whether power protection is active.
	/// </summary>
	public bool? ProtectionActive
		{
		get;
		}

	/// <summary>
	/// Gets the raw JSON payload returned by the device.
	/// </summary>
	public string RawJson
		{
		get;
		}
	}

/// <summary>
/// Represents normalized fan state.
/// </summary>
public sealed class FanState
	{
	internal FanState (bool? isOn, string rawJson)
		{
		IsOn = isOn;
		RawJson = rawJson;
		}

	/// <summary>
	/// Gets whether the fan is on.
	/// </summary>
	public bool? IsOn
		{
		get;
		}

	/// <summary>
	/// Gets the raw JSON payload returned by the device.
	/// </summary>
	public string RawJson
		{
		get;
		}
	}

/// <summary>
/// Represents normalized speaker state.
/// </summary>
public sealed class SpeakerState
	{
	internal SpeakerState (bool? isAvailable, string rawJson)
		{
		IsAvailable = isAvailable;
		RawJson = rawJson;
		}

	/// <summary>
	/// Gets whether speaker capability is available.
	/// </summary>
	public bool? IsAvailable
		{
		get;
		}

	/// <summary>
	/// Gets the raw JSON payload returned by the device.
	/// </summary>
	public string RawJson
		{
		get;
		}
	}

/// <summary>
/// Represents normalized light state for bulbs and light strips.
/// </summary>
public sealed class LightState
	{
	internal LightState (
		bool? isOn,
		int? brightness,
		int? colorTemperature,
		int? hue,
		int? saturation,
		bool supportsEffects,
		LightEffectState? effect,
		HsvColor? hsv,
		IReadOnlyList<LightPresetDefinition> availablePresets,
		string? activePreset,
		string rawJson)
		{
		IsOn = isOn;
		Brightness = brightness;
		ColorTemperature = colorTemperature;
		Hue = hue;
		Saturation = saturation;
		SupportsEffects = supportsEffects;
		Effect = effect;
		Hsv = hsv;
		AvailablePresets = availablePresets;
		ActivePreset = activePreset;
		RawJson = rawJson;
		}

	/// <summary>
	/// Gets a value indicating whether the light is on.
	/// </summary>
	public bool? IsOn
		{
		get;
		}

	/// <summary>
	/// Gets the brightness percentage.
	/// </summary>
	public int? Brightness
		{
		get;
		}

	/// <summary>
	/// Gets the color temperature in kelvin.
	/// </summary>
	public int? ColorTemperature
		{
		get;
		}

	/// <summary>
	/// Gets the hue component.
	/// </summary>
	public int? Hue
		{
		get;
		}

	/// <summary>
	/// Gets the saturation component.
	/// </summary>
	public int? Saturation
		{
		get;
		}

	/// <summary>
	/// Gets a value indicating whether the device reports light-effect capability.
	/// </summary>
	public bool SupportsEffects
		{
		get;
		}

	/// <summary>
	/// Gets the normalized light effect state when the device exposes one.
	/// </summary>
	public LightEffectState? Effect
		{
		get;
		}

	/// <summary>
	/// Gets the normalized HSV color when color information is available.
	/// </summary>
	public HsvColor? Hsv
		{
		get;
		}

	/// <summary>
	/// Gets the named light presets reported by the device.
	/// </summary>
	public IReadOnlyList<LightPresetDefinition> AvailablePresets
		{
		get;
		}

	/// <summary>
	/// Gets the active light preset name when the current state matches one.
	/// </summary>
	public string? ActivePreset
		{
		get;
		}

	/// <summary>
	/// Gets the raw JSON payload returned by the device.
	/// </summary>
	public string RawJson
		{
		get;
		}
	}

/// <summary>
/// Represents a normalized HSV color.
/// </summary>
public sealed class HsvColor
	{
	internal HsvColor (int hue, int saturation, int value)
		{
		Hue = hue;
		Saturation = saturation;
		Value = value;
		}

	/// <summary>
	/// Gets the hue component.
	/// </summary>
	public int Hue
		{
		get;
		}

	/// <summary>
	/// Gets the saturation component.
	/// </summary>
	public int Saturation
		{
		get;
		}

	/// <summary>
	/// Gets the value component.
	/// </summary>
	public int Value
		{
		get;
		}
	}

/// <summary>
/// Represents a normalized lighting effect state.
/// </summary>
public sealed class LightEffectState
	{
	internal LightEffectState (string? identifier, string? name, bool? isEnabled, int? brightness, IReadOnlyList<LightEffectDefinition> availableEffects, string rawJson)
		{
		Identifier = identifier;
		Name = name;
		IsEnabled = isEnabled;
		Brightness = brightness;
		AvailableEffects = availableEffects;
		RawJson = rawJson;
		}

	/// <summary>
	/// Gets the device-specific effect identifier.
	/// </summary>
	public string? Identifier
		{
		get;
		}

	/// <summary>
	/// Gets the user-visible effect name, when available.
	/// </summary>
	public string? Name
		{
		get;
		}

	/// <summary>
	/// Gets a value indicating whether the effect is currently enabled.
	/// </summary>
	public bool? IsEnabled
		{
		get;
		}

	/// <summary>
	/// Gets the effect brightness when the device reports one.
	/// </summary>
	public int? Brightness
		{
		get;
		}

	/// <summary>
	/// Gets the available effect definitions reported by the device.
	/// </summary>
	public IReadOnlyList<LightEffectDefinition> AvailableEffects
		{
		get;
		}

	/// <summary>
	/// Gets the raw JSON payload returned by the device for the effect state.
	/// </summary>
	public string RawJson
		{
		get;
		}
	}

/// <summary>
/// Represents a device-reported light effect definition.
/// </summary>
public sealed class LightEffectDefinition
	{
	internal LightEffectDefinition (string identifier, string? name)
		{
		Identifier = identifier;
		Name = name;
		}

	/// <summary>
	/// Gets the device-specific effect identifier.
	/// </summary>
	public string Identifier
		{
		get;
		}

	/// <summary>
	/// Gets the user-visible effect name, when available.
	/// </summary>
	public string? Name
		{
		get;
		}
	}

/// <summary>
/// Represents a device-reported light preset definition.
/// </summary>
public sealed class LightPresetDefinition
	{
	internal LightPresetDefinition (string name, int? brightness, int? colorTemperature, int? hue, int? saturation, string rawJson)
		{
		Name = name;
		Brightness = brightness;
		ColorTemperature = colorTemperature;
		Hue = hue;
		Saturation = saturation;
		RawJson = rawJson;
		}

	/// <summary>
	/// Gets the user-visible preset name.
	/// </summary>
	public string Name
		{
		get;
		}

	/// <summary>
	/// Gets the preset brightness percentage.
	/// </summary>
	public int? Brightness
		{
		get;
		}

	/// <summary>
	/// Gets the preset color temperature in kelvin.
	/// </summary>
	public int? ColorTemperature
		{
		get;
		}

	/// <summary>
	/// Gets the preset hue component.
	/// </summary>
	public int? Hue
		{
		get;
		}

	/// <summary>
	/// Gets the preset saturation component.
	/// </summary>
	public int? Saturation
		{
		get;
		}

	/// <summary>
	/// Gets the raw preset payload returned by the device.
	/// </summary>
	public string RawJson
		{
		get;
		}
	}

/// <summary>
/// Represents normalized light preset module state.
/// </summary>
public sealed class LightPresetState
	{
	internal LightPresetState (IReadOnlyList<LightPresetDefinition> presets, string? activePreset)
		{
		Presets = presets;
		ActivePreset = activePreset;
		}

	/// <summary>
	/// Gets the presets reported by the device.
	/// </summary>
	public IReadOnlyList<LightPresetDefinition> Presets
		{
		get;
		}

	/// <summary>
	/// Gets the active preset name when the current state matches one.
	/// </summary>
	public string? ActivePreset
		{
		get;
		}
	}

/// <summary>
/// Represents normalized light transition module state.
/// </summary>
public sealed class LightTransitionState
	{
	internal LightTransitionState (int? transitionOnSeconds, int? transitionOffSeconds, string rawJson)
		{
		TransitionOnSeconds = transitionOnSeconds;
		TransitionOffSeconds = transitionOffSeconds;
		RawJson = rawJson;
		}

	/// <summary>
	/// Gets the transition duration used when turning the light on, in seconds.
	/// </summary>
	public int? TransitionOnSeconds
		{
		get;
		}

	/// <summary>
	/// Gets the transition duration used when turning the light off, in seconds.
	/// </summary>
	public int? TransitionOffSeconds
		{
		get;
		}

	/// <summary>
	/// Gets the raw JSON payload returned by the device.
	/// </summary>
	public string RawJson
		{
		get;
		}
	}

/// <summary>
/// Represents normalized light strip effect module state.
/// </summary>
public sealed class LightStripEffectState
	{
	internal LightStripEffectState (LightEffectState? effect, IReadOnlyList<LightEffectDefinition> availableEffects)
		{
		Effect = effect;
		AvailableEffects = availableEffects;
		}

	/// <summary>
	/// Gets the active effect state.
	/// </summary>
	public LightEffectState? Effect
		{
		get;
		}

	/// <summary>
	/// Gets the effects reported by the strip.
	/// </summary>
	public IReadOnlyList<LightEffectDefinition> AvailableEffects
		{
		get;
		}
	}

/// <summary>
/// Represents normalized system information read from a device.
/// </summary>
public sealed class DeviceSystemInfo
	{
	internal DeviceSystemInfo (
		string alias,
		string? model,
		string? deviceId,
		string? macAddress,
		string? hardwareVersion,
		string? softwareVersion,
		int? signalLevel,
		int? rssi,
		string? ssid,
		DeviceType deviceType,
		bool? isOn,
		TimeSpan? onTime,
		IReadOnlyList<ChildDeviceInfo> children,
		string rawJson)
		{
		Alias = alias;
		Model = model;
		DeviceId = deviceId;
		MacAddress = macAddress;
		HardwareVersion = hardwareVersion;
		SoftwareVersion = softwareVersion;
		SignalLevel = signalLevel;
		Rssi = rssi;
		Ssid = ssid;
		DeviceType = deviceType;
		IsOn = isOn;
		OnTime = onTime;
		Children = children;
		RawJson = rawJson;
		}

	/// <summary>
	/// Gets the user-visible alias.
	/// </summary>
	public string Alias
		{
		get;
		}

	/// <summary>
	/// Gets the model identifier, when available.
	/// </summary>
	public string? Model
		{
		get;
		}

	/// <summary>
	/// Gets the device identifier, when available.
	/// </summary>
	public string? DeviceId
		{
		get;
		}

	/// <summary>
	/// Gets the MAC address, when available.
	/// </summary>
	public string? MacAddress
		{
		get;
		}

	/// <summary>
	/// Gets the hardware version, when available.
	/// </summary>
	public string? HardwareVersion
		{
		get;
		}

	/// <summary>
	/// Gets the software version, when available.
	/// </summary>
	public string? SoftwareVersion
		{
		get;
		}

	/// <summary>
	/// Gets the reported Wi-Fi signal level, when available.
	/// </summary>
	public int? SignalLevel
		{
		get;
		}

	/// <summary>
	/// Gets the reported RSSI in dBm, when available.
	/// </summary>
	public int? Rssi
		{
		get;
		}

	/// <summary>
	/// Gets the reported SSID, when available.
	/// </summary>
	public string? Ssid
		{
		get;
		}

	/// <summary>
	/// Gets the inferred device family.
	/// </summary>
	public DeviceType DeviceType
		{
		get;
		}

	/// <summary>
	/// Gets a value indicating whether the device appears to be on.
	/// </summary>
	public bool? IsOn
		{
		get;
		}

	/// <summary>
	/// Gets the reported on-time duration when the device exposes it.
	/// </summary>
	public TimeSpan? OnTime
		{
		get;
		}

	/// <summary>
	/// Gets the child devices reported by the device.
	/// </summary>
	public IReadOnlyList<ChildDeviceInfo> Children
		{
		get;
		}

	/// <summary>
	/// Gets the raw JSON payload returned by the device.
	/// </summary>
	public string RawJson
		{
		get;
		}
	}
