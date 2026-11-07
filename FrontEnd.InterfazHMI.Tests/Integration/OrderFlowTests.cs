using CommunityToolkit.Mvvm.Messaging;
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
    private readonly FakeWsSender _ws = new();
    private readonly FakeQrFactory _qr = new();
    private readonly List<MenuViewModel> _menus = new();
    private readonly List<(Screen Screen, StateSource Source, string? OrderId)> _navigated = new();

    public OrderFlowTests()
    {
        WeakReferenceMessenger.Default.Reset();

        var services = new ServiceCollection();
        services.AddSingleton<INavigationService, NavigationService>();
        services.AddSingleton<CatalogStore>();
        services.AddSingleton<IImageLoader, FakeImageLoader>();
        services.AddSingleton<OrderSession>();
        services.AddTransient<WelcomeViewModel>();
        services.AddTransient<MenuViewModel>();
        services.AddTransient<PaymentViewModel>();
        services.AddTransient<PaymentResultViewModel>();
        services.AddSingleton<IWsSender>(_ws);
        services.AddSingleton<IQrFactory>(_qr);
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
        WeakReferenceMessenger.Default.Reset();
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

    // ---------- órdenes de voz (llegan por el messenger, como las envía BackendLink) ----------

    [Fact]
    public void VoiceSelect_FromTheMenu_ShowsThePaymentScreenWithThatDrink()
    {
        OpenMenu();

        WeakReferenceMessenger.Default.Send(new VoiceSelectDrinkMessage("voz-1", "Daiquiri"));

        var payment = Assert.IsType<PaymentViewModel>(_nav.Current);
        Assert.Equal("Daiquiri", payment.DrinkName);
        Assert.Equal("$6.00", payment.PriceText);
    }

    [Fact]
    public void VoiceSelect_AnnouncesThePaymentScreenWithBackendSourceAndTheOrderId()
    {
        OpenMenu();
        _navigated.Clear();

        WeakReferenceMessenger.Default.Send(new VoiceSelectDrinkMessage("voz-1", "Mojito"));

        Assert.Equal(new[] { (Screen.Payment, StateSource.Backend, (string?)"voz-1") }, _navigated);
    }

    [Fact]
    public void VoiceSelect_OfAnUnknownDrink_StaysInTheMenu()
    {
        var menu = OpenMenu();
        _navigated.Clear();

        WeakReferenceMessenger.Default.Send(new VoiceSelectDrinkMessage("voz-1", "Tequila"));

        Assert.Same(menu, _nav.Current);
        Assert.Empty(_navigated);
        Assert.Null(_order.SelectedDrink);
    }

    [Fact]
    public void VoiceShowAndCloseIngredients_OnlyTogglesThePanel()
    {
        var menu = OpenMenu();
        _navigated.Clear();

        WeakReferenceMessenger.Default.Send(new VoiceShowIngredientsMessage("Mojito"));
        var openedWith = menu.InfoDrink?.Name;
        WeakReferenceMessenger.Default.Send(new VoiceCloseIngredientsMessage());

        Assert.Equal("Mojito", openedWith);
        Assert.False(menu.IsInfoOpen);
        Assert.Same(menu, _nav.Current);
        Assert.Empty(_navigated);
    }

    [Fact]
    public void VoiceSelect_ThenCancelling_AllowsChoosingAgainByVoice()
    {
        OpenMenu();
        WeakReferenceMessenger.Default.Send(new VoiceSelectDrinkMessage("voz-1", "Mojito"));
        Assert.IsType<PaymentViewModel>(_nav.Current).CancelCommand.Execute(null);
        _menus.Add(Assert.IsType<MenuViewModel>(_nav.Current));

        WeakReferenceMessenger.Default.Send(new VoiceSelectDrinkMessage("voz-2", "Daiquiri"));

        var payment = Assert.IsType<PaymentViewModel>(_nav.Current);
        Assert.Equal("Daiquiri", payment.DrinkName);
    }

    [Fact]
    public void VoiceSelect_AfterVisitingTheMenuTwice_AnnouncesOnlyOnePaymentNavigation()
    {
        OpenMenu();
        WeakReferenceMessenger.Default.Send(new VoiceSelectDrinkMessage("voz-1", "Mojito"));
        Assert.IsType<PaymentViewModel>(_nav.Current).CancelCommand.Execute(null);
        _menus.Add(Assert.IsType<MenuViewModel>(_nav.Current));
        _navigated.Clear();

        WeakReferenceMessenger.Default.Send(new VoiceSelectDrinkMessage("voz-2", "Daiquiri"));

        Assert.Equal(new[] { (Screen.Payment, StateSource.Backend, (string?)"voz-2") }, _navigated);
    }

    // ---------- pago por QR ----------

    private PaymentViewModel SelectFirstDrink()
    {
        var menu = OpenMenu();
        menu.SelectDrinkCommand.Execute(menu.Items[0]);
        return Assert.IsType<PaymentViewModel>(_nav.Current);
    }

    private static void BackendStartsPayment(string paymentId = "pago-1", string drink = "Mojito", decimal price = 5.5m)
        => WeakReferenceMessenger.Default.Send(new PaymentStartedMessage(
            new PaymentStartedPayload(paymentId, "http://192.168.0.10:8888/pay/" + paymentId, drink, price)));

    private static void BackendSendsResult(
        PaymentOutcome outcome, string? paymentId = "pago-1", string envelopeId = "env-1", string? reason = null)
        => WeakReferenceMessenger.Default.Send(
            new PaymentResultMessage(envelopeId, new PaymentResultPayload(paymentId, outcome, reason)));

    [Fact]
    public void ShowingThePayment_AsksTheBackendToStartThePaymentOfThatDrink()
    {
        SelectFirstDrink();

        var (type, payload) = Assert.Single(_ws.Sent);
        Assert.Equal(WsTypes.StartPayment, type);
        Assert.Equal(new StartPaymentPayload("Mojito"), payload);
    }

    [Fact]
    public void WhenTheBackendStartsThePayment_TheQrIsShown()
    {
        var payment = SelectFirstDrink();

        BackendStartsPayment("pago-7");

        Assert.True(payment.HasQr);
        Assert.Equal(new[] { "http://192.168.0.10:8888/pay/pago-7" }, _qr.Created);
        Assert.Equal("Escanea el código con tu celular", payment.StatusText);
    }

    [Fact]
    public void ApprovedPayment_ShowsTheSuccessScreen()
    {
        SelectFirstDrink();
        BackendStartsPayment();

        BackendSendsResult(PaymentOutcome.Approved);

        var result = Assert.IsType<PaymentResultViewModel>(_nav.Current);
        Assert.Equal(Screen.PaymentResult, _nav.CurrentScreen);
        Assert.True(result.IsSuccess);
        Assert.Equal("¡Pago realizado!", result.Title);
    }

    [Fact]
    public void TheWholeTouchOrder_IsAnnouncedScreenByScreen()
    {
        var menu = OpenMenu();
        menu.SelectDrinkCommand.Execute(menu.Items[0]);
        BackendStartsPayment();
        BackendSendsResult(PaymentOutcome.Approved, envelopeId: "env-5");
        Assert.IsType<PaymentResultViewModel>(_nav.Current).ContinueCommand.Execute(null);

        Assert.Equal(
            new[]
            {
                (Screen.Menu, StateSource.Touch, (string?)null),
                (Screen.Payment, StateSource.Touch, (string?)null),
                (Screen.PaymentResult, StateSource.Backend, (string?)"env-5"),
                (Screen.Welcome, StateSource.Touch, (string?)null),
            },
            _navigated);
    }

    [Fact]
    public void ApprovedPayment_ThenNewOrder_GoesBackToWelcomeWithAnEmptyOrder()
    {
        SelectFirstDrink();
        BackendStartsPayment();
        BackendSendsResult(PaymentOutcome.Approved);

        Assert.IsType<PaymentResultViewModel>(_nav.Current).ContinueCommand.Execute(null);

        Assert.IsType<WelcomeViewModel>(_nav.Current);
        Assert.Null(_order.SelectedDrink);
        Assert.Null(_order.Result);
    }

    [Theory]
    [InlineData(PaymentOutcome.Rejected, "Pago rechazado")]
    [InlineData(PaymentOutcome.Expired, "El código expiró")]
    public void FailedPayment_ShowsItsMessageAndGoesBackToTheMenu(PaymentOutcome outcome, string title)
    {
        SelectFirstDrink();
        BackendStartsPayment();
        BackendSendsResult(outcome);
        var result = Assert.IsType<PaymentResultViewModel>(_nav.Current);
        Assert.False(result.IsSuccess);
        Assert.Equal(title, result.Title);

        result.ContinueCommand.Execute(null);

        var menu = Assert.IsType<MenuViewModel>(_nav.Current);
        _menus.Add(menu);
        Assert.Equal(new[] { "Mojito", "Daiquiri" }, menu.Items.Select(c => c.Name));
        Assert.Null(_order.SelectedDrink);
    }

    [Fact]
    public void TheBackendCannotStartThePayment_ShowsTheDrinkUnavailableMessage()
    {
        SelectFirstDrink();

        BackendSendsResult(PaymentOutcome.Error, paymentId: null, reason: "drink_unavailable");

        var result = Assert.IsType<PaymentResultViewModel>(_nav.Current);
        Assert.Equal("No pudimos iniciar el pago", result.Title);
        Assert.Equal("Esa bebida ya no está disponible.", result.Message);
    }

    [Fact]
    public void ResultOfAnotherPayment_DoesNotLeaveThePaymentScreen()
    {
        var payment = SelectFirstDrink();
        BackendStartsPayment("pago-2");

        BackendSendsResult(PaymentOutcome.Approved, paymentId: "pago-1");

        Assert.Same(payment, _nav.Current);
        Assert.Null(_order.Result);
    }

    [Fact]
    public void CancellingAfterTheQr_TellsTheBackendWhichPaymentToCancel()
    {
        var payment = SelectFirstDrink();
        BackendStartsPayment("pago-3");
        _ws.Sent.Clear();

        payment.CancelCommand.Execute(null);
        _menus.Add(Assert.IsType<MenuViewModel>(_nav.Current));

        var (type, payload) = Assert.Single(_ws.Sent);
        Assert.Equal(WsTypes.CancelPayment, type);
        Assert.Equal(new CancelPaymentPayload("pago-3"), payload);
    }

    [Fact]
    public void ALateResultAfterCancelling_IsIgnored()
    {
        var payment = SelectFirstDrink();
        BackendStartsPayment("pago-3");
        payment.CancelCommand.Execute(null);
        var menu = Assert.IsType<MenuViewModel>(_nav.Current);
        _menus.Add(menu);
        _navigated.Clear();

        BackendSendsResult(PaymentOutcome.Approved, paymentId: "pago-3");

        Assert.Same(menu, _nav.Current);
        Assert.Empty(_navigated);
        Assert.Null(_order.Result);
    }

    [Fact]
    public void PayingAgainAfterCancelling_RequestsANewPaymentForTheNewDrink()
    {
        var payment = SelectFirstDrink();
        BackendStartsPayment("pago-3");
        payment.CancelCommand.Execute(null);
        var menu = Assert.IsType<MenuViewModel>(_nav.Current);
        _menus.Add(menu);
        _ws.Sent.Clear();

        menu.SelectDrinkCommand.Execute(menu.Items[1]);

        var (type, payload) = Assert.Single(_ws.Sent);
        Assert.Equal(WsTypes.StartPayment, type);
        Assert.Equal(new StartPaymentPayload("Daiquiri"), payload);
        Assert.False(Assert.IsType<PaymentViewModel>(_nav.Current).HasQr);
    }

    [Fact]
    public void VoiceSelection_FollowedByTheQrPayment_CompletesTheOrder()
    {
        OpenMenu();
        WeakReferenceMessenger.Default.Send(new VoiceSelectDrinkMessage("voz-1", "Daiquiri"));
        var payment = Assert.IsType<PaymentViewModel>(_nav.Current);
        BackendStartsPayment("pago-9", "Daiquiri", 6m);

        BackendSendsResult(PaymentOutcome.Approved, "pago-9", "env-9");

        Assert.True(payment.HasQr);
        Assert.Equal(new StartPaymentPayload("Daiquiri"), _ws.Sent[0].Payload);
        var result = Assert.IsType<PaymentResultViewModel>(_nav.Current);
        Assert.True(result.IsSuccess);
        Assert.Equal(
            (Screen.PaymentResult, StateSource.Backend, (string?)"env-9"),
            _navigated[^1]);
    }
}
