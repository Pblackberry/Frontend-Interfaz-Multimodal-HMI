using CommunityToolkit.Mvvm.ComponentModel;
using FrontEnd.InterfazHMI.Tests.Support;
using Frontend_interfaz_Multimodal_HMI.Models;
using Frontend_interfaz_Multimodal_HMI.Services;
using Frontend_interfaz_Multimodal_HMI.ViewModels;

namespace FrontEnd.InterfazHMI.Tests.Unit.Services;

[Collection(MessengerCollection.Name)]
public class NavigationServiceScreenTests : IDisposable
{
    private sealed class FakeServiceProvider : IServiceProvider
    {
        private readonly Dictionary<Type, object> _services = new();

        public List<Type> Requested { get; } = new();

        public void Add<T>(T instance) where T : notnull => _services[typeof(T)] = instance;

        public object? GetService(Type serviceType)
        {
            Requested.Add(serviceType);
            return _services.TryGetValue(serviceType, out var service) ? service : null;
        }

        public int RequestsFor<T>() => Requested.Count(t => t == typeof(T));
    }

    private sealed class OtherViewModel : ObservableObject { }

    private readonly FakeServiceProvider _provider = new();
    private readonly NavigationService _sut;
    private readonly List<MenuViewModel> _menus = new();
    private readonly List<(Screen Screen, StateSource Source, string? OrderId)> _navigated = new();

    public NavigationServiceScreenTests()
    {
        _sut = new NavigationService(_provider);
        _sut.Navigated += (screen, source, orderId) => _navigated.Add((screen, source, orderId));
    }

    public void Dispose()
    {
        foreach (var menu in _menus)
        {
            menu.IsActive = false;
        }
    }

    private WelcomeViewModel RegisterWelcome()
    {
        var welcome = new WelcomeViewModel(_sut);
        _provider.Add(welcome);
        return welcome;
    }

    private MenuViewModel RegisterMenu()
    {
        var menu = new MenuViewModel(new CatalogStore(), new FakeImageLoader());
        _menus.Add(menu);
        _provider.Add(menu);
        return menu;
    }


    [Fact]
    public void NewService_HasNoCurrentScreenAndNoEvents()
    {
        Assert.Null(_sut.CurrentScreen);
        Assert.Empty(_navigated);
    }


    [Fact]
    public void NavigateToScreen_SetsTheViewModelAndTheCurrentScreen()
    {
        var menu = RegisterMenu();

        _sut.NavigateTo(Screen.Menu);

        Assert.Same(menu, _sut.Current);
        Assert.Equal(Screen.Menu, _sut.CurrentScreen);
    }

    [Fact]
    public void NavigateToScreen_ByDefaultReportsATouchNavigationWithoutOrder()
    {
        RegisterWelcome();

        _sut.NavigateTo(Screen.Welcome);

        Assert.Equal(new[] { (Screen.Welcome, StateSource.Touch, (string?)null) }, _navigated);
    }

    [Theory]
    [InlineData(StateSource.Touch, null)]
    [InlineData(StateSource.Init, null)]
    [InlineData(StateSource.Backend, "order-7")]
    public void NavigateToScreen_ReportsTheGivenSourceAndOrderId(StateSource source, string? orderId)
    {
        RegisterMenu();

        _sut.NavigateTo(Screen.Menu, source, orderId);

        Assert.Equal(new[] { (Screen.Menu, source, orderId) }, _navigated);
    }

    [Fact]
    public void NavigateToScreen_ToTheSameScreen_DoesNotRecreateTheViewModel()
    {
        var welcome = RegisterWelcome();
        _sut.NavigateTo(Screen.Welcome);

        _sut.NavigateTo(Screen.Welcome);

        Assert.Same(welcome, _sut.Current);
        Assert.Equal(1, _provider.RequestsFor<WelcomeViewModel>());
    }

    [Fact]
    public void NavigateToScreen_ToTheSameScreen_IsStillAnnounced()
    {
        RegisterWelcome();

        _sut.NavigateTo(Screen.Welcome);
        _sut.NavigateTo(Screen.Welcome);

        Assert.Equal(2, _navigated.Count);
    }

    [Fact]
    public void NavigateToScreen_ToTheSameScreenByBackendOrder_AnnouncesTheNewSourceAndOrder()
    {
        RegisterMenu();
        _sut.NavigateTo(Screen.Menu);

        _sut.NavigateTo(Screen.Menu, StateSource.Backend, "order-3");

        Assert.Equal((Screen.Menu, StateSource.Backend, "order-3"), _navigated[^1]);
    }

    [Fact]
    public void NavigateToScreen_AwayAndBack_ResolvesTheViewModelAgain()
    {
        RegisterWelcome();
        RegisterMenu();

        _sut.NavigateTo(Screen.Welcome);
        _sut.NavigateTo(Screen.Menu);
        _sut.NavigateTo(Screen.Welcome);

        Assert.Equal(2, _provider.RequestsFor<WelcomeViewModel>());
        Assert.Equal(Screen.Welcome, _sut.CurrentScreen);
    }

    [Fact]
    public void NavigateToScreen_AnnouncesOnlyAfterCurrentAndCurrentScreenAreUpdated()
    {
        var menu = RegisterMenu();
        ObservableObject? currentSeen = null;
        Screen? screenSeen = null;
        _sut.Navigated += (_, _, _) =>
        {
            currentSeen = _sut.Current;
            screenSeen = _sut.CurrentScreen;
        };

        _sut.NavigateTo(Screen.Menu);

        Assert.Same(menu, currentSeen);
        Assert.Equal(Screen.Menu, screenSeen);
    }

    [Fact]
    public void NavigateToScreen_NotifiesEverySubscriber()
    {
        RegisterWelcome();
        var otherSubscriberCalls = 0;
        _sut.Navigated += (_, _, _) => otherSubscriberCalls++;

        _sut.NavigateTo(Screen.Welcome);

        Assert.Single(_navigated);
        Assert.Equal(1, otherSubscriberCalls);
    }

    [Fact]
    public void NavigateToScreen_UndefinedValue_IsIgnored()
    {
        var welcome = RegisterWelcome();
        _sut.NavigateTo(Screen.Welcome);
        _navigated.Clear();

        _sut.NavigateTo((Screen)99);

        Assert.Same(welcome, _sut.Current);
        Assert.Equal(Screen.Welcome, _sut.CurrentScreen);
        Assert.Empty(_navigated);
    }

    [Fact]
    public void NavigateToScreen_WhenTheViewModelIsNotRegistered_ThrowsAndKeepsTheState()
    {
        var welcome = RegisterWelcome();
        _sut.NavigateTo(Screen.Welcome);
        _navigated.Clear();

        Assert.Throws<InvalidOperationException>(() => _sut.NavigateTo(Screen.Menu));

        Assert.Same(welcome, _sut.Current);
        Assert.Equal(Screen.Welcome, _sut.CurrentScreen);
        Assert.Empty(_navigated);
    }


    [Theory]
    [InlineData("menu", Screen.Menu)]
    [InlineData("MENU", Screen.Menu)]
    [InlineData("welcome", Screen.Welcome)]
    [InlineData("Welcome", Screen.Welcome)]
    public void NavigateToName_ReportsATouchNavigationToThatScreen(string name, Screen expected)
    {
        RegisterWelcome();
        RegisterMenu();

        _sut.NavigateTo(name);

        Assert.Equal(new[] { (expected, StateSource.Touch, (string?)null) }, _navigated);
        Assert.Equal(expected, _sut.CurrentScreen);
    }

    [Theory]
    [InlineData("no_existe")]
    [InlineData("welome")]
    [InlineData("")]
    [InlineData("5")]
    public void NavigateToName_UnknownName_IsIgnored(string name)
    {
        var welcome = RegisterWelcome();
        _sut.NavigateTo(Screen.Welcome);
        _navigated.Clear();

        _sut.NavigateTo(name);

        Assert.Same(welcome, _sut.Current);
        Assert.Equal(Screen.Welcome, _sut.CurrentScreen);
        Assert.Empty(_navigated);
    }


    [Fact]
    public void NavigateToGeneric_WithAViewModelThatHasAScreen_BehavesLikeNavigatingToThatScreen()
    {
        var menu = RegisterMenu();

        _sut.NavigateTo<MenuViewModel>();

        Assert.Same(menu, _sut.Current);
        Assert.Equal(Screen.Menu, _sut.CurrentScreen);
        Assert.Equal(new[] { (Screen.Menu, StateSource.Touch, (string?)null) }, _navigated);
    }

    [Fact]
    public void NavigateToGeneric_WithAViewModelWithoutAScreen_ShowsItButDoesNotReportIt()
    {
        RegisterMenu();
        _sut.NavigateTo(Screen.Menu);
        _navigated.Clear();
        var other = new OtherViewModel();
        _provider.Add(other);

        _sut.NavigateTo<OtherViewModel>();

        Assert.Same(other, _sut.Current);
        Assert.Null(_sut.CurrentScreen);
        Assert.Empty(_navigated);
    }

    [Fact]
    public void NavigateToScreen_AfterAViewModelWithoutAScreen_ResolvesTheViewModelAgain()
    {
        RegisterWelcome();
        _provider.Add(new OtherViewModel());
        _sut.NavigateTo(Screen.Welcome);
        _sut.NavigateTo<OtherViewModel>();

        _sut.NavigateTo(Screen.Welcome);

        Assert.Equal(2, _provider.RequestsFor<WelcomeViewModel>());
        Assert.Equal(Screen.Welcome, _sut.CurrentScreen);
    }

    [Fact]
    public void NavigateToGeneric_WhenTheViewModelIsNotRegistered_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => _sut.NavigateTo<OtherViewModel>());

        Assert.Null(_sut.Current);
    }
}
