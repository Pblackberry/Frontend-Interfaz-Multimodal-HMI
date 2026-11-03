using System.Windows.Media;
using Frontend_interfaz_Multimodal_HMI.Services;

namespace FrontEnd.InterfazHMI.Tests.Support;

internal sealed class FakeImageLoader : IImageLoader
{
    private readonly TaskCompletionSource<ImageSource?>? _pending;
    private readonly ImageSource? _result;
    private readonly List<string?> _requested = new();

    public FakeImageLoader()
    {
    }

    private FakeImageLoader(ImageSource? result, TaskCompletionSource<ImageSource?>? pending)
    {
        _result = result;
        _pending = pending;
    }

    public static FakeImageLoader Returning(ImageSource? image) => new(image, null);

    public static FakeImageLoader Pending() => new(null, new TaskCompletionSource<ImageSource?>());

    public IReadOnlyList<string?> Requested
    {
        get
        {
            lock (_requested)
            {
                return _requested.ToList();
            }
        }
    }

    public Task<ImageSource?> LoadAsync(string? url)
    {
        lock (_requested)
        {
            _requested.Add(url);
        }
        return _pending?.Task ?? Task.FromResult(_result);
    }

    public void Complete(ImageSource? image) => _pending!.SetResult(image);

    public void Fail(Exception ex) => _pending!.SetException(ex);
}
