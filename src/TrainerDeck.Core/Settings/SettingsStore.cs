using TrainerDeck.Core.Storage;

namespace TrainerDeck.Core.Settings;

public sealed class SettingsStore
{
    public SettingsStore(string path, Action<string>? onWarning = null)
    {
        FilePath = path;
        Current = JsonFile.LoadOrDefault<AppSettings>(path, onWarning);
    }

    public string FilePath { get; }

    public AppSettings Current { get; }

    public event EventHandler? Changed;

    public void Update(Action<AppSettings> change)
    {
        change(Current);
        Save();
    }

    public void Save()
    {
        JsonFile.Save(FilePath, Current);
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
