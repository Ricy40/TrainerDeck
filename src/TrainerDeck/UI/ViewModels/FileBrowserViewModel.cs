using System.IO;
using TrainerDeck.Art;
using TrainerDeck.Core.Input;

namespace TrainerDeck.UI.ViewModels;


public sealed class FileBrowserViewModel : RowListPageViewModel
{
    private readonly bool _folderMode;
    private readonly Action<string> _picked;
    private string? _current;

    public FileBrowserViewModel(MainViewModel main, string title, string? start, bool folderMode, Action<string> picked) : base(main)
    {
        _folderMode = folderMode;
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

    protected override string BackLabel => _current is null ? "Cancel" : "Up";

    protected override bool HandlePage(InputAction action)
    {
        if (action == InputAction.Back && _current is not null)
        {
            GoUp();
            return true;
        }

        return false;
    }

    private void ShowRoot(string? select)
    {
        _current = null;
        Subtitle = "This PC";
        var rows = new List<RowViewModel> { new HeaderRow("Places") };
        AddPlace(rows, "Desktop", Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), Glyphs.Desktop);
        AddPlace(rows, "Downloads", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads"), Glyphs.Download);
        AddPlace(rows, "Documents", Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), Glyphs.Document);
        AddPlace(rows, "Pictures", Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), Glyphs.Image);
        foreach (string folder in Main.Services.Library.Roots)
        {
            AddPlace(rows, Path.GetFileName(folder.TrimEnd('\\')) is { Length: > 0 } n ? n : folder, folder, Glyphs.Game, folder);
        }

        rows.Add(new HeaderRow("Drives"));
        foreach (DriveInfo drive in SafeDrives())
        {
            string label = DriveLabel(drive);
            rows.Add(FolderRow(label, drive.RootDirectory.FullName, Glyphs.Drive, drive.RootDirectory.FullName));
        }

        SetRows(rows, select);
        HintsChanged();
    }

    private void AddPlace(List<RowViewModel> rows, string label, string path, string glyph, string? detail = null)
    {
        if (Directory.Exists(path))
        {
            rows.Add(FolderRow(label, path, glyph, detail));
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
                files = Directory.EnumerateFiles(path, "*", options)
                    .Where(f => ArtService.ImageExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
                    .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Main.ShowError($"Can't open {path}.");
            return;
        }

        _current = path;
        Subtitle = path;
        var rows = new List<RowViewModel>();
        if (_folderMode)
        {
            rows.Add(new ActionRow("Use this folder", Glyphs.Check, () => Pick(path), "Choose", path));
        }

        rows.Add(new ActionRow("Up one level", Glyphs.Up, GoUp, "Open"));
        foreach (string d in dirs)
        {
            rows.Add(FolderRow(Path.GetFileName(d), d, Glyphs.Folder));
        }

        foreach (string f in files)
        {
            rows.Add(new ActionRow(Path.GetFileName(f), Glyphs.Image, () => Pick(f), "Choose", FormatSize(f)));
        }

        if (dirs.Count == 0 && files.Count == 0)
        {
            rows.Add(new HeaderRow(_folderMode ? "No folders here" : "No images here"));
        }

        SetRows(rows, select ?? (_folderMode ? "Use this folder" : null));
        if (select is null)
        {
            SelectedRow = Rows.FirstOrDefault(r => r.IsSelectable && r.Glyph != Glyphs.Up && r.Glyph != Glyphs.Check) ?? SelectedRow;
        }

        HintsChanged();
    }

    private ActionRow FolderRow(string label, string path, string glyph, string? detail = null) =>
        _folderMode
            ? new ActionRow(label, glyph, () => Navigate(path, null), "Open", detail, () => Pick(path), "Choose folder")
            : new ActionRow(label, glyph, () => Navigate(path, null), "Open", detail);

    private void GoUp()
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
