using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Frontend_interfaz_Multimodal_HMI.Models;

public static class WsJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,        // NombreItem <-> nombre_item
        NumberHandling = JsonNumberHandling.AllowReadingFromString,    // "5.50" -> decimal
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) },
    };
}

public static class WsTypes
{
    public const string CatalogUpdate = "catalog_update";
}

// Sobre común de todos los mensajes; el payload queda como JsonElement
// hasta saber el tipo.
public sealed class WsEnvelope
{
    public string Id { get; set; } = "";
    public DateTimeOffset Ts { get; set; }
    public string Type { get; set; } = "";
    public JsonElement Payload { get; set; }
}
