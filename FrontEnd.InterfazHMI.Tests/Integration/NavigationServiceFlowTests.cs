using Frontend_interfaz_Multimodal_HMI.Services;
using Frontend_interfaz_Multimodal_HMI.ViewModels;
using FrontEnd.InterfazHMI.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace FrontEnd.InterfazHMI.Tests.Integration;

// MenuViewModel se registra en el messenger estático.
[Collection(MessengerCollection.Name)]
public class NavigationFlowTests
{
    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddSingleton<INavigationService, NavigationService>();
        services.AddSingleton<CatalogStore>();
        services.AddSingleton<MainViewModel>();
        services.AddTransient<WelcomeViewModel>();
        services.AddTransient<MenuViewModel>();
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
    public void StartOrder_FromWelcome_ShowsTheMenuScreen()
    {
        using var provider = BuildProvider();
        var main = provider.GetRequiredService<MainViewModel>();
        var welcome = Assert.IsType<WelcomeViewModel>(main.Navigation.Current);

        welcome.StartOrderCommand.Execute(null);

        Assert.IsType<MenuViewModel>(main.Navigation.Current);
    }

    [Fact]
    public void GoHome_FromMenu_ReturnsToWelcome()
    {
        using var provider = BuildProvider();
        var main = provider.GetRequiredService<MainViewModel>();
        main.Navigation.NavigateTo("menu");

        main.GoHomeCommand.Execute(null);

        Assert.IsType<WelcomeViewModel>(main.Navigation.Current);
    }

    [Fact]
    public void NavigateByName_ToMenuAndBack_UsesTheSameNamesTheBackendSends()
    {
        using var provider = BuildProvider();
        var main = provider.GetRequiredService<MainViewModel>();

        main.Navigation.NavigateTo("menu");
        Assert.IsType<MenuViewModel>(main.Navigation.Current);

        main.Navigation.NavigateTo("welcome");
        Assert.IsType<WelcomeViewModel>(main.Navigation.Current);
    }

    [Fact]
    public void Menu_ShowsTheCatalogThatTheStoreAlreadyHas()
    {
        using var provider = BuildProvider();
        var store = provider.GetRequiredService<CatalogStore>();
        store.Update(TestData.Payload("f1", TestData.Item("Mojito"), TestData.Item("Daiquiri")));
        var main = provider.GetRequiredService<MainViewModel>();

        main.Navigation.NavigateTo("menu");

        var menu = Assert.IsType<MenuViewModel>(main.Navigation.Current);
        Assert.Equal(new[] { "Mojito", "Daiquiri" }, menu.Items.Select(c => c.Name));
    }

    [Fact]
    public void Menu_IsANewInstanceEachTimeItIsOpened()
    {
        using var provider = BuildProvider();
        var main = provider.GetRequiredService<MainViewModel>();

        main.Navigation.NavigateTo("menu");
        var first = main.Navigation.Current;
        main.GoHomeCommand.Execute(null);
        main.Navigation.NavigateTo("menu");

        Assert.NotSame(first, main.Navigation.Current);   // MenuViewModel es transient
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
