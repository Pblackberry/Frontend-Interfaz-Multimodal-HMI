using System.Text.Json;
using Frontend_interfaz_Multimodal_HMI.Models;

namespace FrontEnd.InterfazHMI.Tests.Unit.Models;

public class WsPaymentModelsTests
{
    private static string Serialize<T>(T value) => JsonSerializer.Serialize(value, WsJson.Options);

    private static T Read<T>(string json) => JsonSerializer.Deserialize<T>(json, WsJson.Options)!;

    // ---------- tipos de mensaje ----------

    [Fact]
    public void PaymentTypes_AreTheOnesTheBackendUses()
    {
        Assert.Equal("start_payment", WsTypes.StartPayment);
        Assert.Equal("cancel_payment", WsTypes.CancelPayment);
        Assert.Equal("payment_started", WsTypes.PaymentStarted);
        Assert.Equal("payment_result", WsTypes.PaymentResult);
    }

    // ---------- front -> backend ----------

    [Fact]
    public void StartPaymentPayload_IsWrittenWithTheDrinkName()
    {
        Assert.Equal("{\"drink\":\"Mojito\"}", Serialize(new StartPaymentPayload("Mojito")));
    }

    [Fact]
    public void StartPaymentPayload_KeepsAccentsAndSpaces()
    {
        Assert.Equal(new StartPaymentPayload("Piña colada"), Read<StartPaymentPayload>(Serialize(new StartPaymentPayload("Piña colada"))));
    }

    [Fact]
    public void CancelPaymentPayload_IsWrittenWithTheSnakeCasePaymentId()
    {
        Assert.Equal("{\"payment_id\":\"pago-1\"}", Serialize(new CancelPaymentPayload("pago-1")));
    }

    [Fact]
    public void CancelPaymentPayload_WithoutId_IsWrittenAsNull()
    {
        Assert.Equal("{\"payment_id\":null}", Serialize(new CancelPaymentPayload(null)));
    }

    [Fact]
    public void StartPaymentEnvelope_HasTheShapeTheBackendExpects()
    {
        var envelope = new WsEnvelope<StartPaymentPayload>
        {
            Type = WsTypes.StartPayment,
            Payload = new StartPaymentPayload("Mojito"),
        };

        using var doc = JsonDocument.Parse(Serialize(envelope));
        var root = doc.RootElement;

        Assert.Equal("start_payment", root.GetProperty("type").GetString());
        Assert.Equal("Mojito", root.GetProperty("payload").GetProperty("drink").GetString());
        Assert.False(string.IsNullOrEmpty(root.GetProperty("id").GetString()));
        Assert.True(root.TryGetProperty("ts", out _));
    }

    [Fact]
    public void CancelPaymentEnvelope_HasTheShapeTheBackendExpects()
    {
        var envelope = new WsEnvelope<CancelPaymentPayload>
        {
            Type = WsTypes.CancelPayment,
            Payload = new CancelPaymentPayload("pago-1"),
        };

        using var doc = JsonDocument.Parse(Serialize(envelope));

        Assert.Equal("cancel_payment", doc.RootElement.GetProperty("type").GetString());
        Assert.Equal("pago-1", doc.RootElement.GetProperty("payload").GetProperty("payment_id").GetString());
    }

    // ---------- backend -> front: payment_started ----------

    [Fact]
    public void PaymentStartedPayload_IsReadFromTheBackendJson()
    {
        var payload = Read<PaymentStartedPayload>(
            "{\"payment_id\":\"tok-1\",\"pay_url\":\"http://h:8888/pay/tok-1\",\"drink_name\":\"Mojito\",\"price\":\"5.50\"}");

        Assert.Equal(new PaymentStartedPayload("tok-1", "http://h:8888/pay/tok-1", "Mojito", 5.5m), payload);
    }

    [Theory]
    [InlineData("\"5.50\"", "5.50")]
    [InlineData("5.5", "5.5")]
    [InlineData("\"7\"", "7")]
    [InlineData("12", "12")]
    [InlineData("\"0.99\"", "0.99")]
    public void PaymentStartedPayload_ReadsThePriceAsTextOrAsNumber(string json, string expected)
    {
        var payload = Read<PaymentStartedPayload>(
            "{\"payment_id\":\"a\",\"pay_url\":\"u\",\"drink_name\":\"n\",\"price\":" + json + "}");

        Assert.Equal(decimal.Parse(expected, System.Globalization.CultureInfo.InvariantCulture), payload.Price);
    }

    [Fact]
    public void PaymentStartedPayload_FromTheFullEnvelope_CanBeExtracted()
    {
        const string json = "{\"id\":\"e1\",\"ts\":\"2026-11-06T10:00:00Z\",\"type\":\"payment_started\","
            + "\"payload\":{\"payment_id\":\"tok-1\",\"pay_url\":\"http://h/pay/tok-1\",\"drink_name\":\"Mojito\",\"price\":\"5.50\"}}";

        var envelope = Read<WsEnvelope>(json);
        var payload = envelope.Payload.Deserialize<PaymentStartedPayload>(WsJson.Options);

        Assert.Equal(WsTypes.PaymentStarted, envelope.Type);
        Assert.Equal("tok-1", payload!.PaymentId);
        Assert.Equal(5.5m, payload.Price);
    }

    [Fact]
    public void PaymentStartedPayload_WithAnInvalidPrice_IsRejected()
    {
        Assert.Throws<JsonException>(() => Read<PaymentStartedPayload>(
            "{\"payment_id\":\"a\",\"pay_url\":\"u\",\"drink_name\":\"n\",\"price\":\"barato\"}"));
    }

    // ---------- backend -> front: payment_result ----------

    [Theory]
    [InlineData("approved", PaymentOutcome.Approved)]
    [InlineData("rejected", PaymentOutcome.Rejected)]
    [InlineData("expired", PaymentOutcome.Expired)]
    [InlineData("error", PaymentOutcome.Error)]
    public void PaymentResultPayload_ReadsEveryStatusTheBackendSends(string status, PaymentOutcome expected)
    {
        var payload = Read<PaymentResultPayload>("{\"payment_id\":\"tok-1\",\"status\":\"" + status + "\",\"reason\":null}");

        Assert.Equal(new PaymentResultPayload("tok-1", expected, null), payload);
    }

    [Fact]
    public void PaymentResultPayload_ReadsTheReason()
    {
        var payload = Read<PaymentResultPayload>(
            "{\"payment_id\":null,\"status\":\"error\",\"reason\":\"drink_unavailable\"}");

        Assert.Null(payload.PaymentId);
        Assert.Equal(PaymentOutcome.Error, payload.Status);
        Assert.Equal("drink_unavailable", payload.Reason);
    }

    [Fact]
    public void PaymentResultPayload_WithoutOptionalFields_IsStillRead()
    {
        var payload = Read<PaymentResultPayload>("{\"status\":\"approved\"}");

        Assert.Null(payload.PaymentId);
        Assert.Null(payload.Reason);
        Assert.Equal(PaymentOutcome.Approved, payload.Status);
    }

    [Theory]
    [InlineData("cancelled")]
    [InlineData("pending")]
    [InlineData("APPROVED_X")]
    [InlineData("")]
    public void PaymentResultPayload_UnknownStatus_IsRejected(string status)
    {
        Assert.Throws<JsonException>(
            () => Read<PaymentResultPayload>("{\"payment_id\":\"a\",\"status\":\"" + status + "\"}"));
    }

    [Fact]
    public void PaymentResultPayload_WritesTheStatusInLowerCase()
    {
        Assert.Equal(
            "{\"payment_id\":\"a\",\"status\":\"approved\",\"reason\":null}",
            Serialize(new PaymentResultPayload("a", PaymentOutcome.Approved, null)));
    }

    [Fact]
    public void PaymentOutcome_HasExactlyTheStatusesTheBackendNotifies()
    {
        var names = Enum.GetNames<PaymentOutcome>().Select(n => n.ToLowerInvariant()).OrderBy(n => n, StringComparer.Ordinal);

        Assert.Equal(new[] { "approved", "error", "expired", "rejected" }, names);
    }
}
