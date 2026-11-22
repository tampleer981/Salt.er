namespace Salter.Presentation.Abstractions;

/// <summary>Looks up user-visible strings by resource key.</summary>
public interface ILocalizer
{
    /// <summary>Gets the string for a key.</summary>
    /// <param name="key">Resource key.</param>
    /// <returns>The string for <paramref name="key"/>.</returns>
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Naming",
        "CA1716:Identifiers should not match keywords",
        Justification = "Get is the agreed API name; the app is C#-only and Get is a keyword only in Visual Basic.")]
    string Get(string key);

    /// <summary>Gets the string for a key and formats it with the given arguments.</summary>
    /// <param name="key">Resource key of a composite format string.</param>
    /// <param name="args">Format arguments.</param>
    /// <returns>The formatted string.</returns>
    string Format(string key, params object[] args);
}
