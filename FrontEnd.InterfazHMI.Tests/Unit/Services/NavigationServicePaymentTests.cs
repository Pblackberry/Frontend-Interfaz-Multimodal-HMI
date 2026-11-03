using FrontEnd.InterfazHMI.Tests.Support;
using Frontend_interfaz_Multimodal_HMI.Models;
using Frontend_interfaz_Multimodal_HMI.Services;
using Frontend_interfaz_Multimodal_HMI.ViewModels;

namespace FrontEnd.InterfazHMI.Tests.Unit.Services;

public class NavigationServicePaymentTests
{
    private sealed class SingleServiceProvider : IServiceProvider
    {
        private readonly object _service;

        public SingleServiceProvider(object service) => _service = service;

        public object? GetService(Type serviceType) => serviceType == _service.GetType() ? _service : null;
    }

    private readonly PaymentViewModel _payment;
    private readonly NavigationService _sut;
    private readonly List<(Screen Screen, StateSource Source, string? OrderId)> _navigated = new();

    public NavigationServicePaymentTests()
    {
        _payment = new PaymentViewModel(new FakeNavigationService(), new OrderSession());
        _sut = new NavigationService(new SingleServiceProvider(_payment));
        _sut.Navigated += (screen, source, orderId) => _navigated.Add((screen, source, orderId));
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
}
