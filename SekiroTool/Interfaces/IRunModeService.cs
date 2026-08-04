namespace SekiroTool.Interfaces;

public interface IRunModeService
{
    /// <summary>
    /// True while Live Run Mode is active.
    /// </summary>
    bool IsActive { get; }

    /// <summary>
    /// Raised whenever <see cref="IsActive"/> changes, so the shell can grey out tabs.
    /// </summary>
    event Action? StateChanged;

    /// <summary>
    /// Number of game-modifying changes the tool currently has live, ignoring anything legal
    /// during a run. Zero means starting a run now is safe.
    /// </summary>
    int CountActiveChanges();

    /// <summary>
    /// Reverts everything the tool changed in the running game.
    /// </summary>
    /// <param name="keepLegalOptions">
    /// True for Live Run Mode: keeps the Settings-tab options that are legal during a run.
    /// False on tool exit: reverts everything.
    /// </param>
    void RevertGameChanges(bool keepLegalOptions);

    /// <summary>
    /// Reverts illegal changes and enters Live Run Mode. Returns false if changes survived the
    /// revert, in which case the mode is NOT entered and <paramref name="failureReason"/> says why.
    /// </summary>
    bool TryStart(out string failureReason);

    /// <summary>
    /// Leaves Live Run Mode. Nothing is re-applied; the user re-ticks what they want.
    /// </summary>
    void Stop();
}
