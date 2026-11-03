using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Frontend_interfaz_Multimodal_HMI.Services;
using Frontend_interfaz_Multimodal_HMI.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace Frontend_interfaz_Multimodal_HMI;

public partial class App : Application
{
    private const string _wsUrl = "ws://localhost:8888/ws";

    private ServiceProvider? _services;
    private readonly CancellationTokenSource _cts = new();

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var sc = new ServiceCollection();
        sc.AddSingleton<INavigationService, NavigationService>();
        sc.AddSingleton<CatalogStore>();
        sc.AddSingleton(new WsClient(new Uri(_wsUrl)));
        sc.AddSingleton<MainViewModel>();
        sc.AddTransient<WelcomeViewModel>();
        sc.AddTransient<MenuViewModel>();
        sc.AddSingleton<BackendLink>();
        sc.AddSingleton<OrderSession>();
        sc.AddTransient<PaymentViewModel>();
        sc.AddSingleton<IImageLoader>(_ =>
        {
            var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("CobotBartenderHMI/1.0 (proyecto de titulacion)");
            return new ImageLoader(http);
        });

        _services = sc.BuildServiceProvider();
        _ = _services.GetRequiredService<BackendLink>();

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
