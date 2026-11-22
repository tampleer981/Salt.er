using Salter.Core.Wallhaven;

namespace Salter.Presentation.Tests.TestDoubles;

/// <summary>API key provider backed by a property; no key by default.</summary>
internal sealed class FakeApiKeyProvider : IApiKeyProvider
{
    public string? Key { get; set; }

    public string? GetApiKey() => Key;
}
