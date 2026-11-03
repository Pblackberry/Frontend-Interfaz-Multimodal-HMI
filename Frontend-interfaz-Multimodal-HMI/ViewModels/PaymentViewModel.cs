using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Frontend_interfaz_Multimodal_HMI.Models;
using Frontend_interfaz_Multimodal_HMI.Services;

namespace Frontend_interfaz_Multimodal_HMI.ViewModels;

public partial class PaymentViewModel : ObservableObject
{
    private readonly INavigationService _nav;
    private readonly OrderSession _order;

    public PaymentViewModel(INavigationService nav, OrderSession order)
    {
        _nav = nav;
        _order = order;

        var drink = order.SelectedDrink;
        DrinkName = drink?.NombreItem ?? "";
        PriceText = drink is null
            ? ""
            : "$" + drink.PrecioItem.ToString("0.00", CultureInfo.InvariantCulture);
    }

    public string DrinkName { get; }
    public string PriceText { get; }

    [RelayCommand]
    private void Cancel()
    {
        _order.Clear();
        _nav.NavigateTo(Screen.Menu);
    }
}
