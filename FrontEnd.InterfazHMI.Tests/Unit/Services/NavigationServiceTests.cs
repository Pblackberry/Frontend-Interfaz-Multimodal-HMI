using System;
using System.Collections.Generic;
using System.Text;
using Frontend_interfaz_Multimodal_HMI.Services;
using Frontend_interfaz_Multimodal_HMI.ViewModels;
using NSubstitute;

namespace FrontEnd.InterfazHMI.Tests.Unit.Services;
public class NavigationServiceTests
{
    private readonly IServiceProvider _serviceProvider = Substitute.For<IServiceProvider>();
    private readonly INavigationService _fakeNavigation = Substitute.For<INavigationService>();

    private NavigationService CreateSut() => new(_serviceProvider);

    /// <summary>Configura el proveedor para que resuelva un WelcomeViewModel.</summary>
    private WelcomeViewModel RegisterWelcomeViewModel()
    {
        var vm = new WelcomeViewModel(_fakeNavigation);
        _serviceProvider.GetService(typeof(WelcomeViewModel)).Returns(vm);
        return vm;
    }

    [Fact]
    public void Current_Initially_IsNull()
    {
        var sut = CreateSut();

        Assert.Null(sut.Current);
    }

    [Fact]
    public void NavigateToGeneric_SetsCurrentToResolvedViewModel()
    {
        var expected = RegisterWelcomeViewModel();
        var sut = CreateSut();

        sut.NavigateTo<WelcomeViewModel>();

        Assert.Same(expected, sut.Current);
    }

    [Fact]
    public void NavigateToGeneric_RaisesPropertyChangedForCurrent()
    {
        RegisterWelcomeViewModel();
        var sut = CreateSut();
        var changed = new List<string?>();
        sut.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        sut.NavigateTo<WelcomeViewModel>();

        Assert.Contains(nameof(NavigationService.Current), changed);
    }

    [Fact]
    public void NavigateToGeneric_WhenViewModelNotRegistered_Throws()
    {
        _serviceProvider.GetService(typeof(WelcomeViewModel)).Returns((object?)null);
        var sut = CreateSut();

        Assert.Throws<InvalidOperationException>(() => sut.NavigateTo<WelcomeViewModel>());
        Assert.Null(sut.Current);
    }

    [Theory]
    [InlineData("welcome")]
    [InlineData("WELCOME")]
    [InlineData("Welcome")]
    public void NavigateToString_Welcome_IsCaseInsensitive(string screen)
    {
        var expected = RegisterWelcomeViewModel();
        var sut = CreateSut();

        sut.NavigateTo(screen);

        Assert.Same(expected, sut.Current);
    }

    // OJO: "menu" esta comentado en NavigationService. Cuando implementes MenuViewModel,
    // quita "menu" de este Theory y agrega un test propio para esa pantalla.
    [Theory]
    [InlineData("menu")]
    [InlineData("pantalla-inexistente")]
    [InlineData("")]
    public void NavigateToString_UnknownScreen_DoesNotChangeCurrentNorThrow(string screen)
    {
        RegisterWelcomeViewModel();
        var sut = CreateSut();

        var exception = Record.Exception(() => sut.NavigateTo(screen));

        Assert.Null(exception);
        Assert.Null(sut.Current);
    }

    [Fact]
    public void NavigateToString_UnknownScreen_KeepsPreviousCurrent()
    {
        var welcome = RegisterWelcomeViewModel();
        var sut = CreateSut();
        sut.NavigateTo("welcome");

        sut.NavigateTo("pantalla-inexistente");

        Assert.Same(welcome, sut.Current);
    }
}
