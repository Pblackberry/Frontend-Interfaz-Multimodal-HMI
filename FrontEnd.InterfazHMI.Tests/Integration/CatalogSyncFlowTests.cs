using System.Threading.Channels;
using CommunityToolkit.Mvvm.Messaging;
using FrontEnd.InterfazHMI.Tests.Support;
using Frontend_interfaz_Multimodal_HMI.Services;
using Frontend_interfaz_Multimodal_HMI.ViewModels;

namespace FrontEnd.InterfazHMI.Tests.Integration;

/// <summary>
/// Flujo completo del frontend: servidor WebSocket -> WsClient -> CatalogStore -> mensaje -> MenuViewModel.
/// Replica la conexión que hace la app (WsClient.CatalogUpdated += store.Update).
/// </summary>
[Collection(MessengerCollection.Name)]
public class CatalogSyncFlowTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    private const string UpdateMessage =
        "{\"id\":\"a1\",\"ts\":\"2026-10-22T10:00:00Z\",\"type\":\"catalog_update\",\"payload\":{" +
        "\"ultima_actualizacion\":\"f1\",\"catalogo\":{\"items\":[" +
        "{\"nombre_item\":\"Mojito\",\"precio_item\":\"5.50\",\"estado\":true,\"ingredientes\":null,\"codigo_modbus\":\"0x01\"}," +
        "{\"nombre_item\":\"Daiquiri\",\"precio_item\":\"6.00\",\"estado\":true,\"ingredientes\":null,\"codigo_modbus\":\"0x02\"}" +
        "],\"total\":2}}}";

    /// <summary>Conecta WsClient y CatalogStore como en la app y espera a que el store procese la actualización.</summary>
    private static async Task<CatalogStore> SyncOnceAsync(FakeWsServer server)
    {
        var store = new CatalogStore();
        var processed = Channel.CreateUnbounded<bool>();
        var client = new WsClient(server.Uri);
        client.CatalogUpdated += payload =>
        {
            store.Update(payload);
            processed.Writer.TryWrite(true);
        };

        using var cts = new CancellationTokenSource();
        var run = client.RunAsync(cts.Token);
        try
        {
            var socket = await server.AcceptAsync();
            await FakeWsServer.SendTextAsync(socket, UpdateMessage);
            await processed.Reader.ReadAsync().AsTask().WaitAsync(Timeout);
        }
        finally
        {
            cts.Cancel();
            await run.WaitAsync(Timeout);
        }

        return store;
    }

    [Fact]
    public async Task ServerUpdate_ReachesTheCatalogStore()
    {
        await using var server = new FakeWsServer();

        var store = await SyncOnceAsync(server);

        Assert.Equal("f1", store.Current!.UltimaActualizacion);
        Assert.Equal(new[] { "Mojito", "Daiquiri" }, store.Items.Select(i => i.NombreItem));
    }

    [Fact]
    public async Task ServerUpdate_PublishesACatalogChangedMessage()
    {
        await using var server = new FakeWsServer();
        var received = new List<CatalogChangedMessage>();
        var spy = new object();
        WeakReferenceMessenger.Default.Register<object, CatalogChangedMessage>(
            spy, (_, message) => { lock (received) { received.Add(message); } });

        try
        {
            var store = await SyncOnceAsync(server);

            lock (received)
            {
                Assert.Contains(received, m => ReferenceEquals(m.Snapshot, store.Current));
            }
        }
        finally
        {
            WeakReferenceMessenger.Default.UnregisterAll(spy);
        }
    }

    [Fact]
    public async Task MenuOpenedAfterTheUpdate_ShowsTheServerCatalog()
    {
        await using var server = new FakeWsServer();
        var store = await SyncOnceAsync(server);

        var menu = new MenuViewModel(store);
        try
        {
            Assert.Equal(new[] { "Mojito", "Daiquiri" }, menu.Items.Select(c => c.Name));
            Assert.Equal(new[] { "$5.50", "$6.00" }, menu.Items.Select(c => c.PriceText));
        }
        finally
        {
            menu.IsActive = false;
        }
    }
}
