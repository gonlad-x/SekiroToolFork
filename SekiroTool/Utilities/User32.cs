using System.Runtime.InteropServices;

namespace SekiroTool.Utilities;

public class User32
{
    public static readonly IntPtr HwndTopmost = new IntPtr(-1);
    public static readonly IntPtr HwndTop = new IntPtr(0);
    public static readonly IntPtr HwndNoTopmost = new IntPtr(-2);

    public const uint SwpNosize = 0x0001;
    public const uint SwpNomove = 0x0002;
    public const uint SwpNoactivate = 0x0010;
    public const uint SwpFramechanged = 0x0020;
    public const uint SwpShowwindow = 0x0040;

    public const int GwlStyle = -16;
    public const int GwlExstyle = -20;

    public const uint WsGroup = 0x00020000;
    public const uint WsMinimizebox = 0x00020000;
    public const uint WsSysmenu = 0x00080000;
    public const uint WsDlgframe = 0x00400000;
    public const uint WsBorder = 0x00800000;
    public const uint WsCaption = 0x00C00000;
    public const uint WsClipsiblings = 0x04000000;
    public const uint WsVisible = 0x10000000;
    public const uint WsMinimize = 0x20000000;
    public const uint WsPopup = 0x80000000;
    public const uint WsExTopmost = 0x00000008;

    public const uint MonitorDefaultToNearest = 0x00000002;

    [StructLayout(LayoutKind.Sequential)]
    public struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;

        public int Width => Right - Left;
        public int Height => Bottom - Top;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MonitorInfo
    {
        public int cbSize;

        /// <summary>Full monitor bounds, including the area behind the taskbar.</summary>
        public Rect rcMonitor;

        /// <summary>Work area, i.e. excluding the taskbar.</summary>
        public Rect rcWork;

        public uint dwFlags;
    }

    [DllImport("user32.dll")]
    public static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    public static extern IntPtr GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll")]
    public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy,
        uint uFlags);

    // GetWindowLongPtr/SetWindowLongPtr only exist as exported names on 64-bit Windows. That is fine here:
    // Sekiro is x64-only, so the tool is always running on an x64 OS alongside it.
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    public static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    public static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll")]
    public static extern bool GetWindowRect(IntPtr hWnd, out Rect lpRect);

    [DllImport("user32.dll")]
    public static extern IntPtr MonitorFromWindow(IntPtr hWnd, uint dwFlags);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern bool GetMonitorInfo(IntPtr hMonitor, ref MonitorInfo lpmi);

    /// <summary>
    /// Full bounds of the monitor a window is on, in physical pixels. Physical is what SetWindowPos wants, and
    /// reading it from the monitor rather than WPF's SystemParameters sidesteps DPI scaling entirely.
    /// </summary>
    public static bool TryGetMonitorBounds(IntPtr hWnd, out Rect bounds)
    {
        bounds = default;

        var monitor = MonitorFromWindow(hWnd, MonitorDefaultToNearest);
        if (monitor == IntPtr.Zero) return false;

        var info = new MonitorInfo { cbSize = Marshal.SizeOf(typeof(MonitorInfo)) };
        if (!GetMonitorInfo(monitor, ref info)) return false;

        bounds = info.rcMonitor;
        return bounds.Width > 0 && bounds.Height > 0;
    }

    public static void SetTopmost(IntPtr hwnd)
    {
        SetWindowPos(hwnd, HwndTopmost, 0, 0, 0, 0, SwpNomove | SwpNosize | SwpNoactivate);
    }
}