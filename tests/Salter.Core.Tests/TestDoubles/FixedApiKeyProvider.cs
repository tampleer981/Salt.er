using Salter.Core.Wallhaven;

namespace Salter.Core.Tests.TestDoubles;

/// <summary>API key provider that always returns the same key.</summary>
internal sealed class FixedApiKeyProvider(string? key) : IApiKeyProvider
{
    public string? GetApiKey() => key;
}
