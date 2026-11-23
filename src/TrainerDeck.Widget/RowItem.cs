using System.ComponentModel;
using System.Runtime.CompilerServices;
using Windows.Data.Json;
using Windows.UI.Xaml;

namespace TrainerDeck.Widget
{
    internal sealed class RowItem : INotifyPropertyChanged
    {
        private string _label = "";
        private string _detail = "";
        private bool _isOn;
        private bool _isToggle;
        private string _value = "";
        private bool _canAdjust;
        private bool _isEnabled = true;

        public RowItem(JsonObject row)
        {
            Id = (int)row.GetNamedNumber("id", 0);
            Kind = row.GetNamedString("kind", "action");
            Update(row);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public int Id { get; }

        public string Kind { get; }

        public bool IsHeader => Kind == "header";

        public string Label { get => _label; private set => Set(ref _label, value); }

        public string Detail { get => _detail; private set => Set(ref _detail, value); }

        public bool IsOn { get => _isOn; private set => Set(ref _isOn, value); }

        public bool IsToggle { get => _isToggle; private set => Set(ref _isToggle, value); }

        public string Value { get => _value; private set => Set(ref _value, value); }

        public bool CanAdjust { get => _canAdjust; private set => Set(ref _canAdjust, value); }

        public double ItemOpacity => _isEnabled ? 1.0 : 0.4;

        public Visibility HeaderVisibility => IsHeader ? Visibility.Visible : Visibility.Collapsed;

        public Visibility ItemVisibility => IsHeader ? Visibility.Collapsed : Visibility.Visible;

        public Visibility DetailVisibility => string.IsNullOrEmpty(Detail) ? Visibility.Collapsed : Visibility.Visible;

        public Visibility ValueVisibility => string.IsNullOrEmpty(Value) ? Visibility.Collapsed : Visibility.Visible;

        public Visibility StepperVisibility => CanAdjust ? Visibility.Visible : Visibility.Collapsed;

        public Visibility SwitchVisibility => IsToggle ? Visibility.Visible : Visibility.Collapsed;

        public Visibility GlyphVisibility => !IsHeader && !IsToggle ? Visibility.Visible : Visibility.Collapsed;

        public bool Matches(JsonObject row) => (int)row.GetNamedNumber("id", -1) == Id && row.GetNamedString("label", "") == Label;

        public void Update(JsonObject row)
        {
            Label = row.GetNamedString("label", "");
            string detail = row.GetNamedString("detail", "");
            string hotkey = row.GetNamedString("hotkey", "");
            Detail = string.IsNullOrEmpty(hotkey) ? detail : string.IsNullOrEmpty(detail) ? hotkey : hotkey + "  ·  " + detail;
            IsOn = row.GetNamedBoolean("isOn", false);
            IsToggle = row.GetNamedBoolean("isToggle", false);
            Value = row.GetNamedString("value", "");
            CanAdjust = row.GetNamedBoolean("canAdjust", false);
            bool enabled = row.GetNamedBoolean("isEnabled", true);
            if (enabled != _isEnabled)
            {
                _isEnabled = enabled;
                Raise(nameof(ItemOpacity));
            }
        }

        private void Set<T>(ref T field, T value)
        {
            if (Equals(field, value))
            {
                return;
            }

            field = value;
            Raise(string.Empty);
        }

        private void Raise([CallerMemberName] string name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
