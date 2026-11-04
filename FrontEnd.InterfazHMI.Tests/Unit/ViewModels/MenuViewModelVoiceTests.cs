using CommunityToolkit.Mvvm.Messaging;
using FrontEnd.InterfazHMI.Tests.Support;
using Frontend_interfaz_Multimodal_HMI.Models;
using Frontend_interfaz_Multimodal_HMI.Services;
using Frontend_interfaz_Multimodal_HMI.ViewModels;

namespace FrontEnd.InterfazHMI.Tests.Unit.ViewModels;

[Collection(MessengerCollection.Name)]
public class MenuViewModelVoiceTests : IDisposable
{
    private readonly OrderSession _order = new();
    private readonly FakeNavigationService _nav = new();
    private readonly MenuViewModel _sut;

    public MenuViewModelVoiceTests()
    {
        WeakReferenceMessenger.Default.Reset();

        var store = new CatalogStore();
        store.Update(TestData.Payload(
            "f1",
            TestData.Item("Mojito", ingredientes: "ron 40%; menta"),
            TestData.Item("Daiquiri", ingredientes: null),
            TestData.Item("Piña colada", ingredientes: "ron; piña")));
        _sut = new MenuViewModel(store, new FakeImageLoader(), _order, _nav);
    }

    public void Dispose()
    {
        _sut.IsActive = false;
        WeakReferenceMessenger.Default.Reset();
    }

    private DrinkCardViewModel Card(string name) => _sut.Items.Single(c => c.Name == name);


    [Fact]
    public void VoiceSelect_StoresTheDrinkInTheOrder()
    {
        _sut.Receive(new VoiceSelectDrinkMessage("voz-1", "Mojito"));

        Assert.Same(Card("Mojito").Item, _order.SelectedDrink);
    }

    [Fact]
    public void VoiceSelect_NavigatesToPaymentWithBackendSourceAndTheMessageIdAsOrder()
    {
        _sut.Receive(new VoiceSelectDrinkMessage("voz-1", "Mojito"));

        Assert.Equal(new[] { (Screen.Payment, StateSource.Backend, (string?)"voz-1") }, _nav.NavigateToCalls);
    }

    [Theory]
    [InlineData("mojito")]
    [InlineData("MOJITO")]
    [InlineData("Mojito")]
    public void VoiceSelect_IgnoresCase(string spoken)
    {
        _sut.Receive(new VoiceSelectDrinkMessage("voz-1", spoken));

        Assert.Same(Card("Mojito").Item, _order.SelectedDrink);
    }

    [Fact]
    public void VoiceSelect_FindsDrinksWithAccents()
    {
        _sut.Receive(new VoiceSelectDrinkMessage("voz-1", "Piña colada"));

        Assert.Same(Card("Piña colada").Item, _order.SelectedDrink);
    }

    [Fact]
    public void VoiceSelect_NeedsTheExactAccentsOfTheCatalogName()
    {
        _sut.Receive(new VoiceSelectDrinkMessage("voz-1", "Pina colada"));

        Assert.Null(_order.SelectedDrink);
        Assert.Empty(_nav.NavigateToCalls);
    }

    [Theory]
    [InlineData("Tequila")]
    [InlineData("")]
    [InlineData("Moji")]
    public void VoiceSelect_WithAnUnknownDrink_DoesNothing(string spoken)
    {
        _sut.Receive(new VoiceSelectDrinkMessage("voz-1", spoken));

        Assert.Null(_order.SelectedDrink);
        Assert.Empty(_nav.NavigateToCalls);
    }

    [Fact]
    public void VoiceSelect_ClosesTheOpenIngredientsPanel()
    {
        _sut.ShowIngredientsCommand.Execute(Card("Mojito"));

        _sut.Receive(new VoiceSelectDrinkMessage("voz-1", "Piña colada"));

        Assert.Null(_sut.InfoDrink);
        Assert.False(_sut.IsInfoOpen);
    }

    [Fact]
    public void VoiceSelect_WithAnUnknownDrink_KeepsTheOpenIngredientsPanel()
    {
        _sut.ShowIngredientsCommand.Execute(Card("Mojito"));

        _sut.Receive(new VoiceSelectDrinkMessage("voz-1", "Tequila"));

        Assert.Same(Card("Mojito"), _sut.InfoDrink);
    }

    [Fact]
    public void VoiceSelect_ReplacesAPreviousSelection()
    {
        _sut.Receive(new VoiceSelectDrinkMessage("voz-1", "Mojito"));

        _sut.Receive(new VoiceSelectDrinkMessage("voz-2", "Daiquiri"));

        Assert.Same(Card("Daiquiri").Item, _order.SelectedDrink);
        Assert.Equal((Screen.Payment, StateSource.Backend, (string?)"voz-2"), _nav.NavigateToCalls[^1]);
    }

    [Fact]
    public void TouchSelect_StillNavigatesWithTouchSourceAndNoOrderId()
    {
        _sut.SelectDrinkCommand.Execute(Card("Mojito"));

        Assert.Equal(new[] { (Screen.Payment, StateSource.Touch, (string?)null) }, _nav.NavigateToCalls);
    }


    [Fact]
    public void VoiceShowIngredients_OpensThePanelOfThatDrink()
    {
        _sut.Receive(new VoiceShowIngredientsMessage("Mojito"));

        Assert.Same(Card("Mojito"), _sut.InfoDrink);
        Assert.True(_sut.IsInfoOpen);
    }

    [Theory]
    [InlineData("mojito")]
    [InlineData("MOJITO")]
    public void VoiceShowIngredients_IgnoresCase(string spoken)
    {
        _sut.Receive(new VoiceShowIngredientsMessage(spoken));

        Assert.Same(Card("Mojito"), _sut.InfoDrink);
    }

    [Fact]
    public void VoiceShowIngredients_OfADrinkWithoutIngredients_DoesNotOpenThePanel()
    {
        _sut.Receive(new VoiceShowIngredientsMessage("Daiquiri"));

        Assert.Null(_sut.InfoDrink);
        Assert.False(_sut.IsInfoOpen);
    }

    [Fact]
    public void VoiceShowIngredients_OfADrinkWithoutIngredients_KeepsThePanelThatWasOpen()
    {
        _sut.Receive(new VoiceShowIngredientsMessage("Mojito"));

        _sut.Receive(new VoiceShowIngredientsMessage("Daiquiri"));

        Assert.Same(Card("Mojito"), _sut.InfoDrink);
    }

    [Fact]
    public void VoiceShowIngredients_WithAnUnknownDrink_DoesNothing()
    {
        _sut.Receive(new VoiceShowIngredientsMessage("Tequila"));

        Assert.Null(_sut.InfoDrink);
    }

    [Fact]
    public void VoiceShowIngredients_ForAnotherDrink_SwitchesThePanel()
    {
        _sut.Receive(new VoiceShowIngredientsMessage("Mojito"));

        _sut.Receive(new VoiceShowIngredientsMessage("Piña colada"));

        Assert.Same(Card("Piña colada"), _sut.InfoDrink);
    }

    [Fact]
    public void VoiceShowIngredients_DoesNotSelectNorNavigate()
    {
        _sut.Receive(new VoiceShowIngredientsMessage("Mojito"));

        Assert.Null(_order.SelectedDrink);
        Assert.Empty(_nav.NavigateToCalls);
    }

    [Fact]
    public void VoiceShowIngredients_RaisesPropertyChangedForInfoDrinkAndIsInfoOpen()
    {
        var raised = new List<string?>();
        _sut.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        _sut.Receive(new VoiceShowIngredientsMessage("Mojito"));

        Assert.Contains(nameof(MenuViewModel.InfoDrink), raised);
        Assert.Contains(nameof(MenuViewModel.IsInfoOpen), raised);
    }


    [Fact]
    public void VoiceClose_ClosesTheOpenPanel()
    {
        _sut.Receive(new VoiceShowIngredientsMessage("Mojito"));

        _sut.Receive(new VoiceCloseIngredientsMessage());

        Assert.Null(_sut.InfoDrink);
        Assert.False(_sut.IsInfoOpen);
    }

    [Fact]
    public void VoiceClose_WhenNothingIsOpen_DoesNothing()
    {
        var exception = Record.Exception(() => _sut.Receive(new VoiceCloseIngredientsMessage()));

        Assert.Null(exception);
        Assert.False(_sut.IsInfoOpen);
        Assert.Empty(_nav.NavigateToCalls);
    }


    [Fact]
    public void MessengerMessages_ReachTheActiveMenu()
    {
        WeakReferenceMessenger.Default.Send(new VoiceShowIngredientsMessage("Mojito"));
        Assert.Same(Card("Mojito"), _sut.InfoDrink);

        WeakReferenceMessenger.Default.Send(new VoiceCloseIngredientsMessage());
        Assert.Null(_sut.InfoDrink);

        WeakReferenceMessenger.Default.Send(new VoiceSelectDrinkMessage("voz-1", "Daiquiri"));
        Assert.Same(Card("Daiquiri").Item, _order.SelectedDrink);
    }

    [Fact]
    public void MessengerMessages_DoNotReachAMenuThatWasDeactivated()
    {
        _sut.IsActive = false;

        WeakReferenceMessenger.Default.Send(new VoiceSelectDrinkMessage("voz-1", "Mojito"));
        WeakReferenceMessenger.Default.Send(new VoiceShowIngredientsMessage("Mojito"));

        Assert.Null(_order.SelectedDrink);
        Assert.Null(_sut.InfoDrink);
    }
}
