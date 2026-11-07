using Frontend_interfaz_Multimodal_HMI.Models;

namespace Frontend_interfaz_Multimodal_HMI.Services;

public sealed class OrderSession
{
    public CobotMenuItem? SelectedDrink { get; private set; }
    public PaymentResultPayload? Result { get; private set; }

    public void Select(CobotMenuItem item) { SelectedDrink = item; Result = null; }
    public void SetResult(PaymentResultPayload result) => Result = result;
    public void Clear() { SelectedDrink = null; Result = null; }
}
