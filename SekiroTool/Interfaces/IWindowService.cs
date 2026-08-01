namespace SekiroTool.Interfaces;

public interface IWindowService
{
    /// <summary>Whether the game's window currently has the borderless (WS_POPUP, no caption/border) style.</summary>
    bool IsBorderless();

    /// <summary>
    /// Switches the game's window to borderless, keeping its current client size and position.
    /// Returns false if it can't be done right now - no window yet, minimised, or in exclusive fullscreen.
    /// </summary>
    bool EnableBorderless(out string failureReason);

    /// <summary>
    /// Restores the ordinary bordered window. Uses the rect captured when borderless was enabled; if that isn't
    /// available (tool restarted while the game was already borderless) it falls back to the current client size.
    /// </summary>
    bool DisableBorderless();
}
