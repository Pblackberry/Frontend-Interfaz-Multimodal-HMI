using System.Text.Json;
using Frontend_interfaz_Multimodal_HMI.Models;

namespace FrontEnd.InterfazHMI.Tests.Unit.Models;

public class WsStateModelsTests
{
    private static string Serialize<T>(T value) => JsonSerializer.Serialize(value, WsJson.Options);

    private static JsonElement ParseObject(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
    }

    private static string[] PropertyNames(JsonElement element)
        => element.EnumerateObject().Select(p => p.Name).ToArray();


    [Fact]
    public void WsTypes_State_MatchesTheBackendTypeName()
    {
        Assert.Equal("state", WsTypes.State);
    }


    [Theory]
    [InlineData(Screen.Welcome, "welcome")]
    [InlineData(Screen.Menu, "menu")]
    public void Screen_IsWrittenInLowerCase(Screen screen, string expected)
    {
        Assert.Equal("\"" + expected + "\"", Serialize(screen));
    }

    [Theory]
    [InlineData("welcome", Screen.Welcome)]
    [InlineData("menu", Screen.Menu)]
    public void Screen_IsReadFromLowerCase(string text, Screen expected)
    {
        Assert.Equal(expected, JsonSerializer.Deserialize<Screen>("\"" + text + "\"", WsJson.Options));
    }

    [Fact]
    public void Screen_HasExactlyTheScreensTheBackendKnows()
    {
        var names = Enum.GetNames<Screen>().Select(n => n.ToLowerInvariant()).OrderBy(n => n);

        Assert.Equal(new[] { "menu", "payment", "welcome" }, names);
    }

    [Theory]
    [InlineData("welome")]
    [InlineData("otra")]
    public void Screen_UnknownName_IsRejected(string text)
    {
        Assert.Throws<JsonException>(
            () => JsonSerializer.Deserialize<Screen>("\"" + text + "\"", WsJson.Options));
    }


    [Theory]
    [InlineData(StateSource.Touch, "touch")]
    [InlineData(StateSource.Backend, "backend")]
    [InlineData(StateSource.Init, "init")]
    public void StateSource_IsWrittenInLowerCase(StateSource source, string expected)
    {
        Assert.Equal("\"" + expected + "\"", Serialize(source));
    }

    [Fact]
    public void StateSource_HasExactlyTheSourcesTheBackendKnows()
    {
        var names = Enum.GetNames<StateSource>().Select(n => n.ToLowerInvariant()).OrderBy(n => n);

        Assert.Equal(new[] { "backend", "init", "touch" }, names);
    }


    [Fact]
    public void StatePayload_RefIsNullByDefault()
    {
        Assert.Null(new StatePayload(Screen.Menu, StateSource.Touch).Ref);
    }

    [Fact]
    public void StatePayload_WithoutRef_WritesRefAsNull()
    {
        var json = Serialize(new StatePayload(Screen.Menu, StateSource.Touch));

        Assert.Equal("{\"screen\":\"menu\",\"source\":\"touch\",\"ref\":null}", json);
    }

    [Fact]
    public void StatePayload_WithRef_WritesTheOrderId()
    {
        var json = Serialize(new StatePayload(Screen.Menu, StateSource.Backend, "order-1"));

        Assert.Equal("{\"screen\":\"menu\",\"source\":\"backend\",\"ref\":\"order-1\"}", json);
    }

    [Fact]
    public void StatePayload_RoundTrip_KeepsEveryValue()
    {
        var original = new StatePayload(Screen.Welcome, StateSource.Backend, "order-9");

        var copy = JsonSerializer.Deserialize<StatePayload>(Serialize(original), WsJson.Options);

        Assert.Equal(original, copy);
    }

    [Fact]
    public void StatePayload_IsComparedByValue()
    {
        Assert.Equal(
            new StatePayload(Screen.Menu, StateSource.Touch),
            new StatePayload(Screen.Menu, StateSource.Touch));
        Assert.NotEqual(
            new StatePayload(Screen.Menu, StateSource.Touch),
            new StatePayload(Screen.Menu, StateSource.Init));
    }


    [Fact]
    public void Envelope_GeneratesA32CharacterHexId()
    {
        var envelope = new WsEnvelope<StatePayload>
        {
            Type = WsTypes.State,
            Payload = new StatePayload(Screen.Menu, StateSource.Touch),
        };

        Assert.Equal(32, envelope.Id.Length);
        Assert.True(Guid.TryParseExact(envelope.Id, "N", out _));
    }

    [Fact]
    public void Envelope_EveryInstanceGetsADifferentId()
    {
        var payload = new StatePayload(Screen.Menu, StateSource.Touch);

        var a = new WsEnvelope<StatePayload> { Type = "state", Payload = payload };
        var b = new WsEnvelope<StatePayload> { Type = "state", Payload = payload };

        Assert.NotEqual(a.Id, b.Id);
    }

    [Fact]
    public void Envelope_TimestampIsTheCurrentTimeInUtc()
    {
        var before = DateTimeOffset.UtcNow;

        var envelope = new WsEnvelope<StatePayload>
        {
            Type = "state",
            Payload = new StatePayload(Screen.Menu, StateSource.Touch),
        };

        var after = DateTimeOffset.UtcNow;
        Assert.InRange(envelope.Ts, before, after);
        Assert.Equal(TimeSpan.Zero, envelope.Ts.Offset);
    }

    [Fact]
    public void Envelope_Serialized_HasExactlyTheFieldsTheBackendAccepts()
    {
        var json = Serialize(new WsEnvelope<StatePayload>
        {
            Type = WsTypes.State,
            Payload = new StatePayload(Screen.Menu, StateSource.Touch),
        });

        var root = ParseObject(json);

        Assert.Equal(new[] { "id", "ts", "type", "payload" }, PropertyNames(root));
        Assert.Equal(new[] { "screen", "source", "ref" }, PropertyNames(root.GetProperty("payload")));
    }

    [Fact]
    public void Envelope_Serialized_CarriesTheTypeAndThePayload()
    {
        var json = Serialize(new WsEnvelope<StatePayload>
        {
            Type = WsTypes.State,
            Payload = new StatePayload(Screen.Menu, StateSource.Backend, "order-1"),
        });

        var root = ParseObject(json);

        Assert.Equal("state", root.GetProperty("type").GetString());
        Assert.Equal("menu", root.GetProperty("payload").GetProperty("screen").GetString());
        Assert.Equal("backend", root.GetProperty("payload").GetProperty("source").GetString());
        Assert.Equal("order-1", root.GetProperty("payload").GetProperty("ref").GetString());
    }

    [Fact]
    public void Envelope_Serialized_WritesTheTimestampAsIso8601WithUtcOffset()
    {
        var json = Serialize(new WsEnvelope<StatePayload>
        {
            Type = "state",
            Payload = new StatePayload(Screen.Menu, StateSource.Touch),
        });

        var ts = ParseObject(json).GetProperty("ts").GetString()!;

        Assert.EndsWith("+00:00", ts);
        Assert.Equal(TimeSpan.Zero, DateTimeOffset.Parse(ts).Offset);
    }

    [Fact]
    public void Envelope_Serialized_CanBeReadWithTheNonGenericEnvelope()
    {
        var json = Serialize(new WsEnvelope<StatePayload>
        {
            Type = WsTypes.State,
            Payload = new StatePayload(Screen.Welcome, StateSource.Init),
        });

        var envelope = JsonSerializer.Deserialize<WsEnvelope>(json, WsJson.Options)!;
        var payload = envelope.Payload.Deserialize<StatePayload>(WsJson.Options);

        Assert.Equal(WsTypes.State, envelope.Type);
        Assert.Equal(new StatePayload(Screen.Welcome, StateSource.Init), payload);
    }
}
