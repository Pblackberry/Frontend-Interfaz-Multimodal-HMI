using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Channels;

namespace FrontEnd.InterfazHMI.Tests.Support;

internal sealed class FakeWsServer : IAsyncDisposable
{
    private const string _handshakeGuid = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";

    private readonly TcpListener _listener;
    private readonly CancellationTokenSource _cts = new();
    private readonly Channel<WebSocket> _connections = Channel.CreateUnbounded<WebSocket>();
    private readonly List<WebSocket> _accepted = new();
    private readonly Task _acceptLoop;
    private int _connectionCount;

    public FakeWsServer(int port = 0)
    {
        _listener = new TcpListener(IPAddress.Loopback, port);
        _listener.Start();
        Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
        _acceptLoop = AcceptLoopAsync();
    }

    public int Port { get; }

    public Uri Uri => new($"ws://127.0.0.1:{Port}/ws");

    public int ConnectionCount => Volatile.Read(ref _connectionCount);

    public async Task<WebSocket> AcceptAsync(TimeSpan? timeout = null)
        => await _connections.Reader.ReadAsync().AsTask().WaitAsync(timeout ?? TimeSpan.FromSeconds(10));

    public static Task SendTextAsync(WebSocket socket, string text)
        => socket.SendAsync(Encoding.UTF8.GetBytes(text), WebSocketMessageType.Text, true, CancellationToken.None);

    public static async Task SendFragmentedAsync(WebSocket socket, string text, int firstFragmentBytes)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        await socket.SendAsync(
            new ArraySegment<byte>(bytes, 0, firstFragmentBytes), WebSocketMessageType.Text, false, CancellationToken.None);
        await socket.SendAsync(
            new ArraySegment<byte>(bytes, firstFragmentBytes, bytes.Length - firstFragmentBytes),
            WebSocketMessageType.Text, true, CancellationToken.None);
    }

    public static int GetFreePort()
    {
        var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        var port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        return port;
    }

    private async Task AcceptLoopAsync()
    {
        try
        {
            while (!_cts.IsCancellationRequested)
            {
                var tcp = await _listener.AcceptTcpClientAsync(_cts.Token);
                _ = Task.Run(() => HandshakeAsync(tcp));
            }
        }
        catch (OperationCanceledException) { }
        catch (ObjectDisposedException) { }
        catch (SocketException) { }
    }

    private async Task HandshakeAsync(TcpClient tcp)
    {
        try
        {
            var stream = tcp.GetStream();
            var request = await ReadHeadersAsync(stream);
            var keyLine = request.Split("\r\n")
                .First(l => l.StartsWith("Sec-WebSocket-Key:", StringComparison.OrdinalIgnoreCase));
            var key = keyLine.Split(':', 2)[1].Trim();
            var accept = Convert.ToBase64String(SHA1.HashData(Encoding.ASCII.GetBytes(key + _handshakeGuid)));

            var response =
                "HTTP/1.1 101 Switching Protocols\r\n" +
                "Upgrade: websocket\r\n" +
                "Connection: Upgrade\r\n" +
                $"Sec-WebSocket-Accept: {accept}\r\n\r\n";
            await stream.WriteAsync(Encoding.ASCII.GetBytes(response));

            var socket = WebSocket.CreateFromStream(
                stream, new WebSocketCreationOptions { IsServer = true, KeepAliveInterval = TimeSpan.Zero });
            lock (_accepted)
            {
                _accepted.Add(socket);
            }

            Interlocked.Increment(ref _connectionCount);
            _connections.Writer.TryWrite(socket);
        }
        catch
        {
            tcp.Dispose();
        }
    }

    private static async Task<string> ReadHeadersAsync(NetworkStream stream)
    {
        var bytes = new List<byte>();
        var one = new byte[1];
        while (true)
        {
            if (await stream.ReadAsync(one) == 0)
            {
                throw new IOException("Conexión cerrada durante el handshake");
            }

            bytes.Add(one[0]);
            var n = bytes.Count;
            if (n >= 4 && bytes[n - 4] == '\r' && bytes[n - 3] == '\n' && bytes[n - 2] == '\r' && bytes[n - 1] == '\n')
            {
                return Encoding.ASCII.GetString(bytes.ToArray());
            }

            if (n > 16 * 1024)
            {
                throw new IOException("Cabeceras demasiado grandes");
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        _cts.Cancel();
        _listener.Stop();
        lock (_accepted)
        {
            foreach (var socket in _accepted)
            {
                socket.Dispose();
            }
        }
        await _acceptLoop;
        _cts.Dispose();
    }
}
