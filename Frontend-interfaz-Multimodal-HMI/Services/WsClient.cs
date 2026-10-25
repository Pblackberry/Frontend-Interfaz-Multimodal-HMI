using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Frontend_interfaz_Multimodal_HMI.Models;

namespace Frontend_interfaz_Multimodal_HMI.Services;

public sealed class WsClient
{
    private readonly Uri _uri;

    public event Action<CatalogUpdatePayload>? CatalogUpdated;

    public WsClient(Uri uri) => _uri = uri;

    // Se lanza una sola vez; mantiene la conexión y reconecta si se cae.
    public async Task RunAsync(CancellationToken ct)
    {
        var intento = 0;
        try
        {
            while (!ct.IsCancellationRequested)
            {
                using var ws = new ClientWebSocket();
                ws.Options.KeepAliveInterval = TimeSpan.FromSeconds(20);
                try
                {
                    await ws.ConnectAsync(_uri, ct);
                    intento = 0;
                    Debug.WriteLine($"[WS] Conectado a {_uri}");
                    await ReceiveLoopAsync(ws, ct);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[WS] Conexión fallida o perdida: {ex.GetType().Name}: {ex.Message}");
                }

                intento++;
                var espera = TimeSpan.FromSeconds(Math.Min(Math.Pow(2, intento), 30));
                Debug.WriteLine($"[WS] Reintentando en {espera.TotalSeconds:0}s");
                await Task.Delay(espera, ct);
            }
        }
        catch (OperationCanceledException) { /* apagado ordenado */ }
    }

    private async Task ReceiveLoopAsync(ClientWebSocket ws, CancellationToken ct)
    {
        var buffer = new byte[8192];
        using var ms = new MemoryStream();

        while (ws.State == WebSocketState.Open && !ct.IsCancellationRequested)
        {
            ms.SetLength(0);
            WebSocketReceiveResult result;
            do
            {
                result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), ct);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await ws.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "bye", CancellationToken.None);
                    return;
                }
                ms.Write(buffer, 0, result.Count);
            } while (!result.EndOfMessage);   // un mensaje puede llegar en varios fragmentos

            HandleMessage(Encoding.UTF8.GetString(ms.GetBuffer(), 0, (int)ms.Length));
        }
    }

    private void HandleMessage(string json)
    {
        try
        {
            var env = JsonSerializer.Deserialize<WsEnvelope>(json, WsJson.Options)!;
            switch (env.Type)
            {
                case WsTypes.CatalogUpdate:
                    var payload = env.Payload.Deserialize<CatalogUpdatePayload>(WsJson.Options)!;
                    CatalogUpdated?.Invoke(payload);
                    break;
                default:
                    Debug.WriteLine($"[WS] Tipo desconocido, se ignora: {env.Type}");
                    break;
            }
        }
        catch (Exception ex)   // un mensaje malo no debe tumbar la conexión
        {
            Debug.WriteLine($"[WS] Error procesando mensaje: {ex.GetType().Name}: {ex.Message}");
        }
    }
}
