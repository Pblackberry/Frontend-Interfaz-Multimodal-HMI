using System;
using System.Diagnostics;
using System.Globalization;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Frontend_interfaz_Multimodal_HMI.Models;
using Frontend_interfaz_Multimodal_HMI.Services;

namespace Frontend_interfaz_Multimodal_HMI.ViewModels;

public enum OrderPhase { Paid, Preparing, Ready }

public sealed partial class PaymentResultViewModel : ObservableRecipient, IRecipient<CobotStatusMessage>
{
    private static readonly TimeSpan _defaultReadyDelay = TimeSpan.FromSeconds(5);

    private readonly INavigationService _nav;
    private readonly OrderSession _session;
    private readonly TimeSpan _readyDelay;

    public PaymentResultViewModel(INavigationService nav, OrderSession session, TimeSpan? readyDelay = null)
    {
        _nav = nav;
        _session = session;
        _readyDelay = readyDelay ?? _defaultReadyDelay;

        var drink = session.SelectedDrink;
        DrinkName = drink?.NombreItem ?? "";
        PriceText = drink is null ? "" : drink.PrecioItem.ToString("0.00", CultureInfo.InvariantCulture);
        var result = session.Result;

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
        IsActive = true;
    }

    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public string IconGlyph { get; } = "";
    public string Title { get; } = "";
    public string Message { get; } = "";
    public string ButtonText { get; } = "";
    public string DrinkName { get; }
    public string PriceText { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPreparing))]
    [NotifyPropertyChangedFor(nameof(IsReady))]
    private OrderPhase _phase = OrderPhase.Paid;

    public bool IsPreparing => Phase == OrderPhase.Preparing;
    public bool IsReady => Phase == OrderPhase.Ready;

    public void Receive(CobotStatusMessage message)
    {
        if (!IsSuccess)
        {
            return;
        }

        switch (message.Payload.Codigo)
        {
            case 102:
                Phase = OrderPhase.Preparing;
                break;
            case 200:
                _ = ShowReadyThenGoHomeAsync();
                break;
            default:
                Debug.WriteLine($"[Cobot] {message.Payload.Codigo}: {message.Payload.Descripcion}");
                break;
        }
    }

    private async Task ShowReadyThenGoHomeAsync()
    {
        Phase = OrderPhase.Ready;
        await Task.Delay(_readyDelay);
        if (!IsActive)
        {
            return;
        }

        _session.Clear();
        _nav.NavigateTo(Screen.Welcome, StateSource.Touch);
    }

    [RelayCommand]
    private void Continue()
    {
        var success = IsSuccess;
        _session.Clear();
        _nav.NavigateTo(success ? Screen.Welcome : Screen.Menu, StateSource.Touch);
    }
}
