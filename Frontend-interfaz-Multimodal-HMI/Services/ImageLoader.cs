using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Frontend_interfaz_Multimodal_HMI.Services;

public interface IImageLoader
{
    Task<ImageSource?> LoadAsync(string? url);
}

public sealed class ImageLoader : IImageLoader
{
    private const int _decodePixelWidth = 300;
    private const long _maxBytes = 5 * 1024 * 1024;

    private readonly HttpClient _http;
    private readonly SemaphoreSlim _gate = new(4);
    private readonly ConcurrentDictionary<string, Task<ImageSource?>> _cache = new();

    public ImageLoader(HttpClient http) => _http = http;

    public async Task<ImageSource?> LoadAsync(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return null;
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            Debug.WriteLine($"[Img] URL no válida: {url}");
            return null;
        }

        var result = await _cache.GetOrAdd(url, _ => DownloadAsync(uri)).ConfigureAwait(false);

        if (result is null)
        {
            _cache.TryRemove(url, out _);
        }

        return result;
    }

    private async Task<ImageSource?> DownloadAsync(Uri uri)
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            using var response = await _http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead)
                                            .ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            if (response.Content.Headers.ContentLength > _maxBytes)
            {
                throw new InvalidOperationException("Imagen demasiado grande");
            }

            var bytes = await ReadLimitedAsync(response).ConfigureAwait(false);
            return Decode(bytes);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Img] No se pudo cargar {uri}: {ex.GetType().Name}: {ex.Message}");
            return null;
        }
        finally
        {
            _gate.Release();
        }
    }

    private static async Task<byte[]> ReadLimitedAsync(HttpResponseMessage response)
    {
        await using var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
        using var ms = new MemoryStream();
        var buffer = new byte[81920];
        int read;
        while ((read = await stream.ReadAsync(buffer).ConfigureAwait(false)) > 0)
        {
            ms.Write(buffer, 0, read);
            if (ms.Length > _maxBytes)
            {
                throw new InvalidOperationException("Imagen demasiado grande");
            }
        }
        return ms.ToArray();
    }

    private static ImageSource Decode(byte[] bytes)
    {
        using var ms = new MemoryStream(bytes);
        var bmp = new BitmapImage();
        bmp.BeginInit();
        bmp.CacheOption = BitmapCacheOption.OnLoad;
        bmp.DecodePixelWidth = _decodePixelWidth;
        bmp.StreamSource = ms;
        bmp.EndInit();
        bmp.Freeze();
        return bmp;
    }
}
