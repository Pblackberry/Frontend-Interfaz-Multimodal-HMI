using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using QRCoder;

namespace Frontend_interfaz_Multimodal_HMI.Services;

public interface IQrFactory { ImageSource Create(string text); }

public sealed class QrFactory : IQrFactory
{
    public ImageSource Create(string text)
    {
        var png = PngByteQRCodeHelper.GetQRCode(text, QRCodeGenerator.ECCLevel.M, 10);   // 10 px por módulo
        using var ms = new MemoryStream(png);
        var bmp = new BitmapImage();
        bmp.BeginInit();
        bmp.CacheOption = BitmapCacheOption.OnLoad;
        bmp.StreamSource = ms;
        bmp.EndInit();
        bmp.Freeze();
        return bmp;
    }
}
