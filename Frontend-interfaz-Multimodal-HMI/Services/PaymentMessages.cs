using Frontend_interfaz_Multimodal_HMI.Models;

namespace Frontend_interfaz_Multimodal_HMI.Services;

public sealed record PaymentStartedMessage(PaymentStartedPayload Payload);
public sealed record PaymentResultMessage(string EnvelopeId, PaymentResultPayload Payload);
