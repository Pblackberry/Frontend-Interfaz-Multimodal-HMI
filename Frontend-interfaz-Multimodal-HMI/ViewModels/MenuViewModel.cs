using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Frontend_interfaz_Multimodal_HMI.Models;
using Frontend_interfaz_Multimodal_HMI.Services;

namespace Frontend_interfaz_Multimodal_HMI.ViewModels;

public partial class MenuViewModel : ObservableRecipient,
    IRecipient<CatalogChangedMessage>,
    IRecipient<VoiceSelectDrinkMessage>,
    IRecipient<VoiceShowIngredientsMessage>,
    IRecipient<VoiceCloseIngredientsMessage>
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsInfoOpen))]
    private DrinkCardViewModel? _infoDrink;
    public bool IsInfoOpen => InfoDrink is not null;
    private readonly IImageLoader _images;
    public ObservableCollection<DrinkCardViewModel> Items { get; } = new();
    private readonly OrderSession _order;
    private readonly INavigationService _nav;

    public MenuViewModel(CatalogStore store, IImageLoader images, OrderSession order, INavigationService nav)
    {
        _images = images;
        _order = order;
        _nav = nav;
        Load(store.Items);
        IsActive = true;
    }

    public void Receive(CatalogChangedMessage message)
        => Application.Current?.Dispatcher.InvokeAsync(() => Load(message.Snapshot.Catalogo.Items));

    private void Load(IEnumerable<CobotMenuItem> items)
    {
        var openName = InfoDrink?.Name;

        Items.Clear();
        foreach (var item in items)
        {
            Items.Add(new DrinkCardViewModel(item, _images));
        }

        InfoDrink = openName is null
            ? null
            : Items.FirstOrDefault(d => d.Name == openName);
    }

    [RelayCommand]
    private void ShowIngredients(DrinkCardViewModel? drink) => InfoDrink = drink;

    [RelayCommand]
    private void CloseIngredients() => InfoDrink = null;

    private DrinkCardViewModel? FindDrink(string name)
        => Items.FirstOrDefault(d => string.Equals(d.Name, name, StringComparison.OrdinalIgnoreCase));

    [RelayCommand]
    private void SelectDrink(DrinkCardViewModel? drink)
    {
        if (drink is not null)
        {
            Choose(drink, StateSource.Touch, null);
        }
    }

    private void Choose(DrinkCardViewModel drink, StateSource source, string? orderId)
    {
        _order.Select(drink.Item);
        _nav.NavigateTo(Screen.Payment, source, orderId);
    }

    public void Receive(VoiceSelectDrinkMessage message)
    {
        var drink = FindDrink(message.DrinkName);
        if (drink is null)
        {
            Debug.WriteLine($"[Voz] Bebida no encontrada en el menú: {message.DrinkName}");
            return;
        }
        InfoDrink = null;
        Choose(drink, StateSource.Backend, message.OrderId);
    }

    public void Receive(VoiceShowIngredientsMessage message)
    {
        var drink = FindDrink(message.DrinkName);
        if (drink is { HasIngredients: true })
        {
            InfoDrink = drink;
        }
    }

    public void Receive(VoiceCloseIngredientsMessage message) => InfoDrink = null;
}
