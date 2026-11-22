namespace Salter.Core.Wallhaven;

/// <summary>Reads wallpapers from the wallhaven API.</summary>
public interface IWallhavenClient
{
    /// <summary>Runs a search and returns one page of results.</summary>
    /// <param name="query">The search to run.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>The requested page.</returns>
    /// <exception cref="WallhavenException">The request failed or the response was unusable.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="ct"/> was cancelled.</exception>
    Task<SearchPage> SearchAsync(SearchQuery query, CancellationToken ct);

    /// <summary>Fetches the details of a single wallpaper.</summary>
    /// <param name="id">The wallpaper id.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>The wallpaper details.</returns>
    /// <exception cref="ArgumentException"><paramref name="id"/> is empty or whitespace.</exception>
    /// <exception cref="WallhavenException">The request failed or the response was unusable.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="ct"/> was cancelled.</exception>
    Task<WallpaperDetails> GetWallpaperAsync(string id, CancellationToken ct);
}
