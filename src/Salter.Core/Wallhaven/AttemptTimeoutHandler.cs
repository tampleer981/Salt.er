namespace Salter.Core.Wallhaven;

/// <summary>
/// Limits each request attempt, including reading the response body, to a fixed time measured by
/// <see cref="TimeProvider"/>. Expiry surfaces as a <see cref="TaskCanceledException"/> wrapping a
/// <see cref="TimeoutException"/>, the same shape as an <see cref="HttpClient"/> timeout.
/// </summary>
/// <remarks>
/// Sits below the retry and rate-limiting handlers so that waiting for a permit or for a
/// <c>Retry-After</c> delay never counts against an attempt.
/// </remarks>
internal sealed class AttemptTimeoutHandler(TimeProvider time, TimeSpan timeout) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        using var timeoutSource = new CancellationTokenSource(timeout, time);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutSource.Token);
        HttpResponseMessage? response = null;
        try
        {
            response = await base.SendAsync(request, linked.Token).ConfigureAwait(false);
            await response.Content.LoadIntoBufferAsync(linked.Token).ConfigureAwait(false);
            return response;
        }
        catch (OperationCanceledException ex)
            when (timeoutSource.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            response?.Dispose();
            throw new TaskCanceledException(
                $"The wallhaven request timed out after {timeout.TotalSeconds:0} s.",
                new TimeoutException(ex.Message, ex));
        }
        catch
        {
            response?.Dispose();
            throw;
        }
    }
}
