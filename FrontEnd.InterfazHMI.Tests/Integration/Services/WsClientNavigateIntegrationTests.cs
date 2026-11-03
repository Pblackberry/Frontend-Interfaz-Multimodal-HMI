using System.Threading.Channels;
using FrontEnd.InterfazHMI.Tests.Support;
using Frontend_interfaz_Multimodal_HMI.Models;
using Frontend_interfaz_Multimodal_HMI.Services;

namespace FrontEnd.InterfazHMI.Tests.Integration.Services;

public class WsClientNavigateIntegrationTests
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(10);

    private const string _ts = "2026-11-02T14:33:53.811394Z";

    private static string NavigateJson(string id, string screen)
        => "{\"id\":\"" + id + "\",\"ts\":\"" + _ts + "\",\"type\":\"navigate\",\"payload\":{\"screen\":\"" + screen + "\"}}";

    private static string CatalogJson(string fecha)
        => "{\"id\":\"c1\",\"ts\":\"" + _ts + "\",\"type\":\"catalog_update\",\"payload\":{" +
           "\"ultima_actualizacion\":\"" + fecha + "\",\"catalogo\":{\"items\":[],\"total\":0}}}";

    private sealed class Listener
    {
        public Channel<(string Id, NavigatePayload Payload)> Navigations { get; }
            = Channel.CreateUnbounded<(string, NavigatePayload)>();

        public Channel<CatalogUpdatePayload> Catalogs { get; } = Channel.CreateUnbounded<CatalogUpdatePayload>();

        public void Attach(WsClient client)
        {
            client.NavigateRequested += (id, payload) => Navigations.Writer.TryWrite((id, payload));
            client.CatalogUpdated += payload => Catalogs.Writer.TryWrite(payload);
        }

        public Task<(string Id, NavigatePayload Payload)> NextNavigationAsync()
            => Navigations.Reader.ReadAsync().AsTask().WaitAsync(_timeout);

        public Task<CatalogUpdatePayload> NextCatalogAsync()
            => Catalogs.Reader.ReadAsync().AsTask().WaitAsync(_timeout);
    }

    [Fact]
    public async Task NavigateMessage_RaisesTheEventWithTheMessageIdAndTheScreen()
    {
        await using var server = new FakeWsServer();
        var listener = new Listener();
        await using var session = await WsClientSession.StartAsync(server, listener.Attach);

        await FakeWsServer.SendTextAsync(session.ServerSocket, NavigateJson("order-1", "menu"));
        var (id, payload) = await listener.NextNavigationAsync();

        Assert.Equal("order-1", id);
        Assert.Equal(new NavigatePayload(Screen.Menu), payload);
    }

    [Theory]
    [InlineData("welcome", Screen.Welcome)]
    [InlineData("menu", Screen.Menu)]
    public async Task NavigateMessage_CarriesEachScreen(string text, Screen expected)
    {
        await using var server = new FakeWsServer();
        var listener = new Listener();
        await using var session = await WsClientSession.StartAsync(server, listener.Attach);

        await FakeWsServer.SendTextAsync(session.ServerSocket, NavigateJson("o", text));

        Assert.Equal(expected, (await listener.NextNavigationAsync()).Payload.Screen);
    }

    [Fact]
    public async Task NavigateMessagesArrive_InOrder()
    {
        await using var server = new FakeWsServer();
        var listener = new Listener();
        await using var session = await WsClientSession.StartAsync(server, listener.Attach);

        await FakeWsServer.SendTextAsync(session.ServerSocket, NavigateJson("o1", "menu"));
        await FakeWsServer.SendTextAsync(session.ServerSocket, NavigateJson("o2", "welcome"));

        Assert.Equal("o1", (await listener.NextNavigationAsync()).Id);
        Assert.Equal("o2", (await listener.NextNavigationAsync()).Id);
    }

    [Fact]
    public async Task NavigateAndCatalogMessages_EachReachOnlyTheirOwnEvent()
    {
        await using var server = new FakeWsServer();
        var listener = new Listener();
        await using var session = await WsClientSession.StartAsync(server, listener.Attach);

        await FakeWsServer.SendTextAsync(session.ServerSocket, NavigateJson("o1", "menu"));
        await FakeWsServer.SendTextAsync(session.ServerSocket, CatalogJson("f1"));
        var catalog = await listener.NextCatalogAsync();

        Assert.Equal("f1", catalog.UltimaActualizacion);
        Assert.True(listener.Navigations.Reader.TryRead(out var navigation));
        Assert.Equal("o1", navigation.Id);
        Assert.False(listener.Navigations.Reader.TryRead(out _));
        Assert.False(listener.Catalogs.Reader.TryRead(out _));
    }

    [Theory]
    [InlineData("{\"id\":\"x\",\"ts\":\"2026-11-02T14:33:53Z\",\"type\":\"navigate\",\"payload\":{\"screen\":\"welome\"}}")]
    [InlineData("{\"id\":\"x\",\"ts\":\"2026-11-02T14:33:53Z\",\"type\":\"navigate\"}")]
    public async Task InvalidNavigateMessage_IsIgnored_AndTheConnectionKeepsWorking(string badMessage)
    {
        await using var server = new FakeWsServer();
        var listener = new Listener();
        await using var session = await WsClientSession.StartAsync(server, listener.Attach);

        await FakeWsServer.SendTextAsync(session.ServerSocket, badMessage);
        await FakeWsServer.SendTextAsync(session.ServerSocket, NavigateJson("valido", "menu"));

        Assert.Equal("valido", (await listener.NextNavigationAsync()).Id);
    }

    [Fact]
    public async Task ASubscriberThatThrows_DoesNotBreakTheConnection()
    {
        await using var server = new FakeWsServer();
        var listener = new Listener();
        await using var session = await WsClientSession.StartAsync(server, client =>
        {
            listener.Attach(client);
            client.NavigateRequested += (_, _) => throw new InvalidOperationException("falla en la UI");
        });

        await FakeWsServer.SendTextAsync(session.ServerSocket, NavigateJson("o1", "menu"));
        await FakeWsServer.SendTextAsync(session.ServerSocket, NavigateJson("o2", "welcome"));

        Assert.Equal("o1", (await listener.NextNavigationAsync()).Id);
        Assert.Equal("o2", (await listener.NextNavigationAsync()).Id);
    }

    [Fact]
    public async Task NavigateMessageWithoutSubscribers_IsHarmless()
    {
        await using var server = new FakeWsServer();
        var catalogs = Channel.CreateUnbounded<CatalogUpdatePayload>();
        await using var session = await WsClientSession.StartAsync(
            server, client => client.CatalogUpdated += payload => catalogs.Writer.TryWrite(payload));

        await FakeWsServer.SendTextAsync(session.ServerSocket, NavigateJson("o1", "menu"));
        await FakeWsServer.SendTextAsync(session.ServerSocket, CatalogJson("f1"));
        var catalog = await catalogs.Reader.ReadAsync().AsTask().WaitAsync(_timeout);

        Assert.Equal("f1", catalog.UltimaActualizacion);
    }
}
