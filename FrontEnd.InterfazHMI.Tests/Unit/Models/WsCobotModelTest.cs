using System.Text.Json;
using Frontend_interfaz_Multimodal_HMI.Models;

namespace FrontEnd.InterfazHMI.Tests.Unit.Models;

public class WsCobotModelsTests
{
    private static string Serialize<T>(T value) => JsonSerializer.Serialize(value, WsJson.Options);

    private static T Read<T>(string json) => JsonSerializer.Deserialize<T>(json, WsJson.Options)!;

    // ---------- tipo de mensaje ----------

    [Fact]
    public void CobotStatusType_IsTheOneTheBackendUses()
    {
        Assert.Equal("cobot_status", WsTypes.CobotStatus);
    }

    // ---------- backend -> front ----------

    [Fact]
    public void CobotStatusPayload_IsReadFromTheBackendJson()
    {
        var payload = Read<CobotStatusPayload>(
            "{\"codigo\":12,\"tipo_evento\":\"alarma\",\"descripcion\":\"Paro de emergencia\"}");

        Assert.Equal(new CobotStatusPayload(12, "alarma", "Paro de emergencia"), payload);
    }

    [Fact]
    public void CobotStatusPayload_ReadsTheSnakeCaseEventType()
    {
        var payload = Read<CobotStatusPayload>("{\"codigo\":1,\"tipo_evento\":\"info\",\"descripcion\":\"ok\"}");

        Assert.Equal("info", payload.TipoEvento);
    }

    [Theory]
    [InlineData("12", 12)]
    [InlineData("\"12\"", 12)]
    [InlineData("0", 0)]
    [InlineData("\"0\"", 0)]
    public void CobotStatusPayload_ReadsTheCodeAsNumberOrText(string codigo, int expected)
    {
        var payload = Read<CobotStatusPayload>(
            "{\"codigo\":" + codigo + ",\"tipo_evento\":\"info\",\"descripcion\":\"x\"}");

        Assert.Equal(expected, payload.Codigo);
    }

    [Fact]
    public void CobotStatusPayload_WithANonNumericCode_IsRejected()
    {
        Assert.Throws<JsonException>(() => Read<CobotStatusPayload>(
            "{\"codigo\":\"abc\",\"tipo_evento\":\"info\",\"descripcion\":\"x\"}"));
    }

    [Fact]
    public void CobotStatusPayload_WithADecimalCode_IsRejected()
    {
        Assert.Throws<JsonException>(() => Read<CobotStatusPayload>(
            "{\"codigo\":1.5,\"tipo_evento\":\"info\",\"descripcion\":\"x\"}"));
    }

    [Fact]
    public void CobotStatusPayload_IgnoresUnknownFields()
    {
        var payload = Read<CobotStatusPayload>(
            "{\"codigo\":3,\"tipo_evento\":\"info\",\"descripcion\":\"x\",\"campo_nuevo\":true}");

        Assert.Equal(new CobotStatusPayload(3, "info", "x"), payload);
    }

    [Fact]
    public void CobotStatusPayload_FromTheFullEnvelope_CanBeExtracted()
    {
        const string json = "{\"id\":\"c1\",\"ts\":\"2026-11-06T10:00:00Z\",\"type\":\"cobot_status\","
            + "\"payload\":{\"codigo\":7,\"tipo_evento\":\"alarma\",\"descripcion\":\"Colisión detectada\"}}";

        var envelope = Read<WsEnvelope>(json);
        var payload = envelope.Payload.Deserialize<CobotStatusPayload>(WsJson.Options);

        Assert.Equal(WsTypes.CobotStatus, envelope.Type);
        Assert.Equal(new CobotStatusPayload(7, "alarma", "Colisión detectada"), payload);
    }

    // ---------- front -> backend / ida y vuelta ----------

    [Fact]
    public void CobotStatusPayload_IsWrittenInSnakeCase()
    {
        Assert.Equal(
            "{\"codigo\":12,\"tipo_evento\":\"alarma\",\"descripcion\":\"Paro\"}",
            Serialize(new CobotStatusPayload(12, "alarma", "Paro")));
    }

    [Fact]
    public void CobotStatusPayload_RoundTrip_KeepsAccentsAndSpaces()
    {
        var original = new CobotStatusPayload(9, "advertencia", "Posición fuera de rango");

        var copia = Read<CobotStatusPayload>(Serialize(original));

        Assert.Equal(original, copia);
    }

    [Fact]
    public void CobotStatusPayload_HasValueEquality()
    {
        Assert.Equal(new CobotStatusPayload(1, "info", "a"), new CobotStatusPayload(1, "info", "a"));
        Assert.NotEqual(new CobotStatusPayload(1, "info", "a"), new CobotStatusPayload(2, "info", "a"));
    }

    [Fact]
    public void CobotStatusEnvelope_HasTheShapeTheBackendExpects()
    {
        var envelope = new WsEnvelope<CobotStatusPayload>
        {
            Type = WsTypes.CobotStatus,
            Payload = new CobotStatusPayload(5, "info", "Listo"),
        };

        using var doc = JsonDocument.Parse(Serialize(envelope));
        var root = doc.RootElement;

        Assert.Equal("cobot_status", root.GetProperty("type").GetString());
        Assert.Equal(5, root.GetProperty("payload").GetProperty("codigo").GetInt32());
        Assert.Equal("info", root.GetProperty("payload").GetProperty("tipo_evento").GetString());
        Assert.Equal("Listo", root.GetProperty("payload").GetProperty("descripcion").GetString());
        Assert.False(string.IsNullOrEmpty(root.GetProperty("id").GetString()));
        Assert.True(root.TryGetProperty("ts", out _));
    }
}
