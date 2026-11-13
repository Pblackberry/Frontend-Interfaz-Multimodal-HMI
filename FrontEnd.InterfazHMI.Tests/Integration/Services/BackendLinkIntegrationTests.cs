using System.Text.Json;
using CommunityToolkit.Mvvm.Messaging;
using FrontEnd.InterfazHMI.Tests.Support;
using Frontend_interfaz_Multimodal_HMI.Models;
using Frontend_interfaz_Multimodal_HMI.Services;

namespace FrontEnd.InterfazHMI.Tests.Integration.Services;

[Collection(MessengerCollection.Name)]
public class BackendLinkIntegrationTests
{
    private static readonly TimeSpan _noMessageWait = TimeSpan.FromMilliseconds(400);
    private static readonly TimeSpan _waitLimit = TimeSpan.FromSeconds(5);

    private static JsonElement Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
    }

    private static async Task<JsonElement> NextMessageAsync(WsClientSession session)
        => Parse(await FakeWsServer.ReceiveTextAsync(session.ServerSocket));

    private static string BackendMessage(string type, string id, string payloadJson)
        => $"{{\"id\":\"{id}\",\"ts\":\"2026-10-22T10:00:00Z\",\"type\":\"{type}\",\"payload\":{payloadJson}}}";

    // El messenger es global (Default), por eso cada test filtra por el contenido esperado
    // y se desregistra al terminar.
    private static async Task<TMessage> SendAndWaitForMessageAsync<TMessage>(
        WsClientSession session, string json, Func<TMessage, bool> match)
        where TMessage : class
    {
        var received = new TaskCompletionSource<TMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        var recipient = new object();
        WeakReferenceMessenger.Default.Register<object, TMessage>(recipient, (_, message) =>
        {
            if (match(message))
            {
                received.TrySetResult(message);
            }
        });

        try
        {
            await FakeWsServer.SendTextAsync(session.ServerSocket, json);
            return await received.Task.WaitAsync(_waitLimit);
        }
        finally
        {
            WeakReferenceMessenger.Default.UnregisterAll(recipient);
        }
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var limit = DateTime.UtcNow + _waitLimit;
        while (!condition() && DateTime.UtcNow < limit)
        {
            await Task.Delay(20);
        }
    }

    // ---------- Navegación local -> backend ----------

    [Fact]
    public async Task Navigated_SendsAStateMessageToTheBackend()
    {
        await using var server = new FakeWsServer();
        await using var session = await WsClientSession.StartAsync(server);
        var nav = new FakeNavigationService();
        _ = new BackendLink(session.Client, nav);

        nav.RaiseNavigated(Screen.Menu, StateSource.Touch);
        var message = await NextMessageAsync(session);

        Assert.Equal("state", message.GetProperty("type").GetString());
        Assert.Equal("menu", message.GetProperty("payload").GetProperty("screen").GetString());
        Assert.Equal("touch", message.GetProperty("payload").GetProperty("source").GetString());
    }

    [Theory]
    [InlineData(Screen.Welcome, StateSource.Touch, null, "welcome", "touch")]
    [InlineData(Screen.Menu, StateSource.Touch, null, "menu", "touch")]
    [InlineData(Screen.Menu, StateSource.Backend, "order-1", "menu", "backend")]
    [InlineData(Screen.Welcome, StateSource.Backend, "order-2", "welcome", "backend")]
    [InlineData(Screen.Welcome, StateSource.Init, null, "welcome", "init")]
    public async Task Navigated_SendsTheScreenSourceAndOrderIdInThePayload(
        Screen screen, StateSource source, string? orderId, string expectedScreen, string expectedSource)
    {
        await using var server = new FakeWsServer();
        await using var session = await WsClientSession.StartAsync(server);
        var nav = new FakeNavigationService();
        _ = new BackendLink(session.Client, nav);

        nav.RaiseNavigated(screen, source, orderId);
        var payload = (await NextMessageAsync(session)).GetProperty("payload");

        Assert.Equal(expectedScreen, payload.GetProperty("screen").GetString());
        Assert.Equal(expectedSource, payload.GetProperty("source").GetString());
        if (orderId is null)
        {
            Assert.Equal(JsonValueKind.Null, payload.GetProperty("ref").ValueKind);
        }
        else
        {
            Assert.Equal(orderId, payload.GetProperty("ref").GetString());
        }
    }

    [Fact]
    public async Task Navigated_SendsAnEnvelopeWithIdAndTimestamp()
    {
        await using var server = new FakeWsServer();
        await using var session = await WsClientSession.StartAsync(server);
        var nav = new FakeNavigationService();
        _ = new BackendLink(session.Client, nav);

        nav.RaiseNavigated(Screen.Menu);
        var message = await NextMessageAsync(session);

        Assert.True(Guid.TryParseExact(message.GetProperty("id").GetString(), "N", out _));
        Assert.True(DateTimeOffset.TryParse(message.GetProperty("ts").GetString(), out var ts));
        Assert.Equal(TimeSpan.Zero, ts.Offset);
    }

    [Fact]
    public async Task EveryNavigationIsReported_InOrder()
    {
        await using var server = new FakeWsServer();
        await using var session = await WsClientSession.StartAsync(server);
        var nav = new FakeNavigationService();
        _ = new BackendLink(session.Client, nav);

        nav.RaiseNavigated(Screen.Menu, StateSource.Touch);
        nav.RaiseNavigated(Screen.Welcome, StateSource.Backend, "order-5");
        nav.RaiseNavigated(Screen.Menu, StateSource.Touch);

        var screens = new List<string?>();
        for (var i = 0; i < 3; i++)
        {
            screens.Add((await NextMessageAsync(session)).GetProperty("payload").GetProperty("screen").GetString());
        }

        Assert.Equal(new[] { "menu", "welcome", "menu" }, screens);
    }

    [Fact]
    public async Task EachMessageHasItsOwnId()
    {
        await using var server = new FakeWsServer();
        await using var session = await WsClientSession.StartAsync(server);
        var nav = new FakeNavigationService();
        _ = new BackendLink(session.Client, nav);

        nav.RaiseNavigated(Screen.Menu);
        nav.RaiseNavigated(Screen.Welcome);
        var first = (await NextMessageAsync(session)).GetProperty("id").GetString();
        var second = (await NextMessageAsync(session)).GetProperty("id").GetString();

        Assert.NotEqual(first, second);
    }

    [Fact]
    public async Task NavigationsMadeBeforeTheConnection_AreNotQueued()
    {
        await using var server = new FakeWsServer();
        var nav = new FakeNavigationService();
        BackendLink? link = null;

        await using var session = await WsClientSession.StartAsync(server, beforeRun: client =>
        {
            link = new BackendLink(client, nav);
            nav.RaiseNavigated(Screen.Menu, StateSource.Touch);
        });
        var late = await FakeWsServer.TryReceiveTextAsync(session.ServerSocket, _noMessageWait);

        Assert.NotNull(link);
        Assert.Null(late);
    }

    // ---------- Conexión ----------

    [Fact]
    public async Task OnConnected_WithACurrentScreen_ReportsItWithSourceInit()
    {
        await using var server = new FakeWsServer();
        var nav = new FakeNavigationService { CurrentScreen = Screen.Menu };

        await using var session = await WsClientSession.StartAsync(server, beforeRun: client =>
        {
            _ = new BackendLink(client, nav);
        });
        var message = await NextMessageAsync(session);

        Assert.Equal("state", message.GetProperty("type").GetString());
        Assert.Equal("menu", message.GetProperty("payload").GetProperty("screen").GetString());
        Assert.Equal("init", message.GetProperty("payload").GetProperty("source").GetString());
        Assert.Equal(JsonValueKind.Null, message.GetProperty("payload").GetProperty("ref").ValueKind);
    }

    [Fact]
    public async Task OnConnected_WithoutACurrentScreen_SendsNothing()
    {
        await using var server = new FakeWsServer();
        var nav = new FakeNavigationService { CurrentScreen = null };

        await using var session = await WsClientSession.StartAsync(server, beforeRun: client =>
        {
            _ = new BackendLink(client, nav);
        });
        var message = await FakeWsServer.TryReceiveTextAsync(session.ServerSocket, _noMessageWait);

        Assert.Null(message);
    }

    // ---------- Backend -> navegación ----------

    [Theory]
    [InlineData("welcome", Screen.Welcome)]
    [InlineData("menu", Screen.Menu)]
    [InlineData("payment", Screen.Payment)]
    [InlineData("payment_result", Screen.PaymentResult)]
    public async Task NavigateFromBackend_NavigatesWithSourceBackendAndTheEnvelopeId(string screenName, Screen expected)
    {
        await using var server = new FakeWsServer();
        await using var session = await WsClientSession.StartAsync(server);
        var nav = new FakeNavigationService();
        _ = new BackendLink(session.Client, nav);

        await FakeWsServer.SendTextAsync(
            session.ServerSocket,
            BackendMessage(WsTypes.Navigate, "nav-1", $"{{\"screen\":\"{screenName}\"}}"));
        await WaitUntilAsync(() => nav.NavigateToCalls.Count > 0);

        var call = Assert.Single(nav.NavigateToCalls);
        Assert.Equal(expected, call.Screen);
        Assert.Equal(StateSource.Backend, call.Source);
        Assert.Equal("nav-1", call.OrderId);
    }

    // ---------- Backend -> mensajes internos (WeakReferenceMessenger) ----------

    [Fact]
    public async Task SelectDrinkFromBackend_SendsAVoiceSelectDrinkMessage()
    {
        await using var server = new FakeWsServer();
        await using var session = await WsClientSession.StartAsync(server);
        _ = new BackendLink(session.Client, new FakeNavigationService());

        var message = await SendAndWaitForMessageAsync<VoiceSelectDrinkMessage>(
            session,
            BackendMessage(WsTypes.SelectDrink, "sel-1", "{\"name\":\"Mojito\"}"),
            m => m.OrderId == "sel-1" && m.DrinkName == "Mojito");

        Assert.Equal("sel-1", message.OrderId);
        Assert.Equal("Mojito", message.DrinkName);
    }

    [Fact]
    public async Task ShowIngredientsFromBackend_SendsAVoiceShowIngredientsMessage()
    {
        await using var server = new FakeWsServer();
        await using var session = await WsClientSession.StartAsync(server);
        _ = new BackendLink(session.Client, new FakeNavigationService());

        var message = await SendAndWaitForMessageAsync<VoiceShowIngredientsMessage>(
            session,
            BackendMessage(WsTypes.ShowIngredients, "ing-1", "{\"name\":\"Daiquiri\"}"),
            m => m.DrinkName == "Daiquiri");

        Assert.Equal("Daiquiri", message.DrinkName);
    }

    [Fact]
    public async Task CloseIngredientsFromBackend_SendsAVoiceCloseIngredientsMessage()
    {
        await using var server = new FakeWsServer();
        await using var session = await WsClientSession.StartAsync(server);
        _ = new BackendLink(session.Client, new FakeNavigationService());

        var message = await SendAndWaitForMessageAsync<VoiceCloseIngredientsMessage>(
            session,
            BackendMessage(WsTypes.CloseIngredients, "close-1", "{}"),
            _ => true);

        Assert.NotNull(message);
    }

    [Fact]
    public async Task PaymentStartedFromBackend_SendsAPaymentStartedMessage()
    {
        await using var server = new FakeWsServer();
        await using var session = await WsClientSession.StartAsync(server);
        _ = new BackendLink(session.Client, new FakeNavigationService());
        var expected = new PaymentStartedPayload("pay-1", "https://pago.test/pay-1", "Mojito", 5.5m);

        var message = await SendAndWaitForMessageAsync<PaymentStartedMessage>(
            session,
            BackendMessage(
                WsTypes.PaymentStarted,
                "ps-1",
                "{\"payment_id\":\"pay-1\",\"pay_url\":\"https://pago.test/pay-1\"," +
                "\"drink_name\":\"Mojito\",\"price\":\"5.50\"}"),
            m => m.Payload.PaymentId == "pay-1");

        Assert.Equal(expected, message.Payload);
    }

    [Fact]
    public async Task PaymentResultFromBackend_SendsAPaymentResultMessageWithTheEnvelopeId()
    {
        await using var server = new FakeWsServer();
        await using var session = await WsClientSession.StartAsync(server);
        _ = new BackendLink(session.Client, new FakeNavigationService());
        var expected = new PaymentResultPayload("pay-2", PaymentOutcome.Approved, null);

        var message = await SendAndWaitForMessageAsync<PaymentResultMessage>(
            session,
            BackendMessage(
                WsTypes.PaymentResult,
                "pr-1",
                "{\"payment_id\":\"pay-2\",\"status\":\"approved\",\"reason\":null}"),
            m => m.EnvelopeId == "pr-1");

        Assert.Equal("pr-1", message.EnvelopeId);
        Assert.Equal(expected, message.Payload);
    }

    [Fact]
    public async Task CobotStatusFromBackend_SendsACobotStatusMessage()
    {
        await using var server = new FakeWsServer();
        await using var session = await WsClientSession.StartAsync(server);
        _ = new BackendLink(session.Client, new FakeNavigationService());
        var expected = new CobotStatusPayload(42, "info", "Preparando bebida");

        var message = await SendAndWaitForMessageAsync<CobotStatusMessage>(
            session,
            BackendMessage(
                WsTypes.CobotStatus,
                "cb-1",
                "{\"codigo\":42,\"tipo_evento\":\"info\",\"descripcion\":\"Preparando bebida\"}"),
            m => m.Payload.Codigo == 42);

        Assert.Equal(expected, message.Payload);
    }
}
