using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using TrainerDeck.Art;
using TrainerDeck.Core.Diagnostics;
using TrainerDeck.Core.Library;
using TrainerDeck.Core.Settings;
using TrainerDeck.UI;
using TrainerDeck.UI.ViewModels;

namespace TrainerDeck;

public partial class App : Application
{
    private TrainerLibrary? _library;
    private MainViewModel? _main;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        PortablePaths paths = PortablePaths.Resolve(AppContext.BaseDirectory);
        Log.Initialize(paths.LogFile);
        Log.Info($"Data folder: {paths.DataDirectory} (portable: {paths.IsPortable})");
        HookExceptionHandlers();

        var settings = new SettingsStore(paths.SettingsFile, Log.Warn);
        var store = LibraryStore.Load(paths.LibraryFile, Log.Warn);
        _library = new TrainerLibrary(settings, paths, store);
        var art = new ArtService(paths.CacheDirectory);

        _main = new MainViewModel(_library, art, settings, Dispatcher);
        var window = new MainWindow(_main, settings);
        MainWindow = window;
        window.Show();

        _ = _library.RescanAsync();
        _library.StartWatching();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _library?.Dispose();
        Log.Info("Exiting.");
        base.OnExit(e);
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
