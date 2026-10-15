using System;
using System.Collections.Generic;
using System.Text;
using Frontend_interfaz_Multimodal_HMI.Services;
using Frontend_interfaz_Multimodal_HMI.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace FrontEnd.InterfazHMI.Tests.Integration;

public class NavigationFlowTests
{
    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddSingleton<INavigationService, NavigationService>();
        services.AddSingleton<MainViewModel>();
        services.AddTransient<WelcomeViewModel>();
        return services.BuildServiceProvider();
    }

    [Fact]
    public void ResolvingMainViewModel_ShowsWelcomeScreen()
    {
        using var provider = BuildProvider();

        var main = provider.GetRequiredService<MainViewModel>();

        Assert.IsType<WelcomeViewModel>(main.Navigation.Current);
    }

    [Fact]
    public void GoHome_FromWelcome_ShowsWelcomeAgain()
    {
        using var provider = BuildProvider();
        var main = provider.GetRequiredService<MainViewModel>();
        var firstWelcome = main.Navigation.Current;

        main.GoHomeCommand.Execute(null);

        Assert.IsType<WelcomeViewModel>(main.Navigation.Current);
        Assert.NotSame(firstWelcome, main.Navigation.Current); // WelcomeViewModel es transient
    }

    [Fact]
    public void StartOrder_WhileMenuIsNotImplemented_KeepsCurrentScreen()
    {
        using var provider = BuildProvider();
        var main = provider.GetRequiredService<MainViewModel>();
        var welcome = Assert.IsType<WelcomeViewModel>(main.Navigation.Current);

        welcome.StartOrderCommand.Execute(null);

        // Cuando exista MenuViewModel, cambia esta asercion a Assert.IsType<MenuViewModel>(...)
        Assert.Same(welcome, main.Navigation.Current);
    }

    [Fact]
    public void Navigation_IsSharedBetweenViewModels()
    {
        using var provider = BuildProvider();
        var main = provider.GetRequiredService<MainViewModel>();
        var nav = provider.GetRequiredService<INavigationService>();

        Assert.Same(nav, main.Navigation);
    }
}
