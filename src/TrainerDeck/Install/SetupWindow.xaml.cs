using System.Diagnostics;
using System.IO;
using System.Windows;
using TrainerDeck.Core.Diagnostics;
using TrainerDeck.Core.Install;
using TrainerDeck.UI.Native;
using Forms = System.Windows.Forms;

namespace TrainerDeck.Install;

public partial class SetupWindow : Window
{
    private readonly string _userSid;
    private readonly bool _systemIntegration;
    private string _target;
    private bool _done;

    public SetupWindow(string target, string userSid, bool systemIntegration, bool isUpdate)
    {
        InitializeComponent();
        _target = target;
        _userSid = userSid;
        _systemIntegration = systemIntegration;
        TargetText.Text = target;
        VersionText.Text = isUpdate ? $"Update to version {Installer.Version}" : $"Version {Installer.Version}";
        Heading.Text = isUpdate ? "Update Trainer Deck" : "Install Trainer Deck";
        InstallButton.Content = isUpdate ? "Update" : "Install";
        BrowseButton.IsEnabled = !isUpdate;
        Logo.Source = IconExtractorSafe();
        if (!WidgetInstaller.Available)
        {
            WidgetCheck.IsChecked = false;
            WidgetCheck.Visibility = Visibility.Collapsed;
        }
        UI.Theme.ThemeManager.Instance?.Attach(this);
    }

    private static System.Windows.Media.ImageSource? IconExtractorSafe() =>
        Environment.ProcessPath is string exe ? Art.IconExtractor.Extract(exe) : null;

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

    private async void OnInstall(object sender, RoutedEventArgs e)
    {
        if (_done)
        {
            LaunchInstalled();
            Close();
            return;
        }

        var options = new InstallOptions(_target, TaskCheck.IsChecked == true, DesktopCheck.IsChecked == true, _userSid, _systemIntegration,
            WidgetCheck.IsChecked == true);
        OptionsPanel.Visibility = Visibility.Collapsed;
        ProgressPanel.Visibility = Visibility.Visible;
        InstallButton.IsEnabled = false;
        CancelButton.IsEnabled = false;
        try
        {
            string? note = await Task.Run(() => Installer.Install(options, text => Dispatcher.BeginInvoke(() => ProgressText.Text = text)));
            _done = true;
            Bar.IsIndeterminate = false;
            Bar.Value = 100;
            Heading.Text = "Trainer Deck is installed";
            ProgressText.Text = $"Installed to {_target}.\n\nFind it in the Start menu. To add it to the Xbox app: My Library › + › browse to {Path.Combine(_target, InstallLayout.ExeName)}."
                + (options.GameBarWidget && note is null && WidgetInstaller.Available ? "\n\nThe Trainer Deck widget is in Game Bar's widget menu." : "")
                + (note is null ? "" : "\n\n" + note);
            InstallButton.Content = "Launch Trainer Deck";
            CancelButton.Content = "Close";
            InstallButton.IsEnabled = true;
            CancelButton.IsEnabled = true;
        }
        catch (Exception ex)
        {
            Log.Error("Install failed", ex);
            Bar.IsIndeterminate = false;
            Heading.Text = "Installation failed";
            ProgressText.Text = ex.Message;
            CancelButton.Content = "Close";
            CancelButton.IsEnabled = true;
        }
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
