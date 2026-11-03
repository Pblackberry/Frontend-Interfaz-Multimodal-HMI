using System.Globalization;
using System.Windows.Media;
using FrontEnd.InterfazHMI.Tests.Support;
using Frontend_interfaz_Multimodal_HMI.ViewModels;

namespace FrontEnd.InterfazHMI.Tests.Unit.ViewModels;

public class DrinkCardViewModelTests
{
    [Fact]
    public void Name_IsTheItemName()
    {
        var sut = new DrinkCardViewModel(TestData.Item("Piña colada"), new FakeImageLoader());

        Assert.Equal("Piña colada", sut.Name);
    }

    [Fact]
    public void Item_IsTheOriginalInstance()
    {
        var item = TestData.Item("Mojito");

        var sut = new DrinkCardViewModel(item, new FakeImageLoader());

        Assert.Same(item, sut.Item);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("https://cdn.test/mojito.png")]
    public void ImageUrl_IsTheItemImagen(string? imagen)
    {
        var item = TestData.Item();
        item.Imagen = imagen;

        var sut = new DrinkCardViewModel(item, new FakeImageLoader());

        Assert.Equal(imagen, sut.ImageUrl);
    }

    [Fact]
    public void Constructor_AsksTheLoaderOnceWithTheItemUrl()
    {
        var item = TestData.Item();
        item.Imagen = "https://cdn.test/mojito.png";
        var loader = new FakeImageLoader();

        _ = new DrinkCardViewModel(item, loader);

        Assert.Equal(new[] { "https://cdn.test/mojito.png" }, loader.Requested);
    }

    [Fact]
    public void Constructor_WithoutImagen_AsksTheLoaderWithNull()
    {
        var item = TestData.Item();
        item.Imagen = null;
        var loader = new FakeImageLoader();

        _ = new DrinkCardViewModel(item, loader);

        Assert.Equal(new string?[] { null }, loader.Requested);
    }

    [Fact]
    public void Image_WhileLoading_IsNullAndHasImageIsFalse()
    {
        var sut = new DrinkCardViewModel(TestData.Item(), FakeImageLoader.Pending());

        Assert.Null(sut.Image);
        Assert.False(sut.HasImage);
    }

    [Fact]
    public void Image_WhenLoadCompletes_IsSetAndHasImageIsTrue()
    {
        var loader = FakeImageLoader.Pending();
        var sut = new DrinkCardViewModel(TestData.Item(), loader);
        var image = new DrawingImage();

        loader.Complete(image);

        Assert.Same(image, sut.Image);
        Assert.True(sut.HasImage);
    }

    [Fact]
    public void Image_WhenLoaderReturnsNull_StaysEmpty()
    {
        var sut = new DrinkCardViewModel(TestData.Item(), FakeImageLoader.Returning(null));

        Assert.Null(sut.Image);
        Assert.False(sut.HasImage);
    }

    [Fact]
    public void Image_WhenImmediatelyAvailable_IsSetAfterConstruction()
    {
        var image = new DrawingImage();

        var sut = new DrinkCardViewModel(TestData.Item(), FakeImageLoader.Returning(image));

        Assert.Same(image, sut.Image);
        Assert.True(sut.HasImage);
    }

    [Fact]
    public void LoadCompleting_RaisesPropertyChangedForImageAndHasImage()
    {
        var loader = FakeImageLoader.Pending();
        var sut = new DrinkCardViewModel(TestData.Item(), loader);
        var raised = new List<string?>();
        sut.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        loader.Complete(new DrawingImage());

        Assert.Contains(nameof(DrinkCardViewModel.Image), raised);
        Assert.Contains(nameof(DrinkCardViewModel.HasImage), raised);
    }

    [Fact]
    public void Constructor_WhenTheLoaderFails_DoesNotThrowAndShowsNoImage()
    {
        var loader = FakeImageLoader.Pending();
        var sut = new DrinkCardViewModel(TestData.Item(), loader);

        loader.Fail(new InvalidOperationException("boom"));

        Assert.Null(sut.Image);
        Assert.False(sut.HasImage);
    }


    [Fact]
    public void Ingredients_AreParsedFromTheItem()
    {
        var item = TestData.Item(ingredientes: "ron 40%; menta");

        var sut = new DrinkCardViewModel(item, new FakeImageLoader());

        Assert.Equal(new[] { "40% Ron", "Menta" }, sut.Ingredients.Select(i => i.Display));
        Assert.True(sut.HasIngredients);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Ingredients_WhenTheItemHasNone_AreEmptyAndHasIngredientsIsFalse(string? raw)
    {
        var item = TestData.Item(ingredientes: raw);

        var sut = new DrinkCardViewModel(item, new FakeImageLoader());

        Assert.Empty(sut.Ingredients);
        Assert.False(sut.HasIngredients);
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

        var sut = new DrinkCardViewModel(item, new FakeImageLoader());

        Assert.Equal(expected, sut.PriceText);
    }

    [Fact]
    public void PriceText_IgnoresTheCurrentCulture()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");

            var sut = new DrinkCardViewModel(TestData.Item(precio: 5.5m), new FakeImageLoader());

            Assert.Equal("$5.50", sut.PriceText);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }
}
