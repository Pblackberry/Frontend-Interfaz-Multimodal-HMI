using System.Globalization;
using System.Threading.Tasks;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Frontend_interfaz_Multimodal_HMI.Models;
using Frontend_interfaz_Multimodal_HMI.Services;

namespace Frontend_interfaz_Multimodal_HMI.ViewModels;

public partial class PaymentViewModel : ObservableRecipient,
    IRecipient<PaymentStartedMessage>, IRecipient<PaymentResultMessage>
{
    private readonly INavigationService _nav;
    private readonly OrderSession _order;
    private readonly IWsSender _ws;
    private readonly IQrFactory _qr;
    private string? _paymentId;

    public PaymentViewModel(INavigationService nav, OrderSession order, IWsSender ws, IQrFactory qr)
    {
        _nav = nav;
        _order = order;
        _ws = ws;
        _qr = qr;

        var drink = order.SelectedDrink;
        _drinkName = drink?.NombreItem ?? "";
        _priceText = drink is null ? "" : Money(drink.PrecioItem);

        IsActive = true;
        _ = RequestPaymentAsync(drink);
    }

    [ObservableProperty] private string _drinkName;
    [ObservableProperty] private string _priceText;
    [ObservableProperty] private string _statusText = "Generando código de pago…";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasQr))]
    private ImageSource? _qrImage;

    public bool HasQr => QrImage is not null;

    private static string Money(decimal value) => "$" + value.ToString("0.00", CultureInfo.InvariantCulture);

    private async Task RequestPaymentAsync(CobotMenuItem? drink)
    {
        if (drink is null)
        {
            StatusText = "No hay una bebida seleccionada.";
            return;
        }
        var ok = await _ws.SendAsync(WsTypes.StartPayment, new StartPaymentPayload(drink.NombreItem));
        if (!ok) { StatusText = "Sin conexión con el sistema. Cancela e inténtalo de nuevo."; }
    }

    public void Receive(PaymentStartedMessage message)
    {
        var p = message.Payload;
        _paymentId = p.PaymentId;
        DrinkName = p.DrinkName;
        PriceText = Money(p.Price);
        QrImage = _qr.Create(p.PayUrl);
        StatusText = "Escanea el código con tu celular";
    }

    public void Receive(PaymentResultMessage message)
    {
        var id = message.Payload.PaymentId;
        if (id is not null && _paymentId is not null && id != _paymentId)
        {
            return;
        }

        _order.SetResult(message.Payload);
        _nav.NavigateTo(Screen.PaymentResult, StateSource.Backend, message.EnvelopeId);
    }

    [RelayCommand]
    private void Cancel()
    {
        _ = _ws.SendAsync(WsTypes.CancelPayment, new CancelPaymentPayload(_paymentId));
        _order.Clear();
        _nav.NavigateTo(Screen.Menu);
    }
}
