using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Gaming.XboxGameBar;
using Microsoft.UI.Xaml.Controls;
using Windows.Data.Json;
using Windows.System;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace TrainerDeck.Widget
{
    public sealed partial class CheatsWidget : Page
    {
        private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1);

        private readonly DeckClient _client = new DeckClient();
        private readonly ObservableCollection<RowItem> _rows = new ObservableCollection<RowItem>();
        private readonly ObservableCollection<RowItem> _active = new ObservableCollection<RowItem>();
        private readonly DispatcherTimer _poll = new DispatcherTimer();
        private XboxGameBarWidget _widget;
        private bool _busy;
        private bool _online;

        public CheatsWidget()
        {
            InitializeComponent();
            Rows.ItemsSource = _rows;
            _poll.Interval = PollInterval;
            _poll.Tick += async (s, e) => await RefreshAsync();
            Window.Current.VisibilityChanged += OnVisibilityChanged;
            ActualThemeChanged += (s, e) => ApplyBackground();
        }

        // Game Bar hides the widget body behind its own UI when it's only pinned over a game.
        private bool PinnedOnly => _widget != null && _widget.GameBarDisplayMode == XboxGameBarDisplayMode.PinnedOnly;

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            _widget = e.Parameter as XboxGameBarWidget;
            if (_widget != null)
            {
                RequestedTheme = _widget.RequestedTheme;
                _widget.RequestedThemeChanged += (s, a) => OnUi(() => RequestedTheme = s.RequestedTheme);
                _widget.RequestedOpacityChanged += (s, a) => OnUi(ApplyBackground);
                _widget.GameBarDisplayModeChanged += (s, a) => OnUi(ApplyMode);
            }

            ApplyBackground();
            ApplyMode();
            _poll.Start();
            await RefreshAsync();
        }

        private async void OnUi(DispatchedHandler action) => await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, action);

        // The body's background follows the theme and the transparency the user picks in Game Bar.
        private void ApplyBackground()
        {
            bool dark = ActualTheme != ElementTheme.Light;
            Color color = dark ? Color.FromArgb(255, 0x25, 0x28, 0x2C) : Color.FromArgb(255, 0xF3, 0xF4, 0xF6);
            Root.Background = new SolidColorBrush(color) { Opacity = _widget?.RequestedOpacity ?? 1.0 };
        }

        private void ApplyMode()
        {
            bool pinned = PinnedOnly;
            Rows.ItemsSource = pinned ? _active : _rows;
            Rows.IsItemClickEnabled = !pinned;
            UpdateActive();
        }

        private async void OnVisibilityChanged(object sender, VisibilityChangedEventArgs e)
        {
            if (e.Visible)
            {
                _poll.Start();
                await RefreshAsync();
            }
            else
            {
                _poll.Stop();
            }
        }

        private async Task RefreshAsync()
        {
            if (_busy)
            {
                return;
            }

            _busy = true;
            try
            {
                Show(await _client.StateAsync());
            }
            finally
            {
                _busy = false;
            }
        }

        private async void OnRowClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is RowItem row && !row.IsHeader)
            {
                Show(await _client.ActivateAsync(row.Id));
            }
        }

        private async void OnRowKeyDown(object sender, KeyRoutedEventArgs e)
        {
            int delta = e.Key == VirtualKey.Left || e.Key == VirtualKey.GamepadDPadLeft ? -1
                : e.Key == VirtualKey.Right || e.Key == VirtualKey.GamepadDPadRight ? 1
                : 0;
            if (delta == 0 || !(e.OriginalSource is ListViewItem item) || !(Rows.ItemFromContainer(item) is RowItem row) || !row.CanAdjust)
            {
                return;
            }

            e.Handled = true;
            Show(await _client.AdjustAsync(row.Id, delta));
        }

        private async void OnOpenClick(object sender, RoutedEventArgs e) =>
            await Launcher.LaunchUriAsync(new Uri("trainerdeck:open"));

        private void Show(JsonObject state)
        {
            _online = state != null;
            OfflinePanel.Visibility = _online ? Visibility.Collapsed : Visibility.Visible;
            Header.Visibility = _online ? Visibility.Visible : Visibility.Collapsed;
            Rows.Visibility = _online ? Visibility.Visible : Visibility.Collapsed;
            if (!_online)
            {
                int error = _client.LastError;
                OfflineDetail.Text = error == 0 || error == 2
                    ? "Open it, or keep it running in the tray, to control your trainer's cheats from here."
                    : "Trainer Deck is running but the widget couldn't connect (error " + error + ").";
                MessageBar.IsOpen = false;
                PinnedEmpty.Visibility = Visibility.Collapsed;
                _rows.Clear();
                _active.Clear();
                return;
            }

            TrainerText.Text = state.GetNamedString("trainer", "No trainer running");
            StatusText.Text = state.GetNamedString("status", "");
            string message = state.GetNamedString("message", "");
            MessageBar.Message = message;
            MessageBar.Severity = state.GetNamedBoolean("messageIsError", false) ? InfoBarSeverity.Error : InfoBarSeverity.Informational;
            MessageBar.IsOpen = message.Length > 0 && !PinnedOnly;
            UpdateRows(state.GetNamedArray("rows", new JsonArray()));
            UpdateActive();
        }

        private void UpdateRows(JsonArray rows)
        {
            bool same = rows.Count == _rows.Count;
            for (int i = 0; same && i < rows.Count; i++)
            {
                same = _rows[i].Matches(rows.GetObjectAt((uint)i));
            }

            if (same)
            {
                for (int i = 0; i < rows.Count; i++)
                {
                    _rows[i].Update(rows.GetObjectAt((uint)i));
                }

                return;
            }

            _rows.Clear();
            foreach (IJsonValue value in rows)
            {
                _rows.Add(new RowItem(value.GetObject()));
            }
        }

        // Pinned view: only cheats that are switched on, in list order.
        private void UpdateActive()
        {
            var on = _rows.Where(r => r.IsToggle && r.IsOn).ToList();
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                if (!on.Contains(_active[i]))
                {
                    _active.RemoveAt(i);
                }
            }

            for (int i = 0; i < on.Count; i++)
            {
                if (i >= _active.Count || _active[i] != on[i])
                {
                    _active.Remove(on[i]);
                    _active.Insert(i, on[i]);
                }
            }

            PinnedEmpty.Visibility = _online && PinnedOnly && on.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }
    }
}
