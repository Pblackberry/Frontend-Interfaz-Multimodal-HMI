using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using Frontend_interfaz_Multimodal_HMI.Models;
using Frontend_interfaz_Multimodal_HMI.ViewModels;
using Microsoft.Extensions.DependencyInjection;
namespace Frontend_interfaz_Multimodal_HMI.Services;

public partial class NavigationService : ObservableObject, INavigationService
{
    private static readonly Dictionary<Screen, Type> _screenMap = new()
    {
        [Screen.Welcome] = typeof(WelcomeViewModel),
        [Screen.Menu] = typeof(MenuViewModel),
    };

    private readonly IServiceProvider _sp;

    public NavigationService(IServiceProvider sp) => _sp = sp;

    [ObservableProperty]
    private ObservableObject? _current;

    public Screen? CurrentScreen { get; private set; }

    public event Action<Screen, StateSource, string?>? Navigated;

    public void NavigateTo<T>() where T : ObservableObject
    {
        var found = _screenMap.FirstOrDefault(kv => kv.Value == typeof(T));
        if (found.Value is null)
        {
            Current = _sp.GetRequiredService<T>();
            CurrentScreen = null;
            return;
        }
        NavigateTo(found.Key);
    }

    public void NavigateTo(string screen)
    {
        if (Enum.TryParse<Screen>(screen, ignoreCase: true, out var parsed) && Enum.IsDefined(parsed))
        {
            NavigateTo(parsed);
        }
        else
        {
            Debug.WriteLine($"Pantalla desconocida: {screen}");
        }
    }

    public void NavigateTo(Screen screen, StateSource source = StateSource.Touch, string? orderId = null)
    {
        if (!_screenMap.TryGetValue(screen, out var vmType))
        {
            Debug.WriteLine($"Pantalla desconocida: {screen}");
            return;
        }

        if (CurrentScreen != screen)
        {
            Current = (ObservableObject)_sp.GetRequiredService(vmType);
        }

        CurrentScreen = screen;
        Navigated?.Invoke(screen, source, orderId);
    }
}
