using System.Windows.Media;
using Frontend_interfaz_Multimodal_HMI.Services;

namespace FrontEnd.InterfazHMI.Tests.Support;

internal sealed class FakeQrFactory : IQrFactory
{
    public List<string> Created { get; } = new();

    public List<ImageSource> Images { get; } = new();

    public ImageSource Create(string text)
    {
        Created.Add(text);
        var image = new DrawingImage();
        Images.Add(image);
        return image;
    }
}
