namespace SekiroTool.Interfaces;

public interface ISaveManagerService
{
    /// <summary>Sekiro's savefile name. Used to name imported savestates and to sanity-check the configured path.</summary>
    string ExpectedSaveName { get; }

    /// <summary>Best guess at %AppData%\Sekiro\&lt;SteamID&gt;\S0000.sl2, or null if it can't be found.</summary>
    string? SuggestSaveFileLocation();

    /// <summary>Immediate subdirectories of the profiles directory, sorted by name. Empty if it doesn't exist.</summary>
    List<string> GetProfileNames(string profilesDirectory);

    /// <summary>Copies the live gamefile into the given folder, suffixing _0, _1, ... on name collision. Returns the new path.</summary>
    string ImportSave(string gameSaveFile, string destinationFolder);

    /// <summary>
    /// Overwrites the live gamefile with the given savestate. Both files end up with the savestate's original
    /// read-only state, so loading a read-only savestate leaves the gamefile read-only - that is the practice loop,
    /// not an oversight.
    /// </summary>
    void LoadSave(string savePath, string gameSaveFile);

    /// <summary>Re-imports the live gamefile over an existing savestate, keeping its name.</summary>
    void ReplaceSave(string savePath, string gameSaveFile);

    bool IsReadOnly(string path);
    void SetReadOnly(string path, bool readOnly);

    /// <summary>Renames a file or directory in place. Returns the new path.</summary>
    string Rename(string path, string newName);

    void Delete(string path);

    /// <summary>Creates a subfolder and returns its path.</summary>
    string CreateFolder(string parentFolder, string name);
}
