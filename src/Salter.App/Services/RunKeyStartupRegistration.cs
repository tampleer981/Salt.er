using System.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using Salter.Presentation.Abstractions;
using Salter.Presentation.Startup;

namespace Salter.App.Services;

/// <summary>
/// Starts the app at sign-in through the per-user <c>HKCU\Software\Microsoft\Windows\CurrentVersion\Run</c> key,
/// with the value <c>Salt.er</c> = <c>"&lt;exe path&gt;" --background</c>.
/// </summary>
internal sealed partial class RunKeyStartupRegistration : IStartupRegistration
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "Salt.er";

    private readonly Func<string> _executablePath;
    private readonly ILogger<RunKeyStartupRegistration> _logger;

    /// <summary>Creates the registration.</summary>
    /// <param name="executablePath">Returns the full path of the executable that sign-in should start.</param>
    /// <param name="logger">Receives failures to read the registration.</param>
    public RunKeyStartupRegistration(Func<string> executablePath, ILogger<RunKeyStartupRegistration> logger)
    {
        ArgumentNullException.ThrowIfNull(executablePath);
        ArgumentNullException.ThrowIfNull(logger);
        _executablePath = executablePath;
        _logger = logger;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// <see langword="true"/> only when the value exists and starts the current executable, so an entry left behind
    /// by a copy of the app in another folder does not count. Never throws: a registry or executable path failure is
    /// logged and reported as <see langword="false"/>.
    /// </remarks>
    public bool IsEnabled
    {
        get
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
                var command = key?.GetValue(ValueName) as string;
                return StartupCommandLine.GetExecutablePath(command) is { } registered
                    && SamePath(registered, _executablePath());
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException
                or InvalidOperationException)
            {
                LogReadFailed(ex);
                return false;
            }
        }
    }

    /// <summary>Gets the path of the running executable.</summary>
    /// <returns>The full path.</returns>
    /// <exception cref="InvalidOperationException">The process path is unknown.</exception>
    public static string CurrentProcessPath() =>
        Environment.ProcessPath ?? throw new InvalidOperationException("The executable path is unknown.");

    /// <inheritdoc/>
    public void SetEnabled(bool enabled)
    {
        var command = enabled ? StartupCommandLine.Format(_executablePath()) : null;
        Guard(() =>
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);
            if (command is null)
            {
                key.DeleteValue(ValueName, throwOnMissingValue: false);
            }
            else
            {
                key.SetValue(ValueName, command, RegistryValueKind.String);
            }
        });
    }

    private static bool SamePath(string registered, string current)
    {
        try
        {
            return string.Equals(Path.GetFullPath(registered), Path.GetFullPath(current), StringComparison.OrdinalIgnoreCase);
        }
        catch (ArgumentException)
        {
            // Not a usable path, so it cannot be this executable.
            return false;
        }
    }

    private static void Guard(Action registryAccess)
    {
        try
        {
            registryAccess();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException)
        {
            throw new InvalidOperationException("The sign-in startup entry could not be accessed.", ex);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not read the sign-in startup entry.")]
    private partial void LogReadFailed(Exception exception);
}
