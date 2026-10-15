using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Frontend_interfaz_Multimodal_HMI.Services;

public interface INavigationService : INotifyPropertyChanged
{
    /// <summary>ViewModel de la pantalla actual (la vista se resuelve por DataTemplate).</summary>
    ObservableObject? Current { get; }

    void NavigateTo<T>() where T : ObservableObject;

    /// <summary>Navega por nombre ("welcome", "menu"). Es lo que usará el mensaje "navigate" del backend.</summary>
    void NavigateTo(string screen);
}
