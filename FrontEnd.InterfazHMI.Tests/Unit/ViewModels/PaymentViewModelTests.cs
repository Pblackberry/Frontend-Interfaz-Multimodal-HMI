using System.Globalization;
using System.Windows.Media;
using CommunityToolkit.Mvvm.Messaging;
using FrontEnd.InterfazHMI.Tests.Support;
using Frontend_interfaz_Multimodal_HMI.Models;
using Frontend_interfaz_Multimodal_HMI.Services;
using Frontend_interfaz_Multimodal_HMI.ViewModels;

namespace FrontEnd.InterfazHMI.Tests.Unit.ViewModels;

[Collection(MessengerCollection.Name)]
public class PaymentViewModelTests : IDisposable
{
    private readonly FakeNavigationService _nav = new();
    private readonly OrderSession _order = new();
    private readonly FakeWsSender _ws = new();
    private readonly FakeQrFactory _qr = new();
    private readonly List<PaymentViewModel> _created = new();

    public PaymentViewModelTests()
    {
        WeakReferenceMessenger.Default.Reset();
    }

    public void Dispose()
    {
        foreach (var vm in _created)
        {
            vm.IsActive = false;
        }
        WeakReferenceMessenger.Default.Reset();
    }

    private PaymentViewModel CreateSut(FakeWsSender? ws = null)
    {
        var sut = new PaymentViewModel(_nav, _order, ws ?? _ws, _qr);
        _created.Add(sut);
        return sut;
    }

    private static PaymentStartedPayload Started(string id = "pago-1", decimal price = 5.5m, string name = "Mojito")
        => new(id, "http://192.168.0.10:8888/pay/" + id, name, price);

    private static PaymentResultPayload Result(
        PaymentOutcome status = PaymentOutcome.Approved, string? id = "pago-1", string? reason = null)
        => new(id, status, reason);

    // ---------- datos de la bebida elegida ----------

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

    // ---------- petición del pago ----------

    [Fact]
    public void Constructor_AsksTheBackendToStartThePaymentOfTheSelectedDrink()
    {
        _order.Select(TestData.Item("Piña colada"));

        CreateSut();

        var (type, payload) = Assert.Single(_ws.Sent);
        Assert.Equal(WsTypes.StartPayment, type);
        Assert.Equal(new StartPaymentPayload("Piña colada"), payload);
    }

    [Fact]
    public void Constructor_WithoutSelectedDrink_DoesNotAskForAnyPayment()
    {
        CreateSut();

        Assert.Empty(_ws.Sent);
    }

    [Fact]
    public void Constructor_WithoutSelectedDrink_SaysThereIsNoDrink()
    {
        Assert.Equal("No hay una bebida seleccionada.", CreateSut().StatusText);
    }

    [Fact]
    public void Constructor_WhileWaitingForTheBackend_ShowsTheGeneratingMessage()
    {
        _order.Select(TestData.Item("Mojito"));

        var sut = CreateSut(FakeWsSender.Pending());

        Assert.Equal("Generando código de pago…", sut.StatusText);
        Assert.Null(sut.QrImage);
        Assert.False(sut.HasQr);
    }

    [Fact]
    public void Constructor_WhenTheRequestIsSent_KeepsTheGeneratingMessage()
    {
        _order.Select(TestData.Item("Mojito"));

        Assert.Equal("Generando código de pago…", CreateSut().StatusText);
    }

    [Fact]
    public void Constructor_WithoutConnection_AsksToCancelAndRetry()
    {
        _order.Select(TestData.Item("Mojito"));

        var sut = CreateSut(new FakeWsSender(result: false));

        Assert.Equal("Sin conexión con el sistema. Cancela e inténtalo de nuevo.", sut.StatusText);
        Assert.False(sut.HasQr);
    }

    [Fact]
    public void WhenTheRequestFailsLater_ShowsTheNoConnectionMessage()
    {
        _order.Select(TestData.Item("Mojito"));
        var pending = FakeWsSender.Pending();
        var sut = CreateSut(pending);

        pending.Complete(false);

        Assert.Equal("Sin conexión con el sistema. Cancela e inténtalo de nuevo.", sut.StatusText);
    }

    [Fact]
    public void WhenTheRequestSucceedsLater_KeepsTheGeneratingMessage()
    {
        _order.Select(TestData.Item("Mojito"));
        var pending = FakeWsSender.Pending();
        var sut = CreateSut(pending);

        pending.Complete(true);

        Assert.Equal("Generando código de pago…", sut.StatusText);
    }

    // ---------- el backend confirma el inicio del pago ----------

    [Fact]
    public void PaymentStarted_ShowsTheQrBuiltFromThePayUrl()
    {
        _order.Select(TestData.Item("Mojito"));
        var sut = CreateSut();

        sut.Receive(new PaymentStartedMessage(Started("pago-7")));

        Assert.Equal(new[] { "http://192.168.0.10:8888/pay/pago-7" }, _qr.Created);
        Assert.Same(_qr.Images[0], sut.QrImage);
        Assert.True(sut.HasQr);
    }

    [Fact]
    public void PaymentStarted_ShowsTheInstructionToScan()
    {
        var sut = CreateSut();

        sut.Receive(new PaymentStartedMessage(Started()));

        Assert.Equal("Escanea el código con tu celular", sut.StatusText);
    }

    [Fact]
    public void PaymentStarted_TakesTheDrinkAndPriceFromTheBackend()
    {
        _order.Select(TestData.Item("Mojito", precio: 5.5m));
        var sut = CreateSut();

        sut.Receive(new PaymentStartedMessage(Started(name: "Mojito Especial", price: 6.25m)));

        Assert.Equal("Mojito Especial", sut.DrinkName);
        Assert.Equal("$6.25", sut.PriceText);
    }

    [Fact]
    public void PaymentStarted_NotifiesTheChangedProperties()
    {
        var sut = CreateSut();
        var changed = new List<string?>();
        sut.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        sut.Receive(new PaymentStartedMessage(Started()));

        Assert.Contains(nameof(PaymentViewModel.QrImage), changed);
        Assert.Contains(nameof(PaymentViewModel.HasQr), changed);
        Assert.Contains(nameof(PaymentViewModel.StatusText), changed);
        Assert.Contains(nameof(PaymentViewModel.DrinkName), changed);
        Assert.Contains(nameof(PaymentViewModel.PriceText), changed);
    }

    [Fact]
    public void PaymentStarted_Twice_ReplacesTheQr()
    {
        var sut = CreateSut();
        sut.Receive(new PaymentStartedMessage(Started("pago-1")));

        sut.Receive(new PaymentStartedMessage(Started("pago-2")));

        Assert.Equal(2, _qr.Created.Count);
        Assert.Same(_qr.Images[1], sut.QrImage);
    }

    [Fact]
    public void PaymentStarted_DoesNotNavigate()
    {
        var sut = CreateSut();

        sut.Receive(new PaymentStartedMessage(Started()));

        Assert.Empty(_nav.NavigateToCalls);
    }

    // ---------- resultado del pago ----------

    [Theory]
    [InlineData(PaymentOutcome.Approved)]
    [InlineData(PaymentOutcome.Rejected)]
    [InlineData(PaymentOutcome.Expired)]
    [InlineData(PaymentOutcome.Error)]
    public void PaymentResult_StoresItInTheOrderAndShowsTheResultScreen(PaymentOutcome outcome)
    {
        var sut = CreateSut();
        sut.Receive(new PaymentStartedMessage(Started("pago-1")));
        var payload = Result(outcome, "pago-1");

        sut.Receive(new PaymentResultMessage("env-9", payload));

        Assert.Same(payload, _order.Result);
        Assert.Equal(
            new[] { (Screen.PaymentResult, StateSource.Backend, (string?)"env-9") },
            _nav.NavigateToCalls);
    }

    [Fact]
    public void PaymentResult_OfAnotherPayment_IsIgnored()
    {
        var sut = CreateSut();
        sut.Receive(new PaymentStartedMessage(Started("pago-1")));

        sut.Receive(new PaymentResultMessage("env-9", Result(PaymentOutcome.Approved, "pago-viejo")));

        Assert.Null(_order.Result);
        Assert.Empty(_nav.NavigateToCalls);
    }

    [Fact]
    public void PaymentResult_WithoutPaymentId_IsAcceptedWhenThePaymentWasStarted()
    {
        var sut = CreateSut();
        sut.Receive(new PaymentStartedMessage(Started("pago-1")));

        sut.Receive(new PaymentResultMessage("env-9", Result(PaymentOutcome.Error, id: null, reason: "x")));

        Assert.Single(_nav.NavigateToCalls);
        Assert.Equal(Screen.PaymentResult, _nav.NavigateToCalls[0].Screen);
    }

    [Fact]
    public void PaymentResult_BeforeThePaymentStarted_IsAccepted()
    {
        _order.Select(TestData.Item("Mojito"));
        var sut = CreateSut();
        var payload = Result(PaymentOutcome.Error, id: null, reason: "drink_unavailable");

        sut.Receive(new PaymentResultMessage("env-2", payload));

        Assert.Same(payload, _order.Result);
        Assert.Equal(
            new[] { (Screen.PaymentResult, StateSource.Backend, (string?)"env-2") },
            _nav.NavigateToCalls);
    }

    [Fact]
    public void PaymentResult_KeepsTheSelectedDrinkForTheResultScreen()
    {
        var item = TestData.Item("Mojito");
        _order.Select(item);
        var sut = CreateSut();
        sut.Receive(new PaymentStartedMessage(Started()));

        sut.Receive(new PaymentResultMessage("env-9", Result()));

        Assert.Same(item, _order.SelectedDrink);
    }

    // ---------- cancelar ----------

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
    public void CancelCommand_AfterThePaymentStarted_TellsTheBackendWhichPaymentToCancel()
    {
        _order.Select(TestData.Item("Mojito"));
        var sut = CreateSut();
        sut.Receive(new PaymentStartedMessage(Started("pago-7")));
        _ws.Sent.Clear();

        sut.CancelCommand.Execute(null);

        var (type, payload) = Assert.Single(_ws.Sent);
        Assert.Equal(WsTypes.CancelPayment, type);
        Assert.Equal(new CancelPaymentPayload("pago-7"), payload);
    }

    [Fact]
    public void CancelCommand_BeforeThePaymentStarted_CancelsWithoutAnId()
    {
        _order.Select(TestData.Item("Mojito"));
        var sut = CreateSut();
        _ws.Sent.Clear();

        sut.CancelCommand.Execute(null);

        var (type, payload) = Assert.Single(_ws.Sent);
        Assert.Equal(WsTypes.CancelPayment, type);
        Assert.Equal(new CancelPaymentPayload(null), payload);
    }

    [Fact]
    public void CancelCommand_WithoutSelectedDrink_StillCancelsAndGoesBackToTheMenu()
    {
        var sut = CreateSut();

        sut.CancelCommand.Execute(null);

        Assert.Null(_order.SelectedDrink);
        Assert.Single(_nav.NavigateToCalls);
        Assert.Equal(Screen.Menu, _nav.NavigateToCalls[0].Screen);
        Assert.Equal(WsTypes.CancelPayment, Assert.Single(_ws.Sent).Type);
    }

    [Fact]
    public void CancelCommand_WithoutConnection_StillGoesBackToTheMenu()
    {
        _order.Select(TestData.Item("Mojito"));
        var sut = CreateSut(new FakeWsSender(result: false));

        sut.CancelCommand.Execute(null);

        Assert.Null(_order.SelectedDrink);
        Assert.Equal(Screen.Menu, Assert.Single(_nav.NavigateToCalls).Screen);
    }

    [Fact]
    public void CancelCommand_CanAlwaysExecute()
    {
        Assert.True(CreateSut().CancelCommand.CanExecute(null));
    }

    // ---------- messenger ----------

    [Fact]
    public void Constructor_ActivatesTheViewModel()
    {
        Assert.True(CreateSut().IsActive);
    }

    [Fact]
    public void MessengerMessages_ReachTheActiveViewModel()
    {
        _order.Select(TestData.Item("Mojito"));
        var sut = CreateSut();

        WeakReferenceMessenger.Default.Send(new PaymentStartedMessage(Started("pago-3")));
        WeakReferenceMessenger.Default.Send(new PaymentResultMessage("env-3", Result(PaymentOutcome.Approved, "pago-3")));

        Assert.True(sut.HasQr);
        Assert.Equal(PaymentOutcome.Approved, _order.Result?.Status);
        Assert.Equal(Screen.PaymentResult, Assert.Single(_nav.NavigateToCalls).Screen);
    }

    [Fact]
    public void MessengerMessages_DoNotReachADeactivatedViewModel()
    {
        var sut = CreateSut();
        sut.IsActive = false;

        WeakReferenceMessenger.Default.Send(new PaymentStartedMessage(Started()));
        WeakReferenceMessenger.Default.Send(new PaymentResultMessage("env-3", Result()));

        Assert.False(sut.HasQr);
        Assert.Null(_order.Result);
        Assert.Empty(_nav.NavigateToCalls);
    }
}
