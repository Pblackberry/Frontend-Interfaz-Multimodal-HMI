using Frontend_interfaz_Multimodal_HMI.Services;

namespace FrontEnd.InterfazHMI.Tests.Unit.Services;

public class VoiceMessagesTests
{
    [Fact]
    public void VoiceSelectDrinkMessage_KeepsTheOrderIdAndTheDrinkName()
    {
        var message = new VoiceSelectDrinkMessage("order-1", "Mojito");

        Assert.Equal("order-1", message.OrderId);
        Assert.Equal("Mojito", message.DrinkName);
    }

    [Fact]
    public void VoiceShowIngredientsMessage_KeepsTheDrinkName()
    {
        Assert.Equal("Mojito", new VoiceShowIngredientsMessage("Mojito").DrinkName);
    }

    [Fact]
    public void VoiceMessages_AreComparedByValue()
    {
        Assert.Equal(new VoiceSelectDrinkMessage("o1", "Mojito"), new VoiceSelectDrinkMessage("o1", "Mojito"));
        Assert.NotEqual(new VoiceSelectDrinkMessage("o1", "Mojito"), new VoiceSelectDrinkMessage("o2", "Mojito"));
        Assert.Equal(new VoiceShowIngredientsMessage("Mojito"), new VoiceShowIngredientsMessage("Mojito"));
        Assert.Equal(new VoiceCloseIngredientsMessage(), new VoiceCloseIngredientsMessage());
    }
}
