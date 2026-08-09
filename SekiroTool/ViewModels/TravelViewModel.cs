using System.Collections.ObjectModel;
using System.Windows.Input;
using SekiroTool.Core;
using SekiroTool.Enums;
using SekiroTool.Interfaces;
using SekiroTool.Models;
using SekiroTool.Utilities;
using SekiroTool.Views.Windows;

namespace SekiroTool.ViewModels;

public class TravelViewModel : BaseViewModel
{
    private readonly ITravelService _travelService;
    private readonly HotkeyManager _hotkeyManager;
    private readonly IEventService _eventService;
    private readonly IPlayerService _playerService;

    public TravelViewModel(ITravelService travelService, IStateService stateService,
        HotkeyManager hotkeyManager, IEventService eventService, IPlayerService playerService)
    {
        _travelService = travelService;
        _hotkeyManager = hotkeyManager;
        _eventService = eventService;
        _playerService = playerService;
        
        RegisterHotkeys();

        stateService.Subscribe(State.Loaded, OnGameLoaded);
        stateService.Subscribe(State.NotLoaded, OnGameNotLoaded);
        
        _mainAreas = new ObservableCollection<string>();
        _warpLocations = new ObservableCollection<Warp>();
        _customMainAreas = new ObservableCollection<string>();
        _customWarpLocations = new ObservableCollection<CustomWarp>();
        
        WarpCommand = new DelegateCommand(Warp);
        UnlockIdolsCommand = new DelegateCommand(UnlockIdols);
        CustomWarpCommand = new DelegateCommand(CustomWarp);
        OpenCreateCustomWarpCommand = new DelegateCommand(OpenCreateCustomWarp);
        
        LoadWarps();
        LoadCustomWarps();
        

        idolEventIds = DataLoader.GetIdolEventIds();
    }
    
    #region Private Fields

    private Dictionary<string, List<Warp>> _warpDict;
    private List<Warp> _allWarps;
    private string _preSearchMainArea;
    private readonly ObservableCollection<Warp> _searchResultsCollection = new ObservableCollection<Warp>();
    private List<long> idolEventIds;

    private Dictionary<string, List<CustomWarp>> _customWarpDict;
    private string _preSearchCustomMainArea;
    #endregion


    #region Commands

    public ICommand WarpCommand { get; set; }
    public ICommand UnlockIdolsCommand { get; set; }
    public ICommand CustomWarpCommand { get; set; }
    public ICommand OpenCreateCustomWarpCommand { get; set; }

    #endregion

    #region Properties

    private bool _areOptionsEnabled;
    public bool AreOptionsEnabled
    {
        get => _areOptionsEnabled;
        set => SetProperty(ref _areOptionsEnabled, value);
    }

    private ObservableCollection<string> _mainAreas;
    public ObservableCollection<string> MainAreas
    {
        get => _mainAreas;
        private set => SetProperty(ref _mainAreas, value);
    }

    private string _selectedMainArea;
    public string SelectedMainArea
    {
        get => _selectedMainArea;
        set
        {
            if (!SetProperty(ref _selectedMainArea, value)) return;

            if (_isSearchActive)
            {
                IsSearchActive = false;
                _searchText = string.Empty;
                OnPropertyChanged(nameof(SearchText));
                _preSearchMainArea = null;
            }

            UpdateLocationsList();
        }
    }

    private ObservableCollection<Warp> _warpLocations;
    public ObservableCollection<Warp> WarpLocations
    {
        get => _warpLocations;
        set => SetProperty(ref _warpLocations, value);
    }

    private Warp _selectedWarpLocation;
    public Warp SelectedWarpLocation
    {
        get => _selectedWarpLocation;
        set => SetProperty(ref _selectedWarpLocation, value);
    }

    private bool _isSearchActive;
    public bool IsSearchActive
    {
        get => _isSearchActive;
        private set => SetProperty(ref _isSearchActive, value);
    }

    private string _searchText = string.Empty;
    public string SearchText
    {
        get => _searchText;
        set
        {
            if (!SetProperty(ref _searchText, value)) return;

            if (string.IsNullOrEmpty(value))
            {
                _isSearchActive = false;

                if (_preSearchMainArea != null)
                {
                    _selectedMainArea = _preSearchMainArea;
                    UpdateLocationsList();
                    _preSearchMainArea = null;
                }
            }
            else
            {
                if (!_isSearchActive)
                {
                    _preSearchMainArea = SelectedMainArea;
                    _isSearchActive = true;
                }

                ApplyFilter();
            }
        }
    }

    private ObservableCollection<string> _customMainAreas;
    public ObservableCollection<string> CustomMainAreas
    {
        get => _customMainAreas;
        private set => SetProperty(ref _customMainAreas, value);
    }

    private ObservableCollection<CustomWarp> _customWarpLocations;
    public ObservableCollection<CustomWarp> CustomWarpLocations
    {
        get => _customWarpLocations;
        set => SetProperty(ref _customWarpLocations, value);
    }

    private string _selectedCustomMainArea;
    public string SelectedCustomMainArea
    {
        get => _selectedCustomMainArea;
        set
        {
            if (!SetProperty(ref _selectedCustomMainArea, value) || value == null) return;

            if (_isCustomSearchActive)
            {
                IsCustomSearchActive = false;
                _customSearchText = string.Empty;
                OnPropertyChanged(nameof(CustomSearchText));
                _preSearchCustomMainArea = null;
            }

            UpdateCustomLocationsList();
        }
    }

    private CustomWarp _selectedCustomWarp;
    public CustomWarp SelectedCustomWarp
    {
        get => _selectedCustomWarp;
        set => SetProperty(ref _selectedCustomWarp, value);
    }

    private bool _isCustomSearchActive;
    public bool IsCustomSearchActive
    {
        get => _isCustomSearchActive;
        private set => SetProperty(ref _isCustomSearchActive, value);
    }

    private string _customSearchText = string.Empty;
    public string CustomSearchText
    {
        get => _customSearchText;
        set
        {
            if (!SetProperty(ref _customSearchText, value)) return;

            if (string.IsNullOrEmpty(value))
            {
                _isCustomSearchActive = false;

                if (_preSearchCustomMainArea != null)
                {
                    _selectedCustomMainArea = _preSearchCustomMainArea;
                    OnPropertyChanged(nameof(SelectedCustomMainArea));
                    UpdateCustomLocationsList();
                    _preSearchCustomMainArea = null;
                }
            }
            else
            {
                if (!_isCustomSearchActive)
                {
                    _preSearchCustomMainArea = SelectedCustomMainArea;
                    _isCustomSearchActive = true;
                }

                ApplyCustomFilter();
            }
        }
    }

    #endregion

    #region Private Methods

    private void RegisterHotkeys()
    {
        _hotkeyManager.RegisterAction(HotkeyActions.WarpOgreOutskirts, () => WarpToBoss("Ogre (Outskirts)"));
        _hotkeyManager.RegisterAction(HotkeyActions.WarpGyoubu, () => WarpToBoss("Gyoubu"));
        _hotkeyManager.RegisterAction(HotkeyActions.WarpBlazingBull, () => WarpToBoss("Bull"));
        _hotkeyManager.RegisterAction(HotkeyActions.WarpGenichiroCastle, () => WarpToBoss("Geni (Castle)"));
        _hotkeyManager.RegisterAction(HotkeyActions.WarpArmoredWarrior, () => WarpToBoss("Armored Warrior"));
        _hotkeyManager.RegisterAction(HotkeyActions.WarpCentipedeGunFort, () => WarpToBoss("Centipede (Gun Fort)"));
        _hotkeyManager.RegisterAction(HotkeyActions.WarpSnakeEyesPoisonPool, () => WarpToBoss("Shirahagi (Poison Pool)"));
        _hotkeyManager.RegisterAction(HotkeyActions.WarpGuardianApe, () => WarpToBoss("Guardian Ape"));
        _hotkeyManager.RegisterAction(HotkeyActions.WarpMistNoble, () => WarpToBoss("Mist Noble"));
        _hotkeyManager.RegisterAction(HotkeyActions.WarpFakeMonk, () => WarpToBoss("Fake Monk"));
        _hotkeyManager.RegisterAction(HotkeyActions.WarpMonkeys, () => WarpToBoss("Monkeys"));
        _hotkeyManager.RegisterAction(HotkeyActions.WarpEmmaIsshin, () => WarpToBoss("Emma & Isshin"));
    }

    // Looks up a warp by name within the built-in "Boss" area (see the Warps CSV resource) and warps
    // to its exact captured coordinates -- not an idol. Used by the fixed Travel hotkey category below.
    private void WarpToBoss(string name)
    {
        var warp = _allWarps.FirstOrDefault(w => w.MainArea == "Boss" && w.Name == name);
        if (warp == null) return;
        _ = Task.Run(() => _travelService.Warp(warp));
    }

    private void OnGameLoaded()
    {
        AreOptionsEnabled = true;
    }

    private void OnGameNotLoaded()
    {
        AreOptionsEnabled = false;
    }

    private void LoadWarps()
    {
        _warpDict = DataLoader.GetWarpLocations();
        _allWarps = _warpDict.Values.SelectMany(x => x).ToList();

        foreach (var area in _warpDict.Keys)
        {
            
            _mainAreas.Add(area);
        }
        
        SelectedMainArea = _mainAreas.FirstOrDefault();
    }

    private void UpdateLocationsList()
    {
        if (string.IsNullOrEmpty(SelectedMainArea) || !_warpDict.ContainsKey(SelectedMainArea))
        {
            WarpLocations = new ObservableCollection<Warp>();
            return;
        }

        WarpLocations = new ObservableCollection<Warp>(_warpDict[SelectedMainArea]);
        SelectedWarpLocation = WarpLocations.FirstOrDefault();
    }

    private void ApplyFilter()
    {
        _searchResultsCollection.Clear();
        var searchTextLower = SearchText.ToLower();

        foreach (var location in _allWarps)
        {
            if (location.Name.ToLower().Contains(searchTextLower) ||
                location.MainArea.ToLower().Contains(searchTextLower))
            {
                _searchResultsCollection.Add(location);
            }
        }

        WarpLocations = new ObservableCollection<Warp>(_searchResultsCollection);
        SelectedWarpLocation = WarpLocations.FirstOrDefault();
    }

    private void Warp() =>  _ = Task.Run(() => _travelService.Warp(SelectedWarpLocation));
    private void UnlockIdols() => idolEventIds.ForEach(id => _eventService.SetEvent(id, true));

    private void LoadCustomWarps()
    {
        _customWarpDict = DataLoader.LoadCustomWarps();
        RebuildCustomMainAreas();
    }

    private void RebuildCustomMainAreas()
    {
        var previousSelection = _selectedCustomMainArea;
        _customMainAreas.Clear();
        foreach (var area in _customWarpDict.Keys)
        {
            _customMainAreas.Add(area);
        }

        if (previousSelection != null && _customWarpDict.ContainsKey(previousSelection))
        {
            SelectedCustomMainArea = previousSelection;
        }
        else
        {
            SelectedCustomMainArea = _customMainAreas.FirstOrDefault();
            if (SelectedCustomMainArea == null) UpdateCustomLocationsList();
        }
    }

    private void UpdateCustomLocationsList()
    {
        if (string.IsNullOrEmpty(SelectedCustomMainArea) || !_customWarpDict.ContainsKey(SelectedCustomMainArea))
        {
            CustomWarpLocations = new ObservableCollection<CustomWarp>();
            SelectedCustomWarp = null;
            return;
        }

        CustomWarpLocations = new ObservableCollection<CustomWarp>(_customWarpDict[SelectedCustomMainArea]);
        SelectedCustomWarp = CustomWarpLocations.FirstOrDefault();
    }

    private void ApplyCustomFilter()
    {
        var searchTextLower = CustomSearchText.ToLower();
        var matches = _customWarpDict
            .SelectMany(kv => kv.Value)
            .Where(w => (w.Name != null && w.Name.ToLower().Contains(searchTextLower)) ||
                        (w.MainArea != null && w.MainArea.ToLower().Contains(searchTextLower)));

        CustomWarpLocations = new ObservableCollection<CustomWarp>(matches);
        SelectedCustomWarp = CustomWarpLocations.FirstOrDefault();
    }

    private void CustomWarp()
    {
        if (SelectedCustomWarp == null) return;

        var warp = SelectedCustomWarp;
        _ = Task.Run(() => _travelService.WarpWithCoords(warp.Coords, warp.Angle, warp.IdolId));
    }

    private void OpenCreateCustomWarp()
    {
        var window = new CreateCustomWarpWindow(
            _customWarpDict,
            AreOptionsEnabled,
            _playerService,
            _travelService,
            OnCustomWarpChanged);
        window.ShowDialog();
    }

    private void OnCustomWarpChanged(CustomWarpChange change)
    {
        switch (change)
        {
            case WarpAdded added:
                if (!_customWarpDict.TryGetValue(added.Warp.MainArea, out var addList))
                {
                    addList = new List<CustomWarp>();
                    _customWarpDict[added.Warp.MainArea] = addList;
                }
                if (!addList.Contains(added.Warp)) addList.Add(added.Warp);
                break;

            case WarpDeleted deleted:
                if (_customWarpDict.TryGetValue(deleted.Category, out var delList))
                {
                    delList.Remove(deleted.Warp);
                    if (delList.Count == 0) _customWarpDict.Remove(deleted.Category);
                }
                break;

            case CategoryDeleted catDeleted:
                _customWarpDict.Remove(catDeleted.Category);
                break;
        }

        RebuildCustomMainAreas();
        DataLoader.SaveCustomWarps(_customWarpDict);
    }

    #endregion
}