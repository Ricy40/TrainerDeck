using System.Windows.Controls;
using System.Windows.Media;

namespace TrainerDeck.UI.Controls;


public sealed class SmoothScroller
{
    private readonly ScrollViewer _viewer;
    private double _from;
    private double _to;
    private TimeSpan _start;
    private bool _running;

    public SmoothScroller(ScrollViewer viewer) => _viewer = viewer;

    public TimeSpan Duration { get; set; } = TimeSpan.FromMilliseconds(220);

    public double Target => _running ? _to : _viewer.VerticalOffset;

    public void ScrollTo(double offset)
    {
        offset = Math.Clamp(offset, 0, _viewer.ScrollableHeight);
        if (Math.Abs(offset - Target) < 0.5)
        {
            return;
        }

        _from = _viewer.VerticalOffset;
        _to = offset;
        _start = TimeSpan.Zero;
        if (!_running)
        {
            _running = true;
            CompositionTarget.Rendering += OnRendering;
        }
    }

    private void OnRendering(object? sender, EventArgs e)
    {
        TimeSpan now = ((RenderingEventArgs)e).RenderingTime;
        if (_start == TimeSpan.Zero)
        {
            _start = now;
        }

        double t = Math.Clamp((now - _start).TotalMilliseconds / Duration.TotalMilliseconds, 0, 1);
        double eased = 1 - Math.Pow(1 - t, 3);
        _viewer.ScrollToVerticalOffset(_from + ((_to - _from) * eased));
        if (t >= 1)
        {
            _running = false;
            CompositionTarget.Rendering -= OnRendering;
        }
    }
}
