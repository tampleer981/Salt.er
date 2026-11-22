namespace Salter.Core.Wallhaven;

/// <summary>Adds the <c>X-API-Key</c> header when the user has configured a key.</summary>
internal sealed class ApiKeyHandler(IApiKeyProvider keys) : DelegatingHandler
{
    internal const string HeaderName = "X-API-Key";

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var key = keys.GetApiKey();
        request.Headers.Remove(HeaderName);
        if (!string.IsNullOrWhiteSpace(key))
        {
            request.Headers.TryAddWithoutValidation(HeaderName, key.Trim());
        }

        return base.SendAsync(request, cancellationToken);
    }
}
