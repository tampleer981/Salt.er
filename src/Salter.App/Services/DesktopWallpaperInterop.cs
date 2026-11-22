using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using Salter.App.Interop;

namespace Salter.App.Services;

/// <summary>
/// The shell's <c>IDesktopWallpaper</c> COM interface (shobjidl_core.h). Methods are declared in vtable order up to
/// the last one the app calls; each returns its <c>HRESULT</c>, and strings are UTF-16 (out strings are allocated by
/// the shell with <c>CoTaskMemAlloc</c> and freed by the generated marshaller).
/// </summary>
[GeneratedComInterface(StringMarshalling = StringMarshalling.Utf16)]
[Guid("B92B56A9-8B55-4E14-9A89-0199BBB6F93B")]
internal partial interface IDesktopWallpaper
{
    [PreserveSig]
    int SetWallpaper(string? monitorId, string wallpaper);

    [PreserveSig]
    int GetWallpaper(string? monitorId, out string? wallpaper);

    [PreserveSig]
    int GetMonitorDevicePathAt(uint monitorIndex, out string? monitorId);

    [PreserveSig]
    int GetMonitorDevicePathCount(out uint count);

    [PreserveSig]
    int GetMonitorRECT(string monitorId, out NativeRect displayRect);

    [PreserveSig]
    int SetBackgroundColor(uint color);

    [PreserveSig]
    int GetBackgroundColor(out uint color);

    [PreserveSig]
    int SetPosition(DesktopWallpaperPosition position);

    [PreserveSig]
    int GetPosition(out DesktopWallpaperPosition position);
}

/// <summary>The native <c>DESKTOP_WALLPAPER_POSITION</c> enumeration.</summary>
internal enum DesktopWallpaperPosition
{
    /// <summary><c>DWPOS_CENTER</c>.</summary>
    Center = 0,

    /// <summary><c>DWPOS_TILE</c>.</summary>
    Tile = 1,

    /// <summary><c>DWPOS_STRETCH</c>.</summary>
    Stretch = 2,

    /// <summary><c>DWPOS_FIT</c>.</summary>
    Fit = 3,

    /// <summary><c>DWPOS_FILL</c>: scaled to cover the monitor, cropping what does not fit.</summary>
    Fill = 4,

    /// <summary><c>DWPOS_SPAN</c>.</summary>
    Span = 5,
}

/// <summary>Creates instances of the shell's <c>DesktopWallpaper</c> coclass.</summary>
internal static class DesktopWallpaperFactory
{
    /// <summary><c>CLSID_DesktopWallpaper</c>.</summary>
    private static readonly Guid ClassId = new("C2CF3110-460E-4FC1-B9D0-8A1C0C9CC4BD");

    private static readonly StrategyBasedComWrappers Wrappers = new();

    /// <summary>Creates a new instance. Call on an STA thread and release it with <see cref="Release"/> there.</summary>
    /// <returns>The interface.</returns>
    /// <exception cref="InvalidOperationException">The shell object could not be created.</exception>
    public static IDesktopWallpaper Create()
    {
        var interfaceId = typeof(IDesktopWallpaper).GUID;
        var hr = NativeMethods.CoCreateInstance(ClassId, 0, NativeMethods.ClassContextAll, interfaceId, out var pointer);
        if (hr < 0)
        {
            throw new InvalidOperationException($"Creating the desktop wallpaper object failed (HRESULT 0x{hr:X8}).");
        }

        try
        {
            return (IDesktopWallpaper)Wrappers.GetOrCreateObjectForComInstance(pointer, CreateObjectFlags.UniqueInstance);
        }
        finally
        {
            // The wrapper holds its own reference.
            Marshal.Release(pointer);
        }
    }

    /// <summary>Releases the native object now instead of when the wrapper is finalized.</summary>
    /// <param name="wallpaper">An instance from <see cref="Create"/>.</param>
    public static void Release(IDesktopWallpaper wallpaper)
    {
        // ComObject is sealed and implements the interface dynamically, so the test goes through object.
        if ((object)wallpaper is ComObject comObject)
        {
            comObject.FinalRelease();
        }
    }
}
