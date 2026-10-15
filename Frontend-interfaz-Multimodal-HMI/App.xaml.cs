using System.Windows;
using Frontend_interfaz_Multimodal_HMI.Services;
using Frontend_interfaz_Multimodal_HMI.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace Frontend_interfaz_Multimodal_HMI;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private ServiceProvider? _services;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var sc = new ServiceCollection();
        sc.AddSingleton<INavigationService, NavigationService>();
        sc.AddSingleton<MainViewModel>();
        sc.AddTransient<WelcomeViewModel>();
        //sc.AddTransient<MenuViewModel>();
        _services = sc.BuildServiceProvider();

        var window = new MainWindow { DataContext = _services.GetRequiredService<MainViewModel>() };
        window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _services?.Dispose();
        base.OnExit(e);
    }
}
