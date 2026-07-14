// Copyright © 2026 Neil Colvin.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// Behavior modeled after the independent python-kasa project (https://github.com/python-kasa/python-kasa)
// for protocol/compatibility reference only; no python-kasa source was copied. See ATTRIBUTIONS.md.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace KasaTapoClient.Internal;

internal interface IDeviceTransport
	{
	Task<string> SendAsync (string commandJson, CancellationToken cancellationToken);
	Task<string> SendManyAsync (IReadOnlyList<string> commandJsonPayloads, CancellationToken cancellationToken);
	}

internal interface IDisposableDeviceTransport : IDeviceTransport, IDisposable
	{
	}

internal static class DeviceTransportFactory
	{
	public static IDeviceTransport Create (DeviceConfiguration configuration)
		{
		DeviceTransportKind transportKind = ResolveTransportKind (configuration);
		return transportKind switch
			{
				DeviceTransportKind.LegacyXor => new LegacyTransport (configuration),
				DeviceTransportKind.HttpToken => CreateHttpFamilyTransport (configuration),
				_ => throw new ArgumentOutOfRangeException (nameof (configuration), transportKind, "Unknown device transport kind."),
				};
		}

	private static IDeviceTransport CreateHttpFamilyTransport (DeviceConfiguration configuration)
		{
		DeviceConnectionParameters? connectionParameters = configuration.ConnectionOptions.ConnectionParameters;
		if (connectionParameters is not null)
			{
			ValidateSupportedConnectionParameters (connectionParameters);
			}

		if (connectionParameters?.DeviceFamily == DeviceFamilyKind.SmartTapoHub
			&& connectionParameters.UseHttps
			&& connectionParameters.EncryptionKind == DeviceEncryptionKind.Klap)
			{
			return new HttpTokenTransport (configuration);
			}

		if (connectionParameters?.EncryptionKind == DeviceEncryptionKind.Klap)
			{
			return new KlapTransport (configuration);
			}

		if (connectionParameters?.EncryptionKind == DeviceEncryptionKind.Tpap)
			{
			return new TpapTransport (configuration);
			}

		return new HttpTokenTransport (configuration);
		}

	private static DeviceTransportKind ResolveTransportKind (DeviceConfiguration configuration)
		{
		DeviceConnectionParameters? connectionParameters = configuration.ConnectionOptions.ConnectionParameters;
		if (configuration.ConnectionOptions.TransportKind != DeviceTransportKind.Auto)
			{
				ValidateExplicitTransportSelection (configuration.ConnectionOptions.TransportKind, connectionParameters);
			return configuration.ConnectionOptions.TransportKind;
			}

		if (connectionParameters is not null)
			{
				ValidateSupportedConnectionParameters (connectionParameters);
				return connectionParameters.TransportKind;
			}

		return configuration.Port == 9999
			? DeviceTransportKind.LegacyXor
			: DeviceTransportKind.HttpToken;
		}

	private static void ValidateExplicitTransportSelection (DeviceTransportKind explicitTransportKind, DeviceConnectionParameters? connectionParameters)
		{
		if (connectionParameters is null)
			{
			return;
			}

		if (connectionParameters.TransportKind != explicitTransportKind)
			{
			throw new InvalidOperationException ($"The explicit transport '{explicitTransportKind}' does not match the parsed connection parameters '{connectionParameters.TransportKind}'.");
			}
		}

	private static void ValidateSupportedConnectionParameters (DeviceConnectionParameters connectionParameters)
		{
		DeviceFamilyKind deviceFamily = connectionParameters.DeviceFamily;
		DeviceEncryptionKind encryptionKind = connectionParameters.EncryptionKind;

		if (deviceFamily == DeviceFamilyKind.IotIpCamera && encryptionKind != DeviceEncryptionKind.Xor)
			{
			throw new NotSupportedException ($"Device family '{deviceFamily}' requires XOR transport parity that is not available for encryption '{encryptionKind}'.");
			}

		if ((deviceFamily == DeviceFamilyKind.SmartIpCamera || deviceFamily == DeviceFamilyKind.SmartTapoDoorbell)
			&& encryptionKind != DeviceEncryptionKind.Aes)
			{
			throw new NotSupportedException ($"Device family '{deviceFamily}' requires AES HTTPS transport parity; '{encryptionKind}' is not supported.");
			}

		if (deviceFamily == DeviceFamilyKind.SmartTapoRobovac && encryptionKind != DeviceEncryptionKind.Aes)
			{
			throw new NotSupportedException ($"Device family '{deviceFamily}' currently requires AES transport parity; '{encryptionKind}' is not supported.");
			}
		}
	}
