using FrontEnd.InterfazHMI.Tests.Support;
using Frontend_interfaz_Multimodal_HMI.Models;
using Frontend_interfaz_Multimodal_HMI.Services;
using Frontend_interfaz_Multimodal_HMI.ViewModels;

namespace FrontEnd.InterfazHMI.Tests.Unit.ViewModels;

public class PaymentResultViewModelTests
{
    private const string CheckGlyph = "";
    private const string CrossGlyph = "";

    private readonly FakeNavigationService _nav = new();
    private readonly OrderSession _order = new();

    private PaymentResultViewModel CreateSut(PaymentOutcome? outcome = null, string? reason = null)
    {
        if (outcome is { } status)
        {
            _order.SetResult(new PaymentResultPayload("pago-1", status, reason));
        }
        return new PaymentResultViewModel(_nav, _order);
    }

    // ---------- textos según el resultado ----------

    [Fact]
    public void Approved_ShowsTheSuccessScreen()
    {
        var sut = CreateSut(PaymentOutcome.Approved);

        Assert.True(sut.IsSuccess);
        Assert.Equal(CheckGlyph, sut.IconGlyph);
        Assert.Equal("¡Pago realizado!", sut.Title);
        Assert.Equal("Estamos preparando tu bebida.", sut.Message);
        Assert.Equal("Nuevo pedido", sut.ButtonText);
    }

    [Fact]
    public void Rejected_ShowsThatNothingWasCharged()
    {
        var sut = CreateSut(PaymentOutcome.Rejected);

        Assert.False(sut.IsSuccess);
        Assert.Equal(CrossGlyph, sut.IconGlyph);
        Assert.Equal("Pago rechazado", sut.Title);
        Assert.Equal("No se realizó ningún cobro.", sut.Message);
        Assert.Equal("Volver al menú", sut.ButtonText);
    }

    [Fact]
    public void Expired_ShowsThatTheCodeExpired()
    {
        var sut = CreateSut(PaymentOutcome.Expired);

        Assert.False(sut.IsSuccess);
        Assert.Equal(CrossGlyph, sut.IconGlyph);
        Assert.Equal("El código expiró", sut.Title);
        Assert.Equal("Genera uno nuevo desde el menú.", sut.Message);
        Assert.Equal("Volver al menú", sut.ButtonText);
    }

    [Fact]
    public void Error_ShowsThatThePaymentCouldNotStart()
    {
        var sut = CreateSut(PaymentOutcome.Error);

        Assert.False(sut.IsSuccess);
        Assert.Equal(CrossGlyph, sut.IconGlyph);
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
        Assert.Equal(CrossGlyph, sut.IconGlyph);
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
