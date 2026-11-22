using System.Diagnostics.CodeAnalysis;

namespace Salter.Core.Settings;

/// <summary>Stores small secrets, such as API keys, in the operating system's protected storage.</summary>
[SuppressMessage(
    "Naming",
    "CA1716:Identifiers should not match keywords",
    Justification = "Get/Set read naturally for a key-value store, and the store is implemented only in C#.")]
public interface ISecretStore
{
    /// <summary>Reads a secret.</summary>
    /// <param name="name">The secret's name, for example <see cref="SecretNames.WallhavenApiKey"/>.</param>
    /// <returns>
    /// The secret, or <see langword="null"/> when none (or an empty one) is stored or the protected storage cannot be
    /// read.
    /// </returns>
    /// <remarks>Implementations do not throw for platform failures; they log them and return <see langword="null"/>.</remarks>
    string? Get(string name);

    /// <summary>Stores or removes a secret.</summary>
    /// <param name="name">The secret's name.</param>
    /// <param name="value">The value to store, or <see langword="null"/> to remove the secret.</param>
    /// <exception cref="InvalidOperationException">
    /// The protected storage could not be written. Implementations wrap platform-specific errors in this exception.
    /// </exception>
    void Set(string name, string? value);
}
