// Copyright (c) 2026 Neil Colvin. See LICENSE in the repository root.
using KasaTapoClient;
using NUnit.Framework;

namespace KasaClient.Tests;

[TestFixture]
public sealed class OutletStateTests
{
    [TestCase(true)]
    [TestCase(false)]
    [TestCase(null)]
    public async Task SmartRelayReadIsOneRequestAndDoesNotFetchModules(bool? reported)
    {
        string state = reported.HasValue ? ",\"device_on\":"+(reported.Value?"true":"false") : "";
        var transport = new FakeDeviceTransport(sendResponses:
            ["{\"result\":{\"responses\":[{\"method\":\"get_device_info\",\"error_code\":0,\"result\":{\"model\":\"KP125M\""+state+"}}]}}"]);
        using var device = new KasaDevice(new DeviceConfiguration("127.0.0.1", connectionOptions:
            new DeviceConnectionOptions(connectionParameters: new DeviceConnectionParameters(DeviceFamilyKind.SmartKasaPlug, DeviceEncryptionKind.Klap))), transport);
        Assert.That(await device.ReadOutletStateAsync(), Is.EqualTo(reported));
        Assert.That(transport.SentCommands, Has.Count.EqualTo(1));
        Assert.That(transport.SentCommands[0], Does.Contain("get_device_info").And.Not.Contain("component_nego").And.Not.Contain("get_energy_usage"));
        Assert.That(transport.SentManyCommands, Is.Empty);
    }

    [Test]
    public async Task LegacyCommandReportsDeviceReadbackRatherThanRequestedState()
    {
        var transport = new FakeDeviceTransport(sendResponses:
            ["{\"system\":{\"set_relay_state\":{\"err_code\":0}}}",
             "{\"system\":{\"get_sysinfo\":{\"relay_state\":0}}}"]);
        using var device = new KasaDevice(new DeviceConfiguration("127.0.0.1"), transport);
        Assert.That(await device.SetOutletStateAndReadBackAsync(true), Is.False);
        Assert.That(transport.SentCommands, Has.Count.EqualTo(2));
        Assert.That(transport.SentManyCommands, Is.Empty);
    }
}
