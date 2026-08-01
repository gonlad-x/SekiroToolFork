using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using SekiroTool.Core;
using SekiroTool.Enums;
using SekiroTool.Interfaces;
using SekiroTool.Utilities;

namespace SekiroTool.ViewModels;

/// <summary>
/// Save Manager tab: manages Sekiro savestates in profile folders, replacing the need to run
/// SoulsSpeedruns-Save-Organizer alongside SekiroTool.
/// </summary>
public class SaveManagerViewModel : BaseViewModel
{
    private readonly ISaveManagerService _saveManagerService;

    public SaveManagerViewModel(ISaveManagerService saveManagerService, IStateService stateService,
        HotkeyManager hotkeyManager)
    {
        _saveManagerService = saveManagerService;

        _saveFileLocation = SettingsManager.Default.SaveFileLocation;
        _profilesDirectory = SettingsManager.Default.ProfilesDirectory;

        BrowseSaveFileCommand = new DelegateCommand(BrowseSaveFile);
        BrowseProfilesCommand = new DelegateCommand(BrowseProfilesDirectory);
        NewProfileCommand = new DelegateCommand(NewProfile);
        ImportCommand = new DelegateCommand(ImportSave);
        LoadCommand = new DelegateCommand(LoadSave);
        ReplaceCommand = new DelegateCommand(ReplaceSave);
        ToggleGameFileReadOnlyCommand = new DelegateCommand(ToggleGameFileReadOnly);
        CreateFolderCommand = new DelegateCommand(CreateFolder);
        RenameCommand = new DelegateCommand(RenameSelected);
        DeleteCommand = new DelegateCommand(DeleteSelected);

        RegisterHotkeys(hotkeyManager);

        // Loading a savestate only takes effect from the main menu, so Load is gated on not being in-game.
        stateService.Subscribe(State.Loaded, () => IsGameLoaded = true);
        stateService.Subscribe(State.NotLoaded, () => IsGameLoaded = false);
        stateService.Subscribe(State.Detached, () => IsGameLoaded = false);

        LoadProfiles();
        RefreshGameFileState();
    }

    #region Commands

    public ICommand BrowseSaveFileCommand { get; }
    public ICommand BrowseProfilesCommand { get; }
    public ICommand NewProfileCommand { get; }
    public ICommand ImportCommand { get; }
    public ICommand LoadCommand { get; }
    public ICommand ReplaceCommand { get; }
    public ICommand ToggleGameFileReadOnlyCommand { get; }
    public ICommand CreateFolderCommand { get; }
    public ICommand RenameCommand { get; }
    public ICommand DeleteCommand { get; }

    #endregion

    #region Properties

    private string _saveFileLocation = "";

    public string SaveFileLocation
    {
        get => _saveFileLocation;
        private set
        {
            if (!SetProperty(ref _saveFileLocation, value)) return;
            SettingsManager.Default.SaveFileLocation = value;
            SettingsManager.Default.Save();
            OnConfigurationChanged();
        }
    }

    private string _profilesDirectory = "";

    public string ProfilesDirectory
    {
        get => _profilesDirectory;
        private set
        {
            if (!SetProperty(ref _profilesDirectory, value)) return;
            SettingsManager.Default.ProfilesDirectory = value;
            SettingsManager.Default.Save();
            LoadProfiles();
            OnConfigurationChanged();
        }
    }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(SaveFileLocation) && File.Exists(SaveFileLocation) &&
        !string.IsNullOrWhiteSpace(ProfilesDirectory) && Directory.Exists(ProfilesDirectory);

    public ObservableCollection<string> Profiles { get; } = new();

    private string? _selectedProfile;

    public string? SelectedProfile
    {
        get => _selectedProfile;
        set
        {
            if (!SetProperty(ref _selectedProfile, value)) return;
            SettingsManager.Default.SelectedProfile = value ?? "";
            SettingsManager.Default.Save();
            RebuildTree();
        }
    }

    public ObservableCollection<SaveEntryViewModel> RootItems { get; } = new();

    private SaveEntryViewModel? _selectedEntry;

    public SaveEntryViewModel? SelectedEntry
    {
        get => _selectedEntry;
        set
        {
            if (!SetProperty(ref _selectedEntry, value)) return;
            OnPropertyChanged(nameof(CanLoad));
            OnPropertyChanged(nameof(CanReplace));
            OnPropertyChanged(nameof(HasSelection));
        }
    }

    private bool _isGameLoaded;

    public bool IsGameLoaded
    {
        get => _isGameLoaded;
        private set
        {
            if (!SetProperty(ref _isGameLoaded, value)) return;
            OnPropertyChanged(nameof(CanLoad));
            OnPropertyChanged(nameof(LoadBlockedReason));
        }
    }

    private bool _isGameFileReadOnly;

    public bool IsGameFileReadOnly
    {
        get => _isGameFileReadOnly;
        private set
        {
            if (!SetProperty(ref _isGameFileReadOnly, value)) return;
            OnPropertyChanged(nameof(ReadOnlyButtonText));
        }
    }

    public string ReadOnlyButtonText => IsGameFileReadOnly ? "Read-Only" : "Writable";

    private bool IsSaveSelected => SelectedEntry is { IsFolder: false };

    public bool HasSelection => SelectedEntry != null;
    public bool CanReplace => IsConfigured && IsSaveSelected;
    public bool CanLoad => IsConfigured && IsSaveSelected && !IsGameLoaded;

    /// <summary>Explains a disabled Load button, so a greyed-out button never looks like a bug.</summary>
    public string LoadBlockedReason =>
        IsGameLoaded ? "Quit out to the main menu to load a savestate." : "";

    private string _statusText = "";

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    #endregion

    #region Configuration

    private void BrowseSaveFile()
    {
        var suggested = _saveManagerService.SuggestSaveFileLocation();

        var dialog = new OpenFileDialog
        {
            Title = "Select Sekiro's savefile",
            Filter = $"Sekiro savefile ({_saveManagerService.ExpectedSaveName})|{_saveManagerService.ExpectedSaveName}" +
                     "|Savefiles (*.sl2)|*.sl2|All files (*.*)|*.*",
            FileName = suggested != null ? Path.GetFileName(suggested) : _saveManagerService.ExpectedSaveName,
            InitialDirectory = suggested != null
                ? Path.GetDirectoryName(suggested)
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Sekiro")
        };

        if (dialog.ShowDialog() != true) return;

        var chosen = dialog.FileName;
        if (!Path.GetFileName(chosen).Equals(_saveManagerService.ExpectedSaveName, StringComparison.OrdinalIgnoreCase) &&
            !MsgBox.ShowOkCancel(
                $"That file isn't named {_saveManagerService.ExpectedSaveName}, which is what Sekiro uses.\n\n" +
                "Use it anyway?"))
            return;

        SaveFileLocation = chosen;
        StatusText = "Savefile location set.";
    }

    private void BrowseProfilesDirectory()
    {
        // WPF/net48 has no folder picker, hence WinForms here. Profile folders contain only subfolders, so a
        // file-based picker would have nothing to select.
        using var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "Select the folder your savestate profiles live in",
            ShowNewFolderButton = true,
            SelectedPath = Directory.Exists(ProfilesDirectory) ? ProfilesDirectory : ""
        };

        if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
        if (!Directory.Exists(dialog.SelectedPath)) return;

        ProfilesDirectory = dialog.SelectedPath;
        StatusText = $"Profiles folder set to {dialog.SelectedPath}";
    }

    private void OnConfigurationChanged()
    {
        OnPropertyChanged(nameof(IsConfigured));
        OnPropertyChanged(nameof(CanLoad));
        OnPropertyChanged(nameof(CanReplace));
        RefreshGameFileState();
    }

    private void LoadProfiles()
    {
        var previous = SettingsManager.Default.SelectedProfile;

        Profiles.Clear();
        foreach (var profile in _saveManagerService.GetProfileNames(ProfilesDirectory))
            Profiles.Add(profile);

        _selectedProfile = Profiles.Contains(previous) ? previous : Profiles.FirstOrDefault();
        OnPropertyChanged(nameof(SelectedProfile));
        RebuildTree();
    }

    private void NewProfile()
    {
        if (!Directory.Exists(ProfilesDirectory))
        {
            MsgBox.Show("Set a profiles folder first.");
            return;
        }

        var name = TextInputWindowPrompt("New Profile", "Profile name:");
        if (name == null) return;

        try
        {
            _saveManagerService.CreateFolder(ProfilesDirectory, name);
        }
        catch (Exception ex)
        {
            MsgBox.Show($"Couldn't create the profile:\n\n{ex.Message}");
            return;
        }

        SettingsManager.Default.SelectedProfile = name;
        LoadProfiles();
        StatusText = $"Created profile '{name}'.";
    }

    #endregion

    #region Tree

    private string? CurrentProfilePath =>
        string.IsNullOrEmpty(SelectedProfile) || !Directory.Exists(ProfilesDirectory)
            ? null
            : Path.Combine(ProfilesDirectory, SelectedProfile);

    private void RebuildTree()
    {
        var previousPath = SelectedEntry?.FullPath;
        var expanded = RootItems.SelectMany(r => r.Flatten())
            .Where(e => e.IsFolder && e.IsExpanded)
            .Select(e => e.FullPath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        RootItems.Clear();
        SelectedEntry = null;

        var profilePath = CurrentProfilePath;
        if (profilePath == null || !Directory.Exists(profilePath)) return;

        // The profile folder itself isn't shown - only its contents, matching the organizer.
        var root = SaveEntryViewModel.BuildTree(profilePath);
        foreach (var child in root.Children)
            RootItems.Add(child);

        foreach (var entry in RootItems.SelectMany(r => r.Flatten()).Where(e => e.IsFolder))
            entry.IsExpanded = expanded.Count == 0 || expanded.Contains(entry.FullPath);

        if (previousPath != null) SelectByPath(previousPath);
    }

    private void SelectByPath(string path)
    {
        var match = RootItems.SelectMany(r => r.Flatten())
            .FirstOrDefault(e => string.Equals(e.FullPath, path, StringComparison.OrdinalIgnoreCase));
        if (match == null) return;

        for (var parent = match.Parent; parent != null; parent = parent.Parent)
            parent.IsExpanded = true;

        match.IsSelected = true;
        SelectedEntry = match;
    }

    private List<SaveEntryViewModel> VisibleSaves()
    {
        // Only savestates - highlighting a folder can't be loaded, so stepping onto one would be a dead move.
        return RootItems.SelectMany(r => r.Flatten()).Where(e => !e.IsFolder).ToList();
    }

    private void StepSelection(int offset)
    {
        var saves = VisibleSaves();
        if (saves.Count == 0) return;

        var index = SelectedEntry == null ? -1 : saves.IndexOf(SelectedEntry);
        var next = index < 0
            ? (offset > 0 ? 0 : saves.Count - 1)
            : Math.Min(saves.Count - 1, Math.Max(0, index + offset));

        SelectByPath(saves[next].FullPath);
        StatusText = saves[next].Name;
    }

    #endregion

    #region Operations

    private void ImportSave()
    {
        if (!EnsureConfigured()) return;

        var destination = SelectedEntry?.ContainingFolder ?? CurrentProfilePath;
        if (destination == null)
        {
            MsgBox.Show("Create or select a profile first.");
            return;
        }

        try
        {
            var created = _saveManagerService.ImportSave(SaveFileLocation, destination);
            RebuildTree();
            SelectByPath(created);
            StatusText = $"Imported {Path.GetFileName(created)}";
        }
        catch (Exception ex)
        {
            MsgBox.Show($"Couldn't import the savefile:\n\n{ex.Message}");
        }
    }

    private void LoadSave()
    {
        if (!EnsureConfigured() || !CanLoad || SelectedEntry == null) return;

        try
        {
            _saveManagerService.LoadSave(SelectedEntry.FullPath, SaveFileLocation);
            RefreshGameFileState();
            StatusText = $"Loaded {SelectedEntry.Name}";
        }
        catch (Exception ex)
        {
            MsgBox.Show($"Couldn't load the savestate:\n\n{ex.Message}");
        }
    }

    private void ReplaceSave()
    {
        if (!EnsureConfigured() || !CanReplace || SelectedEntry == null) return;

        if (!MsgBox.ShowOkCancel($"Replace '{SelectedEntry.Name}' with the game's current savefile?")) return;

        try
        {
            _saveManagerService.ReplaceSave(SelectedEntry.FullPath, SaveFileLocation);
            var path = SelectedEntry.FullPath;
            RebuildTree();
            SelectByPath(path);
            StatusText = $"Replaced {Path.GetFileName(path)}";
        }
        catch (Exception ex)
        {
            MsgBox.Show($"Couldn't replace the savestate:\n\n{ex.Message}");
        }
    }

    private void ToggleGameFileReadOnly()
    {
        if (!EnsureConfigured()) return;

        try
        {
            _saveManagerService.SetReadOnly(SaveFileLocation, !IsGameFileReadOnly);
            RefreshGameFileState();
            StatusText = IsGameFileReadOnly
                ? "Gamefile is read-only - the game can't overwrite it."
                : "Gamefile is writable again.";
        }
        catch (Exception ex)
        {
            MsgBox.Show($"Couldn't change the gamefile's read-only state:\n\n{ex.Message}");
        }
    }

    private void CreateFolder()
    {
        var parent = SelectedEntry?.ContainingFolder ?? CurrentProfilePath;
        if (parent == null)
        {
            MsgBox.Show("Create or select a profile first.");
            return;
        }

        var name = TextInputWindowPrompt("Add Folder", "Folder name:");
        if (name == null) return;

        if (!IsValidName(name)) return;

        try
        {
            var created = _saveManagerService.CreateFolder(parent, name);
            RebuildTree();
            SelectByPath(created);
            StatusText = $"Created folder '{name}'.";
        }
        catch (Exception ex)
        {
            MsgBox.Show($"Couldn't create the folder:\n\n{ex.Message}");
        }
    }

    private void RenameSelected()
    {
        if (SelectedEntry == null) return;

        var name = TextInputWindowPrompt("Rename", SelectedEntry.IsFolder ? "Folder name:" : "Savestate name:",
            SelectedEntry.Name);
        if (name == null || name == SelectedEntry.Name) return;

        if (!IsValidName(name)) return;

        try
        {
            var renamed = _saveManagerService.Rename(SelectedEntry.FullPath, name);
            RebuildTree();
            SelectByPath(renamed);
            StatusText = $"Renamed to '{name}'.";
        }
        catch (Exception ex)
        {
            MsgBox.Show($"Couldn't rename it. It may be read-only or open in another program.\n\n{ex.Message}");
        }
    }

    private void DeleteSelected()
    {
        if (SelectedEntry == null) return;

        var entry = SelectedEntry;
        var warning = entry.IsFolder
            ? $"Delete '{entry.Name}' and everything inside it?"
            : $"Delete '{entry.Name}'?";
        if (!MsgBox.ShowOkCancel(warning)) return;

        try
        {
            // Read-only savestates can't be deleted until the attribute is cleared.
            foreach (var descendant in entry.Flatten().Where(e => !e.IsFolder))
                _saveManagerService.SetReadOnly(descendant.FullPath, false);

            _saveManagerService.Delete(entry.FullPath);
            RebuildTree();
            StatusText = $"Deleted '{entry.Name}'.";
        }
        catch (Exception ex)
        {
            MsgBox.Show($"Couldn't delete it. It may be open in another program.\n\n{ex.Message}");
        }
    }

    #endregion

    #region Private Methods

    private void RegisterHotkeys(HotkeyManager hotkeyManager)
    {
        // Hotkeys fire on the keyboard hook's thread; every one of these touches the tree or shows a dialog,
        // so they have to be marshalled onto the UI thread.
        hotkeyManager.RegisterAction(HotkeyActions.LoadSavestate, () => OnUiThread(LoadSave));
        hotkeyManager.RegisterAction(HotkeyActions.ImportSavestate, () => OnUiThread(ImportSave));
        hotkeyManager.RegisterAction(HotkeyActions.ToggleSaveReadOnly, () => OnUiThread(ToggleGameFileReadOnly));
        hotkeyManager.RegisterAction(HotkeyActions.PreviousSavestate, () => OnUiThread(() => StepSelection(-1)));
        hotkeyManager.RegisterAction(HotkeyActions.NextSavestate, () => OnUiThread(() => StepSelection(1)));
    }

    private static void OnUiThread(Action action)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher == null || dispatcher.CheckAccess()) action();
        else dispatcher.Invoke(action);
    }

    private void RefreshGameFileState()
    {
        IsGameFileReadOnly = !string.IsNullOrWhiteSpace(SaveFileLocation) &&
                             _saveManagerService.IsReadOnly(SaveFileLocation);

        foreach (var entry in RootItems.SelectMany(r => r.Flatten()).Where(e => !e.IsFolder))
        {
            entry.IsMissing = !File.Exists(entry.FullPath);
            if (!entry.IsMissing) entry.IsReadOnly = _saveManagerService.IsReadOnly(entry.FullPath);
        }
    }

    private bool EnsureConfigured()
    {
        if (IsConfigured) return true;
        MsgBox.Show("Set Sekiro's savefile location and a profiles folder first.");
        return false;
    }

    private static bool IsValidName(string name)
    {
        if (name.IndexOfAny(Path.GetInvalidFileNameChars()) < 0) return true;
        MsgBox.Show("That name contains characters Windows doesn't allow in a file name.");
        return false;
    }

    private static string? TextInputWindowPrompt(string title, string prompt, string initialValue = "") =>
        Views.Windows.TextInputWindow.Prompt(title, prompt, initialValue);

    #endregion
}
