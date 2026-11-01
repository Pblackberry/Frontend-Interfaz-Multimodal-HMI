using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Frontend_interfaz_Multimodal_HMI.Models;

namespace Frontend_interfaz_Multimodal_HMI.Services;

public interface INavigationService : INotifyPropertyChanged
{
    ObservableObject? Current { get; }

    void NavigateTo<T>() where T : ObservableObject;

    void NavigateTo(string screen);

    Screen? CurrentScreen { get; }
    event Action<Screen, StateSource, string?>? Navigated;
    void NavigateTo(Screen screen, StateSource source = StateSource.Touch, string? orderId = null);
}
