namespace Frontend_interfaz_Multimodal_HMI.Services;

public sealed record VoiceSelectDrinkMessage(string OrderId, string DrinkName);
public sealed record VoiceShowIngredientsMessage(string DrinkName);
public sealed record VoiceCloseIngredientsMessage();
