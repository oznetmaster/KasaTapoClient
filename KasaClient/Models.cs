// Copyright © 2026 Neil Colvin.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// Behavior modeled after the independent python-kasa project (https://github.com/python-kasa/python-kasa)
// for protocol/compatibility reference only; no python-kasa source was copied. See ATTRIBUTIONS.md.

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
	/// <value>The user name or email address.</value>
	public string? UserName
		{
		get;
		}

	/// <summary>
	/// Gets the password.
	/// </summary>
	/// <value>The password.</value>
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
	/// <param name="deviceFamily">The device protocol family.</param>
	/// <param name="encryptionKind">The encryption protocol used by the device.</param>
	/// <param name="loginVersion">The optional login protocol version.</param>
	/// <param name="useHttps">Whether to use HTTPS for the device connection.</param>
	/// <param name="httpPort">The optional port for the HTTP connection.</param>
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
	/// <value>The device family identifier.</value>
	public DeviceFamilyKind DeviceFamily
		{
		get;
		}

	/// <summary>
	/// Gets the transport encryption identifier.
	/// </summary>
	/// <value>The transport encryption identifier.</value>
	public DeviceEncryptionKind EncryptionKind
		{
		get;
		}

	/// <summary>
	/// Gets the optional login version.
	/// </summary>
	/// <value>The optional login version.</value>
	public int? LoginVersion
		{
		get;
		}

	/// <summary>
	/// Gets a value indicating whether HTTPS is supported or required.
	/// </summary>
	/// <value>A value indicating whether HTTPS is supported or required.</value>
	public bool UseHttps
		{
		get;
		}

	/// <summary>
	/// Gets the HTTP port reported by discovery when available.
	/// </summary>
	/// <value>The HTTP port reported by discovery when available.</value>
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
	/// <value>The transport kind.</value>
	public DeviceTransportKind TransportKind
		{
		get;
		}

	/// <summary>
	/// Gets the parsed connection parameters, when known.
	/// </summary>
	/// <value>The parsed connection parameters, when known.</value>
	public DeviceConnectionParameters? ConnectionParameters
		{
		get;
		}

	/// <summary>
	/// Gets a value indicating whether HTTPS should be used for HTTP token transport.
	/// </summary>
	/// <value>A value indicating whether HTTPS should be used for HTTP token transport.</value>
	public bool UseSsl
		{
		get;
		}

	/// <summary>
	/// Gets a value indicating whether a known default credential profile should be used.
	/// </summary>
	/// <value>A value indicating whether a known default credential profile should be used.</value>
	public bool UseDefaultCredentials
		{
		get;
		}

	/// <summary>
	/// Gets the default credential profile.
	/// </summary>
	/// <value>The default credential profile.</value>
	public DefaultCredentialProfile DefaultCredentialProfile
		{
		get;
		}

	/// <summary>
	/// Gets the application API path.
	/// </summary>
	/// <value>The application API path.</value>
	public string ApplicationPath
		{
		get;
		}

	/// <summary>
	/// Gets a value indicating whether the HTTP transport should use secure passthrough wrapping when available.
	/// </summary>
	/// <value>A value indicating whether the HTTP transport should use secure passthrough wrapping when available.</value>
	public bool UseSecurePassthrough
		{
		get;
		}

	/// <summary>
	/// Gets the optional TPAP keepalive interval.
	/// </summary>
	/// <value>The optional TPAP keepalive interval.</value>
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
	/// <value>The device host name or IP address.</value>
	public string Host
		{
		get;
		}

	/// <summary>
	/// Gets the device control port.
	/// </summary>
	/// <value>The device control port.</value>
	public int Port
		{
		get;
		}

	/// <summary>
	/// Gets the optional device credentials.
	/// </summary>
	/// <value>The optional device credentials.</value>
	public DeviceCredentials? Credentials
		{
		get;
		}

	/// <summary>
	/// Gets the transport-specific connection options.
	/// </summary>
	/// <value>The transport-specific connection options.</value>
	public DeviceConnectionOptions ConnectionOptions
		{
		get;
		}

	/// <summary>
	/// Gets the per-operation timeout.
	/// </summary>
	/// <value>The per-operation timeout.</value>
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
	/// <value>The device host that produced the discovery response.</value>
	public string Host
		{
		get;
		}

	/// <summary>
	/// Gets the inferred device family.
	/// </summary>
	/// <value>The inferred device family.</value>
	public DeviceType DeviceType
		{
		get;
		}

	/// <summary>
	/// Gets the user-visible device alias, when available.
	/// </summary>
	/// <value>The user-visible device alias, when available.</value>
	public string? Alias
		{
		get;
		}

	/// <summary>
	/// Gets the model identifier, when available.
	/// </summary>
	/// <value>The model identifier, when available.</value>
	public string? Model
		{
		get;
		}

	/// <summary>
	/// Gets the device identifier, when available.
	/// </summary>
	/// <value>The device identifier, when available.</value>
	public string? DeviceId
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
	/// <value>The advertised TPAP metadata, when present.</value>
	public TpapDiscoveryMetadata? TpapMetadata
		{
		get;
		}

	/// <summary>
	/// Gets the advertised protocol version, when present.
	/// </summary>
	/// <value>The advertised protocol version, when present.</value>
	public int? ProtocolVersion
		{
		get;
		}

	/// <summary>
	/// Gets a value indicating whether discovery marked TPAP as preferred.
	/// </summary>
	/// <value>A value indicating whether discovery marked TPAP as preferred.</value>
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
	/// <value>The TPAP-advertised port.</value>
	public int? Port
		{
		get;
		}

	/// <summary>
	/// Gets the advertised PAKE modes.
	/// </summary>
	/// <value>The advertised PAKE modes.</value>
	public IReadOnlyList<int>? PakeModes
		{
		get;
		}

	/// <summary>
	/// Gets the advertised TLS mode.
	/// </summary>
	/// <value>The advertised TLS mode.</value>
	public int? Tls
		{
		get;
		}

	/// <summary>
	/// Gets the advertised DAC mode.
	/// </summary>
	/// <value>The advertised DAC mode.</value>
	public int? Dac
		{
		get;
		}

	/// <summary>
	/// Gets the advertised NOC mode.
	/// </summary>
	/// <value>The advertised NOC mode.</value>
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
	/// <value>The stable feature identifier.</value>
	public string Id
		{
		get;
		}

	/// <summary>
	/// Gets the display name of the feature.
	/// </summary>
	/// <value>The display name of the feature.</value>
	public string Name
		{
		get;
		}

	/// <summary>
	/// Gets the kind of feature.
	/// </summary>
	/// <value>The kind of feature.</value>
	public FeatureKind Kind
		{
		get;
		}

	/// <summary>
	/// Gets the current feature value.
	/// </summary>
	/// <value>The current feature value.</value>
	public object? Value
		{
		get;
		}

	/// <summary>
	/// Gets the optional unit associated with <see cref="Value" />.
	/// </summary>
	/// <value>The optional unit associated with <see cref="Value" />.</value>
	public string? Unit
		{
		get;
		}

	/// <summary>
	/// Gets a value indicating whether the feature is read-only.
	/// </summary>
	/// <value>A value indicating whether the feature is read-only.</value>
	public bool IsReadOnly
		{
		get;
		}

	/// <summary>
	/// Gets the optional minimum numeric value.
	/// </summary>
	/// <value>The optional minimum numeric value.</value>
	public double? MinimumValue
		{
		get;
		}

	/// <summary>
	/// Gets the optional maximum numeric value.
	/// </summary>
	/// <value>The optional maximum numeric value.</value>
	public double? MaximumValue
		{
		get;
		}

	/// <summary>
	/// Gets the optional allowed choice values.
	/// </summary>
	/// <value>The optional allowed choice values.</value>
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
		double? monthKilowattHours)
		{
		CurrentPowerWatts = currentPowerWatts;
		VoltageVolts = voltageVolts;
		CurrentAmps = currentAmps;
		TotalKilowattHours = totalKilowattHours;
		TodayKilowattHours = todayKilowattHours;
		MonthKilowattHours = monthKilowattHours;
		}

	/// <summary>
	/// Gets the current power draw in watts.
	/// </summary>
	/// <value>The current power draw in watts.</value>
	public double? CurrentPowerWatts
		{
		get;
		}

	/// <summary>
	/// Gets the line voltage in volts.
	/// </summary>
	/// <value>The line voltage in volts.</value>
	public double? VoltageVolts
		{
		get;
		}

	/// <summary>
	/// Gets the current draw in amps.
	/// </summary>
	/// <value>The current draw in amps.</value>
	public double? CurrentAmps
		{
		get;
		}

	/// <summary>
	/// Gets the total measured energy in kilowatt-hours.
	/// </summary>
	/// <value>The total measured energy in kilowatt-hours.</value>
	public double? TotalKilowattHours
		{
		get;
		}

	/// <summary>
	/// Gets today's measured energy in kilowatt-hours when periodic statistics are available.
	/// </summary>
	/// <value>Today's measured energy in kilowatt-hours when periodic statistics are available.</value>
	public double? TodayKilowattHours
		{
		get;
		}

	/// <summary>
	/// Gets this month's measured energy in kilowatt-hours when periodic statistics are available.
	/// </summary>
	/// <value>This month's measured energy in kilowatt-hours when periodic statistics are available.</value>
	public double? MonthKilowattHours
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
		string? category = null,
		IReadOnlyList<string>? componentIds = null,
		IReadOnlyList<DeviceFeature>? features = null)
		{
		Id = id;
		Alias = alias;
		Model = model;
		DeviceType = deviceType;
		IsOn = isOn;
		Category = category;
		ComponentIds = componentIds ?? Array.Empty<string> ();
		Features = features ?? Array.Empty<DeviceFeature> ();
		}

	/// <summary>
	/// Gets the child device identifier.
	/// </summary>
	/// <value>The child device identifier.</value>
	public string Id
		{
		get;
		}

	/// <summary>
	/// Gets the child device alias.
	/// </summary>
	/// <value>The child device alias.</value>
	public string? Alias
		{
		get;
		}

	/// <summary>
	/// Gets the child device model.
	/// </summary>
	/// <value>The child device model.</value>
	public string? Model
		{
		get;
		}

	/// <summary>
	/// Gets the inferred child device family.
	/// </summary>
	/// <value>The inferred child device family.</value>
	public DeviceType DeviceType
		{
		get;
		}

	/// <summary>
	/// Gets the python-kasa style child category when reported.
	/// </summary>
	/// <value>The python-kasa style child category when reported.</value>
	public string? Category
		{
		get;
		}

	/// <summary>
	/// Gets a value indicating whether the child device appears to be on.
	/// </summary>
	/// <value>A value indicating whether the child device appears to be on.</value>
	public bool? IsOn
		{
		get;
		}

	/// <summary>
	/// Gets the reported child component identifiers.
	/// </summary>
	/// <value>The reported child component identifiers.</value>
	public IReadOnlyList<string> ComponentIds
		{
		get;
		}

	/// <summary>
	/// Gets the normalized child features derived from the latest payload.
	/// </summary>
	/// <value>The normalized child features derived from the latest payload.</value>
	public IReadOnlyList<DeviceFeature> Features
		{
		get;
		}
	}

/// <summary>
/// Represents trigger log state reported by an event-driven child device.
/// </summary>
public sealed record ChildTriggerLogState
	{
	internal ChildTriggerLogState (IReadOnlyList<ChildTriggerLogEntry> logs) => Logs = logs;

	/// <summary>
	/// Gets the trigger log entries reported for the child device.
	/// </summary>
	/// <value>The trigger log entries reported for the child device.</value>
	public IReadOnlyList<ChildTriggerLogEntry> Logs
		{
		get;
		}
	}

/// <summary>
/// Represents a single trigger log entry reported by a child device.
/// </summary>
public sealed record ChildTriggerLogEntry
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
	/// <value>The numeric log identifier, when reported.</value>
	public int? Id
		{
		get;
		}

	/// <summary>
	/// Gets the device-reported event identifier.
	/// </summary>
	/// <value>The device-reported event identifier.</value>
	public string? EventId
		{
		get;
		}

	/// <summary>
	/// Gets the raw event timestamp value, when reported.
	/// </summary>
	/// <value>The raw event timestamp value, when reported.</value>
	public long? Timestamp
		{
		get;
		}

	/// <summary>
	/// Gets the event name such as a button click type.
	/// </summary>
	/// <value>The event name such as a button click type.</value>
	public string? EventName
		{
		get;
		}
	}

/// <summary>
/// Represents battery-related child sensor state.
/// </summary>
public sealed record ChildBatterySensorState
	{
	internal ChildBatterySensorState (int? batteryLevel, bool? batteryLow)
		{
		BatteryLevel = batteryLevel;
		BatteryLow = batteryLow;
		}

	/// <summary>
	/// Gets the latest battery level percentage, when reported.
	/// </summary>
	/// <value>The latest battery level percentage, when reported.</value>
	public int? BatteryLevel
		{
		get;
		}

	/// <summary>
	/// Gets whether the child sensor reports a low battery condition.
	/// </summary>
	/// <value>Whether the child sensor reports a low battery condition.</value>
	public bool? BatteryLow
		{
		get;
		}
	}

/// <summary>
/// Represents contact sensor state for a child device.
/// </summary>
public sealed record ChildContactSensorState
	{
	internal ChildContactSensorState (bool? isOpen) => IsOpen = isOpen;

	/// <summary>
	/// Gets whether the contact sensor is currently open.
	/// </summary>
	/// <value>Whether the contact sensor is currently open.</value>
	public bool? IsOpen
		{
		get;
		}
	}

/// <summary>
/// Represents motion sensor state for a child device.
/// </summary>
public sealed record ChildMotionSensorState
	{
	internal ChildMotionSensorState (bool? motionDetected) => MotionDetected = motionDetected;

	/// <summary>
	/// Gets whether motion is currently detected.
	/// </summary>
	/// <value>Whether motion is currently detected.</value>
	public bool? MotionDetected
		{
		get;
		}
	}

/// <summary>
/// Represents water leak sensor state for a child device.
/// </summary>
public sealed record ChildWaterLeakSensorState
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
	/// <value>The reported water leak status string.</value>
	public string? Status
		{
		get;
		}

	/// <summary>
	/// Gets whether an alert is currently active.
	/// </summary>
	/// <value>Whether an alert is currently active.</value>
	public bool? Alert
		{
		get;
		}

	/// <summary>
	/// Gets the raw timestamp of the latest alert, when reported.
	/// </summary>
	/// <value>The raw timestamp of the latest alert, when reported.</value>
	public long? AlertTimestamp
		{
		get;
		}
	}

/// <summary>
/// Represents temperature sensor state for a child device.
/// </summary>
public sealed record ChildTemperatureSensorState
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
	/// <value>The latest reported temperature value.</value>
	public double? Temperature
		{
		get;
		}

	/// <summary>
	/// Gets whether the child device reports a temperature warning.
	/// </summary>
	/// <value>Whether the child device reports a temperature warning.</value>
	public bool? Warning
		{
		get;
		}

	/// <summary>
	/// Gets the temperature unit such as celsius or fahrenheit.
	/// </summary>
	/// <value>The temperature unit such as celsius or fahrenheit.</value>
	public string? Unit
		{
		get;
		}

	/// <summary>
	/// Gets the minimum comfort temperature, when reported.
	/// </summary>
	/// <value>The minimum comfort temperature, when reported.</value>
	public double? MinimumComfortTemperature
		{
		get;
		}

	/// <summary>
	/// Gets the maximum comfort temperature, when reported.
	/// </summary>
	/// <value>The maximum comfort temperature, when reported.</value>
	public double? MaximumComfortTemperature
		{
		get;
		}
	}

/// <summary>
/// Represents humidity sensor state for a child device.
/// </summary>
public sealed record ChildHumiditySensorState
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
	/// <value>The latest reported humidity percentage.</value>
	public int? Humidity
		{
		get;
		}

	/// <summary>
	/// Gets whether the child device reports a humidity warning.
	/// </summary>
	/// <value>Whether the child device reports a humidity warning.</value>
	public bool? Warning
		{
		get;
		}

	/// <summary>
	/// Gets the minimum comfort humidity, when reported.
	/// </summary>
	/// <value>The minimum comfort humidity, when reported.</value>
	public double? MinimumComfortHumidity
		{
		get;
		}

	/// <summary>
	/// Gets the maximum comfort humidity, when reported.
	/// </summary>
	/// <value>The maximum comfort humidity, when reported.</value>
	public double? MaximumComfortHumidity
		{
		get;
		}
	}

/// <summary>
/// Represents report-mode state for a child sensor device.
/// </summary>
public sealed record ChildReportModeState
	{
	internal ChildReportModeState (int? reportInterval) => ReportInterval = reportInterval;

	/// <summary>
	/// Gets the sensor report interval in seconds, when reported.
	/// </summary>
	/// <value>The sensor report interval in seconds, when reported.</value>
	public int? ReportInterval
		{
		get;
		}
	}

/// <summary>
/// Represents double-click state for a child button device.
/// </summary>
public sealed record ChildDoubleClickState
	{
	internal ChildDoubleClickState (bool? enabled) => Enabled = enabled;

	/// <summary>
	/// Gets whether double-click is enabled.
	/// </summary>
	/// <value>Whether double-click is enabled.</value>
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
	internal DetectedChildDevice (string deviceId, string? model, string? category)
		{
		DeviceId = deviceId;
		Model = model;
		Category = category;
		}

	/// <summary>
	/// Gets the detected device identifier.
	/// </summary>
	/// <value>The detected device identifier.</value>
	public string DeviceId
		{
		get;
		}

	/// <summary>
	/// Gets the detected device model.
	/// </summary>
	/// <value>The detected device model.</value>
	public string? Model
		{
		get;
		}

	/// <summary>
	/// Gets the detected device category.
	/// </summary>
	/// <value>The detected device category.</value>
	public string? Category
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
	/// <value>The supported child categories reported by the hub.</value>
	public IReadOnlyList<string> SupportedCategories
		{
		get;
		}

	/// <summary>
	/// Gets the detected child devices from the latest scan.
	/// </summary>
	/// <value>The detected child devices from the latest scan.</value>
	public IReadOnlyList<DetectedChildDevice> DetectedDevices
		{
		get;
		}
	}

/// <summary>
/// Represents frost-protection state for a child thermostat device.
/// </summary>
public sealed record ChildFrostProtectionState
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
	/// <value>Whether frost protection is currently enabled.</value>
	public bool? Enabled
		{
		get;
		}

	/// <summary>
	/// Gets the minimum frost-protection temperature, when reported.
	/// </summary>
	/// <value>The minimum frost-protection temperature, when reported.</value>
	public int? MinimumTemperature
		{
		get;
		}

	/// <summary>
	/// Gets the reported temperature unit.
	/// </summary>
	/// <value>The reported temperature unit.</value>
	public string? Unit
		{
		get;
		}
	}

/// <summary>
/// Represents child-protection state for a child thermostat device.
/// </summary>
public sealed record ChildProtectionState
	{
	internal ChildProtectionState (bool? enabled) => Enabled = enabled;

	/// <summary>
	/// Gets whether child protection is enabled.
	/// </summary>
	/// <value>Whether child protection is enabled.</value>
	public bool? Enabled
		{
		get;
		}
	}

/// <summary>
/// Represents temperature-control state for a child thermostat device.
/// </summary>
public sealed record ChildTemperatureControlState
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
	/// <value>Whether temperature control is enabled.</value>
	public bool? Enabled
		{
		get;
		}

	/// <summary>
	/// Gets the target temperature, when reported.
	/// </summary>
	/// <value>The target temperature, when reported.</value>
	public double? TargetTemperature
		{
		get;
		}

	/// <summary>
	/// Gets the minimum supported target temperature, when reported.
	/// </summary>
	/// <value>The minimum supported target temperature, when reported.</value>
	public int? MinimumTargetTemperature
		{
		get;
		}

	/// <summary>
	/// Gets the maximum supported target temperature, when reported.
	/// </summary>
	/// <value>The maximum supported target temperature, when reported.</value>
	public int? MaximumTargetTemperature
		{
		get;
		}

	/// <summary>
	/// Gets the temperature offset, when reported.
	/// </summary>
	/// <value>The temperature offset, when reported.</value>
	public int? TemperatureOffset
		{
		get;
		}

	/// <summary>
	/// Gets the raw TRV state flags reported by the device.
	/// </summary>
	/// <value>The raw TRV state flags reported by the device.</value>
	public IReadOnlyList<string> States
		{
		get;
		}
	}

/// <summary>
/// Represents aggregated thermostat state for a child thermostat device.
/// </summary>
public sealed record ChildThermostatState
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
	/// <value>Whether the thermostat is enabled.</value>
	public bool? Enabled
		{
		get;
		}

	/// <summary>
	/// Gets the target temperature, when reported.
	/// </summary>
	/// <value>The target temperature, when reported.</value>
	public double? TargetTemperature
		{
		get;
		}

	/// <summary>
	/// Gets the current measured temperature, when reported.
	/// </summary>
	/// <value>The current measured temperature, when reported.</value>
	public double? CurrentTemperature
		{
		get;
		}

	/// <summary>
	/// Gets the reported temperature unit.
	/// </summary>
	/// <value>The reported temperature unit.</value>
	public string? Unit
		{
		get;
		}

	/// <summary>
	/// Gets the raw TRV state flags reported by the device.
	/// </summary>
	/// <value>The raw TRV state flags reported by the device.</value>
	public IReadOnlyList<string> States
		{
		get;
		}
	}

/// <summary>
/// Represents normalized rule-related state for Kasa local devices.
/// </summary>
public sealed record RuleModuleState
	{
	internal RuleModuleState (
		CountdownRuleState? countdown,
		IReadOnlyList<ScheduledRule> schedules,
		IReadOnlyList<ScheduledRule> antitheftRules)
		{
		Countdown = countdown;
		Schedules = schedules;
		AntitheftRules = antitheftRules;
		}

	/// <summary>
	/// Gets the current countdown timer state, when reported.
	/// </summary>
	/// <value>The current countdown timer state, when reported.</value>
	public CountdownRuleState? Countdown
		{
		get;
		}

	/// <summary>
	/// Gets the configured schedule rules.
	/// </summary>
	/// <value>The configured schedule rules.</value>
	public IReadOnlyList<ScheduledRule> Schedules
		{
		get;
		}

	/// <summary>
	/// Gets the configured antitheft rules.
	/// </summary>
	/// <value>The configured antitheft rules.</value>
	public IReadOnlyList<ScheduledRule> AntitheftRules
		{
		get;
		}
	}

/// <summary>
/// Represents a normalized countdown timer state.
/// </summary>
public sealed record CountdownRuleState
	{
	internal CountdownRuleState (bool? isEnabled, bool? isActive, int? delaySeconds, bool? actionTurnsOn)
		{
		IsEnabled = isEnabled;
		IsActive = isActive;
		DelaySeconds = delaySeconds;
		ActionTurnsOn = actionTurnsOn;
		}

	/// <summary>
	/// Gets a value indicating whether the countdown feature is enabled.
	/// </summary>
	/// <value>A value indicating whether the countdown feature is enabled.</value>
	public bool? IsEnabled
		{
		get;
		}

	/// <summary>
	/// Gets a value indicating whether a countdown is currently active.
	/// </summary>
	/// <value>A value indicating whether a countdown is currently active.</value>
	public bool? IsActive
		{
		get;
		}

	/// <summary>
	/// Gets the configured countdown delay in seconds.
	/// </summary>
	/// <value>The configured countdown delay in seconds.</value>
	public int? DelaySeconds
		{
		get;
		}

	/// <summary>
	/// Gets a value indicating whether the countdown action turns the device on.
	/// </summary>
	/// <value>A value indicating whether the countdown action turns the device on.</value>
	public bool? ActionTurnsOn
		{
		get;
		}
	}

/// <summary>
/// Represents a normalized schedule or antitheft rule.
/// </summary>
public sealed class ScheduledRule
	{
	internal ScheduledRule (string id, string? name, bool? isEnabled, bool? actionTurnsOn, int? startMinute, int? endMinute)
		{
		Id = id;
		Name = name;
		IsEnabled = isEnabled;
		ActionTurnsOn = actionTurnsOn;
		StartMinute = startMinute;
		EndMinute = endMinute;
		}

	/// <summary>
	/// Gets the stable rule identifier.
	/// </summary>
	/// <value>The stable rule identifier.</value>
	public string Id
		{
		get;
		}

	/// <summary>
	/// Gets the optional rule name.
	/// </summary>
	/// <value>The optional rule name.</value>
	public string? Name
		{
		get;
		}

	/// <summary>
	/// Gets a value indicating whether the rule is enabled.
	/// </summary>
	/// <value>A value indicating whether the rule is enabled.</value>
	public bool? IsEnabled
		{
		get;
		}

	/// <summary>
	/// Gets a value indicating whether the rule action turns the device on.
	/// </summary>
	/// <value>A value indicating whether the rule action turns the device on.</value>
	public bool? ActionTurnsOn
		{
		get;
		}

	/// <summary>
	/// Gets the start minute-of-day value when reported.
	/// </summary>
	/// <value>The start minute-of-day value when reported.</value>
	public int? StartMinute
		{
		get;
		}

	/// <summary>
	/// Gets the end minute-of-day value when reported.
	/// </summary>
	/// <value>The end minute-of-day value when reported.</value>
	public int? EndMinute
		{
		get;
		}
	}

/// <summary>
/// Represents normalized firmware-related device state.
/// </summary>
public sealed record FirmwareState
	{
	internal FirmwareState (
		string? currentFirmwareVersion,
		string? currentHardwareVersion,
		bool? autoUpdateEnabled,
		string? availableFirmwareVersion,
		bool? updateAvailable)
		{
		CurrentFirmwareVersion = currentFirmwareVersion;
		CurrentHardwareVersion = currentHardwareVersion;
		AutoUpdateEnabled = autoUpdateEnabled;
		AvailableFirmwareVersion = availableFirmwareVersion;
		UpdateAvailable = updateAvailable;
		}

	/// <summary>
	/// Gets the current firmware version reported by the device.
	/// </summary>
	/// <value>The current firmware version reported by the device.</value>
	public string? CurrentFirmwareVersion
		{
		get;
		}

	/// <summary>
	/// Gets the current hardware version reported by the device.
	/// </summary>
	/// <value>The current hardware version reported by the device.</value>
	public string? CurrentHardwareVersion
		{
		get;
		}

	/// <summary>
	/// Gets whether automatic firmware updates are enabled, when reported.
	/// </summary>
	/// <value>Whether automatic firmware updates are enabled, when reported.</value>
	public bool? AutoUpdateEnabled
		{
		get;
		}

	/// <summary>
	/// Gets the latest available firmware version, when requested and reported.
	/// </summary>
	/// <value>The latest available firmware version, when requested and reported.</value>
	public string? AvailableFirmwareVersion
		{
		get;
		}

	/// <summary>
	/// Gets whether a newer firmware version is available, when known.
	/// </summary>
	/// <value>Whether a newer firmware version is available, when known.</value>
	public bool? UpdateAvailable
		{
		get;
		}
	}

/// <summary>
/// Represents normalized cloud-connectivity information.
/// </summary>
public sealed record CloudConnectionState
	{
	internal CloudConnectionState (bool? isConnected, bool? isProvisioned, string? server, string? userName)
		{
		IsConnected = isConnected;
		IsProvisioned = isProvisioned;
		Server = server;
		UserName = userName;
		}

	/// <summary>
	/// Gets whether the device reports an active cloud connection.
	/// </summary>
	/// <value>Whether the device reports an active cloud connection.</value>
	public bool? IsConnected
		{
		get;
		}

	/// <summary>
	/// Gets whether the device reports cloud provisioning, when available.
	/// </summary>
	/// <value>Whether the device reports cloud provisioning, when available.</value>
	public bool? IsProvisioned
		{
		get;
		}

	/// <summary>
	/// Gets the configured cloud server, when reported.
	/// </summary>
	/// <value>The configured cloud server, when reported.</value>
	public string? Server
		{
		get;
		}

	/// <summary>
	/// Gets the cloud account user name, when reported.
	/// </summary>
	/// <value>The cloud account user name, when reported.</value>
	public string? UserName
		{
		get;
		}
	}

/// <summary>
/// Represents normalized device-local time information.
/// </summary>
public sealed record DeviceTimeState
	{
	internal DeviceTimeState (DateTime? localTime, string? region, int? timeDifferenceMinutes)
		{
		LocalTime = localTime;
		Region = region;
		TimeDifferenceMinutes = timeDifferenceMinutes;
		}

	/// <summary>
	/// Gets the local device time, when reported.
	/// </summary>
	/// <value>The local device time, when reported.</value>
	public DateTime? LocalTime
		{
		get;
		}

	/// <summary>
	/// Gets the reported timezone region, when available.
	/// </summary>
	/// <value>The reported timezone region, when available.</value>
	public string? Region
		{
		get;
		}

	/// <summary>
	/// Gets the reported UTC offset in minutes, when available.
	/// </summary>
	/// <value>The reported UTC offset in minutes, when available.</value>
	public int? TimeDifferenceMinutes
		{
		get;
		}
	}

/// <summary>
/// Represents Matter setup information exposed by a device.
/// </summary>
public sealed class MatterSetupInfo
	{
	internal MatterSetupInfo (string? setupCode, string? setupPayload)
		{
		SetupCode = setupCode;
		SetupPayload = setupPayload;
		}

	/// <summary>
	/// Gets the Matter setup code, when reported.
	/// </summary>
	/// <value>The Matter setup code, when reported.</value>
	public string? SetupCode
		{
		get;
		}

	/// <summary>
	/// Gets the Matter setup payload, when reported.
	/// </summary>
	/// <value>The Matter setup payload, when reported.</value>
	public string? SetupPayload
		{
		get;
		}
	}

/// <summary>
/// Represents HomeKit setup information exposed by a device.
/// </summary>
public sealed class HomeKitSetupInfo
	{
	internal HomeKitSetupInfo (string? setupCode, string? setupPayload)
		{
		SetupCode = setupCode;
		SetupPayload = setupPayload;
		}

	/// <summary>
	/// Gets the HomeKit setup code, when reported.
	/// </summary>
	/// <value>The HomeKit setup code, when reported.</value>
	public string? SetupCode
		{
		get;
		}

	/// <summary>
	/// Gets the HomeKit setup payload, when reported.
	/// </summary>
	/// <value>The HomeKit setup payload, when reported.</value>
	public string? SetupPayload
		{
		get;
		}
	}

/// <summary>
/// Represents normalized auto-off configuration and timer state.
/// </summary>
public sealed record AutoOffState
	{
	internal AutoOffState (bool? enabled, int? delayMinutes, bool? timerActive, DateTime? autoOffAt)
		{
		Enabled = enabled;
		DelayMinutes = delayMinutes;
		TimerActive = timerActive;
		AutoOffAt = autoOffAt;
		}

	/// <summary>
	/// Gets whether auto-off is enabled.
	/// </summary>
	/// <value>Whether auto-off is enabled.</value>
	public bool? Enabled
		{
		get;
		}

	/// <summary>
	/// Gets the configured auto-off delay in minutes, when reported.
	/// </summary>
	/// <value>The configured auto-off delay in minutes, when reported.</value>
	public int? DelayMinutes
		{
		get;
		}

	/// <summary>
	/// Gets whether an auto-off timer is currently active, when reported.
	/// </summary>
	/// <value>Whether an auto-off timer is currently active, when reported.</value>
	public bool? TimerActive
		{
		get;
		}

	/// <summary>
	/// Gets the local time when the device is expected to turn off automatically, when known.
	/// </summary>
	/// <value>The local time when the device is expected to turn off automatically, when known.</value>
	public DateTime? AutoOffAt
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
	/// <value>The configured night-mode start minute, when reported.</value>
	public int? StartMinute
		{
		get;
		}

	/// <summary>
	/// Gets the configured night-mode end minute, when reported.
	/// </summary>
	/// <value>The configured night-mode end minute, when reported.</value>
	public int? EndMinute
		{
		get;
		}

	/// <summary>
	/// Gets the night-mode type, such as a scheduled or sunrise/sunset mode.
	/// </summary>
	/// <value>The night-mode type, such as a scheduled or sunrise/sunset mode.</value>
	public string? ModeType
		{
		get;
		}

	/// <summary>
	/// Gets the configured sunrise offset in minutes, when reported.
	/// </summary>
	/// <value>The configured sunrise offset in minutes, when reported.</value>
	public int? SunriseOffsetMinutes
		{
		get;
		}

	/// <summary>
	/// Gets the configured sunset offset in minutes, when reported.
	/// </summary>
	/// <value>The configured sunset offset in minutes, when reported.</value>
	public int? SunsetOffsetMinutes
		{
		get;
		}
	}

/// <summary>
/// Represents normalized LED status and mode information.
/// </summary>
public sealed record LedState
	{
	internal LedState (bool? enabled, string? mode, LedNightModeSettings? nightModeSettings)
		{
		Enabled = enabled;
		Mode = mode;
		NightModeSettings = nightModeSettings;
		}

	/// <summary>
	/// Gets whether the device LED is enabled.
	/// </summary>
	/// <value>Whether the device LED is enabled.</value>
	public bool? Enabled
		{
		get;
		}

	/// <summary>
	/// Gets the LED mode string, when reported.
	/// </summary>
	/// <value>The LED mode string, when reported.</value>
	public string? Mode
		{
		get;
		}

	/// <summary>
	/// Gets the LED night-mode settings, when reported.
	/// </summary>
	/// <value>The LED night-mode settings, when reported.</value>
	public LedNightModeSettings? NightModeSettings
		{
		get;
		}
	}

/// <summary>
/// Represents normalized child-lock state for a device.
/// </summary>
public sealed record ChildLockState
	{
	internal ChildLockState (bool? enabled)
		{
		Enabled = enabled;
		}

	/// <summary>
	/// Gets whether child lock is enabled.
	/// </summary>
	/// <value>Whether child lock is enabled.</value>
	public bool? Enabled
		{
		get;
		}
	}

/// <summary>
/// Represents normalized alarm state for devices such as hubs and chimes.
/// </summary>
public sealed record AlarmState
	{
	internal AlarmState (bool? isActive, string? source, string? sound, string? volume, int? volumeLevel, int? durationSeconds)
		{
		IsActive = isActive;
		Source = source;
		Sound = sound;
		Volume = volume;
		VolumeLevel = volumeLevel;
		DurationSeconds = durationSeconds;
		}

	/// <summary>
	/// Gets whether the alarm is currently active.
	/// </summary>
	/// <value>Whether the alarm is currently active.</value>
	public bool? IsActive
		{
		get;
		}

	/// <summary>
	/// Gets the alarm source, when reported.
	/// </summary>
	/// <value>The alarm source, when reported.</value>
	public string? Source
		{
		get;
		}

	/// <summary>
	/// Gets the selected alarm sound, when reported.
	/// </summary>
	/// <value>The selected alarm sound, when reported.</value>
	public string? Sound
		{
		get;
		}

	/// <summary>
	/// Gets the selected alarm volume label, when reported.
	/// </summary>
	/// <value>The selected alarm volume label, when reported.</value>
	public string? Volume
		{
		get;
		}

	/// <summary>
	/// Gets the selected alarm volume level, when reported.
	/// </summary>
	/// <value>The selected alarm volume level, when reported.</value>
	public int? VolumeLevel
		{
		get;
		}

	/// <summary>
	/// Gets the configured alarm duration in seconds, when reported.
	/// </summary>
	/// <value>The configured alarm duration in seconds, when reported.</value>
	public int? DurationSeconds
		{
		get;
		}
	}

/// <summary>
/// Represents normalized overheat protection state.
/// </summary>
public sealed record OverheatProtectionState
	{
	internal OverheatProtectionState (bool? overheated)
		{
		Overheated = overheated;
		}

	/// <summary>
	/// Gets whether the device reports an overheat condition.
	/// </summary>
	/// <value>Whether the device reports an overheat condition.</value>
	public bool? Overheated
		{
		get;
		}
	}

/// <summary>
/// Represents normalized power protection state.
/// </summary>
public sealed record PowerProtectionState
	{
	internal PowerProtectionState (bool? protectionActive)
		{
		ProtectionActive = protectionActive;
		}

	/// <summary>
	/// Gets whether power protection is active.
	/// </summary>
	/// <value>Whether power protection is active.</value>
	public bool? ProtectionActive
		{
		get;
		}
	}

/// <summary>
/// Represents normalized fan state.
/// </summary>
public sealed record FanState
	{
	internal FanState (bool? isOn)
		{
		IsOn = isOn;
		}

	/// <summary>
	/// Gets whether the fan is on.
	/// </summary>
	/// <value>Whether the fan is on.</value>
	public bool? IsOn
		{
		get;
		}
	}

/// <summary>
/// Represents normalized speaker state.
/// </summary>
public sealed record SpeakerState
	{
	internal SpeakerState (bool? isAvailable)
		{
		IsAvailable = isAvailable;
		}

	/// <summary>
	/// Gets whether speaker capability is available.
	/// </summary>
	/// <value>Whether speaker capability is available.</value>
	public bool? IsAvailable
		{
		get;
		}
	}

/// <summary>
/// Represents normalized light state for bulbs and light strips.
/// </summary>
public sealed record LightState
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
		string? activePreset)
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
		}

	/// <summary>
	/// Gets a value indicating whether the light is on.
	/// </summary>
	/// <value>A value indicating whether the light is on.</value>
	public bool? IsOn
		{
		get;
		}

	/// <summary>
	/// Gets the brightness percentage.
	/// </summary>
	/// <value>The brightness percentage.</value>
	public int? Brightness
		{
		get;
		}

	/// <summary>
	/// Gets the color temperature in kelvin.
	/// </summary>
	/// <value>The color temperature in kelvin.</value>
	public int? ColorTemperature
		{
		get;
		}

	/// <summary>
	/// Gets the hue component.
	/// </summary>
	/// <value>The hue component.</value>
	public int? Hue
		{
		get;
		}

	/// <summary>
	/// Gets the saturation component.
	/// </summary>
	/// <value>The saturation component.</value>
	public int? Saturation
		{
		get;
		}

	/// <summary>
	/// Gets a value indicating whether the device reports light-effect capability.
	/// </summary>
	/// <value>A value indicating whether the device reports light-effect capability.</value>
	public bool SupportsEffects
		{
		get;
		}

	/// <summary>
	/// Gets the normalized light effect state when the device exposes one.
	/// </summary>
	/// <value>The normalized light effect state when the device exposes one.</value>
	public LightEffectState? Effect
		{
		get;
		}

	/// <summary>
	/// Gets the normalized HSV color when color information is available.
	/// </summary>
	/// <value>The normalized HSV color when color information is available.</value>
	public HsvColor? Hsv
		{
		get;
		}

	/// <summary>
	/// Gets the named light presets reported by the device.
	/// </summary>
	/// <value>The named light presets reported by the device.</value>
	public IReadOnlyList<LightPresetDefinition> AvailablePresets
		{
		get;
		}

	/// <summary>
	/// Gets the active light preset name when the current state matches one.
	/// </summary>
	/// <value>The active light preset name when the current state matches one.</value>
	public string? ActivePreset
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
	/// <value>The hue component.</value>
	public int Hue
		{
		get;
		}

	/// <summary>
	/// Gets the saturation component.
	/// </summary>
	/// <value>The saturation component.</value>
	public int Saturation
		{
		get;
		}

	/// <summary>
	/// Gets the value component.
	/// </summary>
	/// <value>The value component.</value>
	public int Value
		{
		get;
		}
	}

/// <summary>
/// Represents a normalized lighting effect state.
/// </summary>
public sealed record LightEffectState
	{
	internal LightEffectState (string? identifier, string? name, bool? isEnabled, int? brightness, IReadOnlyList<LightEffectDefinition> availableEffects)
		{
		Identifier = identifier;
		Name = name;
		IsEnabled = isEnabled;
		Brightness = brightness;
		AvailableEffects = availableEffects;
		}

	/// <summary>
	/// Gets the device-specific effect identifier.
	/// </summary>
	/// <value>The device-specific effect identifier.</value>
	public string? Identifier
		{
		get;
		}

	/// <summary>
	/// Gets the user-visible effect name, when available.
	/// </summary>
	/// <value>The user-visible effect name, when available.</value>
	public string? Name
		{
		get;
		}

	/// <summary>
	/// Gets a value indicating whether the effect is currently enabled.
	/// </summary>
	/// <value>A value indicating whether the effect is currently enabled.</value>
	public bool? IsEnabled
		{
		get;
		}

	/// <summary>
	/// Gets the effect brightness when the device reports one.
	/// </summary>
	/// <value>The effect brightness when the device reports one.</value>
	public int? Brightness
		{
		get;
		}

	/// <summary>
	/// Gets the available effect definitions reported by the device.
	/// </summary>
	/// <value>The available effect definitions reported by the device.</value>
	public IReadOnlyList<LightEffectDefinition> AvailableEffects
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
	/// <value>The device-specific effect identifier.</value>
	public string Identifier
		{
		get;
		}

	/// <summary>
	/// Gets the user-visible effect name, when available.
	/// </summary>
	/// <value>The user-visible effect name, when available.</value>
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
	internal LightPresetDefinition (string name, int? brightness, int? colorTemperature, int? hue, int? saturation)
		{
		Name = name;
		Brightness = brightness;
		ColorTemperature = colorTemperature;
		Hue = hue;
		Saturation = saturation;
		}

	/// <summary>
	/// Gets the user-visible preset name.
	/// </summary>
	/// <value>The user-visible preset name.</value>
	public string Name
		{
		get;
		}

	/// <summary>
	/// Gets the preset brightness percentage.
	/// </summary>
	/// <value>The preset brightness percentage.</value>
	public int? Brightness
		{
		get;
		}

	/// <summary>
	/// Gets the preset color temperature in kelvin.
	/// </summary>
	/// <value>The preset color temperature in kelvin.</value>
	public int? ColorTemperature
		{
		get;
		}

	/// <summary>
	/// Gets the preset hue component.
	/// </summary>
	/// <value>The preset hue component.</value>
	public int? Hue
		{
		get;
		}

	/// <summary>
	/// Gets the preset saturation component.
	/// </summary>
	/// <value>The preset saturation component.</value>
	public int? Saturation
		{
		get;
		}
	}

/// <summary>
/// Represents normalized light preset module state.
/// </summary>
public sealed record LightPresetState
	{
	internal LightPresetState (IReadOnlyList<LightPresetDefinition> presets, string? activePreset)
		{
		Presets = presets;
		ActivePreset = activePreset;
		}

	/// <summary>
	/// Gets the presets reported by the device.
	/// </summary>
	/// <value>The presets reported by the device.</value>
	public IReadOnlyList<LightPresetDefinition> Presets
		{
		get;
		}

	/// <summary>
	/// Gets the active preset name when the current state matches one.
	/// </summary>
	/// <value>The active preset name when the current state matches one.</value>
	public string? ActivePreset
		{
		get;
		}
	}

/// <summary>
/// Represents normalized light transition module state.
/// </summary>
public sealed record LightTransitionState
	{
	internal LightTransitionState (
		bool? isEnabled,
		bool? isTransitionOnEnabled,
		int? transitionOnDurationSeconds,
		int? transitionOnMaximumDurationSeconds,
		bool? isTransitionOffEnabled,
		int? transitionOffDurationSeconds,
		int? transitionOffMaximumDurationSeconds,
		bool? isChangeTransitionEnabled = null)
		{
		IsEnabled = isEnabled;
		IsTransitionOnEnabled = isTransitionOnEnabled;
		TransitionOnDurationSeconds = transitionOnDurationSeconds;
		TransitionOnMaximumDurationSeconds = transitionOnMaximumDurationSeconds;
		IsTransitionOffEnabled = isTransitionOffEnabled;
		TransitionOffDurationSeconds = transitionOffDurationSeconds;
		TransitionOffMaximumDurationSeconds = transitionOffMaximumDurationSeconds;
		IsChangeTransitionEnabled = isChangeTransitionEnabled;
		}

	/// <summary>
	/// Gets whether fading between light settings is enabled, or <see langword="null" /> if not reported.
	/// </summary>
	/// <value>Whether fading between light settings is enabled, or <see langword="null" /> if not reported.</value>
	public bool? IsChangeTransitionEnabled { get; }

	/// <summary>
	/// Gets a value indicating whether smooth transition behavior is enabled.
	/// </summary>
	/// <value>A value indicating whether smooth transition behavior is enabled.</value>
	public bool? IsEnabled
		{
		get;
		}

	/// <summary>
	/// Gets a value indicating whether the turn-on transition is enabled.
	/// </summary>
	/// <value>A value indicating whether the turn-on transition is enabled.</value>
	public bool? IsTransitionOnEnabled
		{
		get;
		}

	/// <summary>
	/// Gets the effective transition duration used when turning the light on, in seconds.
	/// </summary>
	/// <value>The effective transition duration used when turning the light on, in seconds.</value>
	public int? TransitionOnSeconds => IsTransitionOnEnabled == false ? 0 : TransitionOnDurationSeconds;

	/// <summary>
	/// Gets the stored turn-on transition duration, in seconds.
	/// </summary>
	/// <value>The stored turn-on transition duration, in seconds.</value>
	public int? TransitionOnDurationSeconds
		{
		get;
		}

	/// <summary>
	/// Gets the maximum supported turn-on transition duration, in seconds.
	/// </summary>
	/// <value>The maximum supported turn-on transition duration, in seconds.</value>
	public int? TransitionOnMaximumDurationSeconds
		{
		get;
		}

	/// <summary>
	/// Gets a value indicating whether the turn-off transition is enabled.
	/// </summary>
	/// <value>A value indicating whether the turn-off transition is enabled.</value>
	public bool? IsTransitionOffEnabled
		{
		get;
		}

	/// <summary>
	/// Gets the effective transition duration used when turning the light off, in seconds.
	/// </summary>
	/// <value>The effective transition duration used when turning the light off, in seconds.</value>
	public int? TransitionOffSeconds => IsTransitionOffEnabled == false ? 0 : TransitionOffDurationSeconds;

	/// <summary>
	/// Gets the stored turn-off transition duration, in seconds.
	/// </summary>
	/// <value>The stored turn-off transition duration, in seconds.</value>
	public int? TransitionOffDurationSeconds
		{
		get;
		}

	/// <summary>
	/// Gets the maximum supported turn-off transition duration, in seconds.
	/// </summary>
	/// <value>The maximum supported turn-off transition duration, in seconds.</value>
	public int? TransitionOffMaximumDurationSeconds
		{
		get;
		}
	}

/// <summary>
/// Represents normalized light strip effect module state.
/// </summary>
public sealed record LightStripEffectState
	{
	internal LightStripEffectState (LightEffectState? effect, IReadOnlyList<LightEffectDefinition> availableEffects)
		{
		Effect = effect;
		AvailableEffects = availableEffects;
		}

	/// <summary>
	/// Gets the active effect state.
	/// </summary>
	/// <value>The active effect state.</value>
	public LightEffectState? Effect
		{
		get;
		}

	/// <summary>
	/// Gets the effects reported by the strip.
	/// </summary>
	/// <value>The effects reported by the strip.</value>
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
		IReadOnlyList<ChildDeviceInfo> children)
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
		}

	/// <summary>
	/// Gets the user-visible alias.
	/// </summary>
	/// <value>The user-visible alias.</value>
	public string Alias
		{
		get;
		}

	/// <summary>
	/// Gets the model identifier, when available.
	/// </summary>
	/// <value>The model identifier, when available.</value>
	public string? Model
		{
		get;
		}

	/// <summary>
	/// Gets the device identifier, when available.
	/// </summary>
	/// <value>The device identifier, when available.</value>
	public string? DeviceId
		{
		get;
		}

	/// <summary>
	/// Gets the MAC address, when available.
	/// </summary>
	/// <value>The MAC address, when available.</value>
	public string? MacAddress
		{
		get;
		}

	/// <summary>
	/// Gets the hardware version, when available.
	/// </summary>
	/// <value>The hardware version, when available.</value>
	public string? HardwareVersion
		{
		get;
		}

	/// <summary>
	/// Gets the software version, when available.
	/// </summary>
	/// <value>The software version, when available.</value>
	public string? SoftwareVersion
		{
		get;
		}

	/// <summary>
	/// Gets the reported Wi-Fi signal level, when available.
	/// </summary>
	/// <value>The reported Wi-Fi signal level, when available.</value>
	public int? SignalLevel
		{
		get;
		}

	/// <summary>
	/// Gets the reported RSSI in dBm, when available.
	/// </summary>
	/// <value>The reported RSSI in dBm, when available.</value>
	public int? Rssi
		{
		get;
		}

	/// <summary>
	/// Gets the reported SSID, when available.
	/// </summary>
	/// <value>The reported SSID, when available.</value>
	public string? Ssid
		{
		get;
		}

	/// <summary>
	/// Gets the inferred device family.
	/// </summary>
	/// <value>The inferred device family.</value>
	public DeviceType DeviceType
		{
		get;
		}

	/// <summary>
	/// Gets a value indicating whether the device appears to be on.
	/// </summary>
	/// <value>A value indicating whether the device appears to be on.</value>
	public bool? IsOn
		{
		get;
		}

	/// <summary>
	/// Gets the reported on-time duration when the device exposes it.
	/// </summary>
	/// <value>The reported on-time duration when the device exposes it.</value>
	public TimeSpan? OnTime
		{
		get;
		}

	/// <summary>
	/// Gets the child devices reported by the device.
	/// </summary>
	/// <value>The child devices reported by the device.</value>
	public IReadOnlyList<ChildDeviceInfo> Children
		{
		get;
		}
	}
