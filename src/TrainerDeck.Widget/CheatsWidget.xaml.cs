using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Microsoft.Gaming.XboxGameBar;
using Windows.Data.Json;
using Windows.System;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Navigation;

namespace TrainerDeck.Widget
{
    public sealed partial class CheatsWidget : Page
    {
        private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1);

        private readonly DeckClient _client = new DeckClient();
        private readonly ObservableCollection<RowItem> _rows = new ObservableCollection<RowItem>();
        private readonly DispatcherTimer _poll = new DispatcherTimer();
        private XboxGameBarWidget _widget;
        private bool _busy;

        public CheatsWidget()
        {
            InitializeComponent();
            Rows.ItemsSource = _rows;
            _poll.Interval = PollInterval;
            _poll.Tick += async (s, e) => await RefreshAsync();
            Window.Current.VisibilityChanged += OnVisibilityChanged;
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            _widget = e.Parameter as XboxGameBarWidget;
            if (_widget != null)
            {
                RequestedTheme = _widget.RequestedTheme;
                _widget.RequestedThemeChanged += OnRequestedThemeChanged;
            }

            _poll.Start();
            await RefreshAsync();
        }

        private async void OnRequestedThemeChanged(XboxGameBarWidget sender, object args) =>
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => RequestedTheme = sender.RequestedTheme);

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
            bool online = state != null;
            OfflinePanel.Visibility = online ? Visibility.Collapsed : Visibility.Visible;
            Rows.Visibility = online ? Visibility.Visible : Visibility.Collapsed;
            if (!online)
            {
                TrainerText.Text = "Trainer Deck";
                StatusText.Text = "";
                MessageBar.Visibility = Visibility.Collapsed;
                _rows.Clear();
                return;
            }

            TrainerText.Text = state.GetNamedString("trainer", "Trainer Deck");
            StatusText.Text = state.GetNamedString("status", "");
            string message = state.GetNamedString("message", "");
            MessageText.Text = message;
            MessageBar.Visibility = string.IsNullOrEmpty(message) ? Visibility.Collapsed : Visibility.Visible;
            UpdateRows(state.GetNamedArray("rows", new JsonArray()));
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
    }
}
