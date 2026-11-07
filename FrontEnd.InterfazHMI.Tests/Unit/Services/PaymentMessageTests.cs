using CommunityToolkit.Mvvm.Messaging;
using FrontEnd.InterfazHMI.Tests.Support;
using Frontend_interfaz_Multimodal_HMI.Models;
using Frontend_interfaz_Multimodal_HMI.Services;

namespace FrontEnd.InterfazHMI.Tests.Unit.Services;

[Collection(MessengerCollection.Name)]
public class PaymentMessagesTests : IDisposable
{
    public sealed class Spy : IRecipient<PaymentStartedMessage>, IRecipient<PaymentResultMessage>
    {
        public List<object> Received { get; } = new();

        public void Receive(PaymentStartedMessage message) => Received.Add(message);

        public void Receive(PaymentResultMessage message) => Received.Add(message);
    }

    private static readonly PaymentStartedPayload _started = new("pago-1", "http://host/pay/pago-1", "Mojito", 5.5m);
    private static readonly PaymentResultPayload _result = new("pago-1", PaymentOutcome.Approved, null);

    private readonly Spy _spy = new();

    public PaymentMessagesTests()
    {
        WeakReferenceMessenger.Default.Reset();
        WeakReferenceMessenger.Default.RegisterAll(_spy);
    }

    public void Dispose() => WeakReferenceMessenger.Default.Reset();

    [Fact]
    public void PaymentStartedMessage_KeepsItsPayload()
    {
        Assert.Same(_started, new PaymentStartedMessage(_started).Payload);
    }

    [Fact]
    public void PaymentResultMessage_KeepsTheEnvelopeIdAndThePayload()
    {
        var message = new PaymentResultMessage("env-1", _result);

        Assert.Equal("env-1", message.EnvelopeId);
        Assert.Same(_result, message.Payload);
    }

    [Fact]
    public void Messages_AreComparedByValue()
    {
        Assert.Equal(new PaymentStartedMessage(_started), new PaymentStartedMessage(_started with { }));
        Assert.Equal(new PaymentResultMessage("e", _result), new PaymentResultMessage("e", _result with { }));
        Assert.NotEqual(new PaymentResultMessage("e1", _result), new PaymentResultMessage("e2", _result));
    }

    [Fact]
    public void BothMessages_ReachTheirRecipientsThroughTheMessenger()
    {
        WeakReferenceMessenger.Default.Send(new PaymentStartedMessage(_started));
        WeakReferenceMessenger.Default.Send(new PaymentResultMessage("env-1", _result));

        Assert.Equal(
            new object[] { new PaymentStartedMessage(_started), new PaymentResultMessage("env-1", _result) },
            _spy.Received);
    }
}
