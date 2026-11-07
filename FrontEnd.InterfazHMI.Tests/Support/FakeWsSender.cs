using Frontend_interfaz_Multimodal_HMI.Services;

namespace FrontEnd.InterfazHMI.Tests.Support;

internal sealed class FakeWsSender : IWsSender
{
    private readonly TaskCompletionSource<bool>? _pending;
    private readonly bool _result;

    public FakeWsSender(bool result = true) => _result = result;

    private FakeWsSender(TaskCompletionSource<bool> pending)
    {
        _pending = pending;
        _result = true;
    }

    public static FakeWsSender Pending() => new(new TaskCompletionSource<bool>());

    public List<(string Type, object? Payload)> Sent { get; } = new();

    public Task<bool> SendAsync<T>(string type, T payload, CancellationToken ct = default)
    {
        Sent.Add((type, payload));
        return _pending?.Task ?? Task.FromResult(_result);
    }

    public void Complete(bool result) => _pending!.SetResult(result);
}
