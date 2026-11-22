using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Salter.App.Interop;
using Salter.Presentation.Abstractions;
using Windows.Storage.Pickers;
using WinFolderPicker = Windows.Storage.Pickers.FolderPicker;

namespace Salter.App.Services;

/// <summary>Lets the user choose the download folder with the Windows folder picker, owned by the flyout.</summary>
/// <param name="owner">The flyout; it stays open while the picker has the focus.</param>
/// <param name="logger">Records pickers that fail to open.</param>
internal sealed partial class FolderPicker(FlyoutWindow owner, ILogger<FolderPicker> logger) : IFolderPicker
{
    /// <inheritdoc/>
    /// <remarks>Returns <see langword="null"/> as well when the picker cannot be shown.</remarks>
    public async Task<string?> PickAsync()
    {
        var picker = new WinFolderPicker { SuggestedStartLocation = PickerLocationId.PicturesLibrary };
        picker.FileTypeFilter.Add("*");
        // An unpackaged app must tell the picker which window owns it.
        WinRT.Interop.InitializeWithWindow.Initialize(picker, owner.Handle);

        // The picker takes the focus; without this the flyout would hide behind it.
        owner.SuppressAutoHide = true;
        try
        {
            var folder = await picker.PickSingleFolderAsync();
            return folder?.Path;
        }
        catch (COMException ex)
        {
            LogPickerFailed(logger, ex);
            return null;
        }
        finally
        {
            owner.SuppressAutoHide = false;
            // The flyout missed its Deactivated event while the picker was open; if the user has moved on to another
            // window, hide it now as that event would have.
            if (NativeMethods.GetForegroundWindow() != owner.Handle)
            {
                owner.Dismiss();
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "The folder picker could not be shown.")]
    private static partial void LogPickerFailed(ILogger logger, Exception exception);
}
