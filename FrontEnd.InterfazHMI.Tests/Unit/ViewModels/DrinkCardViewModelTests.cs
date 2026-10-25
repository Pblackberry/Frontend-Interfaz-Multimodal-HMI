using System.Globalization;
using FrontEnd.InterfazHMI.Tests.Support;
using Frontend_interfaz_Multimodal_HMI.ViewModels;

namespace FrontEnd.InterfazHMI.Tests.Unit.ViewModels;

public class DrinkCardViewModelTests
{
    [Fact]
    public void Name_IsTheItemName()
    {
        var sut = new DrinkCardViewModel(TestData.Item("Piña colada"));

        Assert.Equal("Piña colada", sut.Name);
    }

    [Fact]
    public void Item_IsTheOriginalInstance()
    {
        var item = TestData.Item("Mojito");

        var sut = new DrinkCardViewModel(item);

        Assert.Same(item, sut.Item);
    }

    [Fact]
    public void ImageUrl_IsNullUntilImagesAreSupported()
    {
        // Cuando el backend envíe el link de la imagen, esta prueba debe cambiar.
        var sut = new DrinkCardViewModel(TestData.Item());

        Assert.Null(sut.ImageUrl);
    }

    [Theory]
    [InlineData("5.5", "$5.50")]
    [InlineData("6", "$6.00")]
    [InlineData("0", "$0.00")]
    [InlineData("5.678", "$5.68")]
    [InlineData("1234.5", "$1234.50")]
    public void PriceText_HasDollarSignAndTwoDecimals(string price, string expected)
    {
        var item = TestData.Item(precio: decimal.Parse(price, CultureInfo.InvariantCulture));

        var sut = new DrinkCardViewModel(item);

        Assert.Equal(expected, sut.PriceText);
    }

    [Fact]
    public void PriceText_IgnoresTheCurrentCulture()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");   // usa coma decimal

            var sut = new DrinkCardViewModel(TestData.Item(precio: 5.5m));

            Assert.Equal("$5.50", sut.PriceText);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }
}
