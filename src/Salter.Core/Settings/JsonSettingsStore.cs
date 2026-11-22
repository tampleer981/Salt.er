using System.Text.Json;
using System.Text.Json.Serialization;
using Salter.Core.Wallhaven;

namespace Salter.Core.Settings;

/// <summary>
/// Stores <see cref="AppSettings"/> as camel-case JSON with enum names. Missing, unknown or invalid individual
/// values fall back to <see cref="AppSettings.Default"/>; a file that cannot be parsed is renamed to
/// <c>&lt;file&gt;.bak</c> (replacing an older backup) and the defaults are used. Saves write a temporary file
/// first and then replace the settings file, so a crash never leaves a half-written file.
/// </summary>
/// <remarks>
/// <see cref="Load"/> never throws for file system reasons: an unreadable or locked file yields the defaults and
/// is left untouched, and a failed backup is skipped. <see cref="Save"/> propagates file system errors.
/// </remarks>
public sealed class JsonSettingsStore : ISettingsStore
{
    private const Purity AllPurities = Purity.Sfw | Purity.Sketchy | Purity.Nsfw;

    private readonly string _filePath;

    /// <summary>Creates a store for the given file.</summary>
    /// <param name="filePath">The settings file, for example <c>%LOCALAPPDATA%\Salt.er\settings.json</c>.</param>
    /// <exception cref="ArgumentException"><paramref name="filePath"/> is <see langword="null"/>, empty or whitespace.</exception>
    public JsonSettingsStore(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        _filePath = Path.GetFullPath(filePath);
    }

    /// <inheritdoc/>
    public AppSettings Load()
    {
        if (!File.Exists(_filePath))
        {
            return AppSettings.Default;
        }

        SettingsDto? dto;
        try
        {
            // Deserializing from a stream skips a UTF-8 byte order mark, which some editors add.
            using var stream = new FileStream(_filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            dto = JsonSerializer.Deserialize(stream, SettingsJsonContext.Default.SettingsDto);
        }
        catch (JsonException)
        {
            dto = null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Unreadable or locked: keep the file for the next start and run with the defaults.
            return AppSettings.Default;
        }

        if (dto is null)
        {
            TryBackUpCorruptFile();
            return AppSettings.Default;
        }

        var defaults = AppSettings.Default;
        return new AppSettings(
            string.IsNullOrWhiteSpace(dto.DownloadFolder) ? defaults.DownloadFolder : dto.DownloadFolder,
            dto.LaunchAtStartup ?? defaults.LaunchAtStartup,
            dto.Theme is { } theme && Enum.IsDefined(theme) ? theme : defaults.Theme,
            dto.Purity is { } purity && purity != 0 && (purity & ~AllPurities) == 0 ? purity : defaults.Purity);
    }

    private void TryBackUpCorruptFile()
    {
        try
        {
            File.Move(_filePath, _filePath + ".bak", overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Best effort: the defaults are used either way, and the next save replaces the corrupt file.
        }
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException"><paramref name="settings"/> is <see langword="null"/>.</exception>
    public void Save(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
        var dto = new SettingsDto
        {
            DownloadFolder = settings.DownloadFolder,
            LaunchAtStartup = settings.LaunchAtStartup,
            Theme = settings.Theme,
            Purity = settings.Purity,
        };
        var tempPath = _filePath + ".tmp";
        using (var stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            JsonSerializer.Serialize(stream, dto, SettingsJsonContext.Default.SettingsDto);
            stream.Flush(flushToDisk: true);
        }

        File.Move(tempPath, _filePath, overwrite: true);
    }
}

internal sealed class SettingsDto
{
    public string? DownloadFolder { get; set; }

    public bool? LaunchAtStartup { get; set; }

    public ThemePreference? Theme { get; set; }

    public Purity? Purity { get; set; }
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = true,
    Converters = [typeof(JsonStringEnumConverter<ThemePreference>), typeof(JsonStringEnumConverter<Purity>)])]
[JsonSerializable(typeof(SettingsDto))]
internal sealed partial class SettingsJsonContext : JsonSerializerContext;
