using System;                                   // NUEVO
using System.Threading;                         // NUEVO
using System.Threading.Tasks;                   // NUEVO
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
    // Ajusta host, puerto y ruta a los de tu backend
    private const string WsUrl = "ws://localhost:8888/ws";

    private ServiceProvider? _services;
    private readonly CancellationTokenSource _cts = new();

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var sc = new ServiceCollection();
        sc.AddSingleton<INavigationService, NavigationService>();
        sc.AddSingleton<CatalogStore>();
        sc.AddSingleton(new WsClient(new Uri(WsUrl)));
        sc.AddSingleton<MainViewModel>();
        sc.AddTransient<WelcomeViewModel>();
        sc.AddTransient<MenuViewModel>();
        _services = sc.BuildServiceProvider();

        var store = _services.GetRequiredService<CatalogStore>();
        var ws = _services.GetRequiredService<WsClient>();
        ws.CatalogUpdated += store.Update;
        _ = Task.Run(() => ws.RunAsync(_cts.Token));

        var window = new MainWindow { DataContext = _services.GetRequiredService<MainViewModel>() };
        window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _cts.Cancel();
        _services?.Dispose();
        base.OnExit(e);
    }
}
