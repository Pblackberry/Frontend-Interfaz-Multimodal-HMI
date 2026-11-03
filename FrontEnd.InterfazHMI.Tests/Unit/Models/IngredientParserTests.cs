using Frontend_interfaz_Multimodal_HMI.Models;

namespace FrontEnd.InterfazHMI.Tests.Unit.Models;

public class IngredientParserTests
{

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(";")]
    [InlineData(" ; ; ")]
    public void Parse_WithNoIngredients_ReturnsEmpty(string? raw)
    {
        Assert.Empty(IngredientParser.Parse(raw));
    }


    [Fact]
    public void Parse_NameWithPercent_SplitsNameAndPercent()
    {
        var result = IngredientParser.Parse("ron 40%");

        var ingredient = Assert.Single(result);
        Assert.Equal("Ron", ingredient.Name);
        Assert.Equal("40", ingredient.Percent);
    }

    [Fact]
    public void Parse_NameWithoutPercent_HasNoPercent()
    {
        var ingredient = Assert.Single(IngredientParser.Parse("menta"));

        Assert.Equal("Menta", ingredient.Name);
        Assert.Null(ingredient.Percent);
    }

    [Fact]
    public void Parse_SeveralIngredients_KeepsTheOrder()
    {
        var result = IngredientParser.Parse("ron 40%; menta; limón 10%");

        Assert.Equal(
            new[] { new Ingredient("Ron", "40"), new Ingredient("Menta", null), new Ingredient("Limón", "10") },
            result);
    }

    [Theory]
    [InlineData("ron 12.5%", "12.5")]
    [InlineData("ron 12,5%", "12,5")]
    [InlineData("ron 100%", "100")]
    [InlineData("ron 40 %", "40")]
    [InlineData("ron   40%", "40")]
    public void Parse_AcceptsDecimalsAndSpacesBeforeThePercent(string raw, string expectedPercent)
    {
        var ingredient = Assert.Single(IngredientParser.Parse(raw));

        Assert.Equal("Ron", ingredient.Name);
        Assert.Equal(expectedPercent, ingredient.Percent);
    }

    [Fact]
    public void Parse_NameWithSeveralWords_KeepsThemAll()
    {
        var ingredient = Assert.Single(IngredientParser.Parse("jugo de naranja 25%"));

        Assert.Equal("Jugo de naranja", ingredient.Name);
        Assert.Equal("25", ingredient.Percent);
    }


    [Fact]
    public void Parse_TrimsSpacesAndIgnoresEmptyEntries()
    {
        var result = IngredientParser.Parse("  ron 40% ;; menta ;  ");

        Assert.Equal(new[] { new Ingredient("Ron", "40"), new Ingredient("Menta", null) }, result);
    }

    [Fact]
    public void Parse_OnlySemicolonSeparatesIngredients_CommaDoesNot()
    {
        var ingredient = Assert.Single(IngredientParser.Parse("ron, menta"));

        Assert.Equal("Ron, menta", ingredient.Name);
        Assert.Null(ingredient.Percent);
    }


    [Fact]
    public void Parse_CapitalizesOnlyTheFirstLetter()
    {
        var ingredient = Assert.Single(IngredientParser.Parse("ron blanco añejo"));

        Assert.Equal("Ron blanco añejo", ingredient.Name);
    }

    [Fact]
    public void Parse_KeepsTheRestOfTheNameAsWritten()
    {
        var ingredient = Assert.Single(IngredientParser.Parse("ron BACARDI"));

        Assert.Equal("Ron BACARDI", ingredient.Name);
    }


    [Theory]
    [InlineData("ron 40", "Ron 40")]
    [InlineData("40%", "40%")]
    [InlineData("100% jugo", "100% jugo")]
    public void Parse_TextThatIsNotNameAndPercent_IsKeptAsAPlainName(string raw, string expectedName)
    {
        var ingredient = Assert.Single(IngredientParser.Parse(raw));

        Assert.Equal(expectedName, ingredient.Name);
        Assert.Null(ingredient.Percent);
    }


    [Fact]
    public void Display_WithPercent_ShowsPercentFirst()
    {
        Assert.Equal("40% Ron", new Ingredient("Ron", "40").Display);
    }

    [Fact]
    public void Display_WithoutPercent_ShowsOnlyTheName()
    {
        Assert.Equal("Menta", new Ingredient("Menta", null).Display);
    }

    [Fact]
    public void Display_OfParsedText_CombinesBoth()
    {
        var displays = IngredientParser.Parse("ron 40%; menta").Select(i => i.Display);

        Assert.Equal(new[] { "40% Ron", "Menta" }, displays);
    }
}
