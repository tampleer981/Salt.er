namespace Salter.Core.Tests.TestDoubles;

/// <summary>Creates clients over a single shared handler, which the clients do not dispose.</summary>
internal sealed class StubHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
{
    /// <summary>Names passed to <see cref="CreateClient"/>, in order.</summary>
    public List<string> RequestedNames { get; } = [];

    public HttpClient CreateClient(string name)
    {
        RequestedNames.Add(name);
        return new HttpClient(handler, disposeHandler: false);
    }
}
