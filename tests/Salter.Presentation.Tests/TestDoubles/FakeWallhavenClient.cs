using Salter.Core.Wallhaven;

namespace Salter.Presentation.Tests.TestDoubles;

/// <summary>Wallhaven client that records searches and answers them from a queue of scripted responses.</summary>
internal sealed class FakeWallhavenClient : IWallhavenClient
{
    private readonly Queue<Func<Task<SearchPage>>> _responses = new();

    public List<SearchQuery> Queries { get; } = [];

    public List<CancellationToken> Tokens { get; } = [];

    public void Enqueue(SearchPage page) => _responses.Enqueue(() => Task.FromResult(page));

    public void EnqueueFailure(Exception exception) => _responses.Enqueue(() => Task.FromException<SearchPage>(exception));

    /// <summary>Queues a response that completes only when the returned source is completed.</summary>
    public TaskCompletionSource<SearchPage> EnqueuePending()
    {
        var source = new TaskCompletionSource<SearchPage>();
        _responses.Enqueue(() => source.Task);
        return source;
    }

    public Task<SearchPage> SearchAsync(SearchQuery query, CancellationToken ct)
    {
        Queries.Add(query);
        Tokens.Add(ct);
        if (!_responses.TryDequeue(out var response))
        {
            throw new InvalidOperationException($"No response queued for page {query.Page}.");
        }

        return response();
    }

    /// <summary>Answers <see cref="GetWallpaperAsync"/>; detail requests are unsupported while unset.</summary>
    public Func<string, CancellationToken, Task<WallpaperDetails>>? Details { get; set; }

    public List<string> DetailRequests { get; } = [];

    public Task<WallpaperDetails> GetWallpaperAsync(string id, CancellationToken ct)
    {
        DetailRequests.Add(id);
        return Details is null ? throw new NotSupportedException() : Details(id, ct);
    }
}
