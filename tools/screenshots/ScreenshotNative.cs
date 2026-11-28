// Win32 calls used by Capture-Screenshots.ps1, compiled at run time with Add-Type. Not part of the app.
using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Threading;

public static class ScreenshotNative
{
    private const uint InputMouse = 0;
    private const uint InputKeyboard = 1;
    private const uint MouseMove = 0x0001;
    private const uint MouseAbsolute = 0x8000;
    private const uint MouseLeftDown = 0x0002;
    private const uint MouseLeftUp = 0x0004;
    private const uint KeyEventKeyUp = 0x0002;
    private const uint KeyEventUnicode = 0x0004;
    private const ushort VirtualKeyMenu = 0x12;
    private const uint SetDesktopWallpaper = 0x0014;
    private const uint UpdateIniFileAndSendChange = 0x01 | 0x02;
    private const uint GetClientAreaAnimationAction = 0x1042;
    private const uint SetClientAreaAnimationAction = 0x1043;
    private const uint GetUiEffectsAction = 0x103E;
    private const uint SetUiEffectsAction = 0x103F;
    private const int EnumCurrentSettings = -1;
    private const int DisplayFieldsWidthHeight = 0x00080000 | 0x00100000;
    private static readonly IntPtr HwndBroadcast = new IntPtr(0xFFFF);
    private const uint WmSettingChange = 0x001A;
    private const uint WmClose = 0x0010;
    private const uint SendMessageAbortIfHung = 0x0002;

    [StructLayout(LayoutKind.Sequential)]
    private struct MouseInput
    {
        public int Dx;
        public int Dy;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public IntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KeyboardInput
    {
        public ushort VirtualKey;
        public ushort ScanCode;
        public uint Flags;
        public uint Time;
        public IntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)] public MouseInput Mouse;
        [FieldOffset(0)] public KeyboardInput Keyboard;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Input
    {
        public uint Type;
        public InputUnion Data;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DevMode
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string DeviceName;
        public short SpecVersion;
        public short DriverVersion;
        public short Size;
        public short DriverExtra;
        public int Fields;
        public int PositionX;
        public int PositionY;
        public int DisplayOrientation;
        public int DisplayFixedOutput;
        public short Color;
        public short Duplex;
        public short YResolution;
        public short TTOption;
        public short Collate;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string FormName;
        public short LogPixels;
        public int BitsPerPel;
        public int PelsWidth;
        public int PelsHeight;
        public int DisplayFlags;
        public int DisplayFrequency;
        public int IcmMethod;
        public int IcmIntent;
        public int MediaType;
        public int DitherType;
        public int Reserved1;
        public int Reserved2;
        public int PanningWidth;
        public int PanningHeight;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetProcessDpiAwarenessContext(IntPtr value);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool SystemParametersInfo(uint action, uint param, string value, uint winIni);

    [DllImport("user32.dll", EntryPoint = "SystemParametersInfoW", SetLastError = true)]
    private static extern bool SystemParametersInfoValue(uint action, uint param, IntPtr value, uint winIni);

    [DllImport("user32.dll", EntryPoint = "SystemParametersInfoW", SetLastError = true)]
    private static extern bool SystemParametersInfoGet(uint action, uint param, out int value, uint winIni);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr window);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetCursorPos(int x, int y);

    [StructLayout(LayoutKind.Sequential)]
    private struct Point
    {
        public int X;
        public int Y;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetCursorPos(out Point point);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint count, Input[] inputs, int size);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int index);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool PostMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool EnumDisplaySettings(string deviceName, int mode, ref DevMode devMode);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int ChangeDisplaySettings(ref DevMode devMode, int flags);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr SendMessageTimeout(
        IntPtr window, uint message, IntPtr wParam, string lParam, uint flags, uint timeout, out IntPtr result);

    /// <summary>Makes coordinates physical pixels, as UI Automation reports them; false if already set.</summary>
    public static bool MakeDpiAware()
    {
        // DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2
        return SetProcessDpiAwarenessContext(new IntPtr(-4));
    }

    public static int ScreenWidth { get { return GetSystemMetrics(0); } }

    public static int ScreenHeight { get { return GetSystemMetrics(1); } }

    public static void SetWallpaper(string path)
    {
        if (!SystemParametersInfo(SetDesktopWallpaper, 0, path, UpdateIniFileAndSendChange))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }
    }

    /// <summary>
    /// Turns the Windows "Animation effects" setting on or off: client area animations (what
    /// UISettings.AnimationsEnabled reports, and so what WinUI's implicit transitions follow) and UI effects. Saved
    /// to the user profile and broadcast, so apps started afterwards see it.
    /// </summary>
    public static void SetAnimationEffects(bool enabled)
    {
        var value = new IntPtr(enabled ? 1 : 0);
        if (!SystemParametersInfoValue(SetClientAreaAnimationAction, 0, value, UpdateIniFileAndSendChange))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        if (!SystemParametersInfoValue(SetUiEffectsAction, 0, value, UpdateIniFileAndSendChange))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }
    }

    /// <summary>Gets whether client area animations are on (SPI_GETCLIENTAREAANIMATION).</summary>
    public static bool ClientAreaAnimationsEnabled
    {
        get
        {
            int value;
            return SystemParametersInfoGet(GetClientAreaAnimationAction, 0, out value, 0) && value != 0;
        }
    }

    /// <summary>Gets whether UI effects are on (SPI_GETUIEFFECTS).</summary>
    public static bool UiEffectsEnabled
    {
        get
        {
            int value;
            return SystemParametersInfoGet(GetUiEffectsAction, 0, out value, 0) && value != 0;
        }
    }

    /// <summary>Tells running programs that a setting changed, for example "ImmersiveColorSet" after a theme change.</summary>
    public static void BroadcastSettingChange(string area)
    {
        IntPtr ignored;
        SendMessageTimeout(HwndBroadcast, WmSettingChange, IntPtr.Zero, area, SendMessageAbortIfHung, 5000, out ignored);
    }

    /// <summary>Changes the primary display resolution; returns the DISP_CHANGE code (0 = success).</summary>
    public static int SetResolution(int width, int height)
    {
        var mode = new DevMode();
        mode.Size = (short)Marshal.SizeOf(typeof(DevMode));
        if (!EnumDisplaySettings(null, EnumCurrentSettings, ref mode))
        {
            return int.MinValue;
        }

        mode.PelsWidth = width;
        mode.PelsHeight = height;
        mode.Fields = DisplayFieldsWidthHeight;
        return ChangeDisplaySettings(ref mode, 0);
    }

    /// <summary>Asks a window to close, as its close button would.</summary>
    public static void CloseWindow(IntPtr window)
    {
        if (!PostMessage(window, WmClose, IntPtr.Zero, IntPtr.Zero))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }
    }

    public static IntPtr ForegroundWindow()
    {
        return GetForegroundWindow();
    }

    /// <summary>Brings a window to the foreground; an Alt key press first lifts the foreground lock.</summary>
    public static bool BringToForeground(IntPtr window)
    {
        if (GetForegroundWindow() == window)
        {
            return true;
        }

        Send(Key(VirtualKeyMenu, 0, 0), Key(VirtualKeyMenu, 0, KeyEventKeyUp));
        SetForegroundWindow(window);
        return GetForegroundWindow() == window;
    }

    public static void MoveCursor(int x, int y)
    {
        if (!SetCursorPos(x, y))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }
    }

    /// <summary>
    /// Moves the pointer to (x, y) on the primary display with real mouse input, in small steps from where it is.
    /// Unlike SetCursorPos, which only places the cursor, this reaches the window under it as pointer movement, so
    /// XAML raises PointerEntered and PointerExited (and shows hover states).
    /// </summary>
    public static void MovePointer(int x, int y, int steps)
    {
        Point start;
        if (!GetCursorPos(out start))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        steps = Math.Max(1, steps);
        for (var i = 1; i <= steps; i++)
        {
            var px = start.X + (x - start.X) * i / steps;
            var py = start.Y + (y - start.Y) * i / steps;
            var input = Mouse(MouseMove | MouseAbsolute);
            // Absolute coordinates are normalized to 0..65535 across the primary display.
            input.Data.Mouse.Dx = Normalize(px, ScreenWidth);
            input.Data.Mouse.Dy = Normalize(py, ScreenHeight);
            Send(input);
            Thread.Sleep(15);
        }
    }

    private static int Normalize(int value, int size)
    {
        return size <= 1 ? 0 : (int)Math.Round(value * 65535.0 / (size - 1));
    }

    public static void Click(int x, int y)
    {
        MoveCursor(x, y);
        Send(Mouse(MouseLeftDown), Mouse(MouseLeftUp));
    }

    public static void PressKey(ushort virtualKey)
    {
        Send(Key(virtualKey, 0, 0), Key(virtualKey, 0, KeyEventKeyUp));
    }

    /// <summary>Presses a key while a modifier (for example VK_CONTROL, 0x11) is held.</summary>
    public static void PressKeyWith(ushort modifier, ushort virtualKey)
    {
        Send(
            Key(modifier, 0, 0),
            Key(virtualKey, 0, 0),
            Key(virtualKey, 0, KeyEventKeyUp),
            Key(modifier, 0, KeyEventKeyUp));
    }

    public static void TypeText(string text)
    {
        foreach (var c in text)
        {
            Send(Key(0, c, KeyEventUnicode), Key(0, c, KeyEventUnicode | KeyEventKeyUp));
        }
    }

    private static Input Mouse(uint flags)
    {
        var input = new Input { Type = InputMouse };
        input.Data.Mouse.Flags = flags;
        return input;
    }

    private static Input Key(ushort virtualKey, ushort scanCode, uint flags)
    {
        var input = new Input { Type = InputKeyboard };
        input.Data.Keyboard.VirtualKey = virtualKey;
        input.Data.Keyboard.ScanCode = scanCode;
        input.Data.Keyboard.Flags = flags;
        return input;
    }

    private static void Send(params Input[] inputs)
    {
        if (SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(Input))) != inputs.Length)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }
    }
}
