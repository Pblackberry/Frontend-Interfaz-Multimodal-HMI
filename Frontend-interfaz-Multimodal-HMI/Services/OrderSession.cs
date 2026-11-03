using Frontend_interfaz_Multimodal_HMI.Models;

namespace Frontend_interfaz_Multimodal_HMI.Services;

public sealed class OrderSession
{
    public CobotMenuItem? SelectedDrink { get; private set; }

    public void Select(CobotMenuItem item) => SelectedDrink = item;
    public void Clear() => SelectedDrink = null;
}
