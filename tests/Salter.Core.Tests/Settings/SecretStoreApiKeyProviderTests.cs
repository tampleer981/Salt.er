using Salter.Core.Settings;
using Salter.Core.Tests.TestDoubles;

namespace Salter.Core.Tests.Settings;

public class SecretStoreApiKeyProviderTests
{
    [Fact]
    public void Returns_stored_wallhaven_key()
    {
        var secrets = new InMemorySecretStore();
        secrets.Set("wallhaven-api-key", "secret");

        Assert.Equal("secret", new SecretStoreApiKeyProvider(secrets).GetApiKey());
        Assert.Equal("wallhaven-api-key", SecretNames.WallhavenApiKey);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Missing_or_blank_key_returns_null(string? stored)
    {
        var secrets = new InMemorySecretStore();
        secrets.Set(SecretNames.WallhavenApiKey, stored);

        Assert.Null(new SecretStoreApiKeyProvider(secrets).GetApiKey());
    }

    [Fact]
    public void Reads_the_store_on_every_call()
    {
        var secrets = new InMemorySecretStore();
        var provider = new SecretStoreApiKeyProvider(secrets);

        Assert.Null(provider.GetApiKey());
        secrets.Set(SecretNames.WallhavenApiKey, "new");
        Assert.Equal("new", provider.GetApiKey());
    }
}
