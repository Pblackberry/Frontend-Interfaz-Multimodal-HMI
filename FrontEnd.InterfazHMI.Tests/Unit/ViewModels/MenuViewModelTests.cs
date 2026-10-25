using CommunityToolkit.Mvvm.Messaging;
using FrontEnd.InterfazHMI.Tests.Support;
using Frontend_interfaz_Multimodal_HMI.Models;
using Frontend_interfaz_Multimodal_HMI.Services;
using Frontend_interfaz_Multimodal_HMI.ViewModels;

namespace FrontEnd.InterfazHMI.Tests.Unit.ViewModels;

// MenuViewModel se registra en el messenger estático (IsActive = true).
[Collection(MessengerCollection.Name)]
public class MenuViewModelTests : IDisposable
{
    private readonly List<MenuViewModel> _created = new();

    public void Dispose()
    {
        foreach (var vm in _created)
        {
            vm.IsActive = false;
        }
    }

    private (MenuViewModel Sut, CatalogStore Store) CreateSut(params CobotMenuItem[] items)
    {
        var store = new CatalogStore();
        if (items.Length > 0)
        {
            store.Update(TestData.Payload("f1", items));
        }

        var sut = new MenuViewModel(store);
        _created.Add(sut);
        return (sut, store);
    }

    // ---------- carga inicial ----------

    [Fact]
    public void Constructor_WithEmptyStore_HasNoItems()
    {
        var (sut, _) = CreateSut();

        Assert.Empty(sut.Items);
    }

    [Fact]
    public void Constructor_CreatesOneCardPerStoreItem_InOrder()
    {
        var mojito = TestData.Item("Mojito");
        var daiquiri = TestData.Item("Daiquiri");

        var (sut, _) = CreateSut(mojito, daiquiri);

        Assert.Equal(2, sut.Items.Count);
        Assert.Equal(new[] { "Mojito", "Daiquiri" }, sut.Items.Select(c => c.Name));
        Assert.Same(mojito, sut.Items[0].Item);
        Assert.Same(daiquiri, sut.Items[1].Item);
    }

    [Fact]
    public void Constructor_BuildsCardsWithFormattedPrice()
    {
        var (sut, _) = CreateSut(TestData.Item("Mojito", precio: 5.5m));

        Assert.Equal("$5.50", sut.Items[0].PriceText);
    }

    // ---------- activación en el messenger ----------

    [Fact]
    public void Constructor_ActivatesTheViewModelAndRegistersItInTheMessenger()
    {
        var (sut, _) = CreateSut();

        Assert.True(sut.IsActive);
        Assert.True(WeakReferenceMessenger.Default.IsRegistered<CatalogChangedMessage>(sut));
    }

    [Fact]
    public void Deactivating_UnregistersItFromTheMessenger()
    {
        var (sut, _) = CreateSut();

        sut.IsActive = false;

        Assert.False(WeakReferenceMessenger.Default.IsRegistered<CatalogChangedMessage>(sut));
    }

    // ---------- Receive ----------

    [Fact]
    public void Receive_WithoutAWpfApplication_DoesNotThrowAndKeepsTheItems()
    {
        // En xUnit no existe Application.Current, así que Receive no llega a refrescar la lista.
        var (sut, _) = CreateSut(TestData.Item("Mojito"));
        var message = new CatalogChangedMessage(TestData.Payload("f2", TestData.Item("Daiquiri")));

        var exception = Record.Exception(() => sut.Receive(message));

        Assert.Null(exception);
        Assert.Equal(new[] { "Mojito" }, sut.Items.Select(c => c.Name));
    }

    [Fact]
    public void StoreUpdate_ReachingAnActiveViewModelThroughTheMessenger_DoesNotThrow()
    {
        var (_, store) = CreateSut(TestData.Item("Mojito"));

        var exception = Record.Exception(
            () => store.Update(TestData.Payload("f2", TestData.Item("Daiquiri"))));

        Assert.Null(exception);
    }

    // ---------- SelectDrinkCommand ----------

    [Fact]
    public void SelectDrinkCommand_CanAlwaysExecute()
    {
        var (sut, _) = CreateSut(TestData.Item("Mojito"));

        Assert.True(sut.SelectDrinkCommand.CanExecute(sut.Items[0]));
        Assert.True(sut.SelectDrinkCommand.CanExecute(null));
    }

    [Fact]
    public void SelectDrinkCommand_WithADrink_DoesNotThrowNorChangeTheItems()
    {
        var (sut, _) = CreateSut(TestData.Item("Mojito"), TestData.Item("Daiquiri"));

        var exception = Record.Exception(() => sut.SelectDrinkCommand.Execute(sut.Items[0]));

        Assert.Null(exception);
        Assert.Equal(2, sut.Items.Count);
    }

    [Fact]
    public void SelectDrinkCommand_WithNull_DoesNothing()
    {
        var (sut, _) = CreateSut(TestData.Item("Mojito"));

        var exception = Record.Exception(() => sut.SelectDrinkCommand.Execute(null));

        Assert.Null(exception);
        Assert.Single(sut.Items);
    }
}
