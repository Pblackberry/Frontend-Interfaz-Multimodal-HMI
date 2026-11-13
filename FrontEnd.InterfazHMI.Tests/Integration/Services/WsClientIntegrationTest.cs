using System.Threading.Channels;
using FrontEnd.InterfazHMI.Tests.Support;
using Frontend_interfaz_Multimodal_HMI.Models;
using Frontend_interfaz_Multimodal_HMI.Services;

namespace FrontEnd.InterfazHMI.Tests.Integration.Services;

/// <summary>
/// WsClient y el estado del cobot, contra un servidor WebSocket real en localhost:
/// recepción de cobot_status y su aislamiento frente a los demás mensajes.
/// </summary>
public class WsClientCobotIntegrationTests
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(10);

    private const string _ts = "2026-11-06T14:33:53.811394Z";

    private static string CobotJson(
        string id = "c1", string codigo = "12", string tipo = "alarma", string descripcion = "Paro de emergencia")
        => "{\"id\":\"" + id + "\",\"ts\":\"" + _ts + "\",\"type\":\"cobot_status\",\"payload\":{"
            + "\"codigo\":" + codigo + ",\"tipo_evento\":\"" + tipo + "\",\"descripcion\":\"" + descripcion + "\"}}";

    private static string ResultJson(string id, string status)
        => "{\"id\":\"" + id + "\",\"ts\":\"" + _ts + "\",\"type\":\"payment_result\",\"payload\":{"
            + "\"payment_id\":\"tok-1\",\"status\":\"" + status + "\",\"reason\":null}}";

    private static string NavigateJson(string id)
        => "{\"id\":\"" + id + "\",\"ts\":\"" + _ts + "\",\"type\":\"navigate\",\"payload\":{\"screen\":\"menu\"}}";

    private sealed class Listener
    {
        public Channel<CobotStatusPayload> Statuses { get; } = Channel.CreateUnbounded<CobotStatusPayload>();

        public Channel<string> Others { get; } = Channel.CreateUnbounded<string>();

        public void Attach(WsClient client)
        {
            client.CobotStatusReceived += payload => Statuses.Writer.TryWrite(payload);
            client.PaymentStarted += payload => Others.Writer.TryWrite($"started:{payload.PaymentId}");
            client.PaymentResultReceived += (id, payload) => Others.Writer.TryWrite($"result:{id}:{payload.Status}");
        }

        public Task<CobotStatusPayload> NextStatusAsync() => Statuses.Reader.ReadAsync().AsTask().WaitAsync(_timeout);

        public Task<string> NextOtherAsync() => Others.Reader.ReadAsync().AsTask().WaitAsync(_timeout);
    }

    // ---------- recepción ----------

    [Fact]
    public async Task CobotStatusMessage_RaisesTheEventWithTheFullPayload()
    {
        await using var server = new FakeWsServer();
        var listener = new Listener();
        await using var session = await WsClientSession.StartAsync(server, listener.Attach);

        await FakeWsServer.SendTextAsync(session.ServerSocket, CobotJson());
        var payload = await listener.NextStatusAsync();

        Assert.Equal(new CobotStatusPayload(12, "alarma", "Paro de emergencia"), payload);
    }

    [Theory]
    [InlineData("12")]
    [InlineData("\"12\"")]
    public async Task CobotStatusMessage_AcceptsTheCodeAsNumberOrText(string codigo)
    {
        await using var server = new FakeWsServer();
        var listener = new Listener();
        await using var session = await WsClientSession.StartAsync(server, listener.Attach);

        await FakeWsServer.SendTextAsync(session.ServerSocket, CobotJson(codigo: codigo));

        Assert.Equal(12, (await listener.NextStatusAsync()).Codigo);
    }

    [Fact]
    public async Task CobotStatusMessage_KeepsAccentsInTheDescription()
    {
        await using var server = new FakeWsServer();
        var listener = new Listener();
        await using var session = await WsClientSession.StartAsync(server, listener.Attach);

        await FakeWsServer.SendTextAsync(session.ServerSocket, CobotJson(descripcion: "Posición fuera de rango"));

        Assert.Equal("Posición fuera de rango", (await listener.NextStatusAsync()).Descripcion);
    }

    [Fact]
    public async Task EachCobotStatusMessage_RaisesTheEvent_InOrder()
    {
        await using var server = new FakeWsServer();
        var listener = new Listener();
        await using var session = await WsClientSession.StartAsync(server, listener.Attach);

        await FakeWsServer.SendTextAsync(session.ServerSocket, CobotJson(codigo: "1", tipo: "info"));
        await FakeWsServer.SendTextAsync(session.ServerSocket, CobotJson(codigo: "2", tipo: "advertencia"));
        await FakeWsServer.SendTextAsync(session.ServerSocket, CobotJson(codigo: "3", tipo: "alarma"));

        Assert.Equal(1, (await listener.NextStatusAsync()).Codigo);
        Assert.Equal(2, (await listener.NextStatusAsync()).Codigo);
        Assert.Equal(3, (await listener.NextStatusAsync()).Codigo);
    }

    [Fact]
    public async Task CobotStatusMessage_SplitInSeveralFrames_ArrivesWhole()
    {
        await using var server = new FakeWsServer();
        var listener = new Listener();
        await using var session = await WsClientSession.StartAsync(server, listener.Attach);

        await FakeWsServer.SendFragmentedAsync(session.ServerSocket, CobotJson(codigo: "44"), 30);

        Assert.Equal(44, (await listener.NextStatusAsync()).Codigo);
    }

    [Fact]
    public async Task CobotStatusMessage_DoesNotRaiseThePaymentEvents()
    {
        await using var server = new FakeWsServer();
        var listener = new Listener();
        await using var session = await WsClientSession.StartAsync(server, listener.Attach);

        await FakeWsServer.SendTextAsync(session.ServerSocket, CobotJson());
        await FakeWsServer.SendTextAsync(session.ServerSocket, ResultJson("r1", "approved"));

        Assert.Equal("result:r1:Approved", await listener.NextOtherAsync());
        Assert.Equal(12, (await listener.NextStatusAsync()).Codigo);
        Assert.False(listener.Others.Reader.TryRead(out _));
    }

    [Fact]
    public async Task PaymentMessages_DoNotRaiseTheCobotEvent()
    {
        await using var server = new FakeWsServer();
        var listener = new Listener();
        await using var session = await WsClientSession.StartAsync(server, listener.Attach);

        await FakeWsServer.SendTextAsync(session.ServerSocket, ResultJson("r1", "rejected"));
        await FakeWsServer.SendTextAsync(session.ServerSocket, CobotJson(codigo: "5"));

        Assert.Equal(5, (await listener.NextStatusAsync()).Codigo);
        Assert.False(listener.Statuses.Reader.TryRead(out _));
    }

    // ---------- mensajes que no deben tumbar la conexión ----------

    [Theory]
    [InlineData("{\"id\":\"x\",\"ts\":\"2026-11-06T14:33:53Z\",\"type\":\"cobot_status\"}")]
    [InlineData("{\"id\":\"x\",\"ts\":\"2026-11-06T14:33:53Z\",\"type\":\"cobot_status\",\"payload\":\"alarma\"}")]
    [InlineData("{\"id\":\"x\",\"ts\":\"2026-11-06T14:33:53Z\",\"type\":\"cobot_status\",\"payload\":{\"codigo\":\"abc\",\"tipo_evento\":\"info\",\"descripcion\":\"x\"}}")]
    [InlineData("{\"id\":\"x\",\"ts\":\"2026-11-06T14:33:53Z\",\"type\":\"cobot_status\",\"payload\":{\"codigo\":1.5,\"tipo_evento\":\"info\",\"descripcion\":\"x\"}}")]
    public async Task InvalidCobotStatusMessage_IsIgnored_AndTheConnectionKeepsWorking(string badMessage)
    {
        await using var server = new FakeWsServer();
        var listener = new Listener();
        await using var session = await WsClientSession.StartAsync(server, listener.Attach);

        await FakeWsServer.SendTextAsync(session.ServerSocket, badMessage);
        await FakeWsServer.SendTextAsync(session.ServerSocket, CobotJson(codigo: "8"));

        Assert.Equal(8, (await listener.NextStatusAsync()).Codigo);
        Assert.True(session.Client.IsConnected);
    }

    [Fact]
    public async Task CobotStatusMessage_WithoutSubscribers_DoesNotBreakTheConnection()
    {
        await using var server = new FakeWsServer();
        var navigated = Channel.CreateUnbounded<string>();
        await using var session = await WsClientSession.StartAsync(
            server, client => client.NavigateRequested += (id, _) => navigated.Writer.TryWrite("navigate:" + id));

        await FakeWsServer.SendTextAsync(session.ServerSocket, CobotJson());
        await FakeWsServer.SendTextAsync(session.ServerSocket, NavigateJson("n"));

        Assert.Equal("navigate:n", await navigated.Reader.ReadAsync().AsTask().WaitAsync(_timeout));
    }

    [Fact]
    public async Task ASubscriberOfCobotStatusThatThrows_DoesNotBreakTheConnection()
    {
        await using var server = new FakeWsServer();
        var listener = new Listener();
        await using var session = await WsClientSession.StartAsync(server, client =>
        {
            client.CobotStatusReceived += _ => throw new InvalidOperationException("suscriptor defectuoso");
            listener.Attach(client);
        });

        await FakeWsServer.SendTextAsync(session.ServerSocket, CobotJson(codigo: "1"));
        await FakeWsServer.SendTextAsync(session.ServerSocket, ResultJson("r1", "approved"));

        Assert.Equal("result:r1:Approved", await listener.NextOtherAsync());
        Assert.True(session.Client.IsConnected);
    }

    [Fact]
    public async Task EverySubscriber_ReceivesTheCobotStatus()
    {
        await using var server = new FakeWsServer();
        var first = new Listener();
        var second = new Listener();
        await using var session = await WsClientSession.StartAsync(server, client =>
        {
            first.Attach(client);
            second.Attach(client);
        });

        await FakeWsServer.SendTextAsync(session.ServerSocket, CobotJson(codigo: "21"));

        Assert.Equal(21, (await first.NextStatusAsync()).Codigo);
        Assert.Equal(21, (await second.NextStatusAsync()).Codigo);
    }
}
