using System.Net;

namespace Salter.Core.Wallhaven;

/// <summary>
/// <see cref="IWallhavenClient"/> over an <see cref="HttpClient"/> whose base address is the wallhaven API root.
/// Failures surface as <see cref="WallhavenException"/>; caller cancellation surfaces as
/// <see cref="OperationCanceledException"/>.
/// </summary>
internal sealed class WallhavenClient(HttpClient http) : IWallhavenClient
{
    public async Task<SearchPage> SearchAsync(SearchQuery query, CancellationToken ct)
    {
        var uri = QueryStringBuilder.Build(query);
        using var response = await SendAsync(uri, ct).ConfigureAwait(false);
        return WallhavenJson.ParseSearch(await ReadAsync(response, ct).ConfigureAwait(false));
    }

    public async Task<WallpaperDetails> GetWallpaperAsync(string id, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        using var response = await SendAsync("w/" + Uri.EscapeDataString(id), ct).ConfigureAwait(false);
        return WallhavenJson.ParseWallpaper(await ReadAsync(response, ct).ConfigureAwait(false));
    }

    private async Task<HttpResponseMessage> SendAsync(string relativeUri, CancellationToken ct)
    {
        HttpResponseMessage response;
        try
        {
            // The pipeline's AttemptTimeoutHandler buffers the body within the attempt timeout.
            response = await http.GetAsync(
                new Uri(relativeUri, UriKind.Relative), HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException ex)
        {
            throw new WallhavenException(WallhavenErrorKind.Network, "The wallhaven request timed out.", ex);
        }
        catch (HttpRequestException ex)
        {
            throw new WallhavenException(WallhavenErrorKind.Network, "wallhaven could not be reached.", ex);
        }

        if (response.IsSuccessStatusCode)
        {
            return response;
        }

        var status = response.StatusCode;
        response.Dispose();
        throw status switch
        {
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => new WallhavenException(
                WallhavenErrorKind.Unauthorized, "wallhaven rejected the API key."),
            HttpStatusCode.NotFound => new WallhavenException(
                WallhavenErrorKind.NotFound, "The wallpaper was not found on wallhaven."),
            HttpStatusCode.TooManyRequests => new WallhavenException(
                WallhavenErrorKind.RateLimited, "wallhaven is rate-limiting requests; try again shortly."),
            _ => new WallhavenException(
                WallhavenErrorKind.Server, $"wallhaven returned HTTP {(int)status}."),
        };
    }

    private static Task<Stream> ReadAsync(HttpResponseMessage response, CancellationToken ct) =>
        response.Content.ReadAsStreamAsync(ct);
}
