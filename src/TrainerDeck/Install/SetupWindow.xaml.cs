using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using TrainerDeck.Core.Diagnostics;
using TrainerDeck.Core.Install;
using Forms = System.Windows.Forms;

namespace TrainerDeck.Install;

public partial class SetupWindow : Window
{
    private enum Mode { Install, Update, Repair, Uninstall }

    private readonly string _userSid;
    private readonly bool _systemIntegration;
    private readonly ExistingInstall? _existing;
    private readonly bool _uninstallOnly;
    private string _target;
    private Mode _mode;
    private bool _done;

    public SetupWindow(string target, string userSid, bool systemIntegration, ExistingInstall? existing, bool uninstallOnly = false)
    {
        InitializeComponent();
        _target = existing?.Directory ?? target;
        _userSid = userSid;
        _systemIntegration = systemIntegration;
        _existing = existing;
        _uninstallOnly = uninstallOnly && existing is not null;
        TargetText.Text = _target;
        Logo.Source = IconExtractorSafe();
        if (!WidgetInstaller.Available)
        {
            WidgetCheck.IsChecked = false;
            WidgetCheck.Visibility = Visibility.Collapsed;
        }

        UI.Theme.ThemeManager.Instance?.Attach(this);
        if (_uninstallOnly)
        {
            ShowUninstall();
        }
        else if (existing is not null)
        {
            ShowMaintenance();
        }
        else
        {
            ShowOptions(Mode.Install);
        }
    }

    private static System.Windows.Media.ImageSource? IconExtractorSafe() =>
        Environment.ProcessPath is string exe ? Art.IconExtractor.Extract(exe) : null;

    private string InstalledVersion => _existing?.Version is { Length: > 0 } v ? v : "an earlier version";

    private void ShowMaintenance()
    {
        Show(MaintenancePanel);
        Title = "Trainer Deck setup";
        Heading.Text = "Trainer Deck is installed";
        VersionText.Text = $"Version {InstalledVersion} in {_existing!.Directory}";
        int compare = InstallLayout.CompareVersions(_existing.Version, Installer.Version);
        UpdateCard.IsEnabled = compare != 0;
        UpdateTitle.Text = compare switch
        {
            0 => "Up to date",
            > 0 => $"Install version {Installer.Version}",
            _ => $"Update to version {Installer.Version}",
        };
        UpdateDetail.Text = compare switch
        {
            0 => $"Version {Installer.Version} is already installed. Use Repair to reinstall it.",
            > 0 => $"Replaces the newer version {InstalledVersion}. Settings and trainers are kept.",
            _ => $"Replaces version {InstalledVersion}. Settings and trainers are kept.",
        };
        RepairDetail.Text = $"Reinstalls version {Installer.Version} and resets its shortcuts, the no-UAC start, the trainerdeck: link "
            + "and the Game Bar widget. Settings and trainers are kept.";
        BackButton.Visibility = Visibility.Collapsed;
        InstallButton.Visibility = Visibility.Collapsed;
        CancelButton.Content = "Cancel";
        CancelButton.Style = (Style)FindResource("FlatButton");
        Guide(UpdateCard.IsEnabled ? UpdateCard : RepairCard);
    }

    private void ShowOptions(Mode mode)
    {
        _mode = mode;
        Show(OptionsPanel);
        bool existing = _existing is not null;
        Title = mode switch { Mode.Update => "Update Trainer Deck", Mode.Repair => "Repair Trainer Deck", _ => "Install Trainer Deck" };
        Heading.Text = Title;
        VersionText.Text = mode switch
        {
            Mode.Update => $"Version {InstalledVersion} → {Installer.Version}",
            Mode.Repair => $"Reinstall version {Installer.Version}",
            _ => $"Version {Installer.Version}",
        };
        InstallButton.Content = mode switch { Mode.Update => "Update", Mode.Repair => "Repair", _ => "Install" };
        InstallButton.Style = (Style)FindResource("AccentButton");
        InstallButton.Visibility = Visibility.Visible;
        CancelButton.Style = (Style)FindResource("FlatButton");
        Guide(InstallButton);
        BrowseButton.IsEnabled = !existing;
        OptionsNote.Visibility = existing ? Visibility.Collapsed : Visibility.Visible;
        BackButton.Visibility = existing ? Visibility.Visible : Visibility.Collapsed;
        if (existing)
        {
            TaskCheck.IsChecked = Installer.TaskExists();
            DesktopCheck.IsChecked = Installer.HasDesktopShortcut;
            if (WidgetInstaller.Available)
            {
                WidgetCheck.IsChecked = WidgetInstaller.IsInstalled();
            }
        }
    }

    private void ShowUninstall()
    {
        _mode = Mode.Uninstall;
        Show(UninstallPanel);
        Title = "Uninstall Trainer Deck";
        Heading.Text = Title;
        VersionText.Text = $"Version {InstalledVersion} in {_target}";
        string trainers = Path.Combine(_target, "Trainers");
        int count = CountTrainers(trainers);
        TrainersDetail.Text = count switch
        {
            < 0 => $"{trainers}  ·  not found",
            0 => $"{trainers}  ·  empty",
            1 => $"{trainers}  ·  1 trainer or zip",
            _ => $"{trainers}  ·  {count} trainers and zips",
        };
        DeleteTrainersCheck.IsEnabled = count >= 0;
        DataDetail.Text = _systemIntegration ? Installer.UserDataDirectory(_userSid) : "Not available in test installs";
        if (!_systemIntegration)
        {
            DeleteDataCheck.IsChecked = false;
            DeleteDataCheck.IsEnabled = false;
        }
        InstallButton.Content = "Uninstall";
        InstallButton.Style = (Style)FindResource("DangerButton");
        InstallButton.Visibility = Visibility.Visible;
        CancelButton.Style = (Style)FindResource("FlatButton");
        Guide(InstallButton);
        BackButton.Visibility = _uninstallOnly ? Visibility.Collapsed : Visibility.Visible;
    }

    private void Guide(Control next)
    {
        foreach (Button b in new[] { InstallButton, CancelButton })
        {
            b.IsDefault = b == next;
        }

        Dispatcher.BeginInvoke(DispatcherPriority.Input, () =>
        {
            next.Focus();
            Keyboard.Focus(next);
        });
    }

    private void GuideToClose()
    {
        CancelButton.Style = (Style)FindResource("AccentButton");
        CancelButton.IsEnabled = true;
        if (InstallButton.Visibility == Visibility.Visible)
        {
            InstallButton.Style = (Style)FindResource("FlatButton");
        }

        Guide(CancelButton);
    }

    private void Show(FrameworkElement page)
    {
        foreach (FrameworkElement p in new FrameworkElement[] { MaintenancePanel, OptionsPanel, UninstallPanel, ProgressPanel })
        {
            p.Visibility = p == page ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private static int CountTrainers(string folder)
    {
        try
        {
            return Directory.Exists(folder)
                ? Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories).Count(f => f.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                : -1;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return 0;
        }
    }

    private void OnChooseUpdate(object sender, RoutedEventArgs e) => ShowOptions(Mode.Update);

    private void OnChooseRepair(object sender, RoutedEventArgs e) => ShowOptions(Mode.Repair);

    private void OnChooseUninstall(object sender, RoutedEventArgs e) => ShowUninstall();

    private void OnBack(object sender, RoutedEventArgs e) => ShowMaintenance();

    private void OnBrowse(object sender, RoutedEventArgs e)
    {
        using var dialog = new Forms.FolderBrowserDialog
        {
            Description = "Choose where to install Trainer Deck",
            UseDescriptionForTitle = true,
            SelectedPath = Path.GetDirectoryName(_target) ?? _target,
        };
        if (dialog.ShowDialog() == Forms.DialogResult.OK)
        {
            _target = Path.Combine(dialog.SelectedPath, InstallLayout.AppName);
            TargetText.Text = _target;
        }
    }

    private void OnCancel(object sender, RoutedEventArgs e) => Close();

    private async void OnPrimary(object sender, RoutedEventArgs e)
    {
        if (_done)
        {
            if (_mode != Mode.Uninstall)
            {
                LaunchInstalled();
            }

            Close();
            return;
        }

        Show(ProgressPanel);
        BackButton.Visibility = Visibility.Collapsed;
        InstallButton.IsEnabled = false;
        CancelButton.IsEnabled = false;
        Action<string> progress = text => Dispatcher.BeginInvoke(() => ProgressText.Text = text);
        try
        {
            if (_mode == Mode.Uninstall)
            {
                await Uninstall(progress);
            }
            else
            {
                await Install(progress);
            }
        }
        catch (Exception ex)
        {
            Log.Error($"{_mode} failed", ex);
            Bar.IsIndeterminate = false;
            Heading.Text = _mode == Mode.Uninstall ? "Uninstall failed" : $"{_mode} failed";
            ProgressText.Text = ex.Message;
            CancelButton.Content = "Close";
            GuideToClose();
        }
    }

    private async Task Install(Action<string> progress)
    {
        var options = new InstallOptions(_target, TaskCheck.IsChecked == true, DesktopCheck.IsChecked == true, _userSid, _systemIntegration,
            WidgetCheck.IsChecked == true, Repair: _mode == Mode.Repair);
        string? note = await Task.Run(() => Installer.Install(options, progress));
        _done = true;
        Bar.IsIndeterminate = false;
        Bar.Value = 100;
        Heading.Text = _mode switch { Mode.Update => "Trainer Deck is updated", Mode.Repair => "Trainer Deck is repaired", _ => "Trainer Deck is installed" };
        ProgressText.Text = (_mode == Mode.Install
                ? $"Installed to {_target}.\n\nFind it in the Start menu. To add it to the Xbox app: My Library › + › browse to {Path.Combine(_target, InstallLayout.ExeName)}."
                : $"Version {Installer.Version} is installed in {_target}.")
            + (options.GameBarWidget && note is null && WidgetInstaller.Available ? "\n\nThe Trainer Deck widget is in Game Bar's widget menu." : "")
            + (note is null ? "" : "\n\n" + note);
        InstallButton.Content = "Launch Trainer Deck";
        CancelButton.Content = "Close";
        InstallButton.IsEnabled = true;
        GuideToClose();
    }

    private async Task Uninstall(Action<string> progress)
    {
        bool keepTrainers = DeleteTrainersCheck.IsChecked != true;
        string? data = DeleteDataCheck.IsChecked == true ? Installer.UserDataDirectory(_userSid) : null;
        progress("Removing Trainer Deck…");
        await Task.Run(() => Installer.Uninstall(_target, keepTrainers, data, _systemIntegration));
        _done = true;
        Bar.IsIndeterminate = false;
        Bar.Value = 100;
        Heading.Text = "Trainer Deck has been removed";
        ProgressText.Text = "The app, its shortcuts, the no-UAC start, the trainerdeck: link and the Game Bar widget are gone."
            + (keepTrainers ? $"\n\nYour trainers are still in {Path.Combine(_target, "Trainers")}." : "")
            + (data is null ? $"\n\nYour settings are still in {Installer.UserDataDirectory(_userSid)}." : "")
            + "\n\nThe remaining files are removed when you close this window.";
        InstallButton.Visibility = Visibility.Collapsed;
        CancelButton.Content = "Close";
        GuideToClose();
    }

    private void LaunchInstalled()
    {
        try
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{Path.Combine(_target, InstallLayout.ExeName)}\"") { UseShellExecute = false });
        }
        catch (Exception ex)
        {
            Log.Warn($"Could not launch after install: {ex.Message}");
        }
    }
}
