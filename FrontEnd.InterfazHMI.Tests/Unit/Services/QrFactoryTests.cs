using System.Windows.Media.Imaging;
using Frontend_interfaz_Multimodal_HMI.Services;

namespace FrontEnd.InterfazHMI.Tests.Unit.Services;

public class QrFactoryTests
{
    private const string PayUrl = "http://192.168.0.10:8888/pay/Zm9vYmFyYmF6cXV4MTIz";

    private readonly QrFactory _sut = new();

    [Fact]
    public void Create_ReturnsABitmap()
    {
        var image = _sut.Create(PayUrl);

        Assert.IsType<BitmapImage>(image);
    }

    [Fact]
    public void Create_ReturnsAFrozenImage_SoTheUiThreadCanUseIt()
    {
        Assert.True(_sut.Create(PayUrl).IsFrozen);
    }

    [Fact]
    public void Create_ReturnsASquareImageWithContent()
    {
        var image = Assert.IsType<BitmapImage>(_sut.Create(PayUrl));

        Assert.True(image.PixelWidth > 0);
        Assert.Equal(image.PixelWidth, image.PixelHeight);
    }

    [Fact]
    public void Create_UsesTenPixelsPerModule()
    {
        var image = Assert.IsType<BitmapImage>(_sut.Create(PayUrl));

        Assert.Equal(0, image.PixelWidth % 10);
    }

    [Fact]
    public void Create_IsDeterministicInSize()
    {
        var first = Assert.IsType<BitmapImage>(_sut.Create(PayUrl));
        var second = Assert.IsType<BitmapImage>(_sut.Create(PayUrl));

        Assert.Equal(first.PixelWidth, second.PixelWidth);
    }

    [Fact]
    public void Create_ReturnsANewImageEachTime()
    {
        Assert.NotSame(_sut.Create(PayUrl), _sut.Create(PayUrl));
    }

    [Fact]
    public void Create_ALongerTextNeverProducesASmallerCode()
    {
        var shortCode = Assert.IsType<BitmapImage>(_sut.Create("http://h/pay/a"));
        var longCode = Assert.IsType<BitmapImage>(_sut.Create(PayUrl + "/" + new string('x', 150)));

        Assert.True(longCode.PixelWidth >= shortCode.PixelWidth);
    }

    [Fact]
    public async Task Create_CanRunOutsideTheUiThread()
    {
        var image = await Task.Run(() => _sut.Create(PayUrl));

        Assert.True(image.IsFrozen);
    }

    [Fact]
    public void QrFactory_IsUsableThroughItsInterface()
    {
        IQrFactory factory = _sut;

        Assert.NotNull(factory.Create(PayUrl));
    }
}
