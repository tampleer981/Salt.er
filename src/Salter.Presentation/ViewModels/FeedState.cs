namespace Salter.Presentation.ViewModels;

/// <summary>What the feed is currently showing.</summary>
public enum FeedState
{
    /// <summary>Nothing has been requested yet.</summary>
    Idle,
    /// <summary>The first page of a new query is loading.</summary>
    Loading,
    /// <summary>Results are shown.</summary>
    Ready,
    /// <summary>The query returned no results.</summary>
    Empty,
    /// <summary>The first page could not be loaded; see <see cref="FeedViewModel.ErrorMessage"/>.</summary>
    Error,
}
