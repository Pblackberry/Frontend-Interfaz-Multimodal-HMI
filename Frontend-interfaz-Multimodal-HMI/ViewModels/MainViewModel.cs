using System;
using System.Collections.Generic;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Frontend_interfaz_Multimodal_HMI.Services;

namespace Frontend_interfaz_Multimodal_HMI.ViewModels;

public partial class MainViewModel : ObservableObject
{
    public INavigationService Navigation { get; }

    public MainViewModel(INavigationService navigation)
    {
        Navigation = navigation;
        Navigation.NavigateTo("welcome");
    }

    [RelayCommand]
    private void GoHome() => Navigation.NavigateTo("welcome");
}
