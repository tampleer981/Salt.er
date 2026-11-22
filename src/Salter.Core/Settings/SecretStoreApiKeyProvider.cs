using Salter.Core.Wallhaven;

namespace Salter.Core.Settings;

/// <summary>Reads the wallhaven API key from an <see cref="ISecretStore"/> on every request.</summary>
public sealed class SecretStoreApiKeyProvider : IApiKeyProvider
{
    private readonly ISecretStore _secrets;

    /// <summary>Creates a provider over the given store.</summary>
    /// <param name="secrets">The store holding <see cref="SecretNames.WallhavenApiKey"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="secrets"/> is <see langword="null"/>.</exception>
    public SecretStoreApiKeyProvider(ISecretStore secrets)
    {
        ArgumentNullException.ThrowIfNull(secrets);
        _secrets = secrets;
    }

    /// <inheritdoc/>
    /// <returns>The stored key, or <see langword="null"/> when none or only whitespace is stored.</returns>
    public string? GetApiKey()
    {
        var key = _secrets.Get(SecretNames.WallhavenApiKey);
        return string.IsNullOrWhiteSpace(key) ? null : key;
    }
}
