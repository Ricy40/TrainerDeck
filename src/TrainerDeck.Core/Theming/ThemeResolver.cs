namespace TrainerDeck.Core.Theming;

public enum AppearanceMode
{
    TrainerDeck,

    Windows,

    Custom,
}

public enum BaseTheme
{
    Dark,
    Light,
    System,
}

public enum NeutralSet
{
    BrandDark,
    Dark,
    Light,
}

public sealed record ResolvedTheme(
    NeutralSet Neutrals,
    AccentPalette Palette,
    Rgb AccentFill,
    Rgb AccentText,
    Rgb TextOnAccent,
    Rgb Background)
{
    public bool IsDark => Neutrals != NeutralSet.Light;
}

public static class ThemeResolver
{
    public static readonly Rgb BrandBackground = Rgb.Parse("#0D0F13");
    public static readonly Rgb DarkBackground = Rgb.Parse("#202020");
    public static readonly Rgb LightBackground = Rgb.Parse("#F3F3F3");

    public static ResolvedTheme Resolve(
        AppearanceMode mode,
        BaseTheme customBase,
        string? customAccent,
        bool systemUsesLightTheme,
        AccentPalette? systemPalette)
    {
        NeutralSet neutrals;
        AccentPalette palette;
        switch (mode)
        {
            case AppearanceMode.Windows:
                neutrals = systemUsesLightTheme ? NeutralSet.Light : NeutralSet.Dark;
                palette = systemPalette ?? AccentPalette.WindowsDefault;
                break;
            case AppearanceMode.Custom:
                bool light = customBase == BaseTheme.Light || (customBase == BaseTheme.System && systemUsesLightTheme);
                neutrals = light ? NeutralSet.Light : NeutralSet.Dark;
                palette = Rgb.TryParse(customAccent, out Rgb accent) ? AccentPalette.FromBase(accent) : AccentPalette.WindowsDefault;
                break;
            default:
                neutrals = NeutralSet.BrandDark;
                palette = AccentPalette.WindowsDefault;
                break;
        }

        return Build(neutrals, palette);
    }

    public static Rgb EnsureContrast(Rgb color, Rgb background, double minimum)
    {
        Rgb toward = background.Luminance > 0.5 ? new Rgb(0, 0, 0) : new Rgb(255, 255, 255);
        Rgb result = color;
        for (int step = 1; step <= 10 && Rgb.Contrast(result, background) < minimum; step++)
        {
            result = color.Mix(toward, step / 10.0);
        }

        return result;
    }

    public static ResolvedTheme Build(NeutralSet neutrals, AccentPalette palette)
    {
        Rgb background = neutrals switch
        {
            NeutralSet.BrandDark => BrandBackground,
            NeutralSet.Dark => DarkBackground,
            _ => LightBackground,
        };
        bool dark = neutrals != NeutralSet.Light;

        Rgb[] fillOrder = dark
            ? [palette.Light2, palette.Light3, Rgb.White]
            : [palette.Dark1, palette.Dark2, palette.Dark3, Rgb.Black];
        Rgb fill = FirstWithContrast(fillOrder, background, 3.0);

        Rgb[] textOrder = dark
            ? [palette.Light3, palette.Light2, Rgb.White]
            : [palette.Dark2, palette.Dark3, Rgb.Black];
        Rgb text = FirstWithContrast(textOrder, background, 4.5);

        Rgb onAccent = Rgb.Contrast(Rgb.Black, fill) >= Rgb.Contrast(Rgb.White, fill) ? Rgb.Black : Rgb.White;
        return new ResolvedTheme(neutrals, palette, fill, text, onAccent, background);
    }

    private static Rgb FirstWithContrast(Rgb[] candidates, Rgb background, double minimum) =>
        candidates.FirstOrDefault(c => Rgb.Contrast(c, background) >= minimum, candidates[^1]);
}
