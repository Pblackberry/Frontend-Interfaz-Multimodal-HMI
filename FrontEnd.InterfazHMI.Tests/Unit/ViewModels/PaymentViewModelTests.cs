using System.Globalization;
using FrontEnd.InterfazHMI.Tests.Support;
using Frontend_interfaz_Multimodal_HMI.Models;
using Frontend_interfaz_Multimodal_HMI.Services;
using Frontend_interfaz_Multimodal_HMI.ViewModels;

namespace FrontEnd.InterfazHMI.Tests.Unit.ViewModels;

public class PaymentViewModelTests
{
    private readonly FakeNavigationService _nav = new();
    private readonly OrderSession _order = new();

    private PaymentViewModel CreateSut() => new(_nav, _order);


    [Fact]
    public void Constructor_WithoutSelectedDrink_HasEmptyTexts()
    {
        var sut = CreateSut();

        Assert.Equal("", sut.DrinkName);
        Assert.Equal("", sut.PriceText);
    }

    [Fact]
    public void Constructor_WithSelectedDrink_ShowsItsNameAndPrice()
    {
        _order.Select(TestData.Item("Piña colada", precio: 7.25m));

        var sut = CreateSut();

        Assert.Equal("Piña colada", sut.DrinkName);
        Assert.Equal("$7.25", sut.PriceText);
    }

    [Theory]
    [InlineData("5.5", "$5.50")]
    [InlineData("6", "$6.00")]
    [InlineData("0", "$0.00")]
    [InlineData("5.678", "$5.68")]
    [InlineData("1234.5", "$1234.50")]
    public void PriceText_HasDollarSignAndTwoDecimals(string price, string expected)
    {
        _order.Select(TestData.Item(precio: decimal.Parse(price, CultureInfo.InvariantCulture)));

        Assert.Equal(expected, CreateSut().PriceText);
    }

    [Fact]
    public void PriceText_IgnoresTheCurrentCulture()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");
            _order.Select(TestData.Item(precio: 5.5m));

            Assert.Equal("$5.50", CreateSut().PriceText);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public void Texts_AreTakenWhenCreated_NotUpdatedLater()
    {
        _order.Select(TestData.Item("Mojito", precio: 5.5m));
        var sut = CreateSut();

        _order.Select(TestData.Item("Daiquiri", precio: 6m));

        Assert.Equal("Mojito", sut.DrinkName);
        Assert.Equal("$5.50", sut.PriceText);
    }


    [Fact]
    public void CancelCommand_ClearsTheOrder()
    {
        _order.Select(TestData.Item("Mojito"));
        var sut = CreateSut();

        sut.CancelCommand.Execute(null);

        Assert.Null(_order.SelectedDrink);
    }

    [Fact]
    public void CancelCommand_GoesBackToTheMenu()
    {
        _order.Select(TestData.Item("Mojito"));
        var sut = CreateSut();

        sut.CancelCommand.Execute(null);

        Assert.Equal(new[] { (Screen.Menu, StateSource.Touch, (string?)null) }, _nav.NavigateToCalls);
    }

    [Fact]
    public void CancelCommand_WithoutSelectedDrink_StillGoesBackToTheMenu()
    {
        var sut = CreateSut();

        sut.CancelCommand.Execute(null);

        Assert.Null(_order.SelectedDrink);
        Assert.Single(_nav.NavigateToCalls);
        Assert.Equal(Screen.Menu, _nav.NavigateToCalls[0].Screen);
    }

    [Fact]
    public void CancelCommand_CanAlwaysExecute()
    {
        Assert.True(CreateSut().CancelCommand.CanExecute(null));
    }

    [Fact]
    public void Constructor_DoesNotNavigateByItself()
    {
        _order.Select(TestData.Item("Mojito"));

        _ = CreateSut();

        Assert.Empty(_nav.NavigateToCalls);
    }
}
