using System.Windows.Media;
using CommunityToolkit.Mvvm.Messaging;
using FrontEnd.InterfazHMI.Tests.Support;
using Frontend_interfaz_Multimodal_HMI.Models;
using Frontend_interfaz_Multimodal_HMI.Services;
using Frontend_interfaz_Multimodal_HMI.ViewModels;

namespace FrontEnd.InterfazHMI.Tests.Unit.ViewModels;

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

    private readonly FakeImageLoader _loader = new();
    private readonly OrderSession _order = new();
    private readonly FakeNavigationService _nav = new();

    private (MenuViewModel Sut, CatalogStore Store) CreateSut(params CobotMenuItem[] items)
    {
        var store = new CatalogStore();
        if (items.Length > 0)
        {
            store.Update(TestData.Payload("f1", items));
        }

        var sut = new MenuViewModel(store, _loader, _order, _nav);
        _created.Add(sut);
        return (sut, store);
    }


    [Fact]
    public void Constructor_AsksTheLoaderForEachCardImage_InOrder()
    {
        var mojito = TestData.Item("Mojito");
        mojito.Imagen = "https://cdn.test/mojito.png";
        var daiquiri = TestData.Item("Daiquiri");
        daiquiri.Imagen = null;

        CreateSut(mojito, daiquiri);

        Assert.Equal(new string?[] { "https://cdn.test/mojito.png", null }, _loader.Requested);
    }

    [Fact]
    public void Cards_ShowTheImageProvidedByTheLoader()
    {
        var image = new DrawingImage();
        var store = new CatalogStore();
        store.Update(TestData.Payload("f1", TestData.Item("Mojito"), TestData.Item("Daiquiri")));

        var sut = new MenuViewModel(store, FakeImageLoader.Returning(image), _order, _nav);
        _created.Add(sut);

        Assert.All(sut.Items, card => Assert.Same(image, card.Image));
    }


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


    [Fact]
    public void Receive_WithoutAWpfApplication_DoesNotThrowAndKeepsTheItems()
    {
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


    [Fact]
    public void SelectDrinkCommand_CanAlwaysExecute()
    {
        var (sut, _) = CreateSut(TestData.Item("Mojito"));

        Assert.True(sut.SelectDrinkCommand.CanExecute(sut.Items[0]));
        Assert.True(sut.SelectDrinkCommand.CanExecute(null));
    }

    [Fact]
    public void SelectDrinkCommand_WithADrink_StoresItInTheOrder()
    {
        var (sut, _) = CreateSut(TestData.Item("Mojito"), TestData.Item("Daiquiri"));

        sut.SelectDrinkCommand.Execute(sut.Items[1]);

        Assert.Same(sut.Items[1].Item, _order.SelectedDrink);
        Assert.Equal(2, sut.Items.Count);
    }

    [Fact]
    public void SelectDrinkCommand_WithADrink_NavigatesToPayment()
    {
        var (sut, _) = CreateSut(TestData.Item("Mojito"));

        sut.SelectDrinkCommand.Execute(sut.Items[0]);

        Assert.Equal(new[] { (Screen.Payment, StateSource.Touch, (string?)null) }, _nav.NavigateToCalls);
    }

    [Fact]
    public void SelectDrinkCommand_SelectingAgain_ReplacesTheDrink()
    {
        var (sut, _) = CreateSut(TestData.Item("Mojito"), TestData.Item("Daiquiri"));

        sut.SelectDrinkCommand.Execute(sut.Items[0]);
        sut.SelectDrinkCommand.Execute(sut.Items[1]);

        Assert.Same(sut.Items[1].Item, _order.SelectedDrink);
    }

    [Fact]
    public void SelectDrinkCommand_WithNull_DoesNothing()
    {
        var (sut, _) = CreateSut(TestData.Item("Mojito"));

        sut.SelectDrinkCommand.Execute(null);

        Assert.Null(_order.SelectedDrink);
        Assert.Empty(_nav.NavigateToCalls);
        Assert.Single(sut.Items);
    }


    [Fact]
    public void InfoDrink_StartsClosed()
    {
        var (sut, _) = CreateSut(TestData.Item("Mojito"));

        Assert.Null(sut.InfoDrink);
        Assert.False(sut.IsInfoOpen);
    }

    [Fact]
    public void ShowIngredientsCommand_OpensTheInfoOfThatDrink()
    {
        var (sut, _) = CreateSut(TestData.Item("Mojito"), TestData.Item("Daiquiri"));

        sut.ShowIngredientsCommand.Execute(sut.Items[1]);

        Assert.Same(sut.Items[1], sut.InfoDrink);
        Assert.True(sut.IsInfoOpen);
    }

    [Fact]
    public void ShowIngredientsCommand_RaisesPropertyChangedForInfoDrinkAndIsInfoOpen()
    {
        var (sut, _) = CreateSut(TestData.Item("Mojito"));
        var raised = new List<string?>();
        sut.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        sut.ShowIngredientsCommand.Execute(sut.Items[0]);

        Assert.Contains(nameof(MenuViewModel.InfoDrink), raised);
        Assert.Contains(nameof(MenuViewModel.IsInfoOpen), raised);
    }

    [Fact]
    public void ShowIngredientsCommand_WithAnotherDrink_SwitchesTheInfo()
    {
        var (sut, _) = CreateSut(TestData.Item("Mojito"), TestData.Item("Daiquiri"));
        sut.ShowIngredientsCommand.Execute(sut.Items[0]);

        sut.ShowIngredientsCommand.Execute(sut.Items[1]);

        Assert.Same(sut.Items[1], sut.InfoDrink);
    }

    [Fact]
    public void ShowIngredientsCommand_WithNull_ClosesTheInfo()
    {
        var (sut, _) = CreateSut(TestData.Item("Mojito"));
        sut.ShowIngredientsCommand.Execute(sut.Items[0]);

        sut.ShowIngredientsCommand.Execute(null);

        Assert.Null(sut.InfoDrink);
        Assert.False(sut.IsInfoOpen);
    }

    [Fact]
    public void ShowIngredientsCommand_DoesNotSelectNorNavigate()
    {
        var (sut, _) = CreateSut(TestData.Item("Mojito"));

        sut.ShowIngredientsCommand.Execute(sut.Items[0]);

        Assert.Null(_order.SelectedDrink);
        Assert.Empty(_nav.NavigateToCalls);
    }

    [Fact]
    public void CloseIngredientsCommand_ClosesTheInfo()
    {
        var (sut, _) = CreateSut(TestData.Item("Mojito"));
        sut.ShowIngredientsCommand.Execute(sut.Items[0]);

        sut.CloseIngredientsCommand.Execute(null);

        Assert.Null(sut.InfoDrink);
        Assert.False(sut.IsInfoOpen);
    }

    [Fact]
    public void CloseIngredientsCommand_WhenAlreadyClosed_DoesNothing()
    {
        var (sut, _) = CreateSut(TestData.Item("Mojito"));

        var exception = Record.Exception(() => sut.CloseIngredientsCommand.Execute(null));

        Assert.Null(exception);
        Assert.False(sut.IsInfoOpen);
    }
}
