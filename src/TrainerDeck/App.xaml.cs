using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using TrainerDeck.Art;
using TrainerDeck.Cheats;
using TrainerDeck.Core.Diagnostics;
using TrainerDeck.Core.Input;
using TrainerDeck.Input;
using TrainerDeck.Install;
using TrainerDeck.Core.Install;
using TrainerDeck.Launch;
using TrainerDeck.Steam;
using TrainerDeck.Core.Launch;
using TrainerDeck.Core.Library;
using TrainerDeck.Core.Settings;
using TrainerDeck.UI;
using TrainerDeck.UI.Theme;
using TrainerDeck.UI.ViewModels;

namespace TrainerDeck;

public partial class App : Application
{
    public const string ImportTrainerFlag = "--import-trainer";

    private TrainerLibrary? _library;
    private MainViewModel? _main;
    private ControllerInput? _controllers;
    private LaunchService? _launcher;
    private SingleInstance? _instance;
    private TrayIcon? _tray;
    private OverlayController? _overlay;
    private Widget.WidgetPipeServer? _widgetPipe;
    private bool _exiting;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        if (e.Args.Length == 3 && e.Args[0].Equals("--pack-icon", StringComparison.OrdinalIgnoreCase))
        {
            int code = 0;
            AttachConsole(-1);
            try
            {
                IReadOnlyList<int> sizes = Core.Art.IcoWriter.PackFolder(e.Args[1], e.Args[2]);
                Console.WriteLine($"Wrote {e.Args[2]} with sizes: {string.Join(", ", sizes)}");
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
            {
                Console.WriteLine($"Couldn't build the icon: {ex.Message}");
                code = 1;
            }

            Shutdown(code);
            return;
        }

        string self = Environment.ProcessPath ?? "";
        if (InstallLayout.IsUninstallInvocation(e.Args))
        {
            RunUninstall(e.Args);
            return;
        }

        if (InstallLayout.IsSetupInvocation(self, e.Args))
        {
            RunSetup(e.Args);
            return;
        }

        if (e.Args.Length >= 2 && e.Args[0].Equals(WidgetInstaller.CommandFlag, StringComparison.OrdinalIgnoreCase))
        {
            Log.Initialize(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "TrainerDeck-setup.log"));
            Shutdown(WidgetInstaller.RunCommand(e.Args, self));
            return;
        }

        if (e.Args.Length == 3 && e.Args[0].Equals(ImportTrainerFlag, StringComparison.OrdinalIgnoreCase))
        {
            TrainerImportResult import = TrainerImport.Import(e.Args[1], e.Args[2]);
            Shutdown(import.Success ? 0 : 1);
            return;
        }

        PortablePaths paths = PortablePaths.Resolve(AppContext.BaseDirectory);
        Log.Initialize(paths.LogFile);
        Log.Info($"Data folder: {paths.DataDirectory} (portable: {paths.IsPortable})");
        HookExceptionHandlers();

        StartupOptions options = StartupOptions.Parse(e.Args);
        _instance = AcquireInstance(options);
        if (_instance is null)
        {
            Log.Info("Trainer Deck is already running; asked it to come forward.");
            Shutdown();
            return;
        }

        bool elevated = SingleInstance.IsElevated;
        Log.Info($"Version {typeof(App).Assembly.GetName().Version}, elevated: {elevated}");

        if (paths.IsInstalled && !elevated && !options.NoElevate && !options.ElevatedRelaunch && InstallState.HasElevatedTask(paths))
        {
            _instance.Dispose();
            _instance = null;
            AllowSetForegroundWindow(-1);
            if (Installer.StartViaTask())
            {
                Log.Info("Started the administrator copy through Task Scheduler.");
                BringOtherCopyToFront();
                Shutdown();
                return;
            }

            Log.Warn("The Task Scheduler entry didn't start; continuing without it.");
            _instance = SingleInstance.TryAcquire();
            if (_instance is null)
            {
                Shutdown();
                return;
            }
        }

        var settings = new SettingsStore(paths.SettingsFile, Log.Warn);
        bool relaunchDeclined = false;
        if (options.ShouldRelaunchElevated(settings.Current.RunAsAdministrator, elevated))
        {
            if (TryRelaunchElevated(e.Args))
            {
                Shutdown();
                return;
            }

            relaunchDeclined = true;
        }

        var theme = new ThemeManager(settings);
        theme.Start();

        var store = LibraryStore.Load(paths.LibraryFile, Log.Warn);
        _library = new TrainerLibrary(settings, paths, store);
        var steam = new SteamService(_library, settings, paths);
        var art = new ArtService(paths, steam);

        MainWindow? window = null;
        _launcher = new LaunchService(_library, () => window is null ? IntPtr.Zero : new WindowInteropHelper(window).Handle);
        var cheats = new CheatService(_library, _launcher, settings, art, elevated);
        _main = new MainViewModel(new AppServices(_library, art, _launcher, steam, settings, paths, elevated, cheats), Dispatcher);
        string[] args = e.Args;
        _main.RestartElevatedRequested += () => RestartElevated(args);
        theme.Changed += (_, _) => _main.RefreshHints();
        window = new MainWindow(_main, settings);
        MainWindow = window;
        window.Show();
        window.BringToFront();
        window.Activated += (_, _) => _launcher.ScanRunningInBackground();
        _instance.ActivationRequested += () => Dispatcher.BeginInvoke(() => ShowMainWindow(window));
        _tray = new TrayIcon(() => ShowMainWindow(window), () => _overlay?.Open(), ExitApp);
        window.Closing += (_, args) =>
        {
            if (_exiting)
            {
                return;
            }

            switch (settings.Current.CloseButton)
            {
                case CloseButtonBehavior.Tray:
                    args.Cancel = true;
                    HideToTray(window);
                    break;
                case CloseButtonBehavior.Ask:
                    args.Cancel = true;
                    Dispatcher.BeginInvoke(() => AskHowToClose(window));
                    break;
            }
        };
        _main.CloseRequested += (_, _) => window.Close();
        window.HideToTrayRequested += (_, _) => HideToTray(window);
        window.ExitRequested += (_, _) => ExitApp();
        if (relaunchDeclined)
        {
            _main.ShowMessage("Running without administrator rights.");
        }

        if (!settings.Current.AntivirusNoticeDismissed)
        {
            _main.OpenAntivirusNotice();
        }

        _library.Scanned += (_, _) => _ = steam.RefreshAsync();

        _ = _library.RescanAsync();
        _library.StartWatching();

        MainViewModel main = _main;
        var overlay = new OverlayController(main, window);
        _overlay = overlay;
        var background = new BackgroundInput(cheats, settings, _launcher.Running, overlay.Open);
        _controllers = new ControllerInput(
            window,
            (action, kind) => Dispatcher.BeginInvoke(() =>
            {
                if (overlay.IsActive)
                {
                    overlay.Handle(action, InputSource.Controller, kind);
                }
                else
                {
                    main.Handle(action, InputSource.Controller, kind);
                }
            }),
            (name, connected) => Dispatcher.BeginInvoke(() => main.OnControllerConnectionChanged(name, connected)),
            () => background.Wanted,
            background.OnButtons);
        overlay.Controllers = _controllers;
        overlay.Attach();
        main.OpenOverlay = overlay.Open;
        main.SuspendOverlayHotkey = overlay.SuspendHotkey;
        var widgetBridge = new Widget.WidgetBridge(main, () => ShowMainWindow(window));
        _widgetPipe = new Widget.WidgetPipeServer(request => Dispatcher.InvokeAsync(() => widgetBridge.Handle(request)).Task);
        _widgetPipe.Start();
        cheats.ShortcutStateChanged += (_, _) => _controllers?.Wake();
        _launcher.Running.Changed += (_, _) => _controllers?.Wake();
        settings.Changed += (_, _) => _controllers?.Wake();
        _controllers.Start();
    }

    private void RestartElevated(string[] args)
    {
        if (TryRelaunchElevated(args.Where(a => !a.Equals(StartupOptions.ElevatedFlag, StringComparison.OrdinalIgnoreCase)).ToArray()))
        {
            Shutdown();
        }
        else
        {
            _main?.ShowMessage("Still running without administrator rights.");
        }
    }

    private void HideToTray(Window window)
    {
        window.Hide();
        _main?.Library.CloseRail();
        _tray?.ShowHiddenNotice("It keeps listening for your cheat shortcuts and the overlay. Click the tray icon to open it.");
        _controllers?.Wake();
    }

    private void AskHowToClose(MainWindow window)
    {
        ShowMainWindow(window);
        if (_main is null || _main.CurrentPage is CloseChoiceViewModel)
        {
            return;
        }

        _main.Push(new CloseChoiceViewModel(_main, () => HideToTray(window), ExitApp));
    }

    private void ShowMainWindow(MainWindow window)
    {
        if (_tray is not null)
        {
            _tray.Visible = false;
        }

        window.BringToFront();
    }

    private void ExitApp()
    {
        _exiting = true;
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _tray?.Dispose();
        _overlay?.Dispose();
        _widgetPipe?.Dispose();
        _controllers?.Dispose();
        _launcher?.Dispose();
        _library?.Dispose();
        _instance?.Dispose();
        Log.Info("Exiting.");
        base.OnExit(e);
    }

    private void RunSetup(string[] args)
    {
        Log.Initialize(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "TrainerDeck-setup.log"));
        string? testDir = ArgValue(args, "--install-test-dir");
        string userSid = ArgValue(args, "--install-user") ?? Installer.CurrentUserSid();
        if (testDir is null && !SingleInstance.IsElevated)
        {
            ShellResult r = ShellLauncher.Run(Environment.ProcessPath!, $"{InstallLayout.InstallFlag} --install-user {userSid}", null, "runas", IntPtr.Zero);
            r.Process?.Dispose();
            if (r.Error != 0)
            {
                MessageBox.Show("Installing Trainer Deck needs administrator permission.", "Trainer Deck setup", MessageBoxButton.OK, MessageBoxImage.Information);
            }

            Shutdown();
            return;
        }

        ExistingInstall? existing = testDir is null ? Installer.FindExisting() : Installer.FindTestInstall(testDir);
        string target = testDir ?? existing?.Directory ?? InstallLayout.DefaultTargetDirectory(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles));
        new ThemeManager(null).Start();
        var setup = new SetupWindow(target, userSid, systemIntegration: testDir is null, existing);
        MainWindow = setup;
        setup.Show();
    }

    private void RunUninstall(string[] args)
    {
        Log.Initialize(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "TrainerDeck-setup.log"));
        bool quiet = args.Any(a => a.Equals("--quiet", StringComparison.OrdinalIgnoreCase));
        string userSid = ArgValue(args, "--install-user") ?? Installer.CurrentUserSid();
        if (!SingleInstance.IsElevated)
        {
            ShellResult r = ShellLauncher.Run(Environment.ProcessPath!, $"{string.Join(" ", args)} --install-user {userSid}", null, "runas", IntPtr.Zero);
            r.Process?.Dispose();
            Shutdown();
            return;
        }

        string target = AppContext.BaseDirectory.TrimEnd('\\');
        if (!quiet)
        {
            new ThemeManager(null).Start();
            var existing = Installer.FindExisting() ?? new ExistingInstall(target, Installer.Version);
            var window = new SetupWindow(target, userSid, systemIntegration: true, existing, uninstallOnly: true);
            MainWindow = window;
            window.Show();
            return;
        }

        try
        {
            Installer.Uninstall(target, keepTrainers: true);
        }
        catch (Exception ex)
        {
            Log.Error("Uninstall failed", ex);
        }

        Shutdown();
    }

    private static string? ArgValue(string[] args, string name)
    {
        int i = Array.FindIndex(args, a => a.Equals(name, StringComparison.OrdinalIgnoreCase));
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool AllowSetForegroundWindow(int processId);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hwnd);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hwnd);

    private static void BringOtherCopyToFront()
    {
        int self = Environment.ProcessId;
        DateTime deadline = DateTime.UtcNow.AddSeconds(10);
        while (DateTime.UtcNow < deadline)
        {
            foreach (System.Diagnostics.Process p in System.Diagnostics.Process.GetProcessesByName("TrainerDeck"))
            {
                using (p)
                {
                    IntPtr hwnd = p.Id == self ? IntPtr.Zero : p.MainWindowHandle;
                    if (hwnd != IntPtr.Zero && IsWindowVisible(hwnd))
                    {
                        AllowSetForegroundWindow(p.Id);
                        SetForegroundWindow(hwnd);
                        return;
                    }
                }
            }

            Thread.Sleep(150);
        }

        Log.Warn("The administrator copy's window didn't appear in time to bring it to the front.");
    }

    [System.Runtime.InteropServices.DllImport("kernel32.dll")]
    private static extern bool AttachConsole(int processId);

    private static SingleInstance? AcquireInstance(StartupOptions options)
    {
        SingleInstance? instance = SingleInstance.TryAcquire();

        for (int i = 0; instance is null && options.ElevatedRelaunch && i < 30; i++)
        {
            Thread.Sleep(100);
            instance = SingleInstance.TryAcquire();
        }

        return instance;
    }

    private bool TryRelaunchElevated(string[] args)
    {
        string? exe = Environment.ProcessPath;
        if (exe is null)
        {
            return false;
        }

        string arguments = string.Join(" ", args.Append(StartupOptions.ElevatedFlag).Select(a => a.Contains(' ') ? $"\"{a}\"" : a));
        _instance?.Dispose();
        _instance = null;

        ShellResult r = ShellLauncher.Run(exe, arguments, AppContext.BaseDirectory, "runas", IntPtr.Zero);
        r.Process?.Dispose();
        if (r.Error == 0)
        {
            Log.Info("Relaunched as administrator.");
            return true;
        }

        Log.Info(r.Error == LaunchErrors.Cancelled ? "Elevation declined; continuing unelevated." : $"Elevated relaunch failed ({r.Error}).");
        _instance = SingleInstance.TryAcquire();
        return _instance is null;
    }

    private void HookExceptionHandlers()
    {
        DispatcherUnhandledException += (_, args) =>
        {
            Log.Error("Unhandled UI exception", args.Exception);
            _main?.ShowMessage("Something went wrong. Details are in trainerdeck.log.");
            args.Handled = true;
        };
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            Log.Error("Unobserved task exception", args.Exception);
            args.SetObserved();
        };
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            Log.Error("Fatal unhandled exception", args.ExceptionObject as Exception);
    }
}
