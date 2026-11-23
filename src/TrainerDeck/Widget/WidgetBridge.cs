using System.Windows.Threading;
using TrainerDeck.Core.Launch;
using TrainerDeck.Core.Widget;
using TrainerDeck.UI.ViewModels;

namespace TrainerDeck.Widget;

internal sealed class WidgetBridge
{
    private static readonly TimeSpan IdleAfter = TimeSpan.FromSeconds(20);

    private readonly MainViewModel _main;
    private readonly Action _openMainWindow;
    private readonly DispatcherTimer _idle;
    private MainViewModel? _vm;
    private string? _trainerId;
    private DateTime _lastRequest;

    public WidgetBridge(MainViewModel main, Action openMainWindow)
    {
        _main = main;
        _openMainWindow = openMainWindow;
        _idle = new DispatcherTimer(TimeSpan.FromSeconds(5), DispatcherPriority.Background, (_, _) => DropWhenIdle(), main.Dispatcher) { IsEnabled = false };
    }

    public WidgetState Handle(WidgetRequest request)
    {
        _lastRequest = DateTime.UtcNow;
        _idle.Start();
        EnsurePage();

        switch (request.Op)
        {
            case WidgetOps.Activate when Row(request.Row) is { IsSelectable: true } row && Page is { } page:
                page.ActivateRow(row);
                while (_vm is { Depth: > 1 })
                {
                    _vm.Pop();
                }

                break;
            case WidgetOps.Adjust when Row(request.Row) is { CanAdjust: true } row:
                row.Adjust(Math.Sign(request.Delta ?? 1));
                break;
            case WidgetOps.Open:
                _openMainWindow();
                break;
        }

        return Snapshot();
    }

    private CheatsViewModel? Page => _vm?.CurrentPage as CheatsViewModel;

    private RowViewModel? Row(int? id) =>
        Page is { } page && id is int i && i >= 0 && i < page.Rows.Count ? page.Rows[i] : null;

    private void EnsurePage()
    {
        string? newest = _main.Services.Launcher.Running.Snapshot().OrderByDescending(r => r.StartedUtc).FirstOrDefault()?.TrainerId;
        bool stillRunning = _trainerId is not null && _main.Services.Launcher.Running.IsRunning(_trainerId);
        string? target = stillRunning ? _trainerId : newest;
        if (target == _trainerId && _vm is not null)
        {
            return;
        }

        Drop();
        _trainerId = target;
        if (target is not null && _main.Library.CardFor(target) is { } card)
        {
            _vm = new MainViewModel(_main, nav => new CheatsViewModel(nav, card));
        }
    }

    private WidgetState Snapshot()
    {
        if (Page is not { } page)
        {
            return new WidgetState(WidgetProtocol.Version, null, "No trainer is running", Array.Empty<WidgetRow>());
        }

        var rows = new List<WidgetRow>(page.Rows.Count);
        for (int i = 0; i < page.Rows.Count; i++)
        {
            rows.Add(page.Rows[i] switch
            {
                HeaderRow h => new WidgetRow(i, WidgetRowKinds.Header, h.Label),
                CheatRow c => new WidgetRow(i, WidgetRowKinds.Cheat, c.Label, c.Detail, c.HotkeyText, c.IsOn, c.StateKnown, c.Value, c.CanAdjust, c.IsEnabled),
                RowViewModel r => new WidgetRow(i, WidgetRowKinds.Action, r.Label, r.Detail, IsEnabled: r.IsEnabled),
            });
        }

        return new WidgetState(WidgetProtocol.Version, page.Title, page.Subtitle ?? "", rows,
            _vm!.IsMessageVisible ? _vm.Message : null, _vm.IsMessageVisible && _vm.MessageIsError);
    }

    private void DropWhenIdle()
    {
        if (DateTime.UtcNow - _lastRequest > IdleAfter)
        {
            Drop();
            _trainerId = null;
            _idle.Stop();
        }
    }

    private void Drop()
    {
        _vm?.CloseAll();
        _vm = null;
    }
}
