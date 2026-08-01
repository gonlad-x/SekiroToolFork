using System.Diagnostics;
using SekiroTool.Interfaces;
using SekiroTool.Utilities;

namespace SekiroTool.Services;

/// <summary>
/// Borderless window support for the game. Deliberately memory-free: this is pure Win32 window styling, so unlike
/// most of the tool it can't corrupt game state or crash on unresolved offsets.
/// <para>
/// Approach taken from SekiroFpsUnlockAndMore (MIT, github.com/uberhalit/SekiroFpsUnlockAndMore), which does the
/// same thing by flipping WS_POPUP on the game's window. Two deliberate differences: the window is snapped to the
/// full monitor bounds (that tool's separate "fullscreen stretch" option) because otherwise Windows keeps drawing
/// the taskbar over it, and the monitor rect comes from GetMonitorInfo rather than reading the game's render
/// resolution out of memory via an AOB-scanned offset - no pattern to maintain, no DPI conversion, and nothing to
/// break on a game patch.
/// </para>
/// </summary>
public class WindowService(IMemoryService memoryService) : IWindowService
{
    private const uint WindowedStyle = User32.WsVisible | User32.WsCaption | User32.WsBorder |
                                       User32.WsClipsiblings | User32.WsDlgframe | User32.WsSysmenu |
                                       User32.WsGroup | User32.WsMinimizebox;

    private const uint BorderlessStyle = User32.WsVisible | User32.WsPopup;

    /// <summary>The window rect from before borderless was applied, so windowed mode can be restored to it.</summary>
    private User32.Rect? _restoreRect;

    public bool IsBorderless()
    {
        var hWnd = GetGameWindow();
        return hWnd != IntPtr.Zero && HasBorderlessStyle(hWnd);
    }

    public bool EnableBorderless(out string failureReason)
    {
        failureReason = "";

        var hWnd = GetGameWindow();
        if (hWnd == IntPtr.Zero)
        {
            failureReason = "The game window wasn't found.";
            return false;
        }

        var style = GetStyle(hWnd, User32.GwlStyle);
        if (style == 0)
        {
            failureReason = "Couldn't read the game window's style.";
            return false;
        }

        if ((style & User32.WsMinimize) != 0)
        {
            failureReason = "Restore the game window before enabling borderless.";
            return false;
        }

        if (IsExclusiveFullscreen(hWnd, style))
        {
            failureReason = "Exit fullscreen in the game's display settings before enabling borderless.";
            return false;
        }

        if (HasBorderlessStyle(hWnd)) return true;

        // Capture the bordered rect first - it's the only record of where to put the window back.
        if (User32.GetWindowRect(hWnd, out var windowRect)) _restoreRect = windowRect;

        // Snap to the full bounds of whichever monitor the game is on. This is what makes the taskbar disappear:
        // Windows only treats a window as fullscreen (and lets it cover the always-on-top taskbar) when it
        // exactly covers the monitor. Sizing to the game's own client area instead leaves the taskbar drawn on
        // top, which is correct-but-useless for a borderless mode.
        if (!User32.TryGetMonitorBounds(hWnd, out var monitor))
        {
            failureReason = "Couldn't determine which monitor the game is on.";
            return false;
        }

        User32.SetWindowLongPtr(hWnd, User32.GwlStyle, new IntPtr(BorderlessStyle));
        User32.SetWindowPos(hWnd, User32.HwndTop, monitor.Left, monitor.Top,
            monitor.Width, monitor.Height, User32.SwpFramechanged | User32.SwpShowwindow);

        return true;
    }

    public bool DisableBorderless()
    {
        var hWnd = GetGameWindow();
        if (hWnd == IntPtr.Zero) return false;
        if (!HasBorderlessStyle(hWnd)) return true;

        int x, y, width, height;

        if (_restoreRect is { } rect && rect.Height > 0)
        {
            x = rect.Left;
            y = rect.Top;
            width = rect.Width;
            height = rect.Height;
        }
        else
        {
            // No captured rect: the tool was started while the game was already borderless. Restore at the
            // current size and position rather than refusing to act.
            if (!User32.GetWindowRect(hWnd, out var current)) return false;
            x = current.Left;
            y = current.Top;
            width = current.Width;
            height = current.Height;
        }

        User32.SetWindowLongPtr(hWnd, User32.GwlStyle, new IntPtr(WindowedStyle));
        User32.SetWindowPos(hWnd, User32.HwndNoTopmost, x, y, width, height,
            User32.SwpFramechanged | User32.SwpShowwindow);

        _restoreRect = null;
        return true;
    }

    private IntPtr GetGameWindow()
    {
        Process? process = memoryService.TargetProcess;
        if (process == null) return IntPtr.Zero;

        try
        {
            // MainWindowHandle is cached on the Process object and is zero if it was first read before the
            // game created its window, so refresh before trusting it.
            process.Refresh();
            return process.MainWindowHandle;
        }
        catch
        {
            return IntPtr.Zero;
        }
    }

    private static uint GetStyle(IntPtr hWnd, int index) => (uint)User32.GetWindowLongPtr(hWnd, index).ToInt64();

    private static bool HasBorderlessStyle(IntPtr hWnd)
    {
        var style = GetStyle(hWnd, User32.GwlStyle);
        if (style == 0) return false;

        return (style & User32.WsPopup) != 0 &&
               (style & User32.WsCaption) == 0 &&
               (style & User32.WsBorder) == 0;
    }

    /// <summary>
    /// Exclusive fullscreen looks like borderless except it's topmost and never has WS_POPUP. Switching styles
    /// out from under it leaves the game in a broken display state, so it has to be excluded.
    /// </summary>
    private static bool IsExclusiveFullscreen(IntPtr hWnd, uint style)
    {
        var exStyle = GetStyle(hWnd, User32.GwlExstyle);
        if (exStyle == 0) return false;

        return (exStyle & User32.WsExTopmost) != 0 &&
               (style & User32.WsPopup) == 0 &&
               (style & User32.WsCaption) == 0 &&
               (style & User32.WsBorder) == 0;
    }
}
