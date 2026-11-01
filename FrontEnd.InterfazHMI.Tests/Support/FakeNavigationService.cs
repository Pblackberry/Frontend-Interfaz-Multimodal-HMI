using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Frontend_interfaz_Multimodal_HMI.Models;
using Frontend_interfaz_Multimodal_HMI.Services;

namespace FrontEnd.InterfazHMI.Tests.Support;

internal sealed class FakeNavigationService : INavigationService
{
    private Action<Screen, StateSource, string?>? _navigated;

    public int NavigatedSubscribers => _navigated?.GetInvocationList().Length ?? 0;

    public event PropertyChangedEventHandler? PropertyChanged { add { } remove { } }

    public event Action<Screen, StateSource, string?>? Navigated
    {
        add => _navigated += value;
        remove => _navigated -= value;
    }

    public ObservableObject? Current => null;

    public Screen? CurrentScreen { get; set; }

    public void RaiseNavigated(Screen screen, StateSource source = StateSource.Touch, string? orderId = null)
        => _navigated?.Invoke(screen, source, orderId);

    public void NavigateTo<T>() where T : ObservableObject { }

    public void NavigateTo(string screen) { }

    public void NavigateTo(Screen screen, StateSource source = StateSource.Touch, string? orderId = null) { }
}
