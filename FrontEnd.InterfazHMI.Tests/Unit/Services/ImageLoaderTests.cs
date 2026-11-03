using System.Net;
using System.Windows.Media.Imaging;
using Frontend_interfaz_Multimodal_HMI.Services;

namespace FrontEnd.InterfazHMI.Tests.Unit.Services;

public class ImageLoaderTests
{
    private const long _maxBytes = 5 * 1024 * 1024;
    private const string _url = "https://cdn.test/mojito.png";

    private static readonly byte[] _png = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==");

    private readonly StubHandler _handler = new();

    private ImageLoader CreateSut() => new(new HttpClient(_handler));

    private static byte[] PngPaddedTo(long length)
    {
        var body = new byte[length];
        _png.CopyTo(body, 0);
        return body;
    }

    private static HttpResponseMessage Ok(byte[]? body = null) => new(HttpStatusCode.OK)
    {
        Content = new ByteArrayContent(body ?? _png),
    };


    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task LoadAsync_WithEmptyUrl_ReturnsNullWithoutRequest(string? url)
    {
        var result = await CreateSut().LoadAsync(url);

        Assert.Null(result);
        Assert.Equal(0, _handler.RequestCount);
    }

    [Theory]
    [InlineData("no es una url")]
    [InlineData("/imagenes/mojito.png")]
    [InlineData("ftp://cdn.test/mojito.png")]
    [InlineData("file:///C:/imagenes/mojito.png")]
    public async Task LoadAsync_WithNonHttpUrl_ReturnsNullWithoutRequest(string url)
    {
        var result = await CreateSut().LoadAsync(url);

        Assert.Null(result);
        Assert.Equal(0, _handler.RequestCount);
    }


    [Theory]
    [InlineData("http://cdn.test/mojito.png")]
    [InlineData("https://cdn.test/mojito.png")]
    public async Task LoadAsync_WithValidImage_ReturnsAFrozenBitmap(string url)
    {
        _handler.Respond = _ => Task.FromResult(Ok());

        var result = await CreateSut().LoadAsync(url);

        var bitmap = Assert.IsType<BitmapImage>(result);
        Assert.True(bitmap.IsFrozen);
        Assert.Equal(new Uri(url), _handler.Requests.Single());
    }


    [Theory]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task LoadAsync_WithErrorStatus_ReturnsNull(HttpStatusCode status)
    {
        _handler.Respond = _ => Task.FromResult(new HttpResponseMessage(status));

        Assert.Null(await CreateSut().LoadAsync(_url));
    }

    [Fact]
    public async Task LoadAsync_WhenTheBodyIsNotAnImage_ReturnsNull()
    {
        _handler.Respond = _ => Task.FromResult(Ok(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 }));

        Assert.Null(await CreateSut().LoadAsync(_url));
    }

    [Fact]
    public async Task LoadAsync_WhenTheRequestThrows_ReturnsNull()
    {
        _handler.Respond = _ => throw new HttpRequestException("sin red");

        Assert.Null(await CreateSut().LoadAsync(_url));
    }

    [Fact]
    public async Task LoadAsync_WhenTheRequestTimesOut_ReturnsNull()
    {
        _handler.Respond = _ => throw new TaskCanceledException("timeout");

        Assert.Null(await CreateSut().LoadAsync(_url));
    }


    [Fact]
    public async Task LoadAsync_WhenContentLengthExceedsTheLimit_ReturnsNull()
    {
        _handler.Respond = _ => Task.FromResult(Ok(PngPaddedTo(_maxBytes + 1)));

        Assert.Null(await CreateSut().LoadAsync(_url));
    }

    [Fact]
    public async Task LoadAsync_WhenTheHeaderAnnouncesMoreThanTheLimit_ReturnsNull()
    {
        _handler.Respond = _ =>
        {
            var content = new ByteArrayContent(_png);
            content.Headers.ContentLength = _maxBytes + 1;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
        };

        Assert.Null(await CreateSut().LoadAsync(_url));
    }

    [Fact]
    public async Task LoadAsync_WhenTheStreamExceedsTheLimitWithoutContentLength_ReturnsNull()
    {
        _handler.Respond = _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StreamContent(new NonSeekableStream(PngPaddedTo(_maxBytes + 1))),
        });

        Assert.Null(await CreateSut().LoadAsync(_url));
    }

    [Fact]
    public async Task LoadAsync_WithAnImageJustUnderTheLimit_IsNotRejectedBySize()
    {
        _handler.Respond = _ => Task.FromResult(Ok(PngPaddedTo(_maxBytes)));

        Assert.NotNull(await CreateSut().LoadAsync(_url));
    }


    [Fact]
    public async Task LoadAsync_SameUrlTwice_DownloadsOnceAndReturnsTheSameInstance()
    {
        var sut = CreateSut();

        var first = await sut.LoadAsync(_url);
        var second = await sut.LoadAsync(_url);

        Assert.NotNull(first);
        Assert.Same(first, second);
        Assert.Equal(1, _handler.RequestCount);
    }

    [Fact]
    public async Task LoadAsync_DifferentUrls_DownloadEachOne()
    {
        var sut = CreateSut();

        await sut.LoadAsync("https://cdn.test/a.png");
        await sut.LoadAsync("https://cdn.test/b.png");

        Assert.Equal(2, _handler.RequestCount);
    }

    [Fact]
    public async Task LoadAsync_ConcurrentCallsForTheSameUrl_ShareOneDownload()
    {
        var release = new TaskCompletionSource<HttpResponseMessage>();
        _handler.Respond = _ => release.Task;
        var sut = CreateSut();

        var calls = Enumerable.Range(0, 5).Select(_ => sut.LoadAsync(_url)).ToList();
        release.SetResult(Ok());
        var results = await Task.WhenAll(calls);

        Assert.Equal(1, _handler.RequestCount);
        Assert.NotNull(results[0]);
        Assert.All(results, r => Assert.Same(results[0], r));
    }

    [Fact]
    public async Task LoadAsync_AfterAFailure_RetriesInsteadOfCachingTheFailure()
    {
        var attempts = 0;
        _handler.Respond = _ => Task.FromResult(++attempts == 1
            ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
            : Ok());
        var sut = CreateSut();

        var first = await sut.LoadAsync(_url);
        var second = await sut.LoadAsync(_url);

        Assert.Null(first);
        Assert.NotNull(second);
        Assert.Equal(2, _handler.RequestCount);
    }


    [Fact]
    public async Task LoadAsync_RunsAtMostFourDownloadsAtTheSameTime()
    {
        var release = new TaskCompletionSource<HttpResponseMessage>();
        _handler.Respond = _ => release.Task;
        var sut = CreateSut();

        var calls = Enumerable.Range(0, 10)
            .Select(i => sut.LoadAsync($"https://cdn.test/{i}.png"))
            .ToList();

        await WaitUntil(() => _handler.InFlight == 4);
        await Task.Delay(100);
        var inFlightWhileBlocked = _handler.InFlight;

        release.SetResult(Ok());
        await Task.WhenAll(calls);

        Assert.Equal(4, inFlightWhileBlocked);
        Assert.Equal(4, _handler.MaxInFlight);
        Assert.Equal(10, _handler.RequestCount);
    }

    private static async Task WaitUntil(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!condition())
        {
            Assert.True(DateTime.UtcNow < deadline, "Timeout esperando la condición");
            await Task.Delay(10);
        }
    }


    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly List<Uri> _requests = new();
        private int _count;
        private int _inFlight;
        private int _maxInFlight;

        public Func<HttpRequestMessage, Task<HttpResponseMessage>> Respond { get; set; }
            = _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(_png),
            });

        public int RequestCount => Volatile.Read(ref _count);
        public int InFlight => Volatile.Read(ref _inFlight);
        public int MaxInFlight => Volatile.Read(ref _maxInFlight);

        public IReadOnlyList<Uri> Requests
        {
            get
            {
                lock (_requests)
                {
                    return _requests.ToList();
                }
            }
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            lock (_requests)
            {
                _requests.Add(request.RequestUri!);
            }
            Interlocked.Increment(ref _count);
            var now = Interlocked.Increment(ref _inFlight);
            int seen;
            while (now > (seen = Volatile.Read(ref _maxInFlight)))
            {
                Interlocked.CompareExchange(ref _maxInFlight, now, seen);
            }

            try
            {
                return await Respond(request);
            }
            finally
            {
                Interlocked.Decrement(ref _inFlight);
            }
        }
    }

    private sealed class NonSeekableStream : MemoryStream
    {
        public NonSeekableStream(byte[] data) : base(data)
        {
        }

        public override bool CanSeek => false;
    }
}
