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
    private static readonly Dictionary<Screen, Type> ScreenMap = new()
    {
        [Screen.Welcome] = typeof(WelcomeViewModel),
        [Screen.Menu] = typeof(MenuViewModel),
    };

    private readonly IServiceProvider _sp;

    public NavigationService(IServiceProvider sp) => _sp = sp;

    [ObservableProperty]
    private ObservableObject? _current;

    // Pantalla actual (null si todavía no se navegó a ninguna conocida)
    public Screen? CurrentScreen { get; private set; }

    // Se dispara en cada navegación: (pantalla, origen, id de la orden del backend)
    public event Action<Screen, StateSource, string?>? Navigated;

    public void NavigateTo<T>() where T : ObservableObject
    {
        var found = ScreenMap.FirstOrDefault(kv => kv.Value == typeof(T));
        if (found.Value is null)
        {
            // ViewModel sin pantalla registrada: se comporta como antes, pero no se reporta
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

    // Punto único de navegación: táctil (por defecto) u orden del backend
    public void NavigateTo(Screen screen, StateSource source = StateSource.Touch, string? orderId = null)
    {
        if (!ScreenMap.TryGetValue(screen, out var vmType))
        {
            Debug.WriteLine($"Pantalla desconocida: {screen}");
            return;
        }

        if (CurrentScreen != screen)   // si ya está en esa pantalla, no se recrea el ViewModel
        {
            Current = (ObservableObject)_sp.GetRequiredService(vmType);
        }

        CurrentScreen = screen;
        Navigated?.Invoke(screen, source, orderId);   // se anuncia siempre, también si no cambió
    }
}
