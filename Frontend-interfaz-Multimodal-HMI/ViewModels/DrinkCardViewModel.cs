using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Frontend_interfaz_Multimodal_HMI.Models;

namespace Frontend_interfaz_Multimodal_HMI.ViewModels;

public sealed class DrinkCardViewModel
{
    public DrinkCardViewModel(CobotMenuItem item)
    {
        Item = item;
        Name = item.NombreItem;
        
        PriceText = "$" + item.PrecioItem.ToString("0.00", CultureInfo.InvariantCulture);
        ImageUrl = null; 
    }

    public CobotMenuItem Item { get; }   // el objeto original, para cuando se haga el pedido
    public string Name { get; }
    public string PriceText { get; }
    public string? ImageUrl { get; }
}
