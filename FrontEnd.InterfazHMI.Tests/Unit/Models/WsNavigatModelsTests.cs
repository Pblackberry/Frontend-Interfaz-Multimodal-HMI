using System.Text.Json;
using Frontend_interfaz_Multimodal_HMI.Models;

namespace FrontEnd.InterfazHMI.Tests.Unit.Models;

public class WsNavigateModelsTests
{
    private const string _backendNavigate =
        "{\"id\":\"7adeaa98bcc54b7f95db77321cf826ef\",\"ts\":\"2026-11-02T14:33:53.811394Z\"," +
        "\"type\":\"navigate\",\"payload\":{\"screen\":\"menu\"}}";

    [Fact]
    public void WsTypes_Navigate_MatchesTheBackendTypeName()
    {
        Assert.Equal("navigate", WsTypes.Navigate);
    }

    [Theory]
    [InlineData(Screen.Welcome, "{\"screen\":\"welcome\"}")]
    [InlineData(Screen.Menu, "{\"screen\":\"menu\"}")]
    public void NavigatePayload_IsWrittenInLowerCase(Screen screen, string expectedJson)
    {
        Assert.Equal(expectedJson, JsonSerializer.Serialize(new NavigatePayload(screen), WsJson.Options));
    }

    [Theory]
    [InlineData("welcome", Screen.Welcome)]
    [InlineData("menu", Screen.Menu)]
    public void NavigatePayload_IsReadFromLowerCase(string text, Screen expected)
    {
        var payload = JsonSerializer.Deserialize<NavigatePayload>($"{{\"screen\":\"{text}\"}}", WsJson.Options);

        Assert.Equal(new NavigatePayload(expected), payload);
    }

    [Theory]
    [InlineData("{\"screen\":\"welome\"}")]
    [InlineData("{\"screen\":\"otra\"}")]
    public void NavigatePayload_UnknownScreen_IsRejected(string json)
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<NavigatePayload>(json, WsJson.Options));
    }

    [Fact]
    public void NavigatePayload_IsComparedByValue()
    {
        Assert.Equal(new NavigatePayload(Screen.Menu), new NavigatePayload(Screen.Menu));
        Assert.NotEqual(new NavigatePayload(Screen.Menu), new NavigatePayload(Screen.Welcome));
    }

    [Fact]
    public void ABackendNavigateMessage_CanBeReadWithTheEnvelopeAndThePayload()
    {
        var envelope = JsonSerializer.Deserialize<WsEnvelope>(_backendNavigate, WsJson.Options)!;
        var payload = envelope.Payload.Deserialize<NavigatePayload>(WsJson.Options);

        Assert.Equal(WsTypes.Navigate, envelope.Type);
        Assert.Equal("7adeaa98bcc54b7f95db77321cf826ef", envelope.Id);
        Assert.Equal(TimeSpan.Zero, envelope.Ts.Offset);
        Assert.Equal(new NavigatePayload(Screen.Menu), payload);
    }
}
