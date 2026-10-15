using System;
using System.Collections.Generic;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Frontend_interfaz_Multimodal_HMI.Services;

namespace Frontend_interfaz_Multimodal_HMI.ViewModels;

public partial class WelcomeViewModel : ObservableObject
{
    private readonly INavigationService _nav;

    public WelcomeViewModel(INavigationService nav) => _nav = nav;

    [RelayCommand]
    private void StartOrder() => _nav.NavigateTo("menu");

    // TODO (cuando conectemos el backend): enviar { "type": "touch_action", "target": "btn_mic" }
    [RelayCommand]
    private void StartListening() { }
}
