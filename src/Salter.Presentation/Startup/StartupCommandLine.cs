namespace Salter.Presentation.Startup;

/// <summary>Builds and reads the command line that starts the app when the user signs in.</summary>
public static class StartupCommandLine
{
    /// <summary>Builds the sign-in command line: the quoted executable path followed by <see cref="LaunchArguments.Background"/>.</summary>
    /// <param name="executablePath">Full path of the executable to start.</param>
    /// <returns>The command line, for example <c>"C:\Apps\Salter.App.exe" --background</c>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="executablePath"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="executablePath"/> is empty, whitespace, or contains a quotation mark.
    /// </exception>
    public static string Format(string executablePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
        if (executablePath.Contains('"', StringComparison.Ordinal))
        {
            throw new ArgumentException("The path must not contain quotation marks.", nameof(executablePath));
        }

        return $"\"{executablePath}\" {LaunchArguments.Background}";
    }

    /// <summary>Reads the executable path from a command line: the leading quoted string or the first token.</summary>
    /// <param name="commandLine">The command line, or <see langword="null"/>.</param>
    /// <returns>The path, or <see langword="null"/> when the command line holds none or has an unclosed quote.</returns>
    public static string? GetExecutablePath(string? commandLine)
    {
        var text = commandLine?.TrimStart();
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        string path;
        if (text[0] == '"')
        {
            var end = text.IndexOf('"', 1);
            if (end < 0)
            {
                return null;
            }

            path = text[1..end];
        }
        else
        {
            var end = text.IndexOfAny([' ', '\t']);
            path = end < 0 ? text : text[..end];
        }

        return string.IsNullOrWhiteSpace(path) ? null : path;
    }
}
