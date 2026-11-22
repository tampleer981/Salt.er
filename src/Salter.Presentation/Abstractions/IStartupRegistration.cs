namespace Salter.Presentation.Abstractions;

/// <summary>Controls whether the app starts when the user signs in.</summary>
public interface IStartupRegistration
{
    /// <summary>Gets a value indicating whether start-at-sign-in is currently enabled.</summary>
    /// <remarks>
    /// Never throws: when the registration cannot be read, implementations report <see langword="false"/>.
    /// </remarks>
    bool IsEnabled { get; }

    /// <summary>Enables or disables start-at-sign-in.</summary>
    /// <param name="enabled"><see langword="true"/> to start with the user session.</param>
    /// <exception cref="InvalidOperationException">
    /// The platform could not change the registration. Implementations wrap platform-specific errors in this
    /// exception.
    /// </exception>
    void SetEnabled(bool enabled);
}
