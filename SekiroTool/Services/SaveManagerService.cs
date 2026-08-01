using System.IO;
using SekiroTool.Interfaces;

namespace SekiroTool.Services;

/// <summary>
/// Pure filesystem operations behind the Save Manager tab. Deliberately touches no game memory: loading a savestate
/// is a file copy, and the game only reads it when the main menu is (re)entered.
/// </summary>
public class SaveManagerService : ISaveManagerService
{
    private const string SteamIdPrefix = "76561";

    public string ExpectedSaveName => "S0000.sl2";

    public string? SuggestSaveFileLocation()
    {
        try
        {
            var sekiroAppData = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Sekiro");
            if (!Directory.Exists(sekiroAppData)) return null;

            // Saves live under a numeric SteamID folder; pick the first one that looks like one.
            foreach (var dir in Directory.GetDirectories(sekiroAppData))
            {
                var name = Path.GetFileName(dir);
                if (!name.StartsWith(SteamIdPrefix) || !name.All(char.IsDigit)) continue;

                var candidate = Path.Combine(dir, ExpectedSaveName);
                if (File.Exists(candidate)) return candidate;
            }
        }
        catch
        {
            // Suggestion only - fall through to letting the user browse.
        }

        return null;
    }

    public List<string> GetProfileNames(string profilesDirectory)
    {
        if (string.IsNullOrWhiteSpace(profilesDirectory) || !Directory.Exists(profilesDirectory))
            return new List<string>();

        return Directory.GetDirectories(profilesDirectory)
            .Select(Path.GetFileName)
            .Where(n => !string.IsNullOrEmpty(n))
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            .ToList()!;
    }

    public string ImportSave(string gameSaveFile, string destinationFolder)
    {
        var name = Path.GetFileName(gameSaveFile);
        var target = Path.Combine(destinationFolder, name);
        for (var i = 0; File.Exists(target); i++)
            target = Path.Combine(destinationFolder, $"{name}_{i}");

        File.Copy(gameSaveFile, target);
        return target;
    }

    public void LoadSave(string savePath, string gameSaveFile)
    {
        // Capture the savestate's own read-only state first, then clear both so the copy can proceed, then restore
        // that state onto both files. A read-only savestate therefore leaves the gamefile read-only after loading.
        var wasReadOnly = IsReadOnly(savePath);

        SetReadOnly(gameSaveFile, false);
        SetReadOnly(savePath, false);

        File.Copy(savePath, gameSaveFile, true);

        SetReadOnly(gameSaveFile, wasReadOnly);
        SetReadOnly(savePath, wasReadOnly);
    }

    public void ReplaceSave(string savePath, string gameSaveFile)
    {
        SetReadOnly(savePath, false);
        File.Copy(gameSaveFile, savePath, true);
    }

    public bool IsReadOnly(string path) => File.Exists(path) && new FileInfo(path).IsReadOnly;

    public void SetReadOnly(string path, bool readOnly)
    {
        if (!File.Exists(path)) return;
        new FileInfo(path).IsReadOnly = readOnly;
    }

    public string Rename(string path, string newName)
    {
        var parent = Path.GetDirectoryName(path)!;
        var target = Path.Combine(parent, newName);
        if (string.Equals(path, target, StringComparison.Ordinal)) return path;

        if (Directory.Exists(path)) Directory.Move(path, target);
        else File.Move(path, target);

        return target;
    }

    public void Delete(string path)
    {
        if (Directory.Exists(path)) Directory.Delete(path, true);
        else if (File.Exists(path)) File.Delete(path);
    }

    public string CreateFolder(string parentFolder, string name)
    {
        var target = Path.Combine(parentFolder, name);
        Directory.CreateDirectory(target);
        return target;
    }
}
