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
    public const string SelectDrink = "select_drink";
    public const string ShowIngredients = "show_ingredients";
    public const string CloseIngredients = "close_ingredients";
    public const string StartPayment = "start_payment";
    public const string CancelPayment = "cancel_payment";
    public const string PaymentStarted = "payment_started";
    public const string PaymentResult = "payment_result";
}

public enum Screen { Welcome, Menu, Payment, PaymentResult }
public enum StateSource { Touch, Backend, Init }


public sealed class WsEnvelope<T>
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public DateTimeOffset Ts { get; set; } = DateTimeOffset.UtcNow;
    public required string Type { get; set; }
    public required T Payload { get; set; }
}

public sealed record DrinkRefPayload(string Name);
public sealed record StatePayload(Screen Screen, StateSource Source, string? Ref = null);
public sealed class WsEnvelope
{
    public string Id { get; set; } = "";
    public DateTimeOffset Ts { get; set; }
    public string Type { get; set; } = "";
    public JsonElement Payload { get; set; }
}

public sealed record NavigatePayload(Screen Screen);

public enum PaymentOutcome { Approved, Rejected, Expired, Error }

public sealed record StartPaymentPayload(string Drink);
public sealed record CancelPaymentPayload(string? PaymentId);
public sealed record PaymentStartedPayload(string PaymentId, string PayUrl, string DrinkName, decimal Price);
public sealed record PaymentResultPayload(string? PaymentId, PaymentOutcome Status, string? Reason);
