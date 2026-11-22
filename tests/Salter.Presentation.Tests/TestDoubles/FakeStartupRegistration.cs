using Salter.Presentation.Abstractions;

namespace Salter.Presentation.Tests.TestDoubles;

/// <summary>Start-at-sign-in registration backed by a property.</summary>
internal sealed class FakeStartupRegistration : IStartupRegistration
{
    public bool IsEnabled { get; set; }

    /// <summary>When set, <see cref="SetEnabled"/> fails with this exception and keeps the current state.</summary>
    public Exception? Failure { get; set; }

    public List<bool> Calls { get; } = [];

    public void SetEnabled(bool enabled)
    {
        Calls.Add(enabled);
        if (Failure is not null)
        {
            throw Failure;
        }

        IsEnabled = enabled;
    }
}
