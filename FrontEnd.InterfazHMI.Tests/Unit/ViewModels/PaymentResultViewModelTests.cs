using System.Globalization;
using CommunityToolkit.Mvvm.Messaging;
using FrontEnd.InterfazHMI.Tests.Support;
using Frontend_interfaz_Multimodal_HMI.Models;
using Frontend_interfaz_Multimodal_HMI.Services;
using Frontend_interfaz_Multimodal_HMI.ViewModels;

namespace FrontEnd.InterfazHMI.Tests.Unit.ViewModels;

[Collection(MessengerCollection.Name)]
public class PaymentResultViewModelTests : IDisposable
{
    private const string _checkGlyph = "\uE73E";
    private const string _crossGlyph = "\uE711";

    private readonly FakeNavigationService _nav = new();
    private readonly OrderSession _order = new();

    public PaymentResultViewModelTests() => WeakReferenceMessenger.Default.Reset();

    public void Dispose() => WeakReferenceMessenger.Default.Reset();

    private PaymentResultViewModel CreateSut(
        PaymentOutcome? outcome = null, string? reason = null, TimeSpan? readyDelay = null)
    {
        if (outcome is { } status)
        {
            _order.SetResult(new PaymentResultPayload("pago-1", status, reason));
        }
        return new PaymentResultViewModel(_nav, _order, readyDelay);
    }

    private static CobotStatusMessage Cobot(int codigo)
        => new(new CobotStatusPayload(codigo, "info", "evento " + codigo));

    // ---------- textos según el resultado ----------

    [Fact]
    public void Approved_ShowsTheSuccessScreen()
    {
        var sut = CreateSut(PaymentOutcome.Approved);

        Assert.True(sut.IsSuccess);
        Assert.Equal(_checkGlyph, sut.IconGlyph);
        Assert.Equal("¡Pago realizado!", sut.Title);
        Assert.Equal("Estamos preparando tu bebida.", sut.Message);
        Assert.Equal("Nuevo pedido", sut.ButtonText);
    }

    [Fact]
    public void Rejected_ShowsThatNothingWasCharged()
    {
        var sut = CreateSut(PaymentOutcome.Rejected);

        Assert.False(sut.IsSuccess);
        Assert.Equal(_crossGlyph, sut.IconGlyph);
        Assert.Equal("Pago rechazado", sut.Title);
        Assert.Equal("No se realizó ningún cobro.", sut.Message);
        Assert.Equal("Volver al menú", sut.ButtonText);
    }

    [Fact]
    public void Expired_ShowsThatTheCodeExpired()
    {
        var sut = CreateSut(PaymentOutcome.Expired);

        Assert.False(sut.IsSuccess);
        Assert.Equal(_crossGlyph, sut.IconGlyph);
        Assert.Equal("El código expiró", sut.Title);
        Assert.Equal("Genera uno nuevo desde el menú.", sut.Message);
        Assert.Equal("Volver al menú", sut.ButtonText);
    }

    [Fact]
    public void Error_ShowsThatThePaymentCouldNotStart()
    {
        var sut = CreateSut(PaymentOutcome.Error);

        Assert.False(sut.IsSuccess);
        Assert.Equal(_crossGlyph, sut.IconGlyph);
        Assert.Equal("No pudimos iniciar el pago", sut.Title);
        Assert.Equal("Inténtalo de nuevo.", sut.Message);
        Assert.Equal("Volver al menú", sut.ButtonText);
    }

    [Fact]
    public void Error_WithDrinkUnavailable_SaysTheDrinkIsNoLongerAvailable()
    {
        var sut = CreateSut(PaymentOutcome.Error, reason: "drink_unavailable");

        Assert.Equal("No pudimos iniciar el pago", sut.Title);
        Assert.Equal("Esa bebida ya no está disponible.", sut.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("otra_razon")]
    [InlineData("DRINK_UNAVAILABLE")]
    public void Error_WithAnyOtherReason_AsksToTryAgain(string? reason)
    {
        Assert.Equal("Inténtalo de nuevo.", CreateSut(PaymentOutcome.Error, reason).Message);
    }

    [Theory]
    [InlineData(PaymentOutcome.Approved)]
    [InlineData(PaymentOutcome.Rejected)]
    [InlineData(PaymentOutcome.Expired)]
    public void TheReason_IsIgnoredWhenThePaymentWasNotAnError(PaymentOutcome outcome)
    {
        var withReason = CreateSut(outcome, "drink_unavailable");
        var withoutReason = new PaymentResultViewModel(_nav, ResultOf(outcome));

        Assert.Equal(withoutReason.Message, withReason.Message);
        Assert.Equal(withoutReason.Title, withReason.Title);
    }

    private static OrderSession ResultOf(PaymentOutcome outcome)
    {
        var order = new OrderSession();
        order.SetResult(new PaymentResultPayload("pago-1", outcome, null));
        return order;
    }

    [Fact]
    public void WithoutAResult_ShowsTheGenericErrorScreen()
    {
        var sut = CreateSut();

        Assert.False(sut.IsSuccess);
        Assert.Equal(_crossGlyph, sut.IconGlyph);
        Assert.Equal("No pudimos iniciar el pago", sut.Title);
        Assert.Equal("Inténtalo de nuevo.", sut.Message);
        Assert.Equal("Volver al menú", sut.ButtonText);
    }

    [Fact]
    public void OnlyAnApprovedPayment_IsASuccess()
    {
        foreach (var outcome in Enum.GetValues<PaymentOutcome>())
        {
            var sut = new PaymentResultViewModel(_nav, ResultOf(outcome));

            Assert.Equal(outcome == PaymentOutcome.Approved, sut.IsSuccess);
        }
    }

    [Fact]
    public void IsFailure_IsTheOppositeOfIsSuccess_ForEveryOutcome()
    {
        foreach (var outcome in Enum.GetValues<PaymentOutcome>())
        {
            var sut = new PaymentResultViewModel(_nav, ResultOf(outcome));

            Assert.Equal(!sut.IsSuccess, sut.IsFailure);
            Assert.Equal(outcome != PaymentOutcome.Approved, sut.IsFailure);
        }
    }

    [Fact]
    public void WithoutAResult_IsAFailure()
    {
        Assert.True(CreateSut().IsFailure);
    }

    [Fact]
    public void EveryOutcome_HasATitleAMessageAndAButton()
    {
        foreach (var outcome in Enum.GetValues<PaymentOutcome>())
        {
            var sut = new PaymentResultViewModel(_nav, ResultOf(outcome));

            Assert.NotEqual("", sut.Title);
            Assert.NotEqual("", sut.Message);
            Assert.NotEqual("", sut.ButtonText);
            Assert.NotEqual("", sut.IconGlyph);
        }
    }

    [Fact]
    public void Texts_AreTakenWhenCreated_NotUpdatedLater()
    {
        var sut = CreateSut(PaymentOutcome.Approved);

        _order.SetResult(new PaymentResultPayload("pago-2", PaymentOutcome.Rejected, null));

        Assert.True(sut.IsSuccess);
        Assert.Equal("¡Pago realizado!", sut.Title);
    }

    // ---------- bebida y precio ----------

    [Fact]
    public void WithASelectedDrink_ShowsItsNameAndThePriceWithTwoDecimals()
    {
        var item = TestData.Item("Mojito");
        _order.Select(item);

        var sut = CreateSut(PaymentOutcome.Approved);

        Assert.Equal(item.NombreItem, sut.DrinkName);
        Assert.Equal(item.PrecioItem.ToString("0.00", CultureInfo.InvariantCulture), sut.PriceText);
        Assert.DoesNotContain(",", sut.PriceText);
    }

    [Fact]
    public void WithoutASelectedDrink_NameAndPriceAreEmpty()
    {
        var sut = CreateSut(PaymentOutcome.Approved);

        Assert.Equal("", sut.DrinkName);
        Assert.Equal("", sut.PriceText);
    }

    [Fact]
    public void DrinkAndPrice_AreTakenWhenCreated_NotUpdatedLater()
    {
        var item = TestData.Item("Mojito");
        _order.Select(item);
        var sut = CreateSut(PaymentOutcome.Approved);

        _order.Clear();

        Assert.Equal(item.NombreItem, sut.DrinkName);
        Assert.NotEqual("", sut.PriceText);
    }

    // ---------- fases del pedido ----------

    [Fact]
    public void Phase_StartsAsPaid()
    {
        var sut = CreateSut(PaymentOutcome.Approved);

        Assert.Equal(OrderPhase.Paid, sut.Phase);
        Assert.False(sut.IsPreparing);
        Assert.False(sut.IsReady);
    }

    [Fact]
    public void CobotCode102_MovesAnApprovedOrderToPreparing()
    {
        var sut = CreateSut(PaymentOutcome.Approved);

        sut.Receive(Cobot(102));

        Assert.Equal(OrderPhase.Preparing, sut.Phase);
        Assert.True(sut.IsPreparing);
        Assert.False(sut.IsReady);
    }

    [Fact]
    public void ChangingThePhase_NotifiesIsPreparingAndIsReady()
    {
        var sut = CreateSut(PaymentOutcome.Approved);
        var changed = new List<string?>();
        sut.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        sut.Receive(Cobot(102));

        Assert.Contains(nameof(PaymentResultViewModel.Phase), changed);
        Assert.Contains(nameof(PaymentResultViewModel.IsPreparing), changed);
        Assert.Contains(nameof(PaymentResultViewModel.IsReady), changed);
    }

    [Fact]
    public void CobotCode200_ShowsTheReadyScreenRightAway()
    {
        var sut = CreateSut(PaymentOutcome.Approved, readyDelay: TimeSpan.FromMinutes(5));

        sut.Receive(Cobot(200));

        Assert.Equal(OrderPhase.Ready, sut.Phase);
        Assert.True(sut.IsReady);
        Assert.False(sut.IsPreparing);
    }

    [Fact]
    public void CobotCode200_KeepsTheOrderUntilTheReadyScreenTimeIsOver()
    {
        _order.Select(TestData.Item("Mojito"));
        var sut = CreateSut(PaymentOutcome.Approved, readyDelay: TimeSpan.FromMinutes(5));

        sut.Receive(Cobot(200));

        Assert.Empty(_nav.NavigateToCalls);
        Assert.NotNull(_order.SelectedDrink);
        Assert.NotNull(_order.Result);
    }

    [Fact]
    public void CobotCode200_AfterTheReadyScreenTime_ClearsTheOrderAndGoesToTheWelcomeScreen()
    {
        _order.Select(TestData.Item("Mojito"));
        var sut = CreateSut(PaymentOutcome.Approved, readyDelay: TimeSpan.Zero);

        sut.Receive(Cobot(200));

        Assert.Equal(new[] { (Screen.Welcome, StateSource.Touch, (string?)null) }, _nav.NavigateToCalls);
        Assert.Null(_order.SelectedDrink);
        Assert.Null(_order.Result);
        Assert.Equal(OrderPhase.Ready, sut.Phase);
    }

    [Fact]
    public async Task CobotCode200_IfTheViewModelWasDeactivatedMeanwhile_DoesNotNavigate()
    {
        _order.Select(TestData.Item("Mojito"));
        var sut = CreateSut(PaymentOutcome.Approved, readyDelay: TimeSpan.FromMilliseconds(150));

        sut.Receive(Cobot(200));
        sut.IsActive = false;
        await Task.Delay(600);

        Assert.Empty(_nav.NavigateToCalls);
        Assert.NotNull(_order.SelectedDrink);
    }

    [Fact]
    public void ThePreparingAndReadyCodes_InOrder_ReachTheFinalPhase()
    {
        var sut = CreateSut(PaymentOutcome.Approved, readyDelay: TimeSpan.FromMinutes(5));

        sut.Receive(Cobot(102));
        Assert.Equal(OrderPhase.Preparing, sut.Phase);

        sut.Receive(Cobot(200));
        Assert.Equal(OrderPhase.Ready, sut.Phase);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    [InlineData(103)]
    [InlineData(999)]
    public void AnyOtherCobotCode_DoesNotChangeThePhaseOrNavigate(int codigo)
    {
        var sut = CreateSut(PaymentOutcome.Approved, readyDelay: TimeSpan.Zero);

        sut.Receive(Cobot(codigo));

        Assert.Equal(OrderPhase.Paid, sut.Phase);
        Assert.Empty(_nav.NavigateToCalls);
    }

    [Theory]
    [InlineData(PaymentOutcome.Rejected, 102)]
    [InlineData(PaymentOutcome.Rejected, 200)]
    [InlineData(PaymentOutcome.Expired, 102)]
    [InlineData(PaymentOutcome.Expired, 200)]
    [InlineData(PaymentOutcome.Error, 102)]
    [InlineData(PaymentOutcome.Error, 200)]
    public void CobotMessages_AreIgnoredWhenThePaymentFailed(PaymentOutcome outcome, int codigo)
    {
        _order.Select(TestData.Item("Mojito"));
        var sut = CreateSut(outcome, readyDelay: TimeSpan.Zero);

        sut.Receive(Cobot(codigo));

        Assert.Equal(OrderPhase.Paid, sut.Phase);
        Assert.Empty(_nav.NavigateToCalls);
        Assert.NotNull(_order.SelectedDrink);
    }

    [Fact]
    public void WithoutAResult_CobotMessagesAreIgnored()
    {
        var sut = CreateSut(readyDelay: TimeSpan.Zero);

        sut.Receive(Cobot(102));
        sut.Receive(Cobot(200));

        Assert.Equal(OrderPhase.Paid, sut.Phase);
        Assert.Empty(_nav.NavigateToCalls);
    }

    // ---------- mensajes por el messenger ----------

    [Fact]
    public void ItReceivesTheCobotStatusMessages_SentThroughTheMessenger()
    {
        var sut = CreateSut(PaymentOutcome.Approved);

        WeakReferenceMessenger.Default.Send(Cobot(102));

        Assert.Equal(OrderPhase.Preparing, sut.Phase);
    }

    [Fact]
    public void AfterBeingDeactivated_ItNoLongerReceivesCobotStatusMessages()
    {
        var sut = CreateSut(PaymentOutcome.Approved);
        sut.IsActive = false;

        WeakReferenceMessenger.Default.Send(Cobot(102));

        Assert.Equal(OrderPhase.Paid, sut.Phase);
    }

    // ---------- botón ----------

    [Fact]
    public void Continue_AfterAnApprovedPayment_StartsOverFromTheWelcomeScreen()
    {
        _order.Select(TestData.Item("Mojito"));
        var sut = CreateSut(PaymentOutcome.Approved);

        sut.ContinueCommand.Execute(null);

        Assert.Equal(new[] { (Screen.Welcome, StateSource.Touch, (string?)null) }, _nav.NavigateToCalls);
    }

    [Theory]
    [InlineData(PaymentOutcome.Rejected)]
    [InlineData(PaymentOutcome.Expired)]
    [InlineData(PaymentOutcome.Error)]
    public void Continue_AfterAFailedPayment_GoesBackToTheMenu(PaymentOutcome outcome)
    {
        _order.Select(TestData.Item("Mojito"));
        var sut = CreateSut(outcome);

        sut.ContinueCommand.Execute(null);

        Assert.Equal(new[] { (Screen.Menu, StateSource.Touch, (string?)null) }, _nav.NavigateToCalls);
    }

    [Fact]
    public void Continue_WithoutAResult_GoesBackToTheMenu()
    {
        var sut = CreateSut();

        sut.ContinueCommand.Execute(null);

        Assert.Equal(Screen.Menu, Assert.Single(_nav.NavigateToCalls).Screen);
    }

    [Theory]
    [InlineData(PaymentOutcome.Approved)]
    [InlineData(PaymentOutcome.Rejected)]
    [InlineData(PaymentOutcome.Expired)]
    [InlineData(PaymentOutcome.Error)]
    public void Continue_ClearsTheDrinkAndTheResult(PaymentOutcome outcome)
    {
        _order.Select(TestData.Item("Mojito"));
        var sut = CreateSut(outcome);

        sut.ContinueCommand.Execute(null);

        Assert.Null(_order.SelectedDrink);
        Assert.Null(_order.Result);
    }

    [Fact]
    public void Continue_CanAlwaysExecute()
    {
        Assert.True(CreateSut(PaymentOutcome.Approved).ContinueCommand.CanExecute(null));
    }

    [Fact]
    public void Continue_AfterTheOrderWasClearedElsewhere_StillUsesTheOutcomeShown()
    {
        var sut = CreateSut(PaymentOutcome.Approved);
        _order.Clear();

        sut.ContinueCommand.Execute(null);

        Assert.Equal(Screen.Welcome, Assert.Single(_nav.NavigateToCalls).Screen);
    }
}
