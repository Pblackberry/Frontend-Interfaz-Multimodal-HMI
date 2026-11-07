using CommunityToolkit.Mvvm.Messaging;
using FrontEnd.InterfazHMI.Tests.Support;
using Frontend_interfaz_Multimodal_HMI.Models;
using Frontend_interfaz_Multimodal_HMI.Services;
using Frontend_interfaz_Multimodal_HMI.ViewModels;

namespace FrontEnd.InterfazHMI.Tests.Unit.Services;

[Collection(MessengerCollection.Name)]
public class NavigationServiceLifecycleTests : IDisposable
{
    private readonly FakeNavigationService _fakeNav = new();
    private readonly OrderSession _order = new();
    private readonly MenuViewModel _menu;
    private readonly PaymentViewModel _payment;
    private readonly PaymentResultViewModel _result;
    private readonly WelcomeViewModel _welcome;
    private readonly NavigationService _sut;

    public NavigationServiceLifecycleTests()
    {
        WeakReferenceMessenger.Default.Reset();

        var store = new CatalogStore();
        _menu = new MenuViewModel(store, new FakeImageLoader(), _order, _fakeNav);
        _payment = new PaymentViewModel(_fakeNav, _order, new FakeWsSender(), new FakeQrFactory());
        _result = new PaymentResultViewModel(_fakeNav, _order);
        _welcome = new WelcomeViewModel(_fakeNav);
        _sut = new NavigationService(
            new MapServiceProvider().Add(_menu).Add(_payment).Add(_result).Add(_welcome));
    }

    public void Dispose()
    {
        _menu.IsActive = false;
        _payment.IsActive = false;
        WeakReferenceMessenger.Default.Reset();
    }

    [Fact]
    public void LeavingThePayment_DeactivatesIt()
    {
        _sut.NavigateTo(Screen.Payment);
        Assert.True(_payment.IsActive);

        _sut.NavigateTo(Screen.PaymentResult);

        Assert.False(_payment.IsActive);
    }

    [Fact]
    public void LeavingTheMenu_DeactivatesIt()
    {
        _sut.NavigateTo(Screen.Menu);
        Assert.True(_menu.IsActive);

        _sut.NavigateTo(Screen.Payment);

        Assert.False(_menu.IsActive);
    }

    [Fact]
    public void AfterLeavingThePayment_ItNoLongerReactsToTheBackendResult()
    {
        _sut.NavigateTo(Screen.Payment);
        _sut.NavigateTo(Screen.Menu);

        WeakReferenceMessenger.Default.Send(
            new PaymentResultMessage("env-1", new PaymentResultPayload("p", PaymentOutcome.Approved, null)));

        Assert.Null(_order.Result);
        Assert.Empty(_fakeNav.NavigateToCalls);
    }

    [Fact]
    public void NavigatingToTheScreenAlreadyShown_DoesNotDeactivateIt()
    {
        _sut.NavigateTo(Screen.Payment);

        _sut.NavigateTo(Screen.Payment, StateSource.Backend, "otra-vez");

        Assert.True(_payment.IsActive);
        Assert.Same(_payment, _sut.Current);
    }

    [Fact]
    public void LeavingAScreenThatIsNotARecipient_DoesNotFail()
    {
        _sut.NavigateTo(Screen.Welcome);

        var exception = Record.Exception(() => _sut.NavigateTo(Screen.Menu));

        Assert.Null(exception);
        Assert.Same(_menu, _sut.Current);
    }

    [Fact]
    public void FirstNavigation_HasNothingToDeactivate()
    {
        var exception = Record.Exception(() => _sut.NavigateTo(Screen.Menu));

        Assert.Null(exception);
    }

    [Fact]
    public void LeavingThePaymentResult_DoesNotFail()
    {
        _sut.NavigateTo(Screen.PaymentResult);

        var exception = Record.Exception(() => _sut.NavigateTo(Screen.Welcome));

        Assert.Null(exception);
        Assert.Same(_welcome, _sut.Current);
    }

    [Fact]
    public void UnknownScreen_DoesNotDeactivateTheCurrentOne()
    {
        _sut.NavigateTo(Screen.Menu);

        _sut.NavigateTo((Screen)99);

        Assert.True(_menu.IsActive);
        Assert.Same(_menu, _sut.Current);
    }
}
