using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Frontend_interfaz_Multimodal_HMI.Models;
using Frontend_interfaz_Multimodal_HMI.Services;

namespace Frontend_interfaz_Multimodal_HMI.ViewModels;

public sealed partial class PaymentResultViewModel : ObservableObject
{
    private readonly INavigationService _nav;
    private readonly OrderSession _order;

    public PaymentResultViewModel(INavigationService nav, OrderSession order)
    {
        _nav = nav;
        _order = order;

        var result = order.Result;
        switch (result?.Status)
        {
            case PaymentOutcome.Approved:
                IsSuccess = true;
                IconGlyph = "\uE73E";
                Title = "¡Pago realizado!";
                Message = "Estamos preparando tu bebida.";
                ButtonText = "Nuevo pedido";
                break;
            case PaymentOutcome.Rejected:
                IconGlyph = "\uE711";
                Title = "Pago rechazado";
                Message = "No se realizó ningún cobro.";
                ButtonText = "Volver al menú";
                break;
            case PaymentOutcome.Expired:
                IconGlyph = "\uE711";
                Title = "El código expiró";
                Message = "Genera uno nuevo desde el menú.";
                ButtonText = "Volver al menú";
                break;
            default:
                IconGlyph = "\uE711";
                Title = "No pudimos iniciar el pago";
                Message = result?.Reason == "drink_unavailable"
                    ? "Esa bebida ya no está disponible."
                    : "Inténtalo de nuevo.";
                ButtonText = "Volver al menú";
                break;
        }
    }

    public bool IsSuccess { get; }
    public string IconGlyph { get; } = "";
    public string Title { get; } = "";
    public string Message { get; } = "";
    public string ButtonText { get; } = "";

    [RelayCommand]
    private void Continue()
    {
        var success = IsSuccess;
        _order.Clear();
        _nav.NavigateTo(success ? Screen.Welcome : Screen.Menu);
    }
}
