using System.Net.WebSockets;
using Frontend_interfaz_Multimodal_HMI.Services;

namespace FrontEnd.InterfazHMI.Tests.Support;

internal sealed class WsClientSession : IAsyncDisposable
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(10);

    private readonly CancellationTokenSource _cts;
    private readonly Task _run;

    private WsClientSession(WsClient client, WebSocket serverSocket, CancellationTokenSource cts, Task run)
    {
        Client = client;
        ServerSocket = serverSocket;
        _cts = cts;
        _run = run;
    }

    public WsClient Client { get; }

    public WebSocket ServerSocket { get; }

    public static async Task<WsClientSession> StartAsync(FakeWsServer server, Action<WsClient>? beforeRun = null)
    {
        var cts = new CancellationTokenSource();
        var client = new WsClient(server.Uri);
        beforeRun?.Invoke(client);

        var run = client.RunAsync(cts.Token);
        var socket = await server.AcceptAsync();
        if (!SpinWait.SpinUntil(() => client.IsConnected, _timeout))
        {
            throw new TimeoutException("El cliente no llegó a estar conectado");
        }

        return new WsClientSession(client, socket, cts, run);
    }

    public async Task StopAsync()
    {
        _cts.Cancel();
        await _run.WaitAsync(_timeout);
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        _cts.Dispose();
    }
}
