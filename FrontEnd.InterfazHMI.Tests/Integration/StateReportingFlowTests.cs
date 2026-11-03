using System.Text.Json;
using FrontEnd.InterfazHMI.Tests.Support;
using Frontend_interfaz_Multimodal_HMI.Models;
using Frontend_interfaz_Multimodal_HMI.Services;
using Frontend_interfaz_Multimodal_HMI.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace FrontEnd.InterfazHMI.Tests.Integration;

[Collection(MessengerCollection.Name)]
public class StateReportingFlowTests
{
    private static readonly TimeSpan _noMessageWait = TimeSpan.FromMilliseconds(400);

    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddSingleton<INavigationService, NavigationService>();
        services.AddSingleton<CatalogStore>();
        services.AddSingleton<IImageLoader, FakeImageLoader>();
        services.AddSingleton<OrderSession>();
        services.AddSingleton<MainViewModel>();
        services.AddTransient<WelcomeViewModel>();
        services.AddTransient<MenuViewModel>();
        return services.BuildServiceProvider();
    }

    private static (string Screen, string Source, string? Ref) ReadState(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        Assert.Equal("state", root.GetProperty("type").GetString());
        var payload = root.GetProperty("payload");
        var reference = payload.GetProperty("ref");
        return (
            payload.GetProperty("screen").GetString()!,
            payload.GetProperty("source").GetString()!,
            reference.ValueKind == JsonValueKind.Null ? null : reference.GetString());
    }

    private static async Task<(string Screen, string Source, string? Ref)> NextStateAsync(WsClientSession session)
        => ReadState(await FakeWsServer.ReceiveTextAsync(session.ServerSocket));

    private static async Task<(ServiceProvider Provider, MainViewModel Main, WsClientSession Session, BackendLink Link)>
        StartAppAsync(FakeWsServer server)
    {
        var provider = BuildProvider();
        var main = provider.GetRequiredService<MainViewModel>();
        var session = await WsClientSession.StartAsync(server);
        var link = new BackendLink(session.Client, provider.GetRequiredService<INavigationService>());
        return (provider, main, session, link);
    }

    [Fact]
    public async Task TappingStartOrder_ReportsTheMenuScreenAsTouch()
    {
        await using var server = new FakeWsServer();
        var (provider, main, session, _) = await StartAppAsync(server);
        await using var _s = session;
        using var _p = provider;
        var welcome = Assert.IsType<WelcomeViewModel>(main.Navigation.Current);

        welcome.StartOrderCommand.Execute(null);

        Assert.Equal(("menu", "touch", (string?)null), await NextStateAsync(session));
    }

    [Fact]
    public async Task GoingHomeFromTheMenu_ReportsWelcomeAsTouch()
    {
        await using var server = new FakeWsServer();
        var (provider, main, session, _) = await StartAppAsync(server);
        await using var _s = session;
        using var _p = provider;
        main.Navigation.NavigateTo("menu");
        await NextStateAsync(session);

        main.GoHomeCommand.Execute(null);

        Assert.Equal(("welcome", "touch", (string?)null), await NextStateAsync(session));
    }

    [Fact]
    public async Task ANavigationOrderedByTheBackend_ReportsTheSourceAndTheOrderId()
    {
        await using var server = new FakeWsServer();
        var (provider, main, session, _) = await StartAppAsync(server);
        await using var _s = session;
        using var _p = provider;

        main.Navigation.NavigateTo(Screen.Menu, StateSource.Backend, "order-42");

        Assert.Equal(("menu", "backend", (string?)"order-42"), await NextStateAsync(session));
    }

    [Fact]
    public async Task GoingHomeWhileAlreadyOnWelcome_IsStillReported()
    {
        await using var server = new FakeWsServer();
        var (provider, main, session, _) = await StartAppAsync(server);
        await using var _s = session;
        using var _p = provider;

        main.GoHomeCommand.Execute(null);

        Assert.Equal(("welcome", "touch", (string?)null), await NextStateAsync(session));
    }

    [Fact]
    public async Task ASequenceOfNavigations_ArrivesInOrder()
    {
        await using var server = new FakeWsServer();
        var (provider, main, session, _) = await StartAppAsync(server);
        await using var _s = session;
        using var _p = provider;

        main.Navigation.NavigateTo("menu");
        main.Navigation.NavigateTo(Screen.Welcome, StateSource.Backend, "order-1");
        main.Navigation.NavigateTo("menu");

        Assert.Equal(("menu", "touch", (string?)null), await NextStateAsync(session));
        Assert.Equal(("welcome", "backend", (string?)"order-1"), await NextStateAsync(session));
        Assert.Equal(("menu", "touch", (string?)null), await NextStateAsync(session));
    }

    [Fact]
    public async Task AnUnknownScreenName_IsNotReported()
    {
        await using var server = new FakeWsServer();
        var (provider, main, session, _) = await StartAppAsync(server);
        await using var _s = session;
        using var _p = provider;

        main.Navigation.NavigateTo("no_existe");
        var message = await FakeWsServer.TryReceiveTextAsync(session.ServerSocket, _noMessageWait);

        Assert.Null(message);
        Assert.Equal(Screen.Welcome, main.Navigation.CurrentScreen);
    }

    [Fact]
    public async Task WhileDisconnected_NavigationStillWorksButNothingIsSentOrQueued()
    {
        await using var server = new FakeWsServer();
        using var provider = BuildProvider();
        var main = provider.GetRequiredService<MainViewModel>();
        var nav = provider.GetRequiredService<INavigationService>();
        BackendLink? link = null;

        await using var session = await WsClientSession.StartAsync(server, beforeRun: client =>
        {
            link = new BackendLink(client, nav);
            nav.NavigateTo("menu");
        });
        var message = await FakeWsServer.TryReceiveTextAsync(session.ServerSocket, _noMessageWait);

        Assert.NotNull(link);
        Assert.IsType<MenuViewModel>(main.Navigation.Current);
        Assert.Null(message);
    }
}
