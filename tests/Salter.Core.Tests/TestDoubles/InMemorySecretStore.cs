using Salter.Core.Settings;

namespace Salter.Core.Tests.TestDoubles;

/// <summary>Secret store backed by a dictionary.</summary>
internal sealed class InMemorySecretStore : ISecretStore
{
    private readonly Dictionary<string, string> _secrets = new(StringComparer.Ordinal);

    public string? Get(string name) => _secrets.GetValueOrDefault(name);

    public void Set(string name, string? value)
    {
        if (value is null)
        {
            _secrets.Remove(name);
        }
        else
        {
            _secrets[name] = value;
        }
    }
}
