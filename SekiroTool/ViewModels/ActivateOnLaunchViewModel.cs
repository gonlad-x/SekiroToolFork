using System.Threading.Tasks;
using SekiroTool.Enums;
using SekiroTool.Interfaces;
using SekiroTool.Utilities;

namespace SekiroTool.ViewModels;

public class ActivateOnLaunchViewModel : BaseViewModel
{
    private readonly PlayerViewModel _playerViewModel;
    private readonly TargetViewModel _targetViewModel;
    private readonly EventViewModel _eventViewModel;
    private readonly TravelViewModel _travelViewModel;
    private readonly EnemyViewModel _enemyViewModel;
    private readonly UtilityViewModel _utilityViewModel;
    private readonly ActivateOnLaunchManager _aol;

    public ActivateOnLaunchViewModel(PlayerViewModel playerViewModel, TargetViewModel targetViewModel,
        EventViewModel eventViewModel, TravelViewModel travelViewModel, EnemyViewModel enemyViewModel,
        UtilityViewModel utilityViewModel, ActivateOnLaunchManager activateOnLaunchManager,
        IStateService stateService)
    {
        _playerViewModel = playerViewModel;
        _targetViewModel = targetViewModel;
        _eventViewModel = eventViewModel;
        _travelViewModel = travelViewModel;
        _enemyViewModel = enemyViewModel;
        _utilityViewModel = utilityViewModel;
        _aol = activateOnLaunchManager;

        RegisterActions();

        stateService.Subscribe(State.AppStart, OnAppStart);
        stateService.Subscribe(State.Attached, OnGameAttached);
        stateService.Subscribe(State.Loaded, OnGameLoaded);
        stateService.Subscribe(State.GameStart, OnNewGameStart);
    }

    #region Properties

    // Master toggle
    private bool _isEnabled = SettingsManager.Default.ActivateOnLaunchEnabled;

    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            if (!SetProperty(ref _isEnabled, value)) return;
            SettingsManager.Default.ActivateOnLaunchEnabled = value;
            SettingsManager.Default.Save();
        }
    }

    // Helper macros
    private bool Get(string id) => _aol.GetBool(id);
    private void Set(string id, bool value) => _aol.SetBool(id, value);

    // Player
    private bool _isInfiniteConfettiChecked;

    public bool IsInfiniteConfettiChecked
    {
        get => _isInfiniteConfettiChecked;
        set
        {
            if (SetProperty(ref _isInfiniteConfettiChecked, value)) Set(nameof(IsInfiniteConfettiChecked), value);
        }
    }

    private bool _isInfiniteGachiinChecked;

    public bool IsInfiniteGachiinChecked
    {
        get => _isInfiniteGachiinChecked;
        set
        {
            if (SetProperty(ref _isInfiniteGachiinChecked, value)) Set(nameof(IsInfiniteGachiinChecked), value);
        }
    }

    private bool _isOneShotHealthChecked;

    public bool IsOneShotHealthChecked
    {
        get => _isOneShotHealthChecked;
        set
        {
            if (SetProperty(ref _isOneShotHealthChecked, value)) Set(nameof(IsOneShotHealthChecked), value);
        }
    }

    private bool _isOneShotPostureChecked;

    public bool IsOneShotPostureChecked
    {
        get => _isOneShotPostureChecked;
        set
        {
            if (SetProperty(ref _isOneShotPostureChecked, value)) Set(nameof(IsOneShotPostureChecked), value);
        }
    }

    private bool _isNoGoodsConsumeChecked;

    public bool IsNoGoodsConsumeChecked
    {
        get => _isNoGoodsConsumeChecked;
        set
        {
            if (SetProperty(ref _isNoGoodsConsumeChecked, value)) Set(nameof(IsNoGoodsConsumeChecked), value);
        }
    }

    private bool _isNoEmblemConsumeChecked;

    public bool IsNoEmblemConsumeChecked
    {
        get => _isNoEmblemConsumeChecked;
        set
        {
            if (SetProperty(ref _isNoEmblemConsumeChecked, value)) Set(nameof(IsNoEmblemConsumeChecked), value);
        }
    }

    private bool _isInfiniteRevivalChecked;

    public bool IsInfiniteRevivalChecked
    {
        get => _isInfiniteRevivalChecked;
        set
        {
            if (SetProperty(ref _isInfiniteRevivalChecked, value)) Set(nameof(IsInfiniteRevivalChecked), value);
        }
    }

    private bool _isPlayerHideChecked;

    public bool IsPlayerHideChecked
    {
        get => _isPlayerHideChecked;
        set
        {
            if (SetProperty(ref _isPlayerHideChecked, value)) Set(nameof(IsPlayerHideChecked), value);
        }
    }

    private bool _isPlayerSilentChecked;

    public bool IsPlayerSilentChecked
    {
        get => _isPlayerSilentChecked;
        set
        {
            if (SetProperty(ref _isPlayerSilentChecked, value)) Set(nameof(IsPlayerSilentChecked), value);
        }
    }

    private bool _isInfinitePoiseChecked;

    public bool IsInfinitePoiseChecked
    {
        get => _isInfinitePoiseChecked;
        set
        {
            if (SetProperty(ref _isInfinitePoiseChecked, value)) Set(nameof(IsInfinitePoiseChecked), value);
        }
    }

    private bool _isNoDeathChecked;

    public bool IsNoDeathChecked
    {
        get => _isNoDeathChecked;
        set
        {
            if (SetProperty(ref _isNoDeathChecked, value)) Set(nameof(IsNoDeathChecked), value);
        }
    }

    private bool _isNoDeathExKillboxChecked;

    public bool IsNoDeathExKillboxChecked
    {
        get => _isNoDeathExKillboxChecked;
        set
        {
            if (SetProperty(ref _isNoDeathExKillboxChecked, value)) Set(nameof(IsNoDeathExKillboxChecked), value);
        }
    }

    private bool _isNoDamageChecked;

    public bool IsNoDamageChecked
    {
        get => _isNoDamageChecked;
        set
        {
            if (SetProperty(ref _isNoDamageChecked, value)) Set(nameof(IsNoDamageChecked), value);
        }
    }

    private bool _isToggleDamageMultiplierChecked;

    public bool IsToggleDamageMultiplierChecked
    {
        get => _isToggleDamageMultiplierChecked;
        set
        {
            if (SetProperty(ref _isToggleDamageMultiplierChecked, value))
                Set(nameof(IsToggleDamageMultiplierChecked), value);
        }
    }

    private double _damageMultiplierValue;

    public double DamageMultiplierValue
    {
        get => _damageMultiplierValue;
        set
        {
            if (SetProperty(ref _damageMultiplierValue, value))
                _aol.SetDouble(nameof(DamageMultiplierValue), value);
        }
    }

    // Target
    private bool _isEnableTargetOptionsChecked;

    public bool IsEnableTargetOptionsChecked
    {
        get => _isEnableTargetOptionsChecked;
        set
        {
            if (SetProperty(ref _isEnableTargetOptionsChecked, value))
                Set(nameof(IsEnableTargetOptionsChecked), value);
        }
    }

    private bool _isTargetOverlayChecked;

    public bool IsTargetOverlayChecked
    {
        get => _isTargetOverlayChecked;
        set
        {
            if (SetProperty(ref _isTargetOverlayChecked, value)) Set(nameof(IsTargetOverlayChecked), value);
        }
    }

    // Enemies
    private bool _isNoButterflySummonsChecked;

    public bool IsNoButterflySummonsChecked
    {
        get => _isNoButterflySummonsChecked;
        set
        {
            if (SetProperty(ref _isNoButterflySummonsChecked, value)) Set(nameof(IsNoButterflySummonsChecked), value);
        }
    }

    private bool _isSnakeIntroLoopChecked;

    public bool IsSnakeIntroLoopChecked
    {
        get => _isSnakeIntroLoopChecked;
        set
        {
            if (SetProperty(ref _isSnakeIntroLoopChecked, value)) Set(nameof(IsSnakeIntroLoopChecked), value);
        }
    }

    // Utility
    private bool _isBorderlessChecked;

    public bool IsBorderlessChecked
    {
        get => _isBorderlessChecked;
        set
        {
            if (SetProperty(ref _isBorderlessChecked, value)) Set(nameof(IsBorderlessChecked), value);
        }
    }

    // Travel
    private bool _isUnlockIdolsChecked;

    public bool IsUnlockIdolsChecked
    {
        get => _isUnlockIdolsChecked;
        set
        {
            if (SetProperty(ref _isUnlockIdolsChecked, value)) Set(nameof(IsUnlockIdolsChecked), value);
        }
    }

    // New Game Cycle
    private bool _isSetMaxHpChecked;

    public bool IsSetMaxHpChecked
    {
        get => _isSetMaxHpChecked;
        set
        {
            if (SetProperty(ref _isSetMaxHpChecked, value)) Set(nameof(IsSetMaxHpChecked), value);
        }
    }

    private bool _isAutoSetNewGame7Checked;

    public bool IsAutoSetNewGame7Checked
    {
        get => _isAutoSetNewGame7Checked;
        set
        {
            if (SetProperty(ref _isAutoSetNewGame7Checked, value)) Set(nameof(IsAutoSetNewGame7Checked), value);
        }
    }

    private bool _isDemonBellOnChecked;

    public bool IsDemonBellOnChecked
    {
        get => _isDemonBellOnChecked;
        set
        {
            if (SetProperty(ref _isDemonBellOnChecked, value)) Set(nameof(IsDemonBellOnChecked), value);
        }
    }

    private bool _isDemonBellOffChecked;

    public bool IsDemonBellOffChecked
    {
        get => _isDemonBellOffChecked;
        set
        {
            if (SetProperty(ref _isDemonBellOffChecked, value)) Set(nameof(IsDemonBellOffChecked), value);
        }
    }

    private bool _isNoKurosCharmOnChecked;

    public bool IsNoKurosCharmOnChecked
    {
        get => _isNoKurosCharmOnChecked;
        set
        {
            if (SetProperty(ref _isNoKurosCharmOnChecked, value)) Set(nameof(IsNoKurosCharmOnChecked), value);
        }
    }

    private bool _isNoKurosCharmOffChecked;

    public bool IsNoKurosCharmOffChecked
    {
        get => _isNoKurosCharmOffChecked;
        set
        {
            if (SetProperty(ref _isNoKurosCharmOffChecked, value)) Set(nameof(IsNoKurosCharmOffChecked), value);
        }
    }

    #endregion

    private void RegisterActions()
    {
        _isInfiniteConfettiChecked = Get(nameof(IsInfiniteConfettiChecked));
        _isInfiniteGachiinChecked = Get(nameof(IsInfiniteGachiinChecked));
        _isOneShotHealthChecked = Get(nameof(IsOneShotHealthChecked));
        _isOneShotPostureChecked = Get(nameof(IsOneShotPostureChecked));
        _isNoGoodsConsumeChecked = Get(nameof(IsNoGoodsConsumeChecked));
        _isNoEmblemConsumeChecked = Get(nameof(IsNoEmblemConsumeChecked));
        _isInfiniteRevivalChecked = Get(nameof(IsInfiniteRevivalChecked));
        _isPlayerHideChecked = Get(nameof(IsPlayerHideChecked));
        _isPlayerSilentChecked = Get(nameof(IsPlayerSilentChecked));
        _isInfinitePoiseChecked = Get(nameof(IsInfinitePoiseChecked));
        _isNoDeathChecked = Get(nameof(IsNoDeathChecked));
        _isNoDeathExKillboxChecked = Get(nameof(IsNoDeathExKillboxChecked));
        _isNoDamageChecked = Get(nameof(IsNoDamageChecked));
        _isToggleDamageMultiplierChecked = Get(nameof(IsToggleDamageMultiplierChecked));
        _damageMultiplierValue = _aol.GetDouble(nameof(DamageMultiplierValue), defaultValue: 1.0);

        _isEnableTargetOptionsChecked = Get(nameof(IsEnableTargetOptionsChecked));
        _isTargetOverlayChecked = Get(nameof(IsTargetOverlayChecked));

        _isNoButterflySummonsChecked = Get(nameof(IsNoButterflySummonsChecked));
        _isSnakeIntroLoopChecked = Get(nameof(IsSnakeIntroLoopChecked));

        _isBorderlessChecked = Get(nameof(IsBorderlessChecked));

        _isUnlockIdolsChecked = Get(nameof(IsUnlockIdolsChecked));

        _isSetMaxHpChecked = Get(nameof(IsSetMaxHpChecked));
        _isAutoSetNewGame7Checked = Get(nameof(IsAutoSetNewGame7Checked));
        _isDemonBellOnChecked = Get(nameof(IsDemonBellOnChecked));
        _isDemonBellOffChecked = Get(nameof(IsDemonBellOffChecked));
        _isNoKurosCharmOnChecked = Get(nameof(IsNoKurosCharmOnChecked));
        _isNoKurosCharmOffChecked = Get(nameof(IsNoKurosCharmOffChecked));
    }

    private void OnAppStart()
    {
        if (!IsEnabled) return;

        // Player
        if (IsInfiniteConfettiChecked) _playerViewModel.IsConfettiFlagEnabled = true;
        if (IsInfiniteGachiinChecked) _playerViewModel.IsGachiinFlagEnabled = true;
        if (IsOneShotHealthChecked) _playerViewModel.IsOneShotEnabled = true;
        if (IsOneShotPostureChecked) _playerViewModel.IsOneShotPostureEnabled = true;
        if (IsNoGoodsConsumeChecked) _playerViewModel.IsNoGoodsConsumeEnabled = true;
        if (IsNoEmblemConsumeChecked) _playerViewModel.IsNoEmblemConsumeEnabled = true;
        if (IsInfiniteRevivalChecked) _playerViewModel.IsNoRevivalConsumeEnabled = true;
        if (IsPlayerHideChecked) _playerViewModel.IsPlayerHideEnabled = true;
        if (IsPlayerSilentChecked) _playerViewModel.IsPlayerSilentEnabled = true;
        if (IsInfinitePoiseChecked) _playerViewModel.IsInfinitePoiseEnabled = true;
        if (IsNoDeathChecked) _playerViewModel.IsNoDeathEnabled = true;
        if (IsNoDeathExKillboxChecked) _playerViewModel.IsNoDeathEnabledWithoutKillbox = true;
        if (IsNoDamageChecked) _playerViewModel.IsNoDamageEnabled = true;
        if (IsToggleDamageMultiplierChecked)
        {
            _playerViewModel.DamageMultiplier = DamageMultiplierValue;
            _playerViewModel.IsDamageMultiplierEnabled = true;
        }

    }

    private void OnGameAttached()
    {
        if (!IsEnabled) return;

        // Borderless belongs here rather than OnGameLoaded: it only needs the game's window to exist, not a
        // loaded character, so it applies as soon as the tool detects the game. UtilityViewModel subscribes to
        // State.Attached too and this runs after it, so its IsGameAttached gate is already set.
        if (IsBorderlessChecked) _utilityViewModel.IsBorderlessEnabled = true;
    }

    private void OnGameLoaded()
    {
        if (!IsEnabled) return;

        // Target
        if (IsEnableTargetOptionsChecked) _targetViewModel.IsTargetOptionsEnabled = true;

        // Ported from master's Startup options, kept on State.Loaded exactly as they were gated there.
        // Not primed at AppStart: the overlay would open empty before a game is ever attached, and the
        // enemy toggles reach EnemyService's hook/shellcode paths, which need resolved offsets.
        if (IsTargetOverlayChecked) _targetViewModel.IsOverlayOpen = true;
        if (IsNoButterflySummonsChecked) _enemyViewModel.IsNoButterflySummonsEnabled = true;
        if (IsSnakeIntroLoopChecked) _enemyViewModel.IsSnakeIntroLoopEnabled = true;
    }

    private async void OnNewGameStart()
    {
        if (!IsEnabled) return;

        // Both ported from master's Startup options, which registered these as new-game actions.
        // NG+7 no longer needs AppStart priming the way the upstream Activate On Launch branch did:
        // master invokes the work directly here instead of setting a flag that PlayerViewModel's own
        // GameStart handler reads, so there is no subscribe-order race left to work around.
        if (IsSetMaxHpChecked) _playerViewModel.SetMaxHpOnNewGame();
        if (IsAutoSetNewGame7Checked) _playerViewModel.SetNewGame7OnNewGame();

        if (IsDemonBellOnChecked) _eventViewModel.SetDemonBellCommand.Execute(true);
        if (IsDemonBellOffChecked) _eventViewModel.SetDemonBellCommand.Execute(false);
        if (IsNoKurosCharmOnChecked) _eventViewModel.SetNoKurosCharmCommand.Execute(true);
        if (IsNoKurosCharmOffChecked) _eventViewModel.SetNoKurosCharmCommand.Execute(false);

        // Travel
        if (IsUnlockIdolsChecked && _travelViewModel.UnlockIdolsCommand.CanExecute(null))
        {
            await Task.Delay(1500);
            _travelViewModel.UnlockIdolsCommand.Execute(null);
        }
    }
}
