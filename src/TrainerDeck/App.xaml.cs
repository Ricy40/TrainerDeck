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
using TrainerDeck.Launch;
using TrainerDeck.Steam;
using TrainerDeck.Core.Launch;
using TrainerDeck.Core.Library;
using TrainerDeck.Core.Settings;
using TrainerDeck.UI;
using TrainerDeck.UI.ViewModels;

namespace TrainerDeck;

public partial class App : Application
{
    private TrainerLibrary? _library;
    private MainViewModel? _main;
    private ControllerInput? _controllers;
    private LaunchService? _launcher;
    private SingleInstance? _instance;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        if (e.Args.Length >= 1 && e.Args[0].Equals("--export-art", StringComparison.OrdinalIgnoreCase))
        {
            string folder = e.Args.Length >= 2 ? e.Args[1] : Path.Combine(AppContext.BaseDirectory, "art");
            BrandArt.Export(folder);
            Shutdown();
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
        window = new MainWindow(_main, settings);
        MainWindow = window;
        window.Show();
        window.Activated += (_, _) => _launcher.ScanRunningInBackground();
        _instance.ActivationRequested += () => Dispatcher.BeginInvoke(window.BringToFront);
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
        _controllers = new ControllerInput(
            window,
            (action, kind) => Dispatcher.BeginInvoke(() => main.Handle(action, InputSource.Controller, kind)),
            (name, connected) => Dispatcher.BeginInvoke(() => main.OnControllerConnectionChanged(name, connected)),
            () => cheats.ActiveShortcutModifier,
            cheats.OnChord);
        cheats.ShortcutStateChanged += (_, _) => _controllers?.Wake();
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

    protected override void OnExit(ExitEventArgs e)
    {
        _controllers?.Dispose();
        _launcher?.Dispose();
        _library?.Dispose();
        _instance?.Dispose();
        Log.Info("Exiting.");
        base.OnExit(e);
    }

    private static SingleInstance? AcquireInstance(StartupOptions options)
    {
        SingleInstance? instance = SingleInstance.TryAcquire();

        // The unelevated copy that relaunched us may still be shutting down.
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
