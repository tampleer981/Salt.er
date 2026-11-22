using System.Runtime.InteropServices;

namespace Salter.App.Interop;

/// <summary>Win32 functions the Windows App SDK does not expose.</summary>
internal static partial class NativeMethods
{
    /// <summary><c>MDT_EFFECTIVE_DPI</c>: the DPI that includes the user's scale setting.</summary>
    internal const int MonitorEffectiveDpi = 0;

    /// <summary><c>DWMWA_WINDOW_CORNER_PREFERENCE</c> (Windows 11).</summary>
    internal const uint DwmWindowCornerPreference = 33;

    /// <summary><c>DWMWCP_ROUND</c>.</summary>
    internal const int DwmCornerRound = 2;

    /// <summary><c>DWMWA_BORDER_COLOR</c> (Windows 11).</summary>
    internal const uint DwmBorderColor = 34;

    /// <summary><c>DWMWA_COLOR_NONE</c>: no window border.</summary>
    internal const int DwmColorNone = unchecked((int)0xFFFFFFFE);

    /// <summary><c>CLSCTX_ALL</c>: in-process, handler, local and remote servers.</summary>
    internal const uint ClassContextAll = 0x17;

    /// <summary><c>CRED_TYPE_GENERIC</c>.</summary>
    internal const uint CredentialTypeGeneric = 1;

    /// <summary><c>CRED_PERSIST_LOCAL_MACHINE</c>: kept for this user on this machine, not roamed.</summary>
    internal const uint CredentialPersistLocalMachine = 2;

    /// <summary><c>ERROR_NOT_FOUND</c>.</summary>
    internal const int ErrorNotFound = 1168;

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool GetCursorPos(out NativePoint point);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool AllowSetForegroundWindow(uint processId);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool SetForegroundWindow(nint window);

    [LibraryImport("user32.dll")]
    internal static partial nint GetForegroundWindow();

    [LibraryImport("shell32.dll", EntryPoint = "Shell_NotifyIconGetRect")]
    internal static partial int ShellNotifyIconGetRect(in NotifyIconIdentifier identifier, out NativeRect iconLocation);

    [LibraryImport("shcore.dll")]
    internal static partial int GetDpiForMonitor(nint monitor, int dpiType, out uint dpiX, out uint dpiY);

    [LibraryImport("dwmapi.dll")]
    internal static partial int DwmSetWindowAttribute(nint window, uint attribute, in int value, uint size);

    [LibraryImport("ole32.dll")]
    internal static partial int CoCreateInstance(
        in Guid classId, nint outer, uint classContext, in Guid interfaceId, out nint instance);

    [LibraryImport("advapi32.dll", EntryPoint = "CredReadW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool CredRead(string targetName, uint type, uint flags, out nint credential);

    [LibraryImport("advapi32.dll", EntryPoint = "CredWriteW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool CredWrite(in NativeCredential credential, uint flags);

    [LibraryImport("advapi32.dll", EntryPoint = "CredDeleteW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool CredDelete(string targetName, uint type, uint flags);

    [LibraryImport("advapi32.dll")]
    internal static partial void CredFree(nint buffer);
}

/// <summary>Win32 <c>POINT</c>.</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct NativePoint
{
    public int X;
    public int Y;
}

/// <summary>Win32 <c>RECT</c>; right and bottom are exclusive.</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct NativeRect
{
    public int Left;
    public int Top;
    public int Right;
    public int Bottom;
}

/// <summary>Win32 <c>NOTIFYICONIDENTIFIER</c>.</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct NotifyIconIdentifier
{
    public uint Size;
    public nint Window;
    public uint Id;
    public Guid Item;
}

/// <summary>Win32 <c>CREDENTIALW</c>; strings are UTF-16 pointers and the blob is raw bytes.</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct NativeCredential
{
    public uint Flags;
    public uint Type;
    public nint TargetName;
    public nint Comment;
    public uint LastWrittenLow;
    public uint LastWrittenHigh;
    public uint CredentialBlobSize;
    public nint CredentialBlob;
    public uint Persist;
    public uint AttributeCount;
    public nint Attributes;
    public nint TargetAlias;
    public nint UserName;
}
