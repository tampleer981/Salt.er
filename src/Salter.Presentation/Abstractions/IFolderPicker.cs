namespace Salter.Presentation.Abstractions;

/// <summary>Lets the user choose a folder.</summary>
public interface IFolderPicker
{
    /// <summary>Shows the folder picker.</summary>
    /// <returns>The chosen folder path, or <see langword="null"/> if the user cancelled.</returns>
    Task<string?> PickAsync();
}
