// Copyright © 2026 Neil Colvin. See LICENSE in the repository root.
using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using KasaTapoClient;
using KasaTapoClient.Internal;
using NUnit.Framework;

namespace KasaClient.Tests;

[TestFixture]
public sealed class TpapSessionRecoveryTests
{
    [TestCase(true)]
    [TestCase(false)]
    public async Task RejectedEstablishedSession_StartsFreshHandshake_WithoutRetryingRejectedLogin(bool keepAliveDue)
    {
        var result = await Exercise(401, keepAliveDue, established: true);
        Assert.Multiple(() =>
        {
            Assert.That(result.Paths, Has.Length.EqualTo(2), "Only the expired-session request and one fresh login should be sent.");
            Assert.That(result.Paths[0], Does.Contain("stok=expired/ds"));
            Assert.That(result.Paths.Last(), Is.EqualTo("/"));
            Assert.That(result.Error, Does.Contain("discover failed"), "The old session must not trap all future polls.");
        });
    }

    [TestCase(403, true)]
    [TestCase(429, true)]
    [TestCase(500, true)]
    [TestCase(403, false)]
    [TestCase(429, false)]
    [TestCase(500, false)]
    public async Task OtherHttpFailures_DoNotTriggerAuthentication(int status, bool keepAliveDue)
    {
        var result = await Exercise(status, keepAliveDue, established: true);
        Assert.That(result.Paths, Has.Length.EqualTo(1));
        Assert.That(result.Error, Does.Contain("status " + status.ToString(CultureInfo.InvariantCulture)));
    }

    [Test]
    public async Task LoginUnauthorized_IsNotMistakenForAnExpiredEstablishedSession()
    {
        var result = await Exercise(401, keepAliveDue: false, established: false);
        Assert.That(result.Paths, Is.EqualTo(new[] { "/" }));
        Assert.That(result.Error, Does.Contain("discover failed"));
    }

    private static async Task<(string[] Paths, string Error)> Exercise(int status, bool keepAliveDue, bool established)
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(12));
        using var stop = deadline.Token.Register(listener.Stop);
        var paths = new ConcurrentQueue<string>();
        var server = Task.Run(async () =>
        {
            try
            {
                while (!deadline.IsCancellationRequested)
                {
                    using var client = await listener.AcceptTcpClientAsync();
                    using var stream = client.GetStream();
                    var header = new StringBuilder();
                    var one = new byte[1];
                    while (!header.ToString().EndsWith("\r\n\r\n", StringComparison.Ordinal))
                    {
                        if (header.Length > 16384 || await stream.ReadAsync(one, 0, 1, deadline.Token) == 0)
                            throw new IOException("Invalid loopback request headers.");
                        header.Append((char)one[0]);
                    }
                    string[] lines = header.ToString().Split(new[] { "\r\n" }, StringSplitOptions.None);
                    string path = lines[0].Split(' ')[1];
                    int length = int.Parse(lines.Single(line => line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase)).Split(':')[1], CultureInfo.InvariantCulture);
                    var body = new byte[length];
                    int received = 0;
                    while (received < length)
                    {
                        int read = await stream.ReadAsync(body, received, length - received, deadline.Token);
                        if (read == 0) throw new IOException("Incomplete loopback request body.");
                        received += read;
                    }
                    paths.Enqueue(path);
                    // Reject the stale session, then reject login. The latter must not start a retry loop.
                    int responseStatus = paths.Count == 1 ? status : 401;
                    byte[] response = Encoding.ASCII.GetBytes("HTTP/1.1 " + responseStatus.ToString(CultureInfo.InvariantCulture) + " Rejected\r\nContent-Length: 2\r\nConnection: close\r\n\r\n{}");
                    await stream.WriteAsync(response, 0, response.Length, deadline.Token);
                }
            }
            catch (Exception) when (deadline.IsCancellationRequested) { }
        });
        using var transport = new TpapTransport(new DeviceConfiguration("127.0.0.1", port, timeout: TimeSpan.FromSeconds(5)));
        if (established)
        {
            // Seed a previously authenticated session without duplicating the PAKE implementation.
            // Requests still cross the actual HttpClient/HttpWebRequest path on each target framework.
            void Set(string name, object value) => typeof(TpapTransport).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(transport, value);
            Set("_sessionId", "expired");
            Set("_sequence", 1);
            Set("_dsUri", new Uri("http://127.0.0.1:" + port.ToString(CultureInfo.InvariantCulture) + "/stok=expired/ds"));
            Set("_key", new byte[16]);
            Set("_baseNonce", new byte[12]);
            Set("_lastActivityUtc", DateTimeOffset.UtcNow.AddMinutes(keepAliveDue ? -2 : 0));
        }
        string error;
        try
        {
            try
            {
                await transport.SendAsync("{\"method\":\"get_device_info\"}", deadline.Token);
                throw new AssertionException("The loopback server always rejects requests.");
            }
            catch (InvalidOperationException ex) { error = ex.Message; }
        }
        finally
        {
            deadline.Cancel();
            listener.Stop();
            await server;
        }
        return (paths.ToArray(), error);
    }
}
