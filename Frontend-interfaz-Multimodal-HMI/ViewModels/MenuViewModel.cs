using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Frontend_interfaz_Multimodal_HMI.Models;
using Frontend_interfaz_Multimodal_HMI.Services;

namespace Frontend_interfaz_Multimodal_HMI.ViewModels;

public partial class MenuViewModel : ObservableRecipient, IRecipient<CatalogChangedMessage>
{
    public ObservableCollection<DrinkCardViewModel> Items { get; } = new();

    public MenuViewModel(CatalogStore store)
    {
        Load(store.Items);
        IsActive = true;
    }

    public void Receive(CatalogChangedMessage message)
        => Application.Current?.Dispatcher.InvokeAsync(() => Load(message.Snapshot.Catalogo.Items));

    private void Load(IEnumerable<CobotMenuItem> items)
    {
        Items.Clear();
        foreach (var item in items)
        {
            Items.Add(new DrinkCardViewModel(item));
        }
    }

    [RelayCommand]
    private void SelectDrink(DrinkCardViewModel? drink)
    {
        if (drink is null)
        {
            return;
        }
        // TODO: flujo de pedido (confirmar, enviar al backend, etc.)
        Debug.WriteLine($"[Menu] Bebida seleccionada: {drink.Name}");
    }
}
