using System.Threading.Channels;
using FrontEnd.InterfazHMI.Tests.Support;
using Frontend_interfaz_Multimodal_HMI.Models;
using Frontend_interfaz_Multimodal_HMI.Services;

namespace FrontEnd.InterfazHMI.Tests.Integration.Services;

public class WsClientVoiceIntegrationTests
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(10);

    private const string _ts = "2026-11-04T14:33:53.811394Z";

    private static string DrinkJson(string type, string id, string name)
        => "{\"id\":\"" + id + "\",\"ts\":\"" + _ts + "\",\"type\":\"" + type + "\",\"payload\":{\"name\":\"" + name + "\"}}";

    private static string CloseJson(string id, string payload = ",\"payload\":{}")
        => "{\"id\":\"" + id + "\",\"ts\":\"" + _ts + "\",\"type\":\"close_ingredients\"" + payload + "}";

    private static string NavigateJson(string id)
        => "{\"id\":\"" + id + "\",\"ts\":\"" + _ts + "\",\"type\":\"navigate\",\"payload\":{\"screen\":\"menu\"}}";

    private sealed class Listener
    {
        public Channel<string> Events { get; } = Channel.CreateUnbounded<string>();

        public void Attach(WsClient client)
        {
            client.SelectDrinkRequested += (id, payload) => Events.Writer.TryWrite($"select:{id}:{payload.Name}");
            client.ShowIngredientsRequested += payload => Events.Writer.TryWrite($"show:{payload.Name}");
            client.CloseIngredientsRequested += () => Events.Writer.TryWrite("close");
            client.NavigateRequested += (id, _) => Events.Writer.TryWrite($"navigate:{id}");
        }

        public Task<string> NextAsync() => Events.Reader.ReadAsync().AsTask().WaitAsync(_timeout);
    }


    [Fact]
    public async Task SelectDrinkMessage_RaisesTheEventWithTheMessageIdAndTheDrinkName()
    {
        await using var server = new FakeWsServer();
        var listener = new Listener();
        await using var session = await WsClientSession.StartAsync(server, listener.Attach);

        await FakeWsServer.SendTextAsync(session.ServerSocket, DrinkJson("select_drink", "voz-1", "Mojito"));

        Assert.Equal("select:voz-1:Mojito", await listener.NextAsync());
    }

    [Fact]
    public async Task ShowIngredientsMessage_RaisesTheEventWithTheDrinkName()
    {
        await using var server = new FakeWsServer();
        var listener = new Listener();
        await using var session = await WsClientSession.StartAsync(server, listener.Attach);

        await FakeWsServer.SendTextAsync(session.ServerSocket, DrinkJson("show_ingredients", "x", "Daiquiri"));

        Assert.Equal("show:Daiquiri", await listener.NextAsync());
    }

    [Theory]
    [InlineData(",\"payload\":{}")]
    [InlineData("")]
    public async Task CloseIngredientsMessage_RaisesTheEvent(string payload)
    {
        await using var server = new FakeWsServer();
        var listener = new Listener();
        await using var session = await WsClientSession.StartAsync(server, listener.Attach);

        await FakeWsServer.SendTextAsync(session.ServerSocket, CloseJson("x", payload));

        Assert.Equal("close", await listener.NextAsync());
    }

    [Fact]
    public async Task DrinkNamesWithAccentsAndSpaces_ArriveIntact()
    {
        await using var server = new FakeWsServer();
        var listener = new Listener();
        await using var session = await WsClientSession.StartAsync(server, listener.Attach);

        await FakeWsServer.SendTextAsync(session.ServerSocket, DrinkJson("select_drink", "v", "Piña colada"));
        await FakeWsServer.SendTextAsync(session.ServerSocket, DrinkJson("show_ingredients", "v", "Old Fashioned"));

        Assert.Equal("select:v:Piña colada", await listener.NextAsync());
        Assert.Equal("show:Old Fashioned", await listener.NextAsync());
    }


    [Fact]
    public async Task MixedMessages_ArriveInOrderAndEachReachesOnlyItsOwnEvent()
    {
        await using var server = new FakeWsServer();
        var listener = new Listener();
        await using var session = await WsClientSession.StartAsync(server, listener.Attach);

        await FakeWsServer.SendTextAsync(session.ServerSocket, NavigateJson("n1"));
        await FakeWsServer.SendTextAsync(session.ServerSocket, DrinkJson("show_ingredients", "x", "Mojito"));
        await FakeWsServer.SendTextAsync(session.ServerSocket, CloseJson("x"));
        await FakeWsServer.SendTextAsync(session.ServerSocket, DrinkJson("select_drink", "s1", "Mojito"));

        Assert.Equal("navigate:n1", await listener.NextAsync());
        Assert.Equal("show:Mojito", await listener.NextAsync());
        Assert.Equal("close", await listener.NextAsync());
        Assert.Equal("select:s1:Mojito", await listener.NextAsync());
        Assert.False(listener.Events.Reader.TryRead(out _));
    }


    [Theory]
    [InlineData("{\"id\":\"x\",\"ts\":\"2026-11-04T14:33:53Z\",\"type\":\"select_drink\"}")]
    [InlineData("{\"id\":\"x\",\"ts\":\"2026-11-04T14:33:53Z\",\"type\":\"select_drink\",\"payload\":\"Mojito\"}")]
    [InlineData("{\"id\":\"x\",\"ts\":\"2026-11-04T14:33:53Z\",\"type\":\"show_ingredients\"}")]
    [InlineData("{\"id\":\"x\",\"ts\":\"2026-11-04T14:33:53Z\",\"type\":\"show_ingredients\",\"payload\":[1]}")]
    public async Task InvalidVoiceMessage_IsIgnored_AndTheConnectionKeepsWorking(string badMessage)
    {
        await using var server = new FakeWsServer();
        var listener = new Listener();
        await using var session = await WsClientSession.StartAsync(server, listener.Attach);

        await FakeWsServer.SendTextAsync(session.ServerSocket, badMessage);
        await FakeWsServer.SendTextAsync(session.ServerSocket, DrinkJson("select_drink", "valido", "Mojito"));

        Assert.Equal("select:valido:Mojito", await listener.NextAsync());
    }

    [Fact]
    public async Task ASubscriberThatThrows_DoesNotBreakTheConnection()
    {
        await using var server = new FakeWsServer();
        var listener = new Listener();
        await using var session = await WsClientSession.StartAsync(server, client =>
        {
            listener.Attach(client);
            client.SelectDrinkRequested += (_, _) => throw new InvalidOperationException("falla en la UI");
            client.CloseIngredientsRequested += () => throw new InvalidOperationException("falla en la UI");
        });

        await FakeWsServer.SendTextAsync(session.ServerSocket, DrinkJson("select_drink", "s1", "Mojito"));
        await FakeWsServer.SendTextAsync(session.ServerSocket, CloseJson("x"));
        await FakeWsServer.SendTextAsync(session.ServerSocket, DrinkJson("show_ingredients", "x", "Mojito"));

        Assert.Equal("select:s1:Mojito", await listener.NextAsync());
        Assert.Equal("close", await listener.NextAsync());
        Assert.Equal("show:Mojito", await listener.NextAsync());
    }

    [Fact]
    public async Task VoiceMessagesWithoutSubscribers_AreHarmless()
    {
        await using var server = new FakeWsServer();
        var navigations = Channel.CreateUnbounded<string>();
        await using var session = await WsClientSession.StartAsync(
            server, client => client.NavigateRequested += (id, _) => navigations.Writer.TryWrite(id));

        await FakeWsServer.SendTextAsync(session.ServerSocket, DrinkJson("select_drink", "x", "Mojito"));
        await FakeWsServer.SendTextAsync(session.ServerSocket, DrinkJson("show_ingredients", "x", "Mojito"));
        await FakeWsServer.SendTextAsync(session.ServerSocket, CloseJson("x"));
        await FakeWsServer.SendTextAsync(session.ServerSocket, NavigateJson("n1"));

        Assert.Equal("n1", await navigations.Reader.ReadAsync().AsTask().WaitAsync(_timeout));
    }
}
