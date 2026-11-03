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
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) },
    };
}

public static class WsTypes
{
    public const string CatalogUpdate = "catalog_update";
    public const string State = "state";
    public const string Navigate = "navigate";
}

public enum Screen { Welcome, Menu, Payment }
public enum StateSource { Touch, Backend, Init }


public sealed class WsEnvelope<T>
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public DateTimeOffset Ts { get; set; } = DateTimeOffset.UtcNow;
    public required string Type { get; set; }
    public required T Payload { get; set; }
}

public sealed record StatePayload(Screen Screen, StateSource Source, string? Ref = null);
public sealed class WsEnvelope
{
    public string Id { get; set; } = "";
    public DateTimeOffset Ts { get; set; }
    public string Type { get; set; } = "";
    public JsonElement Payload { get; set; }
}

public sealed record NavigatePayload(Screen Screen);
