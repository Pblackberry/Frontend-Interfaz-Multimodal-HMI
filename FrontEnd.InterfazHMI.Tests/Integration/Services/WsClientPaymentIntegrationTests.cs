using System.Text.Json;
using System.Threading.Channels;
using FrontEnd.InterfazHMI.Tests.Support;
using Frontend_interfaz_Multimodal_HMI.Models;
using Frontend_interfaz_Multimodal_HMI.Services;

namespace FrontEnd.InterfazHMI.Tests.Integration.Services;

/// <summary>
/// WsClient y el pago por QR, contra un servidor WebSocket real en localhost:
/// recepción de payment_started / payment_result y envío de start_payment / cancel_payment
/// a través de IWsSender.
/// </summary>
public class WsClientPaymentIntegrationTests
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(10);

    private const string _ts = "2026-11-06T14:33:53.811394Z";

    private static string StartedJson(string id = "e1", string paymentId = "tok-1", string price = "\"5.50\"")
        => "{\"id\":\"" + id + "\",\"ts\":\"" + _ts + "\",\"type\":\"payment_started\",\"payload\":{"
            + "\"payment_id\":\"" + paymentId + "\",\"pay_url\":\"http://192.168.0.10:8888/pay/" + paymentId + "\","
            + "\"drink_name\":\"Mojito\",\"price\":" + price + "}}";

    private static string ResultJson(string id, string status, string paymentId = "\"tok-1\"", string reason = "null")
        => "{\"id\":\"" + id + "\",\"ts\":\"" + _ts + "\",\"type\":\"payment_result\",\"payload\":{"
            + "\"payment_id\":" + paymentId + ",\"status\":\"" + status + "\",\"reason\":" + reason + "}}";

    private static string NavigateJson(string id)
        => "{\"id\":\"" + id + "\",\"ts\":\"" + _ts + "\",\"type\":\"navigate\",\"payload\":{\"screen\":\"payment_result\"}}";

    private sealed class Listener
    {
        public Channel<string> Events { get; } = Channel.CreateUnbounded<string>();

        public List<PaymentStartedPayload> Started { get; } = new();

        public List<(string Id, PaymentResultPayload Payload)> Results { get; } = new();

        public void Attach(WsClient client)
        {
            client.PaymentStarted += payload =>
            {
                Started.Add(payload);
                Events.Writer.TryWrite($"started:{payload.PaymentId}");
            };
            client.PaymentResultReceived += (id, payload) =>
            {
                Results.Add((id, payload));
                Events.Writer.TryWrite($"result:{id}:{payload.Status}");
            };
            client.NavigateRequested += (id, payload) => Events.Writer.TryWrite($"navigate:{id}:{payload.Screen}");
        }

        public Task<string> NextAsync() => Events.Reader.ReadAsync().AsTask().WaitAsync(_timeout);
    }

    // ---------- recepción ----------

    [Fact]
    public async Task PaymentStartedMessage_RaisesTheEventWithTheFullPayload()
    {
        await using var server = new FakeWsServer();
        var listener = new Listener();
        await using var session = await WsClientSession.StartAsync(server, listener.Attach);

        await FakeWsServer.SendTextAsync(session.ServerSocket, StartedJson(paymentId: "tok-9"));

        Assert.Equal("started:tok-9", await listener.NextAsync());
        Assert.Equal(
            new PaymentStartedPayload("tok-9", "http://192.168.0.10:8888/pay/tok-9", "Mojito", 5.5m),
            Assert.Single(listener.Started));
    }

    [Theory]
    [InlineData("\"5.50\"")]
    [InlineData("5.5")]
    public async Task PaymentStartedMessage_AcceptsThePriceAsTextOrNumber(string price)
    {
        await using var server = new FakeWsServer();
        var listener = new Listener();
        await using var session = await WsClientSession.StartAsync(server, listener.Attach);

        await FakeWsServer.SendTextAsync(session.ServerSocket, StartedJson(price: price));
        await listener.NextAsync();

        Assert.Equal(5.5m, Assert.Single(listener.Started).Price);
    }

    [Theory]
    [InlineData("approved", PaymentOutcome.Approved)]
    [InlineData("rejected", PaymentOutcome.Rejected)]
    [InlineData("expired", PaymentOutcome.Expired)]
    [InlineData("error", PaymentOutcome.Error)]
    public async Task PaymentResultMessage_RaisesTheEventWithTheEnvelopeIdAndTheOutcome(string status, PaymentOutcome expected)
    {
        await using var server = new FakeWsServer();
        var listener = new Listener();
        await using var session = await WsClientSession.StartAsync(server, listener.Attach);

        await FakeWsServer.SendTextAsync(session.ServerSocket, ResultJson("env-7", status));

        Assert.Equal($"result:env-7:{expected}", await listener.NextAsync());
        Assert.Equal("tok-1", Assert.Single(listener.Results).Payload.PaymentId);
    }

    [Fact]
    public async Task PaymentResultMessage_WithoutPaymentIdButWithReason_ArrivesIntact()
    {
        await using var server = new FakeWsServer();
        var listener = new Listener();
        await using var session = await WsClientSession.StartAsync(server, listener.Attach);

        await FakeWsServer.SendTextAsync(
            session.ServerSocket, ResultJson("env-1", "error", paymentId: "null", reason: "\"drink_unavailable\""));
        await listener.NextAsync();

        Assert.Equal(
            new PaymentResultPayload(null, PaymentOutcome.Error, "drink_unavailable"),
            Assert.Single(listener.Results).Payload);
    }

    [Fact]
    public async Task PaymentMessages_ArriveInOrderAndEachReachesOnlyItsOwnEvent()
    {
        await using var server = new FakeWsServer();
        var listener = new Listener();
        await using var session = await WsClientSession.StartAsync(server, listener.Attach);

        await FakeWsServer.SendTextAsync(session.ServerSocket, StartedJson(paymentId: "tok-1"));
        await FakeWsServer.SendTextAsync(session.ServerSocket, ResultJson("r1", "approved"));
        await FakeWsServer.SendTextAsync(session.ServerSocket, NavigateJson("n1"));

        Assert.Equal("started:tok-1", await listener.NextAsync());
        Assert.Equal("result:r1:Approved", await listener.NextAsync());
        Assert.Equal("navigate:n1:PaymentResult", await listener.NextAsync());
        Assert.False(listener.Events.Reader.TryRead(out _));
    }

    [Theory]
    [InlineData("{\"id\":\"x\",\"ts\":\"2026-11-06T14:33:53Z\",\"type\":\"payment_started\"}")]
    [InlineData("{\"id\":\"x\",\"ts\":\"2026-11-06T14:33:53Z\",\"type\":\"payment_started\",\"payload\":{\"payment_id\":\"a\",\"pay_url\":\"u\",\"drink_name\":\"n\",\"price\":\"gratis\"}}")]
    [InlineData("{\"id\":\"x\",\"ts\":\"2026-11-06T14:33:53Z\",\"type\":\"payment_result\"}")]
    [InlineData("{\"id\":\"x\",\"ts\":\"2026-11-06T14:33:53Z\",\"type\":\"payment_result\",\"payload\":{\"payment_id\":\"a\",\"status\":\"cancelled\"}}")]
    [InlineData("{\"id\":\"x\",\"ts\":\"2026-11-06T14:33:53Z\",\"type\":\"payment_result\",\"payload\":\"approved\"}")]
    public async Task InvalidPaymentMessage_IsIgnored_AndTheConnectionKeepsWorking(string badMessage)
    {
        await using var server = new FakeWsServer();
        var listener = new Listener();
        await using var session = await WsClientSession.StartAsync(server, listener.Attach);

        await FakeWsServer.SendTextAsync(session.ServerSocket, badMessage);
        await FakeWsServer.SendTextAsync(session.ServerSocket, ResultJson("valido", "approved"));

        Assert.Equal("result:valido:Approved", await listener.NextAsync());
    }

    [Fact]
    public async Task PaymentMessages_WithoutSubscribers_DoNotBreakTheConnection()
    {
        await using var server = new FakeWsServer();
        var listener = new Listener();
        await using var session = await WsClientSession.StartAsync(
            server, client => client.NavigateRequested += (id, _) => listener.Events.Writer.TryWrite("navigate:" + id));

        await FakeWsServer.SendTextAsync(session.ServerSocket, StartedJson());
        await FakeWsServer.SendTextAsync(session.ServerSocket, ResultJson("r", "approved"));
        await FakeWsServer.SendTextAsync(session.ServerSocket, NavigateJson("n"));

        Assert.Equal("navigate:n", await listener.NextAsync());
    }

    [Fact]
    public async Task PaymentStartedMessage_SplitInSeveralFrames_ArrivesWhole()
    {
        await using var server = new FakeWsServer();
        var listener = new Listener();
        await using var session = await WsClientSession.StartAsync(server, listener.Attach);

        await FakeWsServer.SendFragmentedAsync(session.ServerSocket, StartedJson(paymentId: "tok-frag"), 30);

        Assert.Equal("started:tok-frag", await listener.NextAsync());
        Assert.Equal("Mojito", Assert.Single(listener.Started).DrinkName);
    }

    [Fact]
    public async Task APaymentTypeTheClientDoesNotKnow_IsIgnored()
    {
        await using var server = new FakeWsServer();
        var listener = new Listener();
        await using var session = await WsClientSession.StartAsync(server, listener.Attach);

        await FakeWsServer.SendTextAsync(
            session.ServerSocket,
            "{\"id\":\"x\",\"ts\":\"" + _ts + "\",\"type\":\"payment_cancelled\",\"payload\":{}}");
        await FakeWsServer.SendTextAsync(session.ServerSocket, ResultJson("valido", "expired"));

        Assert.Equal("result:valido:Expired", await listener.NextAsync());
    }

    [Fact]
    public async Task ASubscriberOfPaymentStartedThatThrows_DoesNotBreakTheConnection()
    {
        await using var server = new FakeWsServer();
        var listener = new Listener();
        await using var session = await WsClientSession.StartAsync(server, client =>
        {
            client.PaymentStarted += _ => throw new InvalidOperationException("suscriptor defectuoso");
            listener.Attach(client);
        });

        await FakeWsServer.SendTextAsync(session.ServerSocket, StartedJson());
        await FakeWsServer.SendTextAsync(session.ServerSocket, ResultJson("r1", "approved"));

        Assert.Equal("result:r1:Approved", await listener.NextAsync());
        Assert.True(session.Client.IsConnected);
    }

    [Fact]
    public async Task ASubscriberOfPaymentResultThatThrows_DoesNotBreakTheConnection()
    {
        await using var server = new FakeWsServer();
        var listener = new Listener();
        await using var session = await WsClientSession.StartAsync(server, client =>
        {
            client.PaymentResultReceived += (_, _) => throw new InvalidOperationException("suscriptor defectuoso");
            listener.Attach(client);
        });

        await FakeWsServer.SendTextAsync(session.ServerSocket, ResultJson("r1", "approved"));
        await FakeWsServer.SendTextAsync(session.ServerSocket, StartedJson(paymentId: "tok-2"));

        Assert.Equal("started:tok-2", await listener.NextAsync());
        Assert.True(session.Client.IsConnected);
    }

    [Fact]
    public async Task EverySubscriber_ReceivesThePaymentResult()
    {
        await using var server = new FakeWsServer();
        var first = new Listener();
        var second = new Listener();
        await using var session = await WsClientSession.StartAsync(server, client =>
        {
            first.Attach(client);
            second.Attach(client);
        });

        await FakeWsServer.SendTextAsync(session.ServerSocket, ResultJson("r1", "rejected"));

        Assert.Equal("result:r1:Rejected", await first.NextAsync());
        Assert.Equal("result:r1:Rejected", await second.NextAsync());
    }

    // ---------- envío ----------

    [Fact]
    public void WsClient_CanBeUsedAsTheSenderOfThePaymentViewModel()
    {
        IWsSender sender = new WsClient(new Uri("ws://127.0.0.1:1/ws"));

        Assert.NotNull(sender);
    }

    [Fact]
    public async Task StartPayment_IsSentWithTheDrinkName()
    {
        await using var server = new FakeWsServer();
        await using var session = await WsClientSession.StartAsync(server);
        IWsSender sender = session.Client;

        var sent = await sender.SendAsync(WsTypes.StartPayment, new StartPaymentPayload("Piña colada"));

        Assert.True(sent);
        using var doc = JsonDocument.Parse(await FakeWsServer.ReceiveTextAsync(session.ServerSocket));
        Assert.Equal("start_payment", doc.RootElement.GetProperty("type").GetString());
        Assert.Equal("Piña colada", doc.RootElement.GetProperty("payload").GetProperty("drink").GetString());
        Assert.False(string.IsNullOrEmpty(doc.RootElement.GetProperty("id").GetString()));
    }

    [Theory]
    [InlineData("tok-1")]
    [InlineData(null)]
    public async Task CancelPayment_IsSentWithThePaymentIdOrNull(string? paymentId)
    {
        await using var server = new FakeWsServer();
        await using var session = await WsClientSession.StartAsync(server);
        IWsSender sender = session.Client;

        var sent = await sender.SendAsync(WsTypes.CancelPayment, new CancelPaymentPayload(paymentId));

        Assert.True(sent);
        using var doc = JsonDocument.Parse(await FakeWsServer.ReceiveTextAsync(session.ServerSocket));
        Assert.Equal("cancel_payment", doc.RootElement.GetProperty("type").GetString());
        var id = doc.RootElement.GetProperty("payload").GetProperty("payment_id");
        Assert.Equal(paymentId, id.ValueKind == JsonValueKind.Null ? null : id.GetString());
    }

    [Fact]
    public async Task StartPayment_WhenNotConnected_ReturnsFalse()
    {
        IWsSender sender = new WsClient(new Uri("ws://127.0.0.1:1/ws"));

        var sent = await sender.SendAsync(WsTypes.StartPayment, new StartPaymentPayload("Mojito"));

        Assert.False(sent);
    }
}
