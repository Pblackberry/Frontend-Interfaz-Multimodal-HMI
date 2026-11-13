using System.Windows;
using CommunityToolkit.Mvvm.Messaging;
using Frontend_interfaz_Multimodal_HMI.Models;

namespace Frontend_interfaz_Multimodal_HMI.Services;

public sealed class BackendLink
{
    private readonly WsClient _ws;
    private readonly INavigationService _nav;

    private static void _onUi(Action action)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null)
        {
            action();
            return;
        }

        dispatcher.InvokeAsync(action);
    }

    public BackendLink(WsClient ws, INavigationService nav)
    {
        _ws = ws;
        _nav = nav;
        _nav.Navigated += OnNavigated;
        _ws.Connected += OnConnected;
        _ws.NavigateRequested += OnNavigateRequested;
        _ws.SelectDrinkRequested += (id, p) => _onUi(() =>
            WeakReferenceMessenger.Default.Send(new VoiceSelectDrinkMessage(id, p.Name)));
        _ws.ShowIngredientsRequested += p => _onUi(() =>
            WeakReferenceMessenger.Default.Send(new VoiceShowIngredientsMessage(p.Name)));
        _ws.CloseIngredientsRequested += () => _onUi(() =>
            WeakReferenceMessenger.Default.Send(new VoiceCloseIngredientsMessage()));
        _ws.PaymentStarted += p => _onUi(() =>
            WeakReferenceMessenger.Default.Send(new PaymentStartedMessage(p)));
        _ws.PaymentResultReceived += (id, p) => _onUi(() =>
            WeakReferenceMessenger.Default.Send(new PaymentResultMessage(id, p)));
        _ws.CobotStatusReceived += p => _onUi(() =>
            WeakReferenceMessenger.Default.Send(new CobotStatusMessage(p)));
    }

    private void OnNavigated(Screen screen, StateSource source, string? orderId)
        => _ = _ws.SendAsync(WsTypes.State, new StatePayload(screen, source, orderId));

    private void OnNavigateRequested(string id, NavigatePayload payload)
        => _onUi(() => _nav.NavigateTo(payload.Screen, StateSource.Backend, id));

    private void OnConnected()
        => _onUi(() =>
        {
            if (_nav.CurrentScreen is { } screen)
            {
                _ = _ws.SendAsync(WsTypes.State, new StatePayload(screen, StateSource.Init));
            }
        });
}
