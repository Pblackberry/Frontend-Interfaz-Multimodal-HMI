using CommunityToolkit.Mvvm.Messaging;
using FrontEnd.InterfazHMI.Tests.Support;
using Frontend_interfaz_Multimodal_HMI.Models;
using Frontend_interfaz_Multimodal_HMI.Services;
using Frontend_interfaz_Multimodal_HMI.ViewModels;

namespace FrontEnd.InterfazHMI.Tests.Unit.Services;

[Collection(MessengerCollection.Name)]
public class NavigationServicePaymentTests : IDisposable
{
    private readonly PaymentViewModel _payment;
    private readonly PaymentResultViewModel _result;
    private readonly NavigationService _sut;
    private readonly List<(Screen Screen, StateSource Source, string? OrderId)> _navigated = new();

    public NavigationServicePaymentTests()
    {
        WeakReferenceMessenger.Default.Reset();
        _payment = new PaymentViewModel(
            new FakeNavigationService(), new OrderSession(), new FakeWsSender(), new FakeQrFactory());
        _result = new PaymentResultViewModel(new FakeNavigationService(), new OrderSession());
        _sut = new NavigationService(new MapServiceProvider().Add(_payment).Add(_result));
        _sut.Navigated += (screen, source, orderId) => _navigated.Add((screen, source, orderId));
    }

    public void Dispose()
    {
        _payment.IsActive = false;
        WeakReferenceMessenger.Default.Reset();
    }

    [Fact]
    public void NavigateToScreen_Payment_ShowsThePaymentViewModel()
    {
        _sut.NavigateTo(Screen.Payment);

        Assert.Same(_payment, _sut.Current);
        Assert.Equal(Screen.Payment, _sut.CurrentScreen);
    }

    [Fact]
    public void NavigateToScreen_Payment_IsAnnouncedWithSourceAndOrder()
    {
        _sut.NavigateTo(Screen.Payment, StateSource.Backend, "order-9");

        Assert.Equal(new[] { (Screen.Payment, StateSource.Backend, (string?)"order-9") }, _navigated);
    }

    [Theory]
    [InlineData("payment")]
    [InlineData("PAYMENT")]
    [InlineData("Payment")]
    public void NavigateToName_Payment_IsCaseInsensitive(string name)
    {
        _sut.NavigateTo(name);

        Assert.Same(_payment, _sut.Current);
        Assert.Equal(Screen.Payment, _sut.CurrentScreen);
    }

    [Fact]
    public void NavigateToGeneric_PaymentViewModel_BehavesLikeNavigatingToPayment()
    {
        _sut.NavigateTo<PaymentViewModel>();

        Assert.Same(_payment, _sut.Current);
        Assert.Equal(Screen.Payment, _sut.CurrentScreen);
        Assert.Equal(new[] { (Screen.Payment, StateSource.Touch, (string?)null) }, _navigated);
    }

    // ---------- pantalla de resultado ----------

    [Fact]
    public void NavigateToScreen_PaymentResult_ShowsThePaymentResultViewModel()
    {
        _sut.NavigateTo(Screen.PaymentResult);

        Assert.Same(_result, _sut.Current);
        Assert.Equal(Screen.PaymentResult, _sut.CurrentScreen);
    }

    [Fact]
    public void NavigateToScreen_PaymentResult_IsAnnouncedWithTheBackendSourceAndTheEnvelopeId()
    {
        _sut.NavigateTo(Screen.PaymentResult, StateSource.Backend, "env-4");

        Assert.Equal(new[] { (Screen.PaymentResult, StateSource.Backend, (string?)"env-4") }, _navigated);
    }

    [Theory]
    [InlineData("PaymentResult")]
    [InlineData("paymentresult")]
    public void NavigateToName_PaymentResult_UsesTheEnumName(string name)
    {
        _sut.NavigateTo(name);

        Assert.Same(_result, _sut.Current);
        Assert.Equal(Screen.PaymentResult, _sut.CurrentScreen);
    }

    [Fact]
    public void NavigateToGeneric_PaymentResultViewModel_BehavesLikeNavigatingToPaymentResult()
    {
        _sut.NavigateTo<PaymentResultViewModel>();

        Assert.Same(_result, _sut.Current);
        Assert.Equal(Screen.PaymentResult, _sut.CurrentScreen);
    }

    [Fact]
    public void FromPaymentToItsResult_ShowsTheResultAndAnnouncesBothScreens()
    {
        _sut.NavigateTo(Screen.Payment);

        _sut.NavigateTo(Screen.PaymentResult, StateSource.Backend, "env-4");

        Assert.Same(_result, _sut.Current);
        Assert.Equal(
            new[]
            {
                (Screen.Payment, StateSource.Touch, (string?)null),
                (Screen.PaymentResult, StateSource.Backend, (string?)"env-4"),
            },
            _navigated);
    }
}
