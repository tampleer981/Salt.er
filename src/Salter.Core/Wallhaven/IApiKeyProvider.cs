namespace Salter.Core.Wallhaven;

/// <summary>Supplies the user's wallhaven API key, which the host application stores securely.</summary>
public interface IApiKeyProvider
{
    /// <summary>Returns the current API key.</summary>
    /// <returns>The key, or <see langword="null"/> or whitespace when none is configured.</returns>
    string? GetApiKey();
}
