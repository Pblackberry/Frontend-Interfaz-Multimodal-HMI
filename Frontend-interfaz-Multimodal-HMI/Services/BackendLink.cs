using System.Windows;
using Frontend_interfaz_Multimodal_HMI.Models;

namespace Frontend_interfaz_Multimodal_HMI.Services;

public sealed class BackendLink
{
    private readonly WsClient _ws;
    private readonly INavigationService _nav;

    public BackendLink(WsClient ws, INavigationService nav)
    {
        _ws = ws;
        _nav = nav;
        _nav.Navigated += OnNavigated;
        _ws.Connected += OnConnected;
    }

    private void OnNavigated(Screen screen, StateSource source, string? orderId)
        => _ = _ws.SendAsync(WsTypes.State, new StatePayload(screen, source, orderId));

    private void OnConnected()
        => Application.Current?.Dispatcher.InvokeAsync(() =>
        {
            if (_nav.CurrentScreen is { } screen)
            {
                _ = _ws.SendAsync(WsTypes.State, new StatePayload(screen, StateSource.Init));
            }
        });
}
