using System.Text.Json;
using Frontend_interfaz_Multimodal_HMI.Models;

namespace FrontEnd.InterfazHMI.Tests.Unit.Models;

public class ScreenPaymentTests
{
    [Fact]
    public void NavigatePayload_Payment_IsWrittenInLowerCase()
    {
        Assert.Equal(
            "{\"screen\":\"payment\"}",
            JsonSerializer.Serialize(new NavigatePayload(Screen.Payment), WsJson.Options));
    }

    [Fact]
    public void NavigatePayload_Payment_IsReadFromLowerCase()
    {
        var payload = JsonSerializer.Deserialize<NavigatePayload>("{\"screen\":\"payment\"}", WsJson.Options);

        Assert.Equal(new NavigatePayload(Screen.Payment), payload);
    }

    [Fact]
    public void StatePayload_Payment_IsWrittenInLowerCase()
    {
        var json = JsonSerializer.Serialize(
            new StatePayload(Screen.Payment, StateSource.Touch), WsJson.Options);

        using var doc = JsonDocument.Parse(json);
        Assert.Equal("payment", doc.RootElement.GetProperty("screen").GetString());
        Assert.Equal("touch", doc.RootElement.GetProperty("source").GetString());
    }

    [Fact]
    public void StatePayload_Payment_RoundTrips()
    {
        var original = new StatePayload(Screen.Payment, StateSource.Backend, "order-3");

        var json = JsonSerializer.Serialize(original, WsJson.Options);

        Assert.Equal(original, JsonSerializer.Deserialize<StatePayload>(json, WsJson.Options));
    }
}
