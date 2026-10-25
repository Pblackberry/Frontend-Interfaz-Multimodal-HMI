using System.Text.Json;
using Frontend_interfaz_Multimodal_HMI.Models;

namespace FrontEnd.InterfazHMI.Tests.Unit.Models;

public class WsModelsTests
{

    private const string _backendCatalogUpdate =
        "{'id':'a1b2c3','ts':'2026-10-22T10:00:00Z','type':'catalog_update'," +
        "'payload':{'ultima_actualizacion':'2026-10-22T09:59:00'," +
        "'catalogo':{'items':[{'nombre_item':'Mojito','precio_item':'5.50','estado':true}]," +
        "'total':1}}}";

    private static string Json(string singleQuoted) => singleQuoted.Replace('\'', '"');

    private static T? Deserialize<T>(string singleQuotedJson)
        => JsonSerializer.Deserialize<T>(Json(singleQuotedJson), WsJson.Options);

    private sealed class ItemDto
    {
        public string NombreItem { get; set; } = "";
        public decimal PrecioItem { get; set; }
        public bool Estado { get; set; }
    }

    private sealed class CatalogDto
    {
        public List<ItemDto> Items { get; set; } = new();
        public int Total { get; set; }
    }

    private sealed class PayloadDto
    {
        public string UltimaActualizacion { get; set; } = "";
        public CatalogDto Catalogo { get; set; } = new();
    }

    public enum Modo { Menu, TouchAction }

    private sealed class ConEnum
    {
        public Modo Modo { get; set; }
    }

    // ---------- WsTypes ----------

    [Fact]
    public void WsTypes_CatalogUpdate_MatchesBackendTypeName()
    {
        Assert.Equal("catalog_update", WsTypes.CatalogUpdate);
    }

    // ---------- WsJson.Options ----------

    [Fact]
    public void Options_IsASharedInstance()
    {
        Assert.Same(WsJson.Options, WsJson.Options);
    }

    [Fact]
    public void Options_Serialize_UsesSnakeCasePropertyNames()
    {
        var json = JsonSerializer.Serialize(
            new ItemDto { NombreItem = "Mojito", PrecioItem = 5.5m, Estado = true },
            WsJson.Options);

        Assert.Contains("\"nombre_item\":\"Mojito\"", json);
        Assert.Contains("\"precio_item\":5.5", json);
        Assert.Contains("\"estado\":true", json);
    }

    [Fact]
    public void Options_Deserialize_ReadsSnakeCasePropertyNames()
    {
        var item = Deserialize<ItemDto>("{'nombre_item':'Daiquiri','precio_item':6,'estado':false}");

        Assert.NotNull(item);
        Assert.Equal("Daiquiri", item!.NombreItem);
        Assert.Equal(6m, item.PrecioItem);
        Assert.False(item.Estado);
    }

    [Theory]
    [InlineData("'5.50'")]  // como lo serializa pydantic para Decimal
    [InlineData("5.5")]
    [InlineData("5.50")]
    public void Options_Deserialize_AcceptsDecimalAsStringOrNumber(string precio)
    {
        var item = Deserialize<ItemDto>("{'nombre_item':'x','precio_item':" + precio + ",'estado':true}");

        Assert.Equal(5.5m, item!.PrecioItem);
    }

    [Fact]
    public void Options_Deserialize_AcceptsIntegerAsString()
    {
        var catalogo = Deserialize<CatalogDto>("{'items':[],'total':'3'}");

        Assert.Equal(3, catalogo!.Total);
    }

    [Fact]
    public void Options_Serialize_WritesEnumsAsSnakeCaseStrings()
    {
        var json = JsonSerializer.Serialize(new ConEnum { Modo = Modo.TouchAction }, WsJson.Options);

        Assert.Equal("{\"modo\":\"touch_action\"}", json);
    }

    [Theory]
    [InlineData("menu", Modo.Menu)]
    [InlineData("touch_action", Modo.TouchAction)]
    public void Options_Deserialize_ReadsEnumsFromSnakeCaseStrings(string valor, Modo esperado)
    {
        var obj = Deserialize<ConEnum>("{'modo':'" + valor + "'}");

        Assert.Equal(esperado, obj!.Modo);
    }

    [Fact]
    public void Options_Deserialize_UnknownEnumValue_Throws()
    {
        Assert.Throws<JsonException>(() => Deserialize<ConEnum>("{'modo':'no_existe'}"));
    }

    [Fact]
    public void Options_Deserialize_IgnoresUnknownProperties()
    {
        var item = Deserialize<ItemDto>("{'nombre_item':'x','campo_nuevo':123,'estado':true}");

        Assert.Equal("x", item!.NombreItem);
    }

    // ---------- WsEnvelope ----------

    [Fact]
    public void Envelope_Defaults_AreEmpty()
    {
        var envelope = new WsEnvelope();

        Assert.Equal("", envelope.Id);
        Assert.Equal("", envelope.Type);
        Assert.Equal(default, envelope.Ts);
        Assert.Equal(JsonValueKind.Undefined, envelope.Payload.ValueKind);
    }

    [Fact]
    public void Envelope_Deserialize_MapsAllFieldsFromBackendMessage()
    {
        var envelope = Deserialize<WsEnvelope>(_backendCatalogUpdate)!;

        Assert.Equal("a1b2c3", envelope.Id);
        Assert.Equal(WsTypes.CatalogUpdate, envelope.Type);
        Assert.Equal(JsonValueKind.Object, envelope.Payload.ValueKind);
    }

    [Fact]
    public void Envelope_Deserialize_KeepsTimestampInUtc()
    {
        var envelope = Deserialize<WsEnvelope>(_backendCatalogUpdate)!;

        Assert.Equal(new DateTimeOffset(2026, 10, 22, 10, 0, 0, TimeSpan.Zero), envelope.Ts);
        Assert.Equal(TimeSpan.Zero, envelope.Ts.Offset);
    }

    [Fact]
    public void Envelope_Deserialize_AcceptsTimestampWithMicroseconds()
    {
        var envelope = Deserialize<WsEnvelope>(
            "{'id':'x','ts':'2026-10-22T10:00:00.123456Z','type':'catalog_update'}")!;

        Assert.Equal(2026, envelope.Ts.Year);
        Assert.Equal(TimeSpan.Zero, envelope.Ts.Offset);
    }

    [Fact]
    public void Envelope_Payload_CanBeConvertedOnceTheTypeIsKnown()
    {
        var envelope = Deserialize<WsEnvelope>(_backendCatalogUpdate)!;

        var payload = envelope.Payload.Deserialize<PayloadDto>(WsJson.Options)!;

        Assert.Equal("2026-10-22T09:59:00", payload.UltimaActualizacion);
        Assert.Equal(1, payload.Catalogo.Total);
        Assert.Equal("Mojito", payload.Catalogo.Items[0].NombreItem);
        Assert.Equal(5.5m, payload.Catalogo.Items[0].PrecioItem);
        Assert.True(payload.Catalogo.Items[0].Estado);
    }

    [Fact]
    public void Envelope_Deserialize_WithoutPayload_LeavesItUndefined()
    {
        var envelope = Deserialize<WsEnvelope>("{'id':'x','ts':'2026-10-22T10:00:00Z','type':'navigate'}")!;

        Assert.Equal(JsonValueKind.Undefined, envelope.Payload.ValueKind);
    }

    [Fact]
    public void Envelope_Deserialize_InvalidJson_Throws()
    {
        Assert.ThrowsAny<JsonException>(() => JsonSerializer.Deserialize<WsEnvelope>("no es json", WsJson.Options));
    }

    [Fact]
    public void Envelope_Serialize_UsesSnakeCaseAndKeepsThePayload()
    {
        var original = Deserialize<WsEnvelope>(_backendCatalogUpdate)!;

        var json = JsonSerializer.Serialize(original, WsJson.Options);
        var roundTrip = JsonSerializer.Deserialize<WsEnvelope>(json, WsJson.Options)!;

        Assert.Contains("\"type\":\"catalog_update\"", json);
        Assert.Equal(original.Id, roundTrip.Id);
        Assert.Equal(original.Ts, roundTrip.Ts);
        Assert.Equal(original.Type, roundTrip.Type);
        Assert.Equal(original.Payload.GetRawText(), roundTrip.Payload.GetRawText());
    }
}
