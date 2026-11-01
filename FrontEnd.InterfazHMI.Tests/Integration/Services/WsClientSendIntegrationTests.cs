using System.Text.Json;
using FrontEnd.InterfazHMI.Tests.Support;
using Frontend_interfaz_Multimodal_HMI.Models;
using Frontend_interfaz_Multimodal_HMI.Services;

namespace FrontEnd.InterfazHMI.Tests.Integration.Services;

public class WsClientSendIntegrationTests
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(10);

    private sealed record Ping(int N, string TextoLibre);

    private static JsonElement Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
    }

    // ---------- IsConnected ----------

    [Fact]
    public void IsConnected_BeforeRunning_IsFalse()
    {
        Assert.False(new WsClient(new Uri("ws://127.0.0.1:1/ws")).IsConnected);
    }

    [Fact]
    public async Task IsConnected_WhileConnected_IsTrue()
    {
        await using var server = new FakeWsServer();
        await using var session = await WsClientSession.StartAsync(server);

        Assert.True(session.Client.IsConnected);
    }

    [Fact]
    public async Task IsConnected_AfterTheClientIsStopped_IsFalse()
    {
        await using var server = new FakeWsServer();
        await using var session = await WsClientSession.StartAsync(server);

        await session.StopAsync();

        Assert.False(session.Client.IsConnected);
    }

    // ---------- SendAsync ----------

    [Fact]
    public async Task SendAsync_WhenNotConnected_ReturnsFalseWithoutThrowing()
    {
        var client = new WsClient(new Uri("ws://127.0.0.1:1/ws"));

        var sent = await client.SendAsync("state", new StatePayload(Screen.Menu, StateSource.Touch));

        Assert.False(sent);
    }

    [Fact]
    public async Task SendAsync_AfterTheClientIsStopped_ReturnsFalse()
    {
        await using var server = new FakeWsServer();
        await using var session = await WsClientSession.StartAsync(server);
        await session.StopAsync();

        var sent = await session.Client.SendAsync("state", new StatePayload(Screen.Menu, StateSource.Touch));

        Assert.False(sent);
    }

    [Fact]
    public async Task SendAsync_WhenConnected_ReturnsTrueAndTheServerReceivesTheMessage()
    {
        await using var server = new FakeWsServer();
        await using var session = await WsClientSession.StartAsync(server);

        var sent = await session.Client.SendAsync("ping", new Ping(3, "hola"));
        var message = Parse(await FakeWsServer.ReceiveTextAsync(session.ServerSocket));

        Assert.True(sent);
        Assert.Equal("ping", message.GetProperty("type").GetString());
    }

    [Fact]
    public async Task SendAsync_WritesThePayloadInSnakeCase()
    {
        await using var server = new FakeWsServer();
        await using var session = await WsClientSession.StartAsync(server);

        await session.Client.SendAsync("ping", new Ping(3, "hola"));
        var payload = Parse(await FakeWsServer.ReceiveTextAsync(session.ServerSocket)).GetProperty("payload");

        Assert.Equal(3, payload.GetProperty("n").GetInt32());
        Assert.Equal("hola", payload.GetProperty("texto_libre").GetString());
    }

    [Fact]
    public async Task SendAsync_WritesEnumsAsLowerCaseText()
    {
        await using var server = new FakeWsServer();
        await using var session = await WsClientSession.StartAsync(server);

        await session.Client.SendAsync(WsTypes.State, new StatePayload(Screen.Menu, StateSource.Backend, "order-1"));
        var message = Parse(await FakeWsServer.ReceiveTextAsync(session.ServerSocket));

        Assert.Equal("state", message.GetProperty("type").GetString());
        Assert.Equal("menu", message.GetProperty("payload").GetProperty("screen").GetString());
        Assert.Equal("backend", message.GetProperty("payload").GetProperty("source").GetString());
        Assert.Equal("order-1", message.GetProperty("payload").GetProperty("ref").GetString());
    }

    [Fact]
    public async Task SendAsync_WrapsThePayloadInAnEnvelopeWithIdAndTimestamp()
    {
        await using var server = new FakeWsServer();
        await using var session = await WsClientSession.StartAsync(server);

        await session.Client.SendAsync("ping", new Ping(1, "x"));
        var message = Parse(await FakeWsServer.ReceiveTextAsync(session.ServerSocket));

        Assert.True(Guid.TryParseExact(message.GetProperty("id").GetString(), "N", out _));
        Assert.True(DateTimeOffset.TryParse(message.GetProperty("ts").GetString(), out _));
    }

    [Fact]
    public async Task SendAsync_EveryMessageGetsADifferentId()
    {
        await using var server = new FakeWsServer();
        await using var session = await WsClientSession.StartAsync(server);

        await session.Client.SendAsync("ping", new Ping(1, "x"));
        await session.Client.SendAsync("ping", new Ping(2, "x"));
        var first = Parse(await FakeWsServer.ReceiveTextAsync(session.ServerSocket)).GetProperty("id").GetString();
        var second = Parse(await FakeWsServer.ReceiveTextAsync(session.ServerSocket)).GetProperty("id").GetString();

        Assert.NotEqual(first, second);
    }

    [Fact]
    public async Task SendAsync_ALargeMessage_ArrivesComplete()
    {
        await using var server = new FakeWsServer();
        await using var session = await WsClientSession.StartAsync(server);
        var text = new string('x', 30_000);

        await session.Client.SendAsync("ping", new Ping(1, text));
        var payload = Parse(await FakeWsServer.ReceiveTextAsync(session.ServerSocket)).GetProperty("payload");

        Assert.Equal(30_000, payload.GetProperty("texto_libre").GetString()!.Length);
    }

    [Fact]
    public async Task SendAsync_ConcurrentCalls_AreAllDeliveredIntact()
    {
        await using var server = new FakeWsServer();
        await using var session = await WsClientSession.StartAsync(server);
        const int total = 25;

        var results = await Task.WhenAll(
            Enumerable.Range(0, total).Select(i => session.Client.SendAsync("ping", new Ping(i, "x"))));

        var received = new List<int>();
        for (var i = 0; i < total; i++)
        {
            var message = Parse(await FakeWsServer.ReceiveTextAsync(session.ServerSocket));
            received.Add(message.GetProperty("payload").GetProperty("n").GetInt32());
        }
        Assert.True(results.All(r => r));
        Assert.Equal(Enumerable.Range(0, total), received.OrderBy(n => n));
    }

    [Fact]
    public async Task SendAsync_WithAnAlreadyCancelledToken_ThrowsOperationCancelled()
    {
        await using var server = new FakeWsServer();
        await using var session = await WsClientSession.StartAsync(server);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => session.Client.SendAsync("ping", new Ping(1, "x"), cts.Token));
    }

    [Fact]
    public async Task SendAsync_WhenTheServerDropsTheConnection_EventuallyReturnsFalseWithoutThrowing()
    {
        await using var server = new FakeWsServer();
        await using var session = await WsClientSession.StartAsync(server);
        session.ServerSocket.Abort();

        var failed = false;
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        while (DateTime.UtcNow < deadline && !failed)
        {
            failed = !await session.Client.SendAsync("ping", new Ping(1, "x"));
            if (!failed)
            {
                await Task.Delay(50);
            }
        }

        Assert.True(failed);
    }

    // ---------- Connected ----------

    [Fact]
    public async Task Connected_IsRaisedWhenTheConnectionIsEstablished_AndTheClientIsAlreadyConnected()
    {
        await using var server = new FakeWsServer();
        var seen = new List<bool>();
        WsClient? created = null;

        await using var session = await WsClientSession.StartAsync(server, beforeRun: client =>
        {
            created = client;
            client.Connected += () => seen.Add(client.IsConnected);
        });

        Assert.NotNull(created);
        Assert.Equal(new[] { true }, seen);
    }

    [Fact]
    public async Task Connected_IsRaisedAgainAfterAReconnect_AndSendingWorksOnTheNewConnection()
    {
        await using var server = new FakeWsServer();
        var connectedCount = 0;
        await using var session = await WsClientSession.StartAsync(
            server, beforeRun: client => client.Connected += () => Interlocked.Increment(ref connectedCount));
        await session.ServerSocket.CloseOutputAsync(
            System.Net.WebSockets.WebSocketCloseStatus.NormalClosure, "bye", CancellationToken.None);

        var second = await server.AcceptAsync();
        SpinWait.SpinUntil(() => session.Client.IsConnected && Volatile.Read(ref connectedCount) == 2, _timeout);
        var sent = await session.Client.SendAsync("ping", new Ping(7, "otra vez"));
        var message = Parse(await FakeWsServer.ReceiveTextAsync(second));

        Assert.Equal(2, Volatile.Read(ref connectedCount));
        Assert.True(sent);
        Assert.Equal(7, message.GetProperty("payload").GetProperty("n").GetInt32());
    }
}
