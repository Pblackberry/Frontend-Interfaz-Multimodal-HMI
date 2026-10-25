using System.Text.Json;
using Frontend_interfaz_Multimodal_HMI.Models;

namespace FrontEnd.InterfazHMI.Tests.Unit.Models;

public class CatalogModelsTests
{
    private const string _backendMessage =
        "{'id':'a1','ts':'2026-10-22T10:00:00Z','type':'catalog_update','payload':{" +
        "'ultima_actualizacion':'2026-10-22T09:59:00','catalogo':{'items':[" +
        "{'nombre_item':'Mojito','precio_item':'5.50','estado':true,'ingredientes':'Ron, menta','codigo_modbus':'0x01'}," +
        "{'nombre_item':'Piña colada','precio_item':'6.25','estado':true,'ingredientes':null,'codigo_modbus':null,'campo_nuevo':1}" +
        "],'total':2}}}";

    private static CatalogUpdatePayload ReadPayload()
    {
        var json = _backendMessage.Replace('\'', '"');
        var envelope = JsonSerializer.Deserialize<WsEnvelope>(json, WsJson.Options)!;
        return envelope.Payload.Deserialize<CatalogUpdatePayload>(WsJson.Options)!;
    }

    // ---------- valores por defecto ----------

    [Fact]
    public void CobotMenuItem_Defaults()
    {
        var item = new CobotMenuItem();

        Assert.Equal("", item.NombreItem);
        Assert.Equal(0m, item.PrecioItem);
        Assert.False(item.Estado);
        Assert.Null(item.Ingredientes);
        Assert.Null(item.CodigoModbus);
    }

    [Fact]
    public void CobotCatalogResponse_Defaults_HasAnEmptyList()
    {
        var catalog = new CobotCatalogResponse();

        Assert.NotNull(catalog.Items);
        Assert.Empty(catalog.Items);
        Assert.Equal(0, catalog.Total);
    }

    [Fact]
    public void CatalogUpdatePayload_Defaults_HasAnEmptyCatalog()
    {
        var payload = new CatalogUpdatePayload();

        Assert.Equal("", payload.UltimaActualizacion);
        Assert.NotNull(payload.Catalogo);
        Assert.Empty(payload.Catalogo.Items);
    }

    // ---------- deserialización del mensaje real ----------

    [Fact]
    public void Payload_FromBackendMessage_ReadsUpdateDateAndTotal()
    {
        var payload = ReadPayload();

        Assert.Equal("2026-10-22T09:59:00", payload.UltimaActualizacion);
        Assert.Equal(2, payload.Catalogo.Total);
        Assert.Equal(2, payload.Catalogo.Items.Count);
    }

    [Fact]
    public void Payload_FromBackendMessage_MapsEveryItemField()
    {
        var item = ReadPayload().Catalogo.Items[0];

        Assert.Equal("Mojito", item.NombreItem);
        Assert.Equal(5.50m, item.PrecioItem);   // el backend envía el Decimal como string
        Assert.True(item.Estado);
        Assert.Equal("Ron, menta", item.Ingredientes);
        Assert.Equal("0x01", item.CodigoModbus);
    }

    [Fact]
    public void Payload_FromBackendMessage_AcceptsNullOptionalFields_AndIgnoresUnknownOnes()
    {
        var item = ReadPayload().Catalogo.Items[1];

        Assert.Equal("Piña colada", item.NombreItem);
        Assert.Equal(6.25m, item.PrecioItem);
        Assert.Null(item.Ingredientes);
        Assert.Null(item.CodigoModbus);
    }
}
