using FrontEnd.InterfazHMI.Tests.Support;
using Frontend_interfaz_Multimodal_HMI.Services;
using Frontend_interfaz_Multimodal_HMI.ViewModels;

namespace FrontEnd.InterfazHMI.Tests.Unit.Services;

// Crea MenuViewModel, que se registra en el messenger estático.
[Collection(MessengerCollection.Name)]
public class NavigationServiceTests : IDisposable
{
    /// <summary>IServiceProvider mínimo: devuelve instancias ya creadas y registra qué se pidió.</summary>
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
    }

    private readonly FakeServiceProvider _provider = new();
    private readonly NavigationService _sut;
    private readonly List<MenuViewModel> _menus = new();

    public NavigationServiceTests() => _sut = new NavigationService(_provider);

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
        var menu = new MenuViewModel(new CatalogStore());
        _menus.Add(menu);
        _provider.Add(menu);
        return menu;
    }

    // ---------- estado inicial ----------

    [Fact]
    public void NewService_HasNoCurrentScreen()
    {
        Assert.Null(_sut.Current);
    }

    // ---------- NavigateTo(string) ----------

    [Fact]
    public void NavigateTo_Welcome_SetsTheWelcomeViewModel()
    {
        var welcome = RegisterWelcome();

        _sut.NavigateTo("welcome");

        Assert.Same(welcome, _sut.Current);
    }

    [Fact]
    public void NavigateTo_Menu_SetsTheMenuViewModel()
    {
        var menu = RegisterMenu();

        _sut.NavigateTo("menu");

        Assert.Same(menu, _sut.Current);
    }

    [Theory]
    [InlineData("menu")]
    [InlineData("MENU")]
    [InlineData("Menu")]
    public void NavigateTo_Menu_IgnoresCase(string name)
    {
        var menu = RegisterMenu();

        _sut.NavigateTo(name);

        Assert.Same(menu, _sut.Current);
    }

    [Theory]
    [InlineData("welcome")]
    [InlineData("WELCOME")]
    [InlineData("Welcome")]
    public void NavigateTo_Welcome_IgnoresCase(string name)
    {
        var welcome = RegisterWelcome();

        _sut.NavigateTo(name);

        Assert.Same(welcome, _sut.Current);
    }

    [Fact]
    public void NavigateTo_ReplacesTheCurrentScreen()
    {
        var welcome = RegisterWelcome();
        var menu = RegisterMenu();

        _sut.NavigateTo("welcome");
        _sut.NavigateTo("menu");

        Assert.Same(menu, _sut.Current);
        Assert.NotSame(welcome, _sut.Current);
    }

    [Fact]
    public void NavigateTo_UnknownScreen_KeepsTheCurrentScreen()
    {
        var welcome = RegisterWelcome();
        _sut.NavigateTo("welcome");
        _provider.Requested.Clear();

        _sut.NavigateTo("no_existe");

        Assert.Same(welcome, _sut.Current);
        Assert.Empty(_provider.Requested);   // ni siquiera intentó resolver un ViewModel
    }

    [Fact]
    public void NavigateTo_UnknownScreen_WithNoPreviousScreen_LeavesCurrentNull()
    {
        _sut.NavigateTo("no_existe");

        Assert.Null(_sut.Current);
    }

    // ---------- NavigateTo<T>() ----------

    [Fact]
    public void NavigateToGeneric_ResolvesTheViewModelFromTheProvider()
    {
        var menu = RegisterMenu();

        _sut.NavigateTo<MenuViewModel>();

        Assert.Same(menu, _sut.Current);
    }

    [Fact]
    public void NavigateToGeneric_WhenTheViewModelIsNotRegistered_ThrowsAndKeepsTheCurrentScreen()
    {
        var welcome = RegisterWelcome();
        _sut.NavigateTo("welcome");

        Assert.Throws<InvalidOperationException>(() => _sut.NavigateTo<MenuViewModel>());

        Assert.Same(welcome, _sut.Current);
    }

    // ---------- PropertyChanged ----------

    [Fact]
    public void NavigateTo_RaisesPropertyChangedForCurrent()
    {
        RegisterWelcome();
        var changed = new List<string?>();
        _sut.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        _sut.NavigateTo("welcome");

        Assert.Equal(new[] { "Current" }, changed);
    }

    [Fact]
    public void NavigateTo_TheSameInstanceTwice_RaisesPropertyChangedOnlyOnce()
    {
        RegisterWelcome();
        var count = 0;
        _sut.PropertyChanged += (_, _) => count++;

        _sut.NavigateTo("welcome");
        _sut.NavigateTo("welcome");

        Assert.Equal(1, count);
    }

    [Fact]
    public void NavigateTo_UnknownScreen_DoesNotRaisePropertyChanged()
    {
        var count = 0;
        _sut.PropertyChanged += (_, _) => count++;

        _sut.NavigateTo("no_existe");

        Assert.Equal(0, count);
    }
}
