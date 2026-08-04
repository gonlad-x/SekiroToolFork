using System.Reflection;
using SekiroTool.Enums;
using SekiroTool.Interfaces;
using SekiroTool.Memory;
using SekiroTool.Utilities;
using static SekiroTool.Memory.Offsets;

namespace SekiroTool.Services;

/// <summary>
/// Reverts everything the tool changed in the running game, and backs the Live Run Mode toggle.
///
/// Deliberately self-contained: it depends only on IMemoryService, the two patch managers,
/// IStateService and the Offsets statics, so it ports to branches whose ViewModels differ.
/// Activate On Launch is reached through delegates rather than a ViewModel reference, so a branch
/// without AOL can pass no-ops.
/// </summary>
public class RunModeService : IRunModeService
{
    /// <summary>
    /// Activate On Launch option that stays allowed in run mode: a runner shouldn't have to play
    /// through NG 0-6 by hand. Blanket AOL suppression kills it, so this class re-applies it.
    /// </summary>
    private const string AutoSetNewGame7OptionId = "IsAutoSetNewGame7Checked";

    private const int SnakeLoopExitTimeoutMs = 1000;

    private readonly IMemoryService _memoryService;
    private readonly HookManager _hookManager;
    private readonly NopManager _nopManager;
    private readonly IPlayerService _playerService;
    private readonly IUtilityService _utilityService;
    private readonly ITargetService _targetService;
    private readonly IReminderService _reminderService;
    private readonly HotkeyManager _hotkeyManager;
    private readonly Func<bool> _getActivateOnLaunchEnabled;
    private readonly Action<bool> _setActivateOnLaunchEnabled;
    private readonly Func<string, bool> _getActivateOnLaunchOption;

    private bool _wasActivateOnLaunchEnabled;

    // Captured by CaptureReinstateSnapshot() just before TryStart's sweep, replayed by Reinstate()
    // when Stop() is called - so options the sweep turned off come back on their own instead of
    // needing a save reload or a manual re-tick.
    private List<int> _reinstateDebugFlagOffsets = [];
    private List<bool> _reinstateCharacterFlags = [];
    private float? _reinstateGameSpeed;
    private float? _reinstatePlayerSpeed;
    private List<(long Address, int Length)> _reinstateNops = [];
    private List<(nint CodeLoc, nint Origin, byte[] OriginalBytes)> _reinstateHooks = [];

    public RunModeService(IMemoryService memoryService, HookManager hookManager, NopManager nopManager,
        IPlayerService playerService, IUtilityService utilityService, ITargetService targetService,
        IReminderService reminderService, HotkeyManager hotkeyManager,
        IStateService stateService, Func<bool> getActivateOnLaunchEnabled,
        Action<bool> setActivateOnLaunchEnabled, Func<string, bool> getActivateOnLaunchOption)
    {
        _memoryService = memoryService;
        _hookManager = hookManager;
        _nopManager = nopManager;
        _playerService = playerService;
        _utilityService = utilityService;
        _targetService = targetService;
        _reminderService = reminderService;
        _hotkeyManager = hotkeyManager;
        _getActivateOnLaunchEnabled = getActivateOnLaunchEnabled;
        _setActivateOnLaunchEnabled = setActivateOnLaunchEnabled;
        _getActivateOnLaunchOption = getActivateOnLaunchOption;

        // Subscribed after every feature ViewModel has been built, so these run last:
        // StateService.Publish iterates subscribers in subscription order. The ViewModels re-apply
        // whatever is still ticked on Loaded, and this undoes it again.
        stateService.Subscribe(State.Loaded, OnGameLoaded);
        stateService.Subscribe(State.GameStart, OnNewGameStart);
    }

    public bool IsActive { get; private set; }

    public event Action? StateChanged;

    #region Public Methods

    public int CountActiveChanges() => DescribeActiveChanges().Count;

    public IReadOnlyList<string> DescribeActiveChanges()
    {
        var changes = new List<string>();
        if (!_memoryService.IsAttached) return changes;

        if (DebugFlags.Base != IntPtr.Zero)
            changes.AddRange(DebugFlagsByName
                .Where(flag => _memoryService.Read<byte>(DebugFlags.Base + flag.Offset) != 0)
                .Select(flag => Humanise(flag.Name)));

        if (_playerService.IsPlayerNoDamageEnabled()) changes.Add("Player No Damage");
        if (_targetService.IsNoDamageEnabled()) changes.Add("Target No Damage");
        if (_targetService.IsNoDeathEnabled()) changes.Add("Target No Death");
        if (_targetService.IsNoPostureBuildupEnabled()) changes.Add("Target No Posture Buildup");
        if (_targetService.IsAiFreezeEnabled()) changes.Add("Target Ai Freeze");
        if (_targetService.IsNoAttackEnabled()) changes.Add("Target No Attack");
        if (_targetService.IsNoMoveEnabled()) changes.Add("Target No Move");

        changes.AddRange(_hookManager.InstalledHookKeys
            .Where(key => !LegalHookKeys.Contains(key))
            .Select(DescribeCaveAddress));

        changes.AddRange(_nopManager.InstalledNopKeys
            .Where(key => !LegalNopKeys.Contains(key))
            .Select(DescribeNopAddress));

        var gameSpeed = _utilityService.GetGameSpeed();
        if (gameSpeed > 0f && Math.Abs(gameSpeed - 1f) > 0.001f)
            changes.Add($"Game speed {gameSpeed:0.##}x");

        return changes;
    }

    public void RevertGameChanges(bool keepLegalOptions)
    {
        if (!_memoryService.IsAttached) return;

        try
        {
            // Order matters. The snake loop runs as a thread inside the code cave, so it has to be
            // out before anything unhooks or frees what it is executing.
            // Each step is logged: every write here goes into a live game process, so when one of
            // them kills the game the log is the only way to know which.
            Log("stopping snake loop");
            StopSnakeCanyonLoop();
            Log("resetting debug flags");
            ResetDebugFlags();
            Log("resetting character bit flags");
            ResetCharacterBitFlags();
            Log("resetting speeds");
            ResetSpeeds();
            Log("restoring patches");
            RestorePatches(keepLegalOptions);
            Log("restoring nops");
            RestoreNops(keepLegalOptions);
            Log("uninstalling hooks");
            UninstallHooks(keepLegalOptions);

            // Last: the reminder marks that behaviour-tampering features were on, and by this point
            // they are off. Re-swapped by the ViewModels on the next Loaded if anything is still
            // ticked, which is why OnGameLoaded re-runs the whole revert.
            Log("restoring idol icon");
            _reminderService.RestoreIdolIcon();
            Log("revert complete");
        }
        catch (Exception e)
        {
            // Cleanup runs while the window is closing; an offset that never resolved must not
            // take the shutdown down with it.
            Console.WriteLine($@"RunModeService revert failed: {e.Message}");
        }
    }

    public bool TryStart(out string failureReason)
    {
        failureReason = string.Empty;
        if (IsActive) return true;

        CaptureReinstateSnapshot();
        RevertGameChanges(keepLegalOptions: true);

        var remaining = CountActiveChanges();
        if (remaining > 0)
        {
            failureReason = $"{remaining} game-modifying change(s) could not be reverted. " +
                            "Live run mode was not started.";
            return false;
        }

        _wasActivateOnLaunchEnabled = _getActivateOnLaunchEnabled();
        _setActivateOnLaunchEnabled(false);
        _hotkeyManager.Stop();

        IsActive = true;
        StateChanged?.Invoke();
        return true;
    }

    public void Stop()
    {
        if (!IsActive) return;

        IsActive = false;
        Reinstate();
        _setActivateOnLaunchEnabled(_wasActivateOnLaunchEnabled);
        if (SettingsManager.Default.EnableHotkeys) _hotkeyManager.Start();

        StateChanged?.Invoke();
    }

    #endregion

    #region Private Methods

    private static void Log(string step) => Console.WriteLine($@"RunModeService: {step}");

    private void OnGameLoaded()
    {
        if (IsActive) RevertGameChanges(keepLegalOptions: true);
    }

    private void OnNewGameStart()
    {
        if (!IsActive) return;

        // AOL is suppressed while in run mode, so its one legal new-game option is applied here.
        if (_wasActivateOnLaunchEnabled && _getActivateOnLaunchOption(AutoSetNewGame7OptionId))
            _playerService.SetNewGame(7);
    }

    /// <summary>
    /// Hooks that are legal during a run: No Camera Spin and Disable Menu Music.
    /// </summary>
    private static IReadOnlyList<nint> LegalHookKeys => CodeCaveOffsets.Base == IntPtr.Zero
        ?
        []
        :
        [
            CodeCaveOffsets.Base + CodeCaveOffsets.NoCameraSpin,
            CodeCaveOffsets.Base + CodeCaveOffsets.NoMenuMusic
        ];

    /// <summary>
    /// Nops that are legal during a run: Disable Cutscenes.
    /// </summary>
    private static IReadOnlyList<long> LegalNopKeys => [Functions.FormatCutscenePathString];

    /// <summary>
    /// Read off the DebugFlags offsets class rather than listed by hand, so a flag added later is
    /// covered automatically and the per-version offsets stay correct.
    /// </summary>
    private static IEnumerable<int> DebugFlagOffsets => DebugFlagsByName.Select(flag => flag.Offset);

    private static IEnumerable<(string Name, int Offset)> DebugFlagsByName =>
        typeof(DebugFlags)
            .GetProperties(BindingFlags.Public | BindingFlags.Static)
            .Where(property => property.PropertyType == typeof(int))
            .Select(property => (property.Name, (int)property.GetValue(null)!));

    /// <summary>
    /// Turns an offset name into something readable for the confirmation dialog, e.g.
    /// PlayerNoDeath -> "Player No Death". Reverse-looked-up by value so no name list is maintained.
    /// </summary>
    private static string Humanise(string name) =>
        System.Text.RegularExpressions.Regex.Replace(name, "(?<!^)([A-Z])", " $1");

    private static string DescribeCaveAddress(nint key)
    {
        if (CodeCaveOffsets.Base != IntPtr.Zero)
        {
            var offset = (int)(key - CodeCaveOffsets.Base);
            var name = typeof(CodeCaveOffsets)
                .GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(field => field.IsLiteral && field.FieldType == typeof(int))
                .FirstOrDefault(field => (int)field.GetRawConstantValue()! == offset)?.Name;

            if (name != null) return Humanise(name);
        }

        return $"hook at 0x{key:X}";
    }

    private static string DescribeNopAddress(long key)
    {
        var name = NamedAddress(typeof(Patches), key) ?? NamedAddress(typeof(Functions), key);
        return name != null ? Humanise(name) : $"nop at 0x{key:X}";
    }

    private static string? NamedAddress(Type offsetsType, long address) =>
        offsetsType
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => !field.IsLiteral && field.FieldType == typeof(nint))
            .FirstOrDefault(field => (nint)field.GetValue(null)! == (nint)address)?.Name;

    private void StopSnakeCanyonLoop()
    {
        if (CodeCaveOffsets.Base == IntPtr.Zero) return;

        var runningFlag = CodeCaveOffsets.Base + CodeCaveOffsets.SnakeLoopIsRunningFlag;
        if (_memoryService.Read<byte>(runningFlag) == 0) return;

        _memoryService.Write(CodeCaveOffsets.Base + CodeCaveOffsets.ShouldExitSnakeLoopFlag, (byte)1);

        // TerminateSnakeAnimationLoop only sets the flag and returns; the thread is still in the
        // cave until it wakes from its Sleep and checks.
        var deadline = Environment.TickCount + SnakeLoopExitTimeoutMs;
        while (_memoryService.Read<byte>(runningFlag) != 0 && Environment.TickCount < deadline)
            Thread.Sleep(10);
    }

    private void ResetDebugFlags()
    {
        if (DebugFlags.Base == IntPtr.Zero) return;

        foreach (var offset in DebugFlagOffsets)
            _memoryService.Write(DebugFlags.Base + offset, (byte)0);
    }

    /// <summary>
    /// Options stored as bits on a character instance rather than in the debug-flag block: Player
    /// No Damage and the Target tab's per-enemy toggles. No registry records these, so unlike hooks
    /// and nops they have to be listed by hand - add to this method when a bit-flag option is added.
    /// Player No Damage being missed here is why it survived the first working build of run mode.
    ///
    /// The target ones need a live locked-on enemy; with none, the pointer read yields zero and the
    /// write fails harmlessly, so they are best-effort. The bits die with the entity anyway.
    /// </summary>
    private void ResetCharacterBitFlags()
    {
        _playerService.TogglePlayerNoDamage(false);

        _targetService.ToggleNoDamage(false);
        _targetService.ToggleNoDeath(false);
        _targetService.ToggleNoPostureBuildup(false);
        _targetService.ToggleAiFreeze(false);
        _targetService.ToggleNoAttack(false);
        _targetService.ToggleNoMove(false);
    }

    /// <summary>
    /// Same options as ResetCharacterBitFlags, paired with their getters. Backs both
    /// CaptureReinstateSnapshot and Reinstate, so add a bit-flag option to both methods together.
    /// </summary>
    private IReadOnlyList<(Func<bool> IsEnabled, Action<bool> Toggle)> CharacterBitFlagOptions =>
    [
        (_playerService.IsPlayerNoDamageEnabled, _playerService.TogglePlayerNoDamage),
        (_targetService.IsNoDamageEnabled, _targetService.ToggleNoDamage),
        (_targetService.IsNoDeathEnabled, _targetService.ToggleNoDeath),
        (_targetService.IsNoPostureBuildupEnabled, _targetService.ToggleNoPostureBuildup),
        (_targetService.IsAiFreezeEnabled, _targetService.ToggleAiFreeze),
        (_targetService.IsNoAttackEnabled, _targetService.ToggleNoAttack),
        (_targetService.IsNoMoveEnabled, _targetService.ToggleNoMove)
    ];

    /// <summary>
    /// Snapshots exactly what RevertGameChanges is about to turn off, read straight from game
    /// memory/the hook and nop registries rather than from any ViewModel - keeping the same
    /// isolation the rest of this class relies on. Called right before the sweep in TryStart.
    /// </summary>
    private void CaptureReinstateSnapshot()
    {
        _reinstateDebugFlagOffsets = [];
        _reinstateCharacterFlags = [];
        _reinstateGameSpeed = null;
        _reinstatePlayerSpeed = null;
        _reinstateNops = [];
        _reinstateHooks = [];

        if (!_memoryService.IsAttached) return;

        if (DebugFlags.Base != IntPtr.Zero)
            _reinstateDebugFlagOffsets = DebugFlagOffsets
                .Where(offset => _memoryService.Read<byte>(DebugFlags.Base + offset) != 0)
                .ToList();

        _reinstateCharacterFlags = CharacterBitFlagOptions.Select(option => option.IsEnabled()).ToList();

        var gameSpeed = _utilityService.GetGameSpeed();
        if (gameSpeed > 0f && Math.Abs(gameSpeed - 1f) > 0.001f) _reinstateGameSpeed = gameSpeed;

        var playerSpeed = _playerService.GetPlayerSpeed();
        if (playerSpeed > 0f && Math.Abs(playerSpeed - 1f) > 0.001f) _reinstatePlayerSpeed = playerSpeed;

        foreach (var key in _nopManager.InstalledNopKeys.Where(key => !LegalNopKeys.Contains(key)))
            if (_nopManager.TryGetNopLength(key, out var length))
                _reinstateNops.Add((key, length));

        foreach (var key in _hookManager.InstalledHookKeys.Where(key => !LegalHookKeys.Contains(key)))
            if (_hookManager.TryGetHookInstallData(key, out var origin, out var originalBytes))
                _reinstateHooks.Add((key, origin, originalBytes));
    }

    /// <summary>
    /// Turns back on whatever CaptureReinstateSnapshot recorded as active right before the sweep.
    /// Called from Stop() so the checkboxes - which the sweep never touches, since they stay
    /// bound to ViewModel properties this class doesn't reach - stop lying about the actual
    /// in-game state once run mode ends.
    /// </summary>
    private void Reinstate()
    {
        if (!_memoryService.IsAttached) return;

        try
        {
            Log("reinstating debug flags");
            foreach (var offset in _reinstateDebugFlagOffsets)
                _memoryService.Write(DebugFlags.Base + offset, (byte)1);

            Log("reinstating character bit flags");
            var options = CharacterBitFlagOptions;
            for (var i = 0; i < options.Count && i < _reinstateCharacterFlags.Count; i++)
                if (_reinstateCharacterFlags[i]) options[i].Toggle(true);

            Log("reinstating speeds");
            if (_reinstateGameSpeed is { } gameSpeed) _utilityService.SetGameSpeed(gameSpeed);
            if (_reinstatePlayerSpeed is { } playerSpeed) _playerService.SetSpeed(playerSpeed);

            Log("reinstating nops");
            foreach (var (address, length) in _reinstateNops)
                _nopManager.InstallNop(address, length);

            Log("reinstating hooks");
            foreach (var (codeLoc, origin, originalBytes) in _reinstateHooks)
                _hookManager.InstallHook(codeLoc, origin, originalBytes);

            Log("reinstate complete");
        }
        catch (Exception e)
        {
            Console.WriteLine($@"RunModeService reinstate failed: {e.Message}");
        }
    }

    private void ResetSpeeds()
    {
        _utilityService.SetGameSpeed(1f);

        // Animation speed needs a live character; with none, the pointer read yields zero and the
        // write fails harmlessly, which is why run mode can be entered before the save is loaded.
        _playerService.SetSpeed(1f);
    }

    private void RestorePatches(bool keepLegalOptions)
    {
        if (!keepLegalOptions)
        {
            RestorePatch(Patches.NoLogo, [0xEB], OriginalBytesByPatch.NoLogo.GetOriginal);
            RestorePatch(Patches.MenuTutorialSkip, [0x90, 0x90, 0x90, 0x90],
                OriginalBytesByPatch.MenuTutorialSkip.GetOriginal);
            RestorePatch(Patches.ShowSmallHintBox, [0x90, 0x90, 0x90, 0x90, 0x90],
                OriginalBytesByPatch.ShowSmallHintBox.GetOriginal);
            RestorePatch(Patches.ShowTutorialText, [0x90, 0x90, 0x90, 0x90, 0x90],
                OriginalBytesByPatch.ShowTutorialText.GetOriginal);

            // Default sound volume is deliberately not reverted: the patched bytes are the user's
            // chosen volume, so there is no fixed pattern to recognise, and leaving a sound volume
            // immediate alone is harmless.
        }

        RestorePatch(Patches.EventView, [0x90, 0x90, 0x90, 0x90, 0x90, 0x90, 0x90, 0x90],
            OriginalBytesByPatch.EventView.GetOriginal);
        RestorePatch(Patches.PlayerSoundView, [0x75], OriginalBytesByPatch.PlayerSoundView.GetOriginal);
        RestorePatch(Patches.SaveInCombat, [0x90, 0x90, 0x90, 0x90, 0x90, 0x90, 0x90, 0x90, 0x90],
            OriginalBytesByPatch.SaveInCombat.GetOriginal);
    }

    /// <summary>
    /// Restores a patch only when the bytes at the address are exactly what the tool writes when it
    /// applies that patch. Several of the "original" byte strings carry hardcoded jump displacements
    /// and have no version switch, so writing one blindly over a patch this session never applied can
    /// corrupt live game code - which crashed the game on the first version of this sweep. Matching
    /// the patched pattern first makes the revert evidence-based: if the pattern is not there, either
    /// the patch was never applied or these bytes are wrong for this game version, and both mean
    /// "leave it alone".
    /// </summary>
    private void RestorePatch(nint address, byte[] patchedBytes, Func<byte[]> getOriginal)
    {
        if (address == IntPtr.Zero) return;

        try
        {
            var current = _memoryService.ReadBytes(address, patchedBytes.Length);
            if (!current.SequenceEqual(patchedBytes)) return;

            _memoryService.WriteBytes(address, getOriginal());
        }
        catch (Exception e)
        {
            Console.WriteLine($@"RunModeService could not restore patch at 0x{address:X}: {e.Message}");
        }
    }

    private void RestoreNops(bool keepLegalOptions)
    {
        foreach (var key in _nopManager.InstalledNopKeys)
        {
            if (keepLegalOptions && LegalNopKeys.Contains(key)) continue;
            _nopManager.RestoreNop(key);
        }
    }

    private void UninstallHooks(bool keepLegalOptions)
    {
        foreach (var key in _hookManager.InstalledHookKeys)
        {
            if (keepLegalOptions && LegalHookKeys.Contains(key)) continue;
            _hookManager.UninstallHook(key);
        }
    }

    #endregion
}
