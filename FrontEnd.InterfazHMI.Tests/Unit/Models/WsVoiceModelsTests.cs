using System.Text.Json;
using Frontend_interfaz_Multimodal_HMI.Models;

namespace FrontEnd.InterfazHMI.Tests.Unit.Models;

public class WsVoiceModelsTests
{
    [Fact]
    public void WsTypes_VoiceMessages_MatchTheBackendTypeNames()
    {
        Assert.Equal("select_drink", WsTypes.SelectDrink);
        Assert.Equal("show_ingredients", WsTypes.ShowIngredients);
        Assert.Equal("close_ingredients", WsTypes.CloseIngredients);
    }

    [Fact]
    public void DrinkRefPayload_IsWrittenWithALowerCaseNameProperty()
    {
        var json = JsonSerializer.Serialize(new DrinkRefPayload("Mojito"), WsJson.Options);

        Assert.Equal("{\"name\":\"Mojito\"}", json);
    }

    [Fact]
    public void DrinkRefPayload_IsReadFromTheBackendJson()
    {
        var payload = JsonSerializer.Deserialize<DrinkRefPayload>("{\"name\":\"Mojito\"}", WsJson.Options);

        Assert.Equal(new DrinkRefPayload("Mojito"), payload);
    }

    [Theory]
    [InlineData("Piña colada")]
    [InlineData("Caipiriña")]
    [InlineData("Old Fashioned")]
    public void DrinkRefPayload_KeepsAccentsAndSpaces(string name)
    {
        var json = JsonSerializer.Serialize(new DrinkRefPayload(name), WsJson.Options);

        Assert.Equal(name, JsonSerializer.Deserialize<DrinkRefPayload>(json, WsJson.Options)!.Name);
    }

    [Fact]
    public void DrinkRefPayload_ReadsAnAccentedNameSentAsPlainUtf8()
    {
        var payload = JsonSerializer.Deserialize<DrinkRefPayload>("{\"name\":\"Piña colada\"}", WsJson.Options);

        Assert.Equal("Piña colada", payload!.Name);
    }

    [Fact]
    public void DrinkRefPayload_IsComparedByValue()
    {
        Assert.Equal(new DrinkRefPayload("Mojito"), new DrinkRefPayload("Mojito"));
        Assert.NotEqual(new DrinkRefPayload("Mojito"), new DrinkRefPayload("Daiquiri"));
    }

    [Fact]
    public void DrinkRefPayload_WithAnotherShape_IsRejected()
    {
        Assert.Throws<JsonException>(
            () => JsonSerializer.Deserialize<DrinkRefPayload>("\"Mojito\"", WsJson.Options));
    }
}
