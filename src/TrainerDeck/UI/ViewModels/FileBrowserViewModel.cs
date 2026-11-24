using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using TrainerDeck.Art;
using TrainerDeck.Core.Input;

namespace TrainerDeck.UI.ViewModels;


public sealed partial class FileBrowserViewModel : RowListPageViewModel
{
    private readonly bool _folderMode;
    private readonly IReadOnlyCollection<string>? _extensions;
    private readonly Action<string> _picked;
    private string? _current;

    public FileBrowserViewModel(MainViewModel main, string title, string? start, bool folderMode, Action<string> picked,
        IReadOnlyCollection<string>? extensions = null) : base(main)
    {
        _folderMode = folderMode;
        _extensions = extensions;
        _picked = picked;
        Title = title;
        if (start is not null && Directory.Exists(start))
        {
            Navigate(start, null);
        }
        else
        {
            ShowRoot(null);
        }
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanGoUp))]
    private string pathText = "";

    public bool CanGoUp => _current is not null;

    public string PathPlaceholder => _folderMode ? "Type or paste a folder path" : "Type or paste a folder or file path";

    protected override string BackLabel => "Cancel";

    public override IReadOnlyList<FooterHint> Hints
    {
        get
        {
            var hints = base.Hints.ToList();
            if (_current is not null)
            {
                hints.Insert(hints.Count - 1, new FooterHint(InputAction.ParentFolder, "Up a folder"));
            }

            return hints;
        }
    }

    protected override bool HandlePage(InputAction action)
    {
        if (action is InputAction.ParentFolder or InputAction.PrevTab && _current is not null)
        {
            GoUp();
            return true;
        }

        return false;
    }

    [RelayCommand]
    public void GoUp()
    {
        if (_current is null)
        {
            return;
        }

        string from = _current;
        DirectoryInfo? parent = Directory.GetParent(from.TrimEnd('\\'));
        string label = Path.GetFileName(from.TrimEnd('\\'));
        if (parent is null)
        {
            DriveInfo? drive = SafeDrives().FirstOrDefault(d => string.Equals(d.RootDirectory.FullName, from, StringComparison.OrdinalIgnoreCase));
            ShowRoot(drive is null ? null : DriveLabel(drive));
        }
        else
        {
            Navigate(parent.FullName, label);
        }
    }

    public void GoToPath(string text)
    {
        string path = Environment.ExpandEnvironmentVariables(text.Trim().Trim('"'));
        if (path.Length == 0)
        {
            ShowRoot(null);
            return;
        }

        try
        {
            path = Path.GetFullPath(path);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            Main.ShowError($"“{text.Trim()}” isn't a valid path.");
            return;
        }

        if (Directory.Exists(path))
        {
            Navigate(path, null);
        }
        else if (File.Exists(path) && !_folderMode && Accepts(path))
        {
            Pick(path);
        }
        else if (File.Exists(path))
        {
            Main.ShowError(_folderMode ? "That's a file; pick a folder." : $"Trainer Deck can't use {Path.GetExtension(path)} files here.");
        }
        else
        {
            Main.ShowError($"Can't find {path}.");
        }
    }

    
    [RelayCommand]
    private void BrowseSystem()
    {
        string initial = _current ?? MainViewModel.DownloadsFolder;
        System.Windows.Window? owner = System.Windows.Application.Current?.MainWindow;
        if (_folderMode)
        {
            var dialog = new OpenFolderDialog { Title = Title, InitialDirectory = initial };
            if (dialog.ShowDialog(owner) == true)
            {
                Pick(dialog.FolderName);
            }

            return;
        }

        IReadOnlyCollection<string> extensions = _extensions ?? ArtService.ImageExtensions;
        string patterns = string.Join(";", extensions.Select(e => "*" + e));
        string label = _extensions is null ? "Images" : "Trainers";
        var files = new OpenFileDialog
        {
            Title = Title,
            InitialDirectory = initial,
            Filter = $"{label} ({string.Join(", ", extensions.Select(e => "*" + e))})|{patterns}",
            CheckFileExists = true,
        };
        if (files.ShowDialog(owner) == true)
        {
            Pick(files.FileName);
        }
    }

    private void ShowRoot(string? select)
    {
        _current = null;
        PathText = "";
        Subtitle = "This PC";
        var rows = new List<RowViewModel> { new HeaderRow("Places") };
        AddPlace(rows, "Desktop", Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), Glyphs.Desktop);
        AddPlace(rows, "Downloads", MainViewModel.DownloadsFolder, Glyphs.Download);
        AddPlace(rows, "Documents", Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), Glyphs.Document);
        AddPlace(rows, "Pictures", Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), Glyphs.Image);
        foreach (string folder in Main.Services.Library.Roots)
        {
            AddPlace(rows, Path.GetFileName(folder.TrimEnd('\\')) is { Length: > 0 } n ? n : folder, folder, Glyphs.Game, folder);
        }

        rows.Add(new HeaderRow("Drives"));
        foreach (DriveInfo drive in SafeDrives())
        {
            rows.Add(FolderRow(DriveLabel(drive), drive.RootDirectory.FullName, Glyphs.Drive, drive.RootDirectory.FullName, "drive"));
        }

        SetRows(rows, select);
        HintsChanged();
    }

    private void AddPlace(List<RowViewModel> rows, string label, string path, string glyph, string? detail = null)
    {
        if (Directory.Exists(path))
        {
            rows.Add(FolderRow(label, path, glyph, detail, "folder"));
        }
    }

    private void Navigate(string path, string? select)
    {
        List<string> dirs;
        List<string> files = new();
        try
        {
            var options = new EnumerationOptions { AttributesToSkip = FileAttributes.Hidden | FileAttributes.System, IgnoreInaccessible = true };
            dirs = Directory.EnumerateDirectories(path, "*", options).OrderBy(d => d, StringComparer.OrdinalIgnoreCase).ToList();
            if (!_folderMode)
            {
                IEnumerable<string> matching = Directory.EnumerateFiles(path, "*", options).Where(Accepts);
                files = _extensions is null
                    ? matching.OrderBy(f => f, StringComparer.OrdinalIgnoreCase).ToList()
                    : matching.OrderByDescending(File.GetLastWriteTimeUtc).ToList();
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Main.ShowError($"Can't open {path}.");
            return;
        }

        _current = path;
        PathText = path;
        Subtitle = null;
        var rows = new List<RowViewModel>();
        if (_folderMode)
        {
            rows.Add(new ActionRow("Use this folder", Glyphs.Check, () => Pick(path), "Choose", path));
        }

        if (dirs.Count > 0)
        {
            rows.Add(new HeaderRow("Folders"));
            rows.AddRange(dirs.Select(d => (RowViewModel)FolderRow(Path.GetFileName(d), d, Glyphs.Folder, null, "folder")));
        }

        if (files.Count > 0)
        {
            rows.Add(new HeaderRow(_extensions is null ? "Images" : "Trainers and zips  ·  newest first"));
            rows.AddRange(files.Select(FileRow));
        }

        if (dirs.Count == 0 && files.Count == 0)
        {
            rows.Add(new HeaderRow(_folderMode ? "No folders here" : _extensions is null ? "No images here" : "No trainers or zips here"));
        }

        SetRows(rows, select ?? (_folderMode ? "Use this folder" : null));
        if (select is null && !_folderMode)
        {
            SelectedRow = Rows.FirstOrDefault(r => r.IsSelectable && r.Tone is "exe" or "zip" or "image")
                ?? Rows.FirstOrDefault(r => r.IsSelectable)
                ?? SelectedRow;
        }

        HintsChanged();
    }

    private RowViewModel FileRow(string file)
    {
        string ext = Path.GetExtension(file).ToLowerInvariant();
        (string glyph, string tone) = ext switch
        {
            ".exe" => (Glyphs.Game, "exe"),
            ".zip" => (Glyphs.Zip, "zip"),
            _ => (Glyphs.Image, "image"),
        };
        string size = FormatSize(file);
        string detail = size.Length > 0 ? $"{ext.TrimStart('.').ToUpperInvariant()}  ·  {size}" : ext.TrimStart('.').ToUpperInvariant();
        return new ActionRow(Path.GetFileName(file), glyph, () => Pick(file), "Choose", detail) { Tone = tone };
    }

    private ActionRow FolderRow(string label, string path, string glyph, string? detail, string tone) =>
        _folderMode
            ? new ActionRow(label, glyph, () => Navigate(path, null), "Open", detail, () => Pick(path), "Choose folder") { Tone = tone }
            : new ActionRow(label, glyph, () => Navigate(path, null), "Open", detail) { Tone = tone };

    private bool Accepts(string file) =>
        (_extensions ?? ArtService.ImageExtensions).Contains(Path.GetExtension(file).ToLowerInvariant());

    private void Pick(string path)
    {
        Main.Pop();
        _picked(path);
    }

    private static IEnumerable<DriveInfo> SafeDrives()
    {
        foreach (DriveInfo d in DriveInfo.GetDrives())
        {
            bool ready;
            try
            {
                ready = d.IsReady;
            }
            catch (IOException)
            {
                ready = false;
            }

            if (ready)
            {
                yield return d;
            }
        }
    }

    private static string DriveLabel(DriveInfo d)
    {
        string name = d.Name.TrimEnd('\\');
        try
        {
            return string.IsNullOrWhiteSpace(d.VolumeLabel) ? $"Local Disk ({name})" : $"{d.VolumeLabel} ({name})";
        }
        catch (IOException)
        {
            return name;
        }
    }

    private static string FormatSize(string file)
    {
        try
        {
            long bytes = new FileInfo(file).Length;
            return bytes >= 1 << 20 ? $"{bytes / (1024.0 * 1024):0.0} MB" : $"{Math.Max(1, bytes / 1024)} KB";
        }
        catch (IOException)
        {
            return "";
        }
    }
}
