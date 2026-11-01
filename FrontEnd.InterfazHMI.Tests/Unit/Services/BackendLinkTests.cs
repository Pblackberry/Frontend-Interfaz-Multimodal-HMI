using FrontEnd.InterfazHMI.Tests.Support;
using Frontend_interfaz_Multimodal_HMI.Models;
using Frontend_interfaz_Multimodal_HMI.Services;

namespace FrontEnd.InterfazHMI.Tests.Unit.Services;

public class BackendLinkTests
{
    private static WsClient UnconnectedClient() => new(new Uri("ws://127.0.0.1:1/ws"));

    [Fact]
    public void Constructor_SubscribesToTheNavigatedEventExactlyOnce()
    {
        var nav = new FakeNavigationService();

        _ = new BackendLink(UnconnectedClient(), nav);

        Assert.Equal(1, nav.NavigatedSubscribers);
    }

    [Fact]
    public void Constructor_DoesNotThrowWithAClientThatIsNotConnected()
    {
        var exception = Record.Exception(() => new BackendLink(UnconnectedClient(), new FakeNavigationService()));

        Assert.Null(exception);
    }

    [Theory]
    [InlineData(Screen.Welcome, StateSource.Touch, null)]
    [InlineData(Screen.Menu, StateSource.Backend, "order-1")]
    [InlineData(Screen.Menu, StateSource.Init, null)]
    public void Navigated_WhileTheClientIsNotConnected_DoesNotThrow(Screen screen, StateSource source, string? orderId)
    {
        var nav = new FakeNavigationService();
        _ = new BackendLink(UnconnectedClient(), nav);

        var exception = Record.Exception(() => nav.RaiseNavigated(screen, source, orderId));

        Assert.Null(exception);
    }
}
