using System.Globalization;
using System.Threading.Tasks;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using Frontend_interfaz_Multimodal_HMI.Models;
using Frontend_interfaz_Multimodal_HMI.Services;

namespace Frontend_interfaz_Multimodal_HMI.ViewModels;

public sealed partial class DrinkCardViewModel : ObservableObject
{
    public DrinkCardViewModel(CobotMenuItem item, IImageLoader images)
    {
        Item = item;
        Name = item.NombreItem;
        PriceText = "$" + item.PrecioItem.ToString("0.00", CultureInfo.InvariantCulture);
        ImageUrl = item.Imagen;
        _ = LoadImageAsync(images);
    }

    public CobotMenuItem Item { get; }
    public string Name { get; }
    public string PriceText { get; }
    public string? ImageUrl { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasImage))]
    private ImageSource? _image;

    public bool HasImage => Image is not null;

    private async Task LoadImageAsync(IImageLoader images)
        => Image = await images.LoadAsync(ImageUrl);
}
