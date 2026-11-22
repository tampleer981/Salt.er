using System.Globalization;

namespace Salter.Presentation.Formatting;

/// <summary>Formats file sizes for display, for example <c>4.1 MB</c>.</summary>
public static class FileSizeText
{
    private const double Step = 1024;
    private static readonly string[] Units = ["KB", "MB", "GB", "TB"];

    /// <summary>Formats a file size with binary units and one decimal; sizes under 1 KB are shown in bytes.</summary>
    /// <param name="bytes">The size in bytes.</param>
    /// <returns>The size, for example <c>512 B</c>, <c>1.5 KB</c> or <c>4.1 MB</c>, independent of the culture.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="bytes"/> is negative.</exception>
    public static string Format(long bytes)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(bytes);

        if (bytes < Step)
        {
            return string.Create(CultureInfo.InvariantCulture, $"{bytes} B");
        }

        var value = bytes / Step;
        var unit = 0;
        // Compare the rounded value, so 1023.96 KB reads 1.0 MB rather than 1024.0 KB.
        while (Math.Round(value, 1) >= Step && unit < Units.Length - 1)
        {
            value /= Step;
            unit++;
        }

        return string.Create(CultureInfo.InvariantCulture, $"{value:0.0} {Units[unit]}");
    }
}
