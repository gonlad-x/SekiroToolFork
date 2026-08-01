using System.Collections.ObjectModel;
using System.IO;

namespace SekiroTool.ViewModels;

/// <summary>
/// One node in the Save Manager tree: either a folder or a savestate file. Backed directly by the filesystem, so the
/// tree is rebuilt from disk rather than kept in sync by hand.
/// </summary>
public class SaveEntryViewModel : BaseViewModel
{
    public SaveEntryViewModel(string fullPath, bool isFolder, SaveEntryViewModel? parent)
    {
        _fullPath = fullPath;
        IsFolder = isFolder;
        Parent = parent;
    }

    public bool IsFolder { get; }
    public SaveEntryViewModel? Parent { get; }
    public ObservableCollection<SaveEntryViewModel> Children { get; } = new();

    private string _fullPath;

    public string FullPath
    {
        get => _fullPath;
        private set
        {
            if (!SetProperty(ref _fullPath, value)) return;
            OnPropertyChanged(nameof(Name));
        }
    }

    public string Name => Path.GetFileName(FullPath);

    /// <summary>Folder a new child would go into: this folder, or a save's parent folder.</summary>
    public string ContainingFolder => IsFolder ? FullPath : Path.GetDirectoryName(FullPath)!;

    private bool _isReadOnly;

    public bool IsReadOnly
    {
        get => _isReadOnly;
        set => SetProperty(ref _isReadOnly, value);
    }

    private bool _isMissing;

    /// <summary>The backing file vanished under us (deleted outside the tool).</summary>
    public bool IsMissing
    {
        get => _isMissing;
        set => SetProperty(ref _isMissing, value);
    }

    private bool _isExpanded = true;

    public bool IsExpanded
    {
        get => _isExpanded;
        set => SetProperty(ref _isExpanded, value);
    }

    private bool _isSelected;

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    /// <summary>Repoints this node (and, for folders, everything beneath it) after a rename.</summary>
    public void Repath(string newFullPath)
    {
        var oldPath = FullPath;
        FullPath = newFullPath;

        foreach (var child in Children)
            child.Repath(child.FullPath.Replace(oldPath, newFullPath));
    }

    /// <summary>Builds the subtree for a directory: folders first, then savestates, each alphabetical.</summary>
    public static SaveEntryViewModel BuildTree(string directory, SaveEntryViewModel? parent = null)
    {
        var node = new SaveEntryViewModel(directory, isFolder: true, parent);

        foreach (var dir in Directory.GetDirectories(directory).OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase))
            node.Children.Add(BuildTree(dir, node));

        foreach (var file in Directory.GetFiles(directory).OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase))
        {
            node.Children.Add(new SaveEntryViewModel(file, isFolder: false, node)
            {
                IsReadOnly = new FileInfo(file).IsReadOnly
            });
        }

        return node;
    }

    /// <summary>Depth-first walk of this node and all descendants.</summary>
    public IEnumerable<SaveEntryViewModel> Flatten()
    {
        yield return this;
        foreach (var descendant in Children.SelectMany(c => c.Flatten()))
            yield return descendant;
    }
}
