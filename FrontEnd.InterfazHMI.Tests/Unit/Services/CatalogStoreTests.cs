using CommunityToolkit.Mvvm.Messaging;
using FrontEnd.InterfazHMI.Tests.Support;
using Frontend_interfaz_Multimodal_HMI.Models;
using Frontend_interfaz_Multimodal_HMI.Services;

namespace FrontEnd.InterfazHMI.Tests.Unit.Services;

[Collection(MessengerCollection.Name)]
public class CatalogStoreTests : IDisposable
{
    private readonly CatalogStore _sut = new();
    private readonly List<CatalogChangedMessage> _received = new();

    public CatalogStoreTests()
    {
        WeakReferenceMessenger.Default.Register<CatalogStoreTests, CatalogChangedMessage>(
            this, static (recipient, message) => recipient._received.Add(message));
    }

    public void Dispose() => WeakReferenceMessenger.Default.UnregisterAll(this);

    [Fact]
    public void NewStore_HasNoSnapshotAndNoItems()
    {
        Assert.Null(_sut.Current);
        Assert.Empty(_sut.Items);
    }

    [Fact]
    public void Update_StoresTheSnapshot()
    {
        var payload = TestData.Payload("f1", TestData.Item("Mojito"));

        _sut.Update(payload);

        Assert.Same(payload, _sut.Current);
    }

    [Fact]
    public void Update_ExposesTheItemsOfTheSnapshot()
    {
        _sut.Update(TestData.Payload("f1", TestData.Item("Mojito"), TestData.Item("Daiquiri")));

        Assert.Equal(new[] { "Mojito", "Daiquiri" }, _sut.Items.Select(i => i.NombreItem));
    }

    [Fact]
    public void Update_ReplacesThePreviousSnapshot()
    {
        _sut.Update(TestData.Payload("f1", TestData.Item("Mojito")));
        var second = TestData.Payload("f2", TestData.Item("Daiquiri"), TestData.Item("Margarita"));

        _sut.Update(second);

        Assert.Same(second, _sut.Current);
        Assert.Equal(new[] { "Daiquiri", "Margarita" }, _sut.Items.Select(i => i.NombreItem));
    }

    [Fact]
    public void Update_WithEmptyCatalog_ExposesNoItems()
    {
        _sut.Update(TestData.Payload("f1", TestData.Item("Mojito")));

        _sut.Update(TestData.Payload("f2"));

        Assert.NotNull(_sut.Current);
        Assert.Empty(_sut.Items);
    }

    [Fact]
    public void Update_SendsACatalogChangedMessageWithTheSnapshot()
    {
        var payload = TestData.Payload("f1", TestData.Item("Mojito"));

        _sut.Update(payload);

        var matching = _received.Where(m => ReferenceEquals(m.Snapshot, payload)).ToList();
        Assert.Single(matching);
    }

    [Fact]
    public void Update_SendsOneMessagePerUpdate()
    {
        var first = TestData.Payload("f1", TestData.Item("Mojito"));
        var second = TestData.Payload("f2", TestData.Item("Daiquiri"));

        _sut.Update(first);
        _sut.Update(second);

        Assert.Single(_received, m => ReferenceEquals(m.Snapshot, first));
        Assert.Single(_received, m => ReferenceEquals(m.Snapshot, second));
    }

    [Fact]
    public void Update_StoresTheSnapshotBeforeNotifying()
    {
        CatalogUpdatePayload? currentWhenNotified = null;
        var spy = new object();
        WeakReferenceMessenger.Default.Register<object, CatalogChangedMessage>(
            spy, (_, _) => currentWhenNotified = _sut.Current);
        var payload = TestData.Payload("f1", TestData.Item("Mojito"));

        try
        {
            _sut.Update(payload);
        }
        finally
        {
            WeakReferenceMessenger.Default.UnregisterAll(spy);
        }

        Assert.Same(payload, currentWhenNotified);
    }
}
