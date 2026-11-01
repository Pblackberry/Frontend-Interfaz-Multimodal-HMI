using System.Text.Json;
using FrontEnd.InterfazHMI.Tests.Support;
using Frontend_interfaz_Multimodal_HMI.Models;
using Frontend_interfaz_Multimodal_HMI.Services;

namespace FrontEnd.InterfazHMI.Tests.Integration.Services;

public class BackendLinkIntegrationTests
{
    private static readonly TimeSpan NoMessageWait = TimeSpan.FromMilliseconds(400);

    private static JsonElement Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
    }

    private static async Task<JsonElement> NextMessageAsync(WsClientSession session)
        => Parse(await FakeWsServer.ReceiveTextAsync(session.ServerSocket));

    [Fact]
    public async Task Navigated_SendsAStateMessageToTheBackend()
    {
        await using var server = new FakeWsServer();
        await using var session = await WsClientSession.StartAsync(server);
        var nav = new FakeNavigationService();
        _ = new BackendLink(session.Client, nav);

        nav.RaiseNavigated(Screen.Menu, StateSource.Touch);
        var message = await NextMessageAsync(session);

        Assert.Equal("state", message.GetProperty("type").GetString());
        Assert.Equal("menu", message.GetProperty("payload").GetProperty("screen").GetString());
        Assert.Equal("touch", message.GetProperty("payload").GetProperty("source").GetString());
    }

    [Theory]
    [InlineData(Screen.Welcome, StateSource.Touch, null, "welcome", "touch")]
    [InlineData(Screen.Menu, StateSource.Touch, null, "menu", "touch")]
    [InlineData(Screen.Menu, StateSource.Backend, "order-1", "menu", "backend")]
    [InlineData(Screen.Welcome, StateSource.Backend, "order-2", "welcome", "backend")]
    [InlineData(Screen.Welcome, StateSource.Init, null, "welcome", "init")]
    public async Task Navigated_SendsTheScreenSourceAndOrderIdInThePayload(
        Screen screen, StateSource source, string? orderId, string expectedScreen, string expectedSource)
    {
        await using var server = new FakeWsServer();
        await using var session = await WsClientSession.StartAsync(server);
        var nav = new FakeNavigationService();
        _ = new BackendLink(session.Client, nav);

        nav.RaiseNavigated(screen, source, orderId);
        var payload = (await NextMessageAsync(session)).GetProperty("payload");

        Assert.Equal(expectedScreen, payload.GetProperty("screen").GetString());
        Assert.Equal(expectedSource, payload.GetProperty("source").GetString());
        if (orderId is null)
        {
            Assert.Equal(JsonValueKind.Null, payload.GetProperty("ref").ValueKind);
        }
        else
        {
            Assert.Equal(orderId, payload.GetProperty("ref").GetString());
        }
    }

    [Fact]
    public async Task Navigated_SendsAnEnvelopeWithIdAndTimestamp()
    {
        await using var server = new FakeWsServer();
        await using var session = await WsClientSession.StartAsync(server);
        var nav = new FakeNavigationService();
        _ = new BackendLink(session.Client, nav);

        nav.RaiseNavigated(Screen.Menu);
        var message = await NextMessageAsync(session);

        Assert.True(Guid.TryParseExact(message.GetProperty("id").GetString(), "N", out _));
        Assert.True(DateTimeOffset.TryParse(message.GetProperty("ts").GetString(), out var ts));
        Assert.Equal(TimeSpan.Zero, ts.Offset);
    }

    [Fact]
    public async Task EveryNavigationIsReported_InOrder()
    {
        await using var server = new FakeWsServer();
        await using var session = await WsClientSession.StartAsync(server);
        var nav = new FakeNavigationService();
        _ = new BackendLink(session.Client, nav);

        nav.RaiseNavigated(Screen.Menu, StateSource.Touch);
        nav.RaiseNavigated(Screen.Welcome, StateSource.Backend, "order-5");
        nav.RaiseNavigated(Screen.Menu, StateSource.Touch);

        var screens = new List<string?>();
        for (var i = 0; i < 3; i++)
        {
            screens.Add((await NextMessageAsync(session)).GetProperty("payload").GetProperty("screen").GetString());
        }

        Assert.Equal(new[] { "menu", "welcome", "menu" }, screens);
    }

    [Fact]
    public async Task EachMessageHasItsOwnId()
    {
        await using var server = new FakeWsServer();
        await using var session = await WsClientSession.StartAsync(server);
        var nav = new FakeNavigationService();
        _ = new BackendLink(session.Client, nav);

        nav.RaiseNavigated(Screen.Menu);
        nav.RaiseNavigated(Screen.Welcome);
        var first = (await NextMessageAsync(session)).GetProperty("id").GetString();
        var second = (await NextMessageAsync(session)).GetProperty("id").GetString();

        Assert.NotEqual(first, second);
    }

    [Fact]
    public async Task NavigationsMadeBeforeTheConnection_AreNotQueued()
    {
        await using var server = new FakeWsServer();
        var nav = new FakeNavigationService();
        BackendLink? link = null;

        await using var session = await WsClientSession.StartAsync(server, beforeRun: client =>
        {
            link = new BackendLink(client, nav);
            nav.RaiseNavigated(Screen.Menu, StateSource.Touch);
        });
        var late = await FakeWsServer.TryReceiveTextAsync(session.ServerSocket, NoMessageWait);

        Assert.NotNull(link);
        Assert.Null(late);
    }
}
