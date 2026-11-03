using FrontEnd.InterfazHMI.Tests.Support;
using Frontend_interfaz_Multimodal_HMI.Models;
using Frontend_interfaz_Multimodal_HMI.Services;
using Frontend_interfaz_Multimodal_HMI.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace FrontEnd.InterfazHMI.Tests.Integration;

[Collection(MessengerCollection.Name)]
public class OrderFlowTests : IDisposable
{
    private readonly ServiceProvider _provider;
    private readonly NavigationService _nav;
    private readonly OrderSession _order;
    private readonly List<MenuViewModel> _menus = new();
    private readonly List<(Screen Screen, StateSource Source, string? OrderId)> _navigated = new();

    public OrderFlowTests()
    {
        var services = new ServiceCollection();
        services.AddSingleton<INavigationService, NavigationService>();
        services.AddSingleton<CatalogStore>();
        services.AddSingleton<IImageLoader, FakeImageLoader>();
        services.AddSingleton<OrderSession>();
        services.AddTransient<WelcomeViewModel>();
        services.AddTransient<MenuViewModel>();
        services.AddTransient<PaymentViewModel>();
        _provider = services.BuildServiceProvider();

        _nav = (NavigationService)_provider.GetRequiredService<INavigationService>();
        _order = _provider.GetRequiredService<OrderSession>();
        _nav.Navigated += (screen, source, orderId) => _navigated.Add((screen, source, orderId));

        _provider.GetRequiredService<CatalogStore>().Update(TestData.Payload(
            "f1",
            TestData.Item("Mojito", precio: 5.5m),
            TestData.Item("Daiquiri", precio: 6m)));
    }

    public void Dispose()
    {
        foreach (var menu in _menus)
        {
            menu.IsActive = false;
        }
        _provider.Dispose();
    }

    private MenuViewModel OpenMenu()
    {
        _nav.NavigateTo(Screen.Menu);
        var menu = Assert.IsType<MenuViewModel>(_nav.Current);
        _menus.Add(menu);
        return menu;
    }

    [Fact]
    public void SelectingADrink_ShowsThePaymentScreenWithThatDrink()
    {
        var menu = OpenMenu();

        menu.SelectDrinkCommand.Execute(menu.Items[1]);

        var payment = Assert.IsType<PaymentViewModel>(_nav.Current);
        Assert.Equal(Screen.Payment, _nav.CurrentScreen);
        Assert.Equal("Daiquiri", payment.DrinkName);
        Assert.Equal("$6.00", payment.PriceText);
    }

    [Fact]
    public void SelectingADrink_AnnouncesThePaymentScreenAsATouchNavigation()
    {
        var menu = OpenMenu();
        _navigated.Clear();

        menu.SelectDrinkCommand.Execute(menu.Items[0]);

        Assert.Equal(new[] { (Screen.Payment, StateSource.Touch, (string?)null) }, _navigated);
    }

    [Fact]
    public void CancellingThePayment_ReturnsToTheMenuAndClearsTheOrder()
    {
        var menu = OpenMenu();
        menu.SelectDrinkCommand.Execute(menu.Items[0]);
        var payment = Assert.IsType<PaymentViewModel>(_nav.Current);

        payment.CancelCommand.Execute(null);

        var newMenu = Assert.IsType<MenuViewModel>(_nav.Current);
        _menus.Add(newMenu);
        Assert.Equal(Screen.Menu, _nav.CurrentScreen);
        Assert.Null(_order.SelectedDrink);
        Assert.Equal(new[] { "Mojito", "Daiquiri" }, newMenu.Items.Select(c => c.Name));
    }

    [Fact]
    public void SelectingAgainAfterCancelling_ShowsTheNewDrink()
    {
        var menu = OpenMenu();
        menu.SelectDrinkCommand.Execute(menu.Items[0]);
        Assert.IsType<PaymentViewModel>(_nav.Current).CancelCommand.Execute(null);
        var newMenu = Assert.IsType<MenuViewModel>(_nav.Current);
        _menus.Add(newMenu);

        newMenu.SelectDrinkCommand.Execute(newMenu.Items[1]);

        var payment = Assert.IsType<PaymentViewModel>(_nav.Current);
        Assert.Equal("Daiquiri", payment.DrinkName);
    }

    [Fact]
    public void OpeningThePaymentWithoutAChoice_ShowsAnEmptyPayment()
    {
        _nav.NavigateTo(Screen.Payment, StateSource.Backend);

        var payment = Assert.IsType<PaymentViewModel>(_nav.Current);
        Assert.Equal("", payment.DrinkName);
        Assert.Equal("", payment.PriceText);
    }

    [Fact]
    public void ShowingIngredients_DoesNotLeaveTheMenu()
    {
        var menu = OpenMenu();
        _navigated.Clear();

        menu.ShowIngredientsCommand.Execute(menu.Items[0]);

        Assert.Same(menu, _nav.Current);
        Assert.True(menu.IsInfoOpen);
        Assert.Empty(_navigated);
        Assert.Null(_order.SelectedDrink);
    }
}
