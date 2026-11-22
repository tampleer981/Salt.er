using Salter.Core.Settings;

namespace Salter.Presentation.Tests.TestDoubles;

/// <summary>Secret store backed by a dictionary.</summary>
internal sealed class FakeSecretStore : ISecretStore
{
    public Dictionary<string, string> Secrets { get; } = new(StringComparer.Ordinal);

    public string? Get(string name) => Secrets.GetValueOrDefault(name);

    /// <summary>When set, <see cref="Set"/> fails with this exception and keeps the stored secrets.</summary>
    public Exception? Failure { get; set; }

    public void Set(string name, string? value)
    {
        if (Failure is not null)
        {
            throw Failure;
        }

        if (value is null)
        {
            Secrets.Remove(name);
        }
        else
        {
            Secrets[name] = value;
        }
    }
}
