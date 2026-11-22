using Salter.Core.Settings;
using Salter.Core.Tests.TestDoubles;
using Salter.Core.Wallhaven;

namespace Salter.Core.Tests.Settings;

public sealed class JsonSettingsStoreTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    public void Dispose() => _temp.Dispose();

    private string SettingsPath => _temp.Combine("Salt.er", "settings.json");

    private void WriteFile(string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
        File.WriteAllText(SettingsPath, content);
    }

    [Fact]
    public void Default_settings_match_constraints()
    {
        var defaults = AppSettings.Default;

        Assert.Equal("Salt.er", Path.GetFileName(defaults.DownloadFolder));
        Assert.Equal("Pictures", Path.GetFileName(Path.GetDirectoryName(defaults.DownloadFolder)));
        Assert.False(defaults.LaunchAtStartup);
        Assert.Equal(ThemePreference.System, defaults.Theme);
        Assert.Equal(Purity.Sfw, defaults.Purity);
        Assert.Empty(defaults.ExcludedTags);
    }

    [Fact]
    public void Missing_file_returns_defaults()
    {
        var store = new JsonSettingsStore(SettingsPath);

        Assert.Equal(AppSettings.Default, store.Load());
        Assert.False(File.Exists(SettingsPath));
    }

    [Fact]
    public void Roundtrip_preserves_values()
    {
        var store = new JsonSettingsStore(SettingsPath);
        var settings = new AppSettings(_temp.Combine("Wallpapers"), true, ThemePreference.Dark, Purity.Sfw | Purity.Sketchy);

        store.Save(settings);

        Assert.Equal(settings, new JsonSettingsStore(SettingsPath).Load());
    }

    [Fact]
    public void File_uses_camel_case_and_enum_names()
    {
        new JsonSettingsStore(SettingsPath).Save(AppSettings.Default with { Theme = ThemePreference.Light });

        var json = File.ReadAllText(SettingsPath);
        Assert.Contains("\"downloadFolder\"", json, StringComparison.Ordinal);
        Assert.Contains("\"launchAtStartup\": false", json, StringComparison.Ordinal);
        Assert.Contains("\"theme\": \"Light\"", json, StringComparison.Ordinal);
        Assert.Contains("\"purity\": \"Sfw\"", json, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("{ not json")]
    [InlineData("")]
    [InlineData("null")]
    [InlineData("[1, 2]")]
    [InlineData("{\"theme\": \"Purple\"}")]
    public void Corrupt_file_returns_defaults_and_keeps_backup(string content)
    {
        WriteFile(content);
        File.WriteAllText(SettingsPath + ".bak", "older backup");

        var settings = new JsonSettingsStore(SettingsPath).Load();

        Assert.Equal(AppSettings.Default, settings);
        Assert.False(File.Exists(SettingsPath));
        Assert.Equal(content, File.ReadAllText(_temp.Combine("Salt.er", "settings.json.bak")));
    }

    [Fact]
    public void Missing_and_unknown_properties_fall_back_per_field()
    {
        WriteFile("""{ "theme": "Dark", "futureOption": [1, 2, 3] }""");

        var settings = new JsonSettingsStore(SettingsPath).Load();

        Assert.Equal(AppSettings.Default with { Theme = ThemePreference.Dark }, settings);
    }

    [Theory]
    [InlineData("\"\"")]
    [InlineData("\"   \"")]
    [InlineData("null")]
    public void Blank_download_folder_falls_back_to_default(string folder)
    {
        WriteFile($$"""{ "downloadFolder": {{folder}}, "launchAtStartup": true }""");

        var settings = new JsonSettingsStore(SettingsPath).Load();

        Assert.Equal(AppSettings.Default with { LaunchAtStartup = true }, settings);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("8")]
    public void Invalid_purity_falls_back_to_default(string purity)
    {
        WriteFile($$"""{ "purity": {{purity}} }""");

        Assert.Equal(AppSettings.Default, new JsonSettingsStore(SettingsPath).Load());
    }

    [Fact]
    public void Save_creates_missing_directory()
    {
        new JsonSettingsStore(SettingsPath).Save(AppSettings.Default);

        Assert.True(File.Exists(SettingsPath));
    }

    [Fact]
    public void Save_is_atomic()
    {
        var store = new JsonSettingsStore(SettingsPath);
        var original = AppSettings.Default with { LaunchAtStartup = true };
        store.Save(original);
        var originalBytes = File.ReadAllBytes(SettingsPath);

        // Writes go to settings.json.tmp first: when that path cannot be written, the existing file is untouched.
        Directory.CreateDirectory(SettingsPath + ".tmp");
        var ex = Record.Exception(() => store.Save(original with { Theme = ThemePreference.Dark }));
        Assert.True(ex is IOException or UnauthorizedAccessException, $"Unexpected exception: {ex}");
        Assert.Equal(originalBytes, File.ReadAllBytes(SettingsPath));
        Directory.Delete(SettingsPath + ".tmp");

        // A stale temp file from an interrupted save is replaced, and the move leaves no temp file behind.
        File.WriteAllText(SettingsPath + ".tmp", "stale");
        store.Save(original with { Theme = ThemePreference.Dark });
        Assert.Equal(original with { Theme = ThemePreference.Dark }, store.Load());
        Assert.Equal(["settings.json"], Directory.GetFiles(Path.GetDirectoryName(SettingsPath)!).Select(Path.GetFileName));
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void Blank_path_is_rejected(string path)
    {
        Assert.Throws<ArgumentException>(() => new JsonSettingsStore(path));
    }

    [Fact]
    public void Corrupt_file_that_cannot_be_backed_up_still_returns_defaults()
    {
        WriteFile("{ not json");
        Directory.CreateDirectory(SettingsPath + ".bak");

        var settings = new JsonSettingsStore(SettingsPath).Load();

        Assert.Equal(AppSettings.Default, settings);
        Assert.Equal("{ not json", File.ReadAllText(SettingsPath));
    }

    [Fact]
    public void Unreadable_settings_path_returns_defaults_without_touching_it()
    {
        Directory.CreateDirectory(SettingsPath);

        var settings = new JsonSettingsStore(SettingsPath).Load();

        Assert.Equal(AppSettings.Default, settings);
        Assert.True(Directory.Exists(SettingsPath));
        Assert.False(Path.Exists(SettingsPath + ".bak"));
    }

    [Fact]
    public void Locked_file_returns_defaults_without_touching_it()
    {
        WriteFile("""{ "theme": "Dark" }""");
        var store = new JsonSettingsStore(SettingsPath);
        AppSettings settings;

        using (new FileStream(SettingsPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            settings = store.Load();
        }

        Assert.Equal(AppSettings.Default, settings);
        Assert.False(File.Exists(SettingsPath + ".bak"));
        Assert.Equal(ThemePreference.Dark, store.Load().Theme);
    }

    [Fact]
    public void File_with_utf8_bom_is_parsed()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
        File.WriteAllText(SettingsPath, """{ "theme": "Dark", "launchAtStartup": true }""", new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        Assert.Equal(0xEF, File.ReadAllBytes(SettingsPath)[0]);

        var settings = new JsonSettingsStore(SettingsPath).Load();

        Assert.Equal(AppSettings.Default with { Theme = ThemePreference.Dark, LaunchAtStartup = true }, settings);
        Assert.False(File.Exists(SettingsPath + ".bak"));
    }

    [Fact]
    public void Roundtrip_preserves_excluded_tags()
    {
        var store = new JsonSettingsStore(SettingsPath);
        var settings = AppSettings.Default with { ExcludedTags = ["people", "car"] };

        store.Save(settings);

        Assert.Contains("\"excludedTags\"", File.ReadAllText(SettingsPath), StringComparison.Ordinal);
        var loaded = new JsonSettingsStore(SettingsPath).Load();
        Assert.Equal(settings, loaded);
        Assert.Equal(["people", "car"], loaded.ExcludedTags);
    }

    [Fact]
    public void File_without_excluded_tags_loads_none()
    {
        // Written by 1.1.0, before exclusions existed.
        WriteFile("""{ "downloadFolder": "D:\\Walls", "launchAtStartup": false, "theme": "Dark", "purity": "Sfw" }""");

        var settings = new JsonSettingsStore(SettingsPath).Load();

        Assert.Empty(settings.ExcludedTags);
        Assert.Equal(ThemePreference.Dark, settings.Theme);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("[]")]
    public void Null_or_empty_excluded_tags_load_none(string tags)
    {
        WriteFile($$"""{ "excludedTags": {{tags}} }""");

        Assert.Equal(AppSettings.Default, new JsonSettingsStore(SettingsPath).Load());
    }

    [Fact]
    public void Invalid_and_duplicate_excluded_tags_are_dropped()
    {
        WriteFile("""{ "excludedTags": [" People ", "people", null, "", "digital art", "car"] }""");

        Assert.Equal(["people", "car"], new JsonSettingsStore(SettingsPath).Load().ExcludedTags);
    }

    [Fact]
    public void Excluded_tags_of_the_wrong_type_make_the_file_corrupt()
    {
        WriteFile("""{ "excludedTags": "people" }""");

        Assert.Equal(AppSettings.Default, new JsonSettingsStore(SettingsPath).Load());
        Assert.True(File.Exists(SettingsPath + ".bak"));
    }

    [Fact]
    public void Settings_compare_excluded_tags_by_content()
    {
        var a = AppSettings.Default with { ExcludedTags = ["people"] };
        var b = AppSettings.Default with { ExcludedTags = new List<string> { "people" } };

        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
        Assert.NotEqual(a, AppSettings.Default);
        Assert.Empty((AppSettings.Default with { ExcludedTags = null! }).ExcludedTags);
    }
}
