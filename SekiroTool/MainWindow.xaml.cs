using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using SekiroTool.Enums;
using SekiroTool.Interfaces;
using SekiroTool.Memory;
using SekiroTool.Services;
using SekiroTool.Utilities;
using SekiroTool.ViewModels;
using SekiroTool.Views.Tabs;
using static SekiroTool.Memory.Offsets;

namespace SekiroTool;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly MemoryService _memoryService;
    private readonly IStateService _stateService;
    private readonly IPlayerService _playerService;
    private readonly IRunModeService _runModeService;

    /// <summary>
    /// Tabs locked while Live Run Mode is active. Settings stays open (all of its options are legal
    /// during a run) and so does Saves, which only touches the filesystem.
    /// </summary>
    private static readonly string[] RunModeLockedTabs =
        ["Player", "Travel", "Enemies", "Target", "Utility", "Items", "Event"];

    private readonly AoBScanner _aobScanner;
    private readonly HotkeyManager _hotkeyManager;
    private readonly NopManager _nopManager;

    private readonly DispatcherTimer _gameLoadedTimer;

    public MainWindow()
    {
        _memoryService = new MemoryService();

        InitializeComponent();

        if (SettingsManager.Default.WindowLeft != 0 || SettingsManager.Default.WindowTop != 0)
        {
            Left = SettingsManager.Default.WindowLeft;
            Top = SettingsManager.Default.WindowTop;
        }
        else WindowStartupLocation = WindowStartupLocation.CenterScreen;

        _aobScanner = new AoBScanner(_memoryService);
        _stateService = new StateService(_memoryService);

        var hookManager = new HookManager(_memoryService, _stateService);

        _nopManager = new NopManager(_memoryService, _stateService);
        _hotkeyManager = new HotkeyManager(_memoryService);

        ITravelService travelService = new TravelService(_memoryService, hookManager);
        _playerService = new PlayerService(_memoryService, hookManager, travelService);
        IReminderService reminderService = new ReminderService(_memoryService);
        IEnemyService enemyService = new EnemyService(_memoryService, hookManager, reminderService);
        ITargetService targetService = new TargetService(_memoryService, hookManager, reminderService);
        IDebugDrawService debugDrawService = new DebugDrawService(_memoryService, _stateService, _nopManager);
        IEventService eventService = new EventService(_memoryService);
        IUtilityService utilityService = new UtilityService(_memoryService, hookManager);
        IItemService itemService = new ItemService(_memoryService);
        ISettingsService settingsService = new SettingsService(_memoryService, _nopManager, hookManager);
        IEzStateService ezStateService = new EzStateService(_memoryService);
        IChrInsService chrInsService = new ChrInsService(_memoryService);
        ISaveManagerService saveManagerService = new SaveManagerService();
        IWindowService windowService = new WindowService(_memoryService);


        PlayerViewModel playerViewModel = new PlayerViewModel(_playerService, _hotkeyManager, _stateService);
        TravelViewModel travelViewModel =
            new TravelViewModel(travelService, _stateService, _hotkeyManager, eventService, _playerService);
        EnemyViewModel enemyViewModel = new EnemyViewModel(enemyService, _hotkeyManager, _stateService,
            debugDrawService, eventService, chrInsService);
        TargetViewModel targetViewModel =
            new TargetViewModel(_stateService, _hotkeyManager, targetService, debugDrawService, _playerService);
        UtilityViewModel utilityViewModel =
            new UtilityViewModel(utilityService, _stateService, _hotkeyManager, debugDrawService, playerViewModel,
                ezStateService, windowService);
        ItemViewModel itemViewModel = new ItemViewModel(itemService, _stateService);
        EventViewModel eventViewModel =
            new EventViewModel(eventService, _stateService, debugDrawService, itemService, _hotkeyManager);
        var activateOnLaunchManager = new ActivateOnLaunchManager();
        ActivateOnLaunchViewModel activateOnLaunchViewModel = new ActivateOnLaunchViewModel(playerViewModel,
            targetViewModel, eventViewModel, travelViewModel, enemyViewModel, utilityViewModel,
            activateOnLaunchManager, _stateService);
        SaveManagerViewModel saveManagerViewModel =
            new SaveManagerViewModel(saveManagerService, _stateService, _hotkeyManager);
        SettingsViewModel settingsViewModel = new SettingsViewModel(settingsService, _stateService, _hotkeyManager,
            activateOnLaunchViewModel, saveManagerViewModel);

        // Built after every ViewModel on purpose: StateService.Publish iterates subscribers in
        // subscription order, so its Loaded handler runs after the ViewModels re-apply their
        // options and can undo them. Activate On Launch is reached through delegates so the
        // service stays free of ViewModel references.
        _runModeService = new RunModeService(_memoryService, hookManager, _nopManager, _playerService,
            utilityService, targetService, reminderService, _hotkeyManager, _stateService,
            () => activateOnLaunchViewModel.IsEnabled,
            isEnabled => activateOnLaunchViewModel.IsEnabled = isEnabled,
            activateOnLaunchManager.GetBool);
        _runModeService.StateChanged += ApplyRunModeToUi;

        var playerTab = new PlayerTab(playerViewModel);
        var travelTab = new TravelTab(travelViewModel);
        var enemyTab = new EnemyTab(enemyViewModel);
        var targetTab = new TargetTab(targetViewModel);
        var utilityTab = new UtilityTab(utilityViewModel);
        var itemTab = new ItemTab(itemViewModel);
        var eventTab = new EventTab(eventViewModel);
        var saveManagerTab = new SaveManagerTab(saveManagerViewModel);
        var settingsTab = new SettingsTab(settingsViewModel);

        MainTabControl.Items.Add(new TabItem { Header = "Player", Content = playerTab });
        MainTabControl.Items.Add(new TabItem { Header = "Travel", Content = travelTab });
        MainTabControl.Items.Add(new TabItem { Header = "Enemies", Content = enemyTab });
        MainTabControl.Items.Add(new TabItem { Header = "Target", Content = targetTab });
        MainTabControl.Items.Add(new TabItem { Header = "Utility", Content = utilityTab });
        MainTabControl.Items.Add(new TabItem { Header = "Items", Content = itemTab });
        MainTabControl.Items.Add(new TabItem { Header = "Event", Content = eventTab });
        MainTabControl.Items.Add(new TabItem { Header = "Saves", Content = saveManagerTab });
        MainTabControl.Items.Add(new TabItem { Header = "Settings", Content = settingsTab });

        MainTabControl.SelectionChanged += MainTabControl_SelectionChanged;

        settingsViewModel.ApplyStartUpOptions();

        // AppStart must be published before attaching: StateService.Publish is synchronous, so every
        // handler runs while ProcessHandle is still IntPtr.Zero and any write it triggers fails
        // cleanly. Attaching first lets AppStart handlers write to a live process using offsets that
        // PatchChecker/AllocCodeCave have not resolved yet.
        _stateService.Publish(State.AppStart);
        _memoryService.StartAutoAttach();

        Closing += MainWindow_Closing;

        _gameLoadedTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(25)
        };
        _gameLoadedTimer.Tick += Timer_Tick;
        _gameLoadedTimer.Start();

        VersionChecker.UpdateVersionText(AppVersion);
        if (SettingsManager.Default.EnableUpdateChecks)
        {
            VersionChecker.CheckForUpdates(this);
        }
    }

    private bool _loaded;

    private bool _hasAllocatedMemory;
    private DateTime? _attachedTime;
    private bool _hasPublishedAttached;
    private bool _hasCheckedPatch;

    private void Timer_Tick(object sender, EventArgs e)
    {
        if (_memoryService.IsAttached)
        {
            IsAttachedText.Text = "Attached to game";
            IsAttachedText.Foreground = (SolidColorBrush)Application.Current.Resources["AttachedBrush"];
            LaunchGameButton.IsEnabled = false;

            if (!_attachedTime.HasValue)
            {
                _attachedTime = DateTime.Now;
                return;
            }

            if ((DateTime.Now - _attachedTime.Value).TotalSeconds < 2)
                return;

            if (!_hasCheckedPatch)
            {
                if (!PatchChecker.Initialize(_memoryService))
                {
                    _aobScanner.DoEarlyScan();
                    _stateService.Publish(State.EarlyAttached);
                    _aobScanner.DoMainScan();
                }

#if DEBUG
                Console.WriteLine($@"Base: 0x{(long)_memoryService.BaseAddress:X}");
#endif
                _hasCheckedPatch = true;
            }

            if (!_hasAllocatedMemory)
            {
                _memoryService.AllocCodeCave();
                Console.WriteLine($"Code cave: 0x{CodeCaveOffsets.Base.ToInt64():X}");
                _hasAllocatedMemory = true;
            }

            if (!_hasPublishedAttached)
            {
                _stateService.Publish(State.Attached);
                _hasPublishedAttached = true;
            }

            if (_stateService.IsLoaded())
            {
                if (_loaded) return;
                _loaded = true;
                _stateService.Publish(State.Loaded);
                TrySetGameStartPrefs();
            }
            else if (_loaded)
            {
                _stateService.Publish(State.NotLoaded);
                _loaded = false;
            }
        }
        else
        {
            if (_hasPublishedAttached)
            {
                _stateService.Publish(State.Detached);
                _hasPublishedAttached = false;
            }

            _attachedTime = null;
            _loaded = false;
            _hasAllocatedMemory = false;
            IsAttachedText.Text = "Not attached";
            IsAttachedText.Foreground = (SolidColorBrush)Application.Current.Resources["NotAttachedBrush"];
            LaunchGameButton.IsEnabled = true;
        }
    }

    private void TrySetGameStartPrefs()
    {
        var igt = _memoryService.Read<long>(_memoryService.Read<nint>(GameDataMan.Base) + GameDataMan.IGT);
        if (igt < 5000) _stateService.Publish(State.GameStart);
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            if (WindowState == WindowState.Maximized)
                WindowState = WindowState.Normal;
            else
                WindowState = WindowState.Maximized;
        }
        else
        {
            DragMove();
        }
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    private void MainWindow_Closing(object sender, CancelEventArgs e)
    {
        SettingsManager.Default.WindowLeft = Left;
        SettingsManager.Default.WindowTop = Top;
        SettingsManager.Default.Save();

        if (SettingsManager.Default.BrowserOverlayEnabled) BrowserOverlayExporter.Clear();

        // Without this the game keeps running with every flag set, hook installed and patch applied
        // until the game itself is closed.
        _runModeService.RevertGameChanges(keepLegalOptions: false);
        _memoryService.Dispose();
    }

    private void RunModeButton_Click(object sender, RoutedEventArgs e)
    {
        if (_runModeService.IsActive)
        {
            _runModeService.Stop();
            MsgBox.Show(
                "Live run mode stopped.\n\n" +
                "Options that were on before live run mode are not re-applied - restart the tool " +
                "(with Activate On Launch back on) to get them back.",
                "Live run mode");
            return;
        }

        if (!_memoryService.IsAttached)
        {
            MsgBox.Show("Attach to the game before starting live run mode.", "Live run mode");
            return;
        }

        var activeChanges = _runModeService.DescribeActiveChanges();
        var message = activeChanges.Count > 0
            ? $"{activeChanges.Count} game-modifying change(s) are active:\n\n" +
              string.Join("\n", activeChanges.Select(change => $"  - {change}")) + "\n\n" +
              "Reset them and start live run mode?\n\n" +
              "Start live run mode BEFORE loading the run's save."
            : "Start live run mode?\n\n" +
              "Game-modifying options will be locked and Activate On Launch suppressed.\n\n" +
              "Start live run mode BEFORE loading the run's save.";

        if (!MsgBox.ShowOkCancel(message, "Live run mode")) return;

        if (!_runModeService.TryStart(out var failureReason))
            MsgBox.Show(failureReason, "Live run mode");
    }

    private void ApplyRunModeToUi()
    {
        var isActive = _runModeService.IsActive;

        foreach (var item in MainTabControl.Items)
        {
            if (item is not TabItem tab || !RunModeLockedTabs.Contains(tab.Header?.ToString())) continue;
            tab.IsEnabled = !isActive;
        }

        if (isActive && MainTabControl.SelectedItem is TabItem { IsEnabled: false })
            MainTabControl.SelectedIndex = MainTabControl.Items.Count - 1;

        RunModeButton.Content = isActive ? "Stop run mode" : "Start live run mode";
        RunModeBanner.Visibility = isActive ? Visibility.Visible : Visibility.Collapsed;

        // The banner, both buttons and the attach status do not fit this row together, and the
        // banner is the one that gets clipped. Launch Game is already useless while attached, so it
        // gives up its space.
        LaunchGameButton.Visibility = isActive ? Visibility.Collapsed : Visibility.Visible;
    }

    private void LaunchGame_Click(object sender, RoutedEventArgs e) => Task.Run(GameLauncher.LaunchSekiro);
    private void CheckUpdate_Click(object sender, RoutedEventArgs e) => VersionChecker.CheckForUpdates(this, true);

    private void MainTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.Source is TabControl && MainTabControl.SelectedItem is TabItem selectedTab)
        {
            if (selectedTab.Header.ToString() == "Event")
            {
                _stateService.Publish(State.EventTabActivated);
            }
        }
    }
}