using System.Collections.Concurrent;
using System.Net;

namespace Salter.Core.Tests.TestDoubles;

/// <summary>
/// Terminal HTTP handler that records every request and answers with a scripted response.
/// </summary>
internal sealed class StubHttpHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _respond;
    private readonly ConcurrentQueue<HttpRequestMessage> _requests = new();

    public StubHttpHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond)
    {
        _respond = respond;
    }

    public StubHttpHandler(Func<HttpRequestMessage, HttpResponseMessage> respond)
        : this((request, _) => Task.FromResult(respond(request)))
    {
    }

    /// <summary>Requests received, in order.</summary>
    public IReadOnlyList<HttpRequestMessage> Requests => [.. _requests];

    /// <summary>Waits until at least <paramref name="count"/> requests have arrived, failing after 10 s.</summary>
    public async Task WaitForRequestsAsync(int count)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (_requests.Count < count)
        {
            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException($"Expected {count} requests, saw {_requests.Count}.");
            }

            await Task.Delay(5);
        }
    }

    /// <summary>Answers each call with the next scripted response; the last one repeats.</summary>
    public static StubHttpHandler Sequence(params Func<HttpResponseMessage>[] responses)
    {
        var index = -1;
        return new StubHttpHandler(_ =>
        {
            var next = Math.Min(Interlocked.Increment(ref index), responses.Length - 1);
            return responses[next]();
        });
    }

    public static HttpResponseMessage Status(HttpStatusCode status) => new(status);

    public static HttpResponseMessage Fixture(string name) => new(HttpStatusCode.OK)
    {
        Content = new ByteArrayContent(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", name))),
    };

    public static HttpResponseMessage TooManyRequests(TimeSpan? retryAfter = null)
    {
        var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
        if (retryAfter is { } delay)
        {
            response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(delay);
        }

        return response;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        _requests.Enqueue(request);
        return _respond(request, cancellationToken);
    }
}
