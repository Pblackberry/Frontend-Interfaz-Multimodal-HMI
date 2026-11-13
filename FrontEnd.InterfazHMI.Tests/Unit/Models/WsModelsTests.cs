using System.Reflection;
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

    private sealed record Ping(string Mensaje);

    // ---------- WsTypes ----------

    [Theory]
    [InlineData(WsTypes.CatalogUpdate, "catalog_update")]
    [InlineData(WsTypes.State, "state")]
    [InlineData(WsTypes.Navigate, "navigate")]
    [InlineData(WsTypes.SelectDrink, "select_drink")]
    [InlineData(WsTypes.ShowIngredients, "show_ingredients")]
    [InlineData(WsTypes.CloseIngredients, "close_ingredients")]
    [InlineData(WsTypes.StartPayment, "start_payment")]
    [InlineData(WsTypes.CancelPayment, "cancel_payment")]
    [InlineData(WsTypes.PaymentStarted, "payment_started")]
    [InlineData(WsTypes.PaymentResult, "payment_result")]
    [InlineData(WsTypes.CobotStatus, "cobot_status")]
    public void WsTypes_Constants_MatchBackendTypeNames(string actual, string esperado)
    {
        Assert.Equal(esperado, actual);
    }

    [Fact]
    public void WsTypes_Constants_AreUnique()
    {
        var valores = typeof(WsTypes)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Select(f => (string)f.GetRawConstantValue()!)
            .ToList();

        Assert.Equal(valores.Count, valores.Distinct().Count());
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

    // ---------- WsEnvelope<T> ----------

    [Fact]
    public void GenericEnvelope_Defaults_GenerateIdAndTimestamp()
    {
        var antes = DateTimeOffset.UtcNow;

        var envelope = new WsEnvelope<Ping> { Type = "ping", Payload = new Ping("hola") };

        Assert.Equal(32, envelope.Id.Length);   // Guid "N": 32 caracteres, sin guiones
        Assert.DoesNotContain("-", envelope.Id);
        Assert.InRange(envelope.Ts, antes, DateTimeOffset.UtcNow);
        Assert.Equal(TimeSpan.Zero, envelope.Ts.Offset);
    }

    [Fact]
    public void GenericEnvelope_EachInstance_GetsADifferentId()
    {
        var a = new WsEnvelope<Ping> { Type = "ping", Payload = new Ping("a") };
        var b = new WsEnvelope<Ping> { Type = "ping", Payload = new Ping("b") };

        Assert.NotEqual(a.Id, b.Id);
    }

    [Fact]
    public void GenericEnvelope_Serialize_UsesSnakeCaseAndNestsPayload()
    {
        var envelope = new WsEnvelope<Ping>
        {
            Id = "abc",
            Ts = new DateTimeOffset(2026, 10, 22, 10, 0, 0, TimeSpan.Zero),
            Type = "ping",
            Payload = new Ping("hola"),
        };

        var json = JsonSerializer.Serialize(envelope, WsJson.Options);

        Assert.Contains("\"id\":\"abc\"", json);
        Assert.Contains("\"type\":\"ping\"", json);
        Assert.Contains("\"payload\":{\"mensaje\":\"hola\"}", json);
    }

    [Fact]
    public void GenericEnvelope_RoundTrip_KeepsAllFields()
    {
        var original = new WsEnvelope<Ping>
        {
            Id = "abc",
            Ts = new DateTimeOffset(2026, 10, 22, 10, 0, 0, TimeSpan.Zero),
            Type = "ping",
            Payload = new Ping("hola"),
        };

        var json = JsonSerializer.Serialize(original, WsJson.Options);
        var copia = JsonSerializer.Deserialize<WsEnvelope<Ping>>(json, WsJson.Options)!;

        Assert.Equal(original.Id, copia.Id);
        Assert.Equal(original.Ts, copia.Ts);
        Assert.Equal(original.Type, copia.Type);
        Assert.Equal(original.Payload, copia.Payload);
    }

    // ---------- Enums ----------

    [Theory]
    [InlineData(Screen.Welcome, "welcome")]
    [InlineData(Screen.Menu, "menu")]
    [InlineData(Screen.Payment, "payment")]
    [InlineData(Screen.PaymentResult, "payment_result")]
    public void Screen_Serializes_AsSnakeCaseString(Screen screen, string esperado)
    {
        var json = JsonSerializer.Serialize(screen, WsJson.Options);

        Assert.Equal($"\"{esperado}\"", json);
    }

    [Theory]
    [InlineData(PaymentOutcome.Approved, "approved")]
    [InlineData(PaymentOutcome.Rejected, "rejected")]
    [InlineData(PaymentOutcome.Expired, "expired")]
    [InlineData(PaymentOutcome.Error, "error")]
    public void PaymentOutcome_RoundTrips_AsSnakeCaseString(PaymentOutcome outcome, string esperado)
    {
        var json = JsonSerializer.Serialize(outcome, WsJson.Options);
        var back = JsonSerializer.Deserialize<PaymentOutcome>(json, WsJson.Options);

        Assert.Equal($"\"{esperado}\"", json);
        Assert.Equal(outcome, back);
    }

    [Theory]
    [InlineData(StateSource.Touch, "touch")]
    [InlineData(StateSource.Backend, "backend")]
    [InlineData(StateSource.Init, "init")]
    public void StateSource_Serializes_AsSnakeCaseString(StateSource source, string esperado)
    {
        Assert.Equal($"\"{esperado}\"", JsonSerializer.Serialize(source, WsJson.Options));
    }
}
