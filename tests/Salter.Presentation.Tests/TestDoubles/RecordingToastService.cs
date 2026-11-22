using Salter.Presentation.Abstractions;

namespace Salter.Presentation.Tests.TestDoubles;

/// <summary>Toast service that records every toast shown.</summary>
internal sealed class RecordingToastService : IToastService
{
    public List<(string Message, ToastKind Kind)> Shown { get; } = [];

    public void Show(string message, ToastKind kind) => Shown.Add((message, kind));
}
