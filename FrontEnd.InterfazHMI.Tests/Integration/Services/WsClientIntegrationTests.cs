using System.Net.WebSockets;
using System.Text;
using System.Threading.Channels;
using Frontend_interfaz_Multimodal_HMI.Models;
using Frontend_interfaz_Multimodal_HMI.Services;
using FrontEnd.InterfazHMI.Tests.Support;

namespace FrontEnd.InterfazHMI.Tests.Integration.Services;

public class WsClientIntegrationTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    private sealed class ClientRun : IAsyncDisposable
    {
        private readonly CancellationTokenSource _cts = new();
        private Task? _run;

        public ClientRun(Uri uri, Action<CatalogUpdatePayload>? before = null, Action<CatalogUpdatePayload>? after = null)
        {
            Client = new WsClient(uri);
            Client.CatalogUpdated += payload =>
            {
                before?.Invoke(payload);
                Updates.Writer.TryWrite(payload);
                after?.Invoke(payload);
            };
        }

        public WsClient Client { get; }

        public Channel<CatalogUpdatePayload> Updates { get; } = Channel.CreateUnbounded<CatalogUpdatePayload>();

        public void Start() => _run = Client.RunAsync(_cts.Token);

        public Task<CatalogUpdatePayload> NextUpdateAsync()
            => Updates.Reader.ReadAsync().AsTask().WaitAsync(Timeout);

        public async Task StopAsync()
        {
            _cts.Cancel();
            if (_run is not null)
                await _run.WaitAsync(Timeout);
        }

        public async ValueTask DisposeAsync()
        {
            await StopAsync();
            _cts.Dispose();
        }
    }

    private static string UpdateJson(string fecha, params string[] nombres)
    {
        var items = string.Join(",", nombres.Select(n =>
            "{\"nombre_item\":\"" + n + "\",\"precio_item\":\"5.50\",\"estado\":true," +
            "\"ingredientes\":null,\"codigo_modbus\":\"0x01\"}"));
        return "{\"id\":\"a1\",\"ts\":\"2026-10-22T10:00:00Z\",\"type\":\"catalog_update\"," +
               "\"payload\":{\"ultima_actualizacion\":\"" + fecha + "\"," +
               "\"catalogo\":{\"items\":[" + items + "],\"total\":" + nombres.Length + "}}}";
    }

    // ---------- recepción ----------

    [Fact]
    public async Task CatalogUpdate_RaisesTheEventWithTheDeserializedPayload()
    {
        await using var server = new FakeWsServer();
        await using var run = new ClientRun(server.Uri);
        run.Start();
        var socket = await server.AcceptAsync();

        await FakeWsServer.SendTextAsync(socket, UpdateJson("f1", "Mojito", "Piña colada"));
        var payload = await run.NextUpdateAsync();

        Assert.Equal("f1", payload.UltimaActualizacion);
        Assert.Equal(2, payload.Catalogo.Total);
        Assert.Equal(new[] { "Mojito", "Piña colada" }, payload.Catalogo.Items.Select(i => i.NombreItem));
        Assert.Equal(5.50m, payload.Catalogo.Items[0].PrecioItem);
    }

    [Fact]
    public async Task EachMessageRaisesTheEvent_InOrder()
    {
        await using var server = new FakeWsServer();
        await using var run = new ClientRun(server.Uri);
        run.Start();
        var socket = await server.AcceptAsync();

        await FakeWsServer.SendTextAsync(socket, UpdateJson("f1", "Mojito"));
        await FakeWsServer.SendTextAsync(socket, UpdateJson("f2", "Daiquiri"));

        Assert.Equal("f1", (await run.NextUpdateAsync()).UltimaActualizacion);
        Assert.Equal("f2", (await run.NextUpdateAsync()).UltimaActualizacion);
    }

    [Fact]
    public async Task MessageSplitInSeveralFrames_IsReassembled()
    {
        await using var server = new FakeWsServer();
        await using var run = new ClientRun(server.Uri);
        run.Start();
        var socket = await server.AcceptAsync();
        var json = UpdateJson("f1", "Piña colada", "Mojito");
        var cut = Encoding.UTF8.GetByteCount(json[..json.IndexOf("\"total\"", StringComparison.Ordinal)]);

        await FakeWsServer.SendFragmentedAsync(socket, json, cut);
        var payload = await run.NextUpdateAsync();

        Assert.Equal(new[] { "Piña colada", "Mojito" }, payload.Catalogo.Items.Select(i => i.NombreItem));
    }

    [Fact]
    public async Task MessageLargerThanTheReceiveBuffer_IsReadCompletely()
    {
        await using var server = new FakeWsServer();
        await using var run = new ClientRun(server.Uri);
        run.Start();
        var socket = await server.AcceptAsync();
        var nombres = Enumerable.Range(1, 400).Select(i => $"Bebida {i}").ToArray();
        var json = UpdateJson("f1", nombres);
        Assert.True(Encoding.UTF8.GetByteCount(json) > 8192);   // más que el buffer del cliente

        await FakeWsServer.SendTextAsync(socket, json);
        var payload = await run.NextUpdateAsync();

        Assert.Equal(400, payload.Catalogo.Items.Count);
        Assert.Equal("Bebida 400", payload.Catalogo.Items[^1].NombreItem);
    }

    // ---------- mensajes que no deben tumbar la conexión ----------

    [Fact]
    public async Task UnknownMessageType_IsIgnored_AndTheConnectionKeepsWorking()
    {
        await using var server = new FakeWsServer();
        await using var run = new ClientRun(server.Uri);
        run.Start();
        var socket = await server.AcceptAsync();

        await FakeWsServer.SendTextAsync(socket,
            "{\"id\":\"1\",\"ts\":\"2026-10-22T10:00:00Z\",\"type\":\"navigate\",\"payload\":{\"screen\":\"menu\"}}");
        await FakeWsServer.SendTextAsync(socket, UpdateJson("f1", "Mojito"));
        var payload = await run.NextUpdateAsync();

        Assert.Equal("f1", payload.UltimaActualizacion);   
    }

    [Fact]
    public async Task MalformedJson_IsIgnored_AndTheConnectionKeepsWorking()
    {
        await using var server = new FakeWsServer();
        await using var run = new ClientRun(server.Uri);
        run.Start();
        var socket = await server.AcceptAsync();

        await FakeWsServer.SendTextAsync(socket, "esto no es json");
        await FakeWsServer.SendTextAsync(socket, UpdateJson("f1", "Mojito"));
        var payload = await run.NextUpdateAsync();

        Assert.Equal("f1", payload.UltimaActualizacion);
    }

    [Theory]
    [InlineData("{\"id\":\"1\",\"ts\":\"2026-10-22T10:00:00Z\",\"type\":\"catalog_update\"}")]  
    [InlineData("{\"id\":\"1\",\"ts\":\"2026-10-22T10:00:00Z\",\"type\":\"catalog_update\",\"payload\":{\"ultima_actualizacion\":{}}}")]
    public async Task CatalogUpdateWithAnInvalidPayload_IsIgnored_AndTheConnectionKeepsWorking(string badMessage)
    {
        await using var server = new FakeWsServer();
        await using var run = new ClientRun(server.Uri);
        run.Start();
        var socket = await server.AcceptAsync();

        await FakeWsServer.SendTextAsync(socket, badMessage);
        await FakeWsServer.SendTextAsync(socket, UpdateJson("f1", "Mojito"));
        var payload = await run.NextUpdateAsync();

        Assert.Equal("f1", payload.UltimaActualizacion);
    }

    [Fact]
    public async Task ASubscriberThatThrows_DoesNotBreakTheConnection()
    {
        await using var server = new FakeWsServer();
        await using var run = new ClientRun(
            server.Uri, after: _ => throw new InvalidOperationException("falla en la UI"));
        run.Start();
        var socket = await server.AcceptAsync();

        await FakeWsServer.SendTextAsync(socket, UpdateJson("f1", "Mojito"));
        await FakeWsServer.SendTextAsync(socket, UpdateJson("f2", "Daiquiri"));

        Assert.Equal("f1", (await run.NextUpdateAsync()).UltimaActualizacion);
        Assert.Equal("f2", (await run.NextUpdateAsync()).UltimaActualizacion);
    }

    // ---------- reconexión ----------

    [Fact]
    public async Task WhenTheServerClosesTheConnection_TheClientReconnectsAndKeepsReceiving()
    {
        await using var server = new FakeWsServer();
        await using var run = new ClientRun(server.Uri);
        run.Start();
        var first = await server.AcceptAsync();

        await first.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "bye", CancellationToken.None);
        var second = await server.AcceptAsync();   
        await FakeWsServer.SendTextAsync(second, UpdateJson("f1", "Mojito"));
        var payload = await run.NextUpdateAsync();

        Assert.Equal("f1", payload.UltimaActualizacion);
        Assert.Equal(2, server.ConnectionCount);
    }

    [Fact]
    public async Task WhenTheServerIsNotAvailableYet_TheClientKeepsRetryingUntilItAppears()
    {
        var port = FakeWsServer.GetFreePort();
        await using var run = new ClientRun(new Uri($"ws://127.0.0.1:{port}/ws"));
        run.Start();
        await Task.Delay(500);   

        await using var server = new FakeWsServer(port);
        var socket = await server.AcceptAsync();
        await FakeWsServer.SendTextAsync(socket, UpdateJson("f1", "Mojito"));
        var payload = await run.NextUpdateAsync();

        Assert.Equal("f1", payload.UltimaActualizacion);
    }

    // ---------- cancelación ----------

    [Fact]
    public async Task Cancelling_WhileConnected_CompletesWithoutThrowing()
    {
        await using var server = new FakeWsServer();
        await using var run = new ClientRun(server.Uri);
        run.Start();
        await server.AcceptAsync();

        var exception = await Record.ExceptionAsync(run.StopAsync);

        Assert.Null(exception);
    }

    [Fact]
    public async Task Cancelling_WhileWaitingToReconnect_CompletesWithoutThrowing()
    {
        var port = FakeWsServer.GetFreePort();
        await using var run = new ClientRun(new Uri($"ws://127.0.0.1:{port}/ws"));
        run.Start();
        await Task.Delay(300);

        var exception = await Record.ExceptionAsync(run.StopAsync);

        Assert.Null(exception);
    }

    [Fact]
    public async Task AlreadyCancelledToken_ReturnsWithoutConnecting()
    {
        await using var server = new FakeWsServer();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await new WsClient(server.Uri).RunAsync(cts.Token).WaitAsync(Timeout);

        Assert.Equal(0, server.ConnectionCount);
    }
}
