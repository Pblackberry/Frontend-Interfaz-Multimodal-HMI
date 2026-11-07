using FrontEnd.InterfazHMI.Tests.Support;
using Frontend_interfaz_Multimodal_HMI.Models;
using Frontend_interfaz_Multimodal_HMI.Services;

namespace FrontEnd.InterfazHMI.Tests.Unit.Services;

public class OrderSessionTests
{
    private readonly OrderSession _sut = new();

    private static PaymentResultPayload Result(PaymentOutcome status = PaymentOutcome.Approved, string? reason = null)
        => new("pago-1", status, reason);

    [Fact]
    public void NewSession_HasNoSelectedDrink()
    {
        Assert.Null(_sut.SelectedDrink);
    }

    [Fact]
    public void NewSession_HasNoPaymentResult()
    {
        Assert.Null(_sut.Result);
    }

    [Fact]
    public void Select_StoresTheSameInstance()
    {
        var item = TestData.Item("Mojito");

        _sut.Select(item);

        Assert.Same(item, _sut.SelectedDrink);
    }

    [Fact]
    public void Select_Twice_KeepsTheLastDrink()
    {
        var daiquiri = TestData.Item("Daiquiri");
        _sut.Select(TestData.Item("Mojito"));

        _sut.Select(daiquiri);

        Assert.Same(daiquiri, _sut.SelectedDrink);
    }

    [Fact]
    public void Clear_RemovesTheSelectedDrink()
    {
        _sut.Select(TestData.Item("Mojito"));

        _sut.Clear();

        Assert.Null(_sut.SelectedDrink);
    }

    [Fact]
    public void Clear_WithoutSelection_DoesNotThrow()
    {
        var exception = Record.Exception(() => _sut.Clear());

        Assert.Null(exception);
        Assert.Null(_sut.SelectedDrink);
        Assert.Null(_sut.Result);
    }

    // ---------- resultado del pago ----------

    [Fact]
    public void SetResult_StoresTheSameInstance()
    {
        var result = Result();

        _sut.SetResult(result);

        Assert.Same(result, _sut.Result);
    }

    [Fact]
    public void SetResult_DoesNotChangeTheSelectedDrink()
    {
        var item = TestData.Item("Mojito");
        _sut.Select(item);

        _sut.SetResult(Result());

        Assert.Same(item, _sut.SelectedDrink);
    }

    [Fact]
    public void SetResult_Twice_KeepsTheLastOne()
    {
        var last = Result(PaymentOutcome.Rejected);
        _sut.SetResult(Result(PaymentOutcome.Approved));

        _sut.SetResult(last);

        Assert.Same(last, _sut.Result);
    }

    [Fact]
    public void Select_DiscardsThePreviousPaymentResult()
    {
        _sut.Select(TestData.Item("Mojito"));
        _sut.SetResult(Result(PaymentOutcome.Approved));

        _sut.Select(TestData.Item("Daiquiri"));

        Assert.Null(_sut.Result);
    }

    [Fact]
    public void Clear_RemovesTheDrinkAndThePaymentResult()
    {
        _sut.Select(TestData.Item("Mojito"));
        _sut.SetResult(Result());

        _sut.Clear();

        Assert.Null(_sut.SelectedDrink);
        Assert.Null(_sut.Result);
    }

    [Fact]
    public void Clear_ThenSelect_StartsAFreshOrder()
    {
        _sut.Select(TestData.Item("Mojito"));
        _sut.SetResult(Result());
        _sut.Clear();

        var daiquiri = TestData.Item("Daiquiri");
        _sut.Select(daiquiri);

        Assert.Same(daiquiri, _sut.SelectedDrink);
        Assert.Null(_sut.Result);
    }
}
