using System;
using System.Collections.Generic;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using Frontend_interfaz_Multimodal_HMI.ViewModels;
using Microsoft.Extensions.DependencyInjection;
namespace Frontend_interfaz_Multimodal_HMI.Services;

public partial class NavigationService : ObservableObject, INavigationService
{
    private readonly IServiceProvider _sp;

    public NavigationService(IServiceProvider sp) => _sp = sp;

    [ObservableProperty]
    private ObservableObject? _current;

    public void NavigateTo<T>() where T : ObservableObject
        => Current = _sp.GetRequiredService<T>();

    public void NavigateTo(string screen)
    {
        switch (screen.ToLowerInvariant())
        {
            case "welcome": NavigateTo<WelcomeViewModel>(); break;
            case "menu": NavigateTo<MenuViewModel>(); break;
            default:
                System.Diagnostics.Debug.WriteLine($"Pantalla desconocida: {screen}");
                break;
        }
    }
}
