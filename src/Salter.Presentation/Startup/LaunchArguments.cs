namespace Salter.Presentation.Startup;

/// <summary>Reads the app's command line options.</summary>
public static class LaunchArguments
{
    /// <summary>The option that starts the app in the notification area without opening the flyout.</summary>
    public const string Background = "--background";

    private static readonly char[] Separators = [' ', '\t'];

    /// <summary>Determines whether the arguments contain <see cref="Background"/> (case-insensitive).</summary>
    /// <param name="args">The parsed arguments, without the executable path.</param>
    /// <returns><see langword="true"/> if the app should start in the background.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="args"/> is <see langword="null"/>.</exception>
    public static bool IsBackground(IEnumerable<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);
        return args.Any(IsBackgroundOption);
    }

    /// <summary>
    /// Determines whether a raw command line, as handed over by a redirected launch, contains
    /// <see cref="Background"/> as a separate, optionally quoted, token (case-insensitive).
    /// </summary>
    /// <param name="commandLine">The command line, possibly starting with the executable path.</param>
    /// <returns><see langword="true"/> if the launch asked to start in the background.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="commandLine"/> is <see langword="null"/>.</exception>
    public static bool IsBackground(string commandLine)
    {
        ArgumentNullException.ThrowIfNull(commandLine);
        return commandLine
            .Split(Separators, StringSplitOptions.RemoveEmptyEntries)
            .Any(token => IsBackgroundOption(token.Trim('"')));
    }

    private static bool IsBackgroundOption(string arg) =>
        string.Equals(arg, Background, StringComparison.OrdinalIgnoreCase);
}
