using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;
using TrainerDeck.Core.Diagnostics;
using TrainerDeck.Core.Settings;
using TrainerDeck.Core.Theming;
using TrainerDeck.UI.Native;

namespace TrainerDeck.UI.Theme;


public sealed class ThemeManager
{
    private const string PersonalizeKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    private const string AccentKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\Accent";

    private readonly SettingsStore? _settings;
    private ResourceDictionary? _resources;

    public ThemeManager(SettingsStore? settings)
    {
        _settings = settings;
        Instance = this;
    }

    public static ThemeManager? Instance { get; private set; }

    public AppearanceMode Mode => _settings?.Current.Appearance ?? AppearanceMode.TrainerDeck;

    public ResolvedTheme Current { get; private set; } = ThemeResolver.Build(NeutralSet.BrandDark, AccentPalette.WindowsDefault);

    public event EventHandler? Changed;

    public static bool SystemUsesLightTheme =>
        Registry.CurrentUser.OpenSubKey(PersonalizeKey)?.GetValue("AppsUseLightTheme") is int v && v == 1;

    public static AccentPalette? SystemAccentPalette =>
        AccentPalette.FromRegistry(Registry.CurrentUser.OpenSubKey(AccentKey)?.GetValue("AccentPalette") as byte[]);

    public void Start()
    {
        Refresh(force: true);
        if (_settings is null)
        {
            return;
        }

        _settings.Changed += (_, _) => Application.Current.Dispatcher.BeginInvoke(() => Refresh());

        SystemEvents.UserPreferenceChanged += (_, e) =>
        {
            if (e.Category is UserPreferenceCategory.General or UserPreferenceCategory.Color or UserPreferenceCategory.VisualStyle)
            {
                Application.Current.Dispatcher.BeginInvoke(() => Refresh());
            }
        };
    }

    public void Refresh(bool force = false)
    {
        AppSettings s = _settings?.Current ?? new AppSettings();
        ResolvedTheme next = ThemeResolver.Resolve(s.Appearance, s.CustomTheme, s.CustomAccent, SystemUsesLightTheme, SystemAccentPalette);
        if (!force && next == Current)
        {
            return;
        }

        Current = next;
        Apply(next);
        Log.Info($"Theme: {s.Appearance}, {next.Neutrals}, accent {next.AccentFill}.");
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public Action Attach(Window window, Func<bool>? fullScreen = null)
    {
        void Paint() => WindowBackdrop.Apply(window, Current, WindowBackdrop.WantsMica(Mode, fullScreen?.Invoke() ?? false));
        EventHandler onChanged = (_, _) => Paint();
        window.SourceInitialized += (_, _) => Paint();
        Changed += onChanged;
        window.Closed += (_, _) => Changed -= onChanged;
        return Paint;
    }

    private void Apply(ResolvedTheme t)
    {
        var d = new ResourceDictionary();
        Neutrals n = Neutrals.For(t.Neutrals);

        void Pair(string name, Color c)
        {
            d[name + "Color"] = c;
            var b = new SolidColorBrush(c);
            b.Freeze();
            d[name + "Brush"] = b;
        }

        Color bg = ToColor(t.Background);
        Pair("Background", bg);
        Pair("Surface", n.Surface);
        Pair("SurfaceHigh", n.SurfaceHigh);
        Pair("SurfaceHigher", n.SurfaceHigher);
        Pair("Border", n.Border);
        Pair("TextPrimary", n.Text);
        Pair("TextSecondary", n.Text2);
        Pair("TextTertiary", n.Text3);
        Pair("Danger", n.Danger);
        Pair("Accent", ToColor(t.AccentFill));
        Pair("AccentDim", ToColor(t.Background.Mix(t.AccentFill, t.IsDark ? 0.28 : 0.18)));
        Pair("AccentText", ToColor(t.AccentText));
        Pair("TextOnAccent", ToColor(t.TextOnAccent));

        Pair("Favourite", Color.FromRgb(0xFF, 0xC8, 0x3D));
        Pair("Running", Color.FromRgb(0x3D, 0xDC, 0x84));

        Pair("OverlayPanel", WithAlpha(bg, 0xF2));
        Pair("Scrim", WithAlpha(bg, 0x99));
        d["HeroFadeTopColor"] = WithAlpha(bg, 0x40);
        d["HeroFadeMidColor"] = WithAlpha(bg, 0xB0);

        Pair("TextFillColorPrimary", n.Text);
        Pair("TextFillColorSecondary", n.Text2);
        Pair("TextFillColorTertiary", n.Text3);
        Pair("CardBackgroundFillColorDefault", n.CardFill);
        Pair("CardBackgroundFillColorSecondary", n.CardFill2);
        Pair("CardStrokeColorDefault", n.CardStroke);
        Pair("ControlFillColorDefault", n.ControlFill);
        Pair("ControlFillColorSecondary", n.ControlFill2);
        Pair("ControlStrokeColorDefault", n.ControlStroke);
        Pair("SubtleFillColorSecondary", n.Subtle2);
        Pair("SubtleFillColorTertiary", n.Subtle3);
        Pair("DividerStrokeColorDefault", n.Divider);
        Pair("SolidBackgroundFillColorBase", bg);
        Pair("AccentFillColorDefault", ToColor(t.AccentFill));
        Pair("AccentTextFillColorPrimary", ToColor(t.AccentText));
        Pair("TextOnAccentFillColorPrimary", ToColor(t.TextOnAccent));

        var merged = Application.Current.Resources.MergedDictionaries;
        if (_resources is not null)
        {
            merged.Remove(_resources);
        }

        merged.Add(d);
        _resources = d;
    }

    private static Color ToColor(Rgb c) => Color.FromRgb(c.R, c.G, c.B);

    private static Color WithAlpha(Color c, byte a) => Color.FromArgb(a, c.R, c.G, c.B);

    private static Color Hex(string s) => (Color)ColorConverter.ConvertFromString(s);

    private sealed record Neutrals(
        Color Surface, Color SurfaceHigh, Color SurfaceHigher, Color Border,
        Color Text, Color Text2, Color Text3, Color Danger,
        Color CardFill, Color CardFill2, Color CardStroke, Color ControlFill, Color ControlFill2, Color ControlStroke,
        Color Subtle2, Color Subtle3, Color Divider)
    {
        private static readonly Neutrals Brand = new(
            Hex("#151820"), Hex("#1E222D"), Hex("#272C3A"), Hex("#2C3240"),
            Hex("#F3F5F9"), Hex("#A3AAB8"), Hex("#6E7686"), Hex("#FF6B6B"),
            Hex("#0DFFFFFF"), Hex("#08FFFFFF"), Hex("#1A000000"), Hex("#0FFFFFFF"), Hex("#15FFFFFF"), Hex("#14FFFFFF"),
            Hex("#0FFFFFFF"), Hex("#0AFFFFFF"), Hex("#15FFFFFF"));

        private static readonly Neutrals Dark = new(
            Hex("#2B2B2B"), Hex("#333333"), Hex("#3C3C3C"), Hex("#3F3F3F"),
            Hex("#FFFFFF"), Hex("#C5C5C5"), Hex("#8A8A8A"), Hex("#FF99A4"),
            Hex("#0DFFFFFF"), Hex("#08FFFFFF"), Hex("#1A000000"), Hex("#0FFFFFFF"), Hex("#15FFFFFF"), Hex("#12FFFFFF"),
            Hex("#0FFFFFFF"), Hex("#0AFFFFFF"), Hex("#15FFFFFF"));

        private static readonly Neutrals Light = new(
            Hex("#FBFBFB"), Hex("#EAEAEA"), Hex("#DFDFDF"), Hex("#D6D6D6"),
            Hex("#1B1B1B"), Hex("#5D5D5D"), Hex("#8A8A8A"), Hex("#C42B1C"),
            Hex("#B3FFFFFF"), Hex("#80F6F6F6"), Hex("#0F000000"), Hex("#B3FFFFFF"), Hex("#80F9F9F9"), Hex("#0F000000"),
            Hex("#09000000"), Hex("#06000000"), Hex("#0F000000"));

        public static Neutrals For(NeutralSet set) => set switch
        {
            NeutralSet.Dark => Dark,
            NeutralSet.Light => Light,
            _ => Brand,
        };
    }
}
