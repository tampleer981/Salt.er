namespace Salter.Presentation.Abstractions;

/// <summary>Severity of a toast message.</summary>
public enum ToastKind
{
    /// <summary>The operation succeeded.</summary>
    Success,

    /// <summary>The operation failed.</summary>
    Error,
}

/// <summary>Shows short transient messages to the user.</summary>
public interface IToastService
{
    /// <summary>Shows a toast.</summary>
    /// <param name="message">Text to display.</param>
    /// <param name="kind">Severity of the message.</param>
    void Show(string message, ToastKind kind);
}
