using Salter.Presentation.Abstractions;

namespace Salter.Presentation.Tests.TestDoubles;

/// <summary>Folder picker that returns a preset result; <see langword="null"/> means the user cancelled.</summary>
internal sealed class FakeFolderPicker : IFolderPicker
{
    public string? Result { get; set; }

    public int Calls { get; private set; }

    public Task<string?> PickAsync()
    {
        Calls++;
        return Task.FromResult(Result);
    }
}
