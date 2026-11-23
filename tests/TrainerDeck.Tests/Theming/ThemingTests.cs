using TrainerDeck.Core.Theming;

namespace TrainerDeck.Tests.Theming;

public class RgbTests
{
    [Theory]
    [InlineData("#0078D4", 0x00, 0x78, 0xD4)]
    [InlineData("4cc2ff", 0x4C, 0xC2, 0xFF)]
    [InlineData(" #fff ", 0xFF, 0xFF, 0xFF)]
    public void Parses_hex(string text, byte r, byte g, byte b)
    {
        Assert.True(Rgb.TryParse(text, out Rgb c));
        Assert.Equal(new Rgb(r, g, b), c);
    }

    [Theory]
    [InlineData("")]
    [InlineData("#12345")]
    [InlineData("blue")]
    [InlineData(null)]
    public void Rejects_bad_hex(string? text) => Assert.False(Rgb.TryParse(text, out _));

    [Fact]
    public void Contrast_matches_wcag_reference_values()
    {
        Assert.Equal(21.0, Rgb.Contrast(Rgb.Black, Rgb.White), 1);
        Assert.Equal(1.0, Rgb.Contrast(Rgb.White, Rgb.White), 3);
        Assert.Equal(4.54, Rgb.Contrast(Rgb.Parse("#767676"), Rgb.White), 2);
    }

    [Theory]
    [InlineData("#0078D4")]
    [InlineData("#FFB900")]
    [InlineData("#767676")]
    [InlineData("#E81123")]
    public void Hsl_round_trips(string hex)
    {
        Rgb c = Rgb.Parse(hex);
        var (h, s, l) = c.ToHsl();
        Rgb back = Rgb.FromHsl(h, s, l);
        Assert.InRange(Math.Abs(back.R - c.R), 0, 1);
        Assert.InRange(Math.Abs(back.G - c.G), 0, 1);
        Assert.InRange(Math.Abs(back.B - c.B), 0, 1);
    }

    [Fact]
    public void Mix_blends_linearly() =>
        Assert.Equal(new Rgb(128, 128, 128), Rgb.Black.Mix(Rgb.White, 0.5));
}

public class AccentPaletteTests
{
    [Theory]
    [InlineData("#0078D4")]
    [InlineData("#FFB900")]
    [InlineData("#107C10")]
    [InlineData("#881798")]
    public void Generated_shades_go_from_light_to_dark_with_the_same_hue(string hex)
    {
        AccentPalette p = AccentPalette.FromBase(Rgb.Parse(hex));
        double[] lightness = p.LightestToDarkest.Select(c => c.ToHsl().L).ToArray();
        for (int i = 1; i < lightness.Length; i++)
        {
            Assert.True(lightness[i] < lightness[i - 1], $"shade {i} should be darker than shade {i - 1}");
        }

        double hue = Rgb.Parse(hex).ToHsl().H;
        Assert.All(p.LightestToDarkest.Where(c => c.ToHsl().S > 0.1), c => Assert.InRange(Math.Abs(c.ToHsl().H - hue), 0, 3));
        Assert.Equal(Rgb.Parse(hex), p.Base);
    }

    [Fact]
    public void Generated_blue_is_close_to_windows_own_palette()
    {
        AccentPalette p = AccentPalette.FromBase(AccentPalette.WindowsDefault.Base);
        Assert.InRange(Math.Abs(p.Dark1.ToHsl().L - AccentPalette.WindowsDefault.Dark1.ToHsl().L), 0, 0.03);
        Assert.InRange(Math.Abs(p.Light2.ToHsl().L - AccentPalette.WindowsDefault.Light2.ToHsl().L), 0, 0.03);
    }

    [Fact]
    public void Reads_the_registry_palette()
    {
        byte[] data = AccentPalette.WindowsDefault.LightestToDarkest
            .SelectMany(c => new byte[] { c.R, c.G, c.B, 0 })
            .Concat(new byte[4])
            .ToArray();
        Assert.Equal(AccentPalette.WindowsDefault, AccentPalette.FromRegistry(data));
        Assert.Null(AccentPalette.FromRegistry([1, 2, 3]));
    }

    [Fact]
    public void Offers_the_48_windows_swatches() => Assert.Equal(48, WindowsAccentColors.All.Distinct().Count());
}

public class ThemeResolverTests
{
    [Theory]
    [InlineData("#F5C21B", "#F3F3F3")]
    [InlineData("#6CCB2E", "#F3F3F3")]
    [InlineData("#3E9BF2", "#202020")]
    [InlineData("#1B1B1B", "#0D0F13")]
    public void EnsureContrast_reaches_the_minimum(string color, string background)
    {
        Rgb bg = Rgb.Parse(background);
        Rgb result = ThemeResolver.EnsureContrast(Rgb.Parse(color), bg, 3.0);
        Assert.True(Rgb.Contrast(result, bg) >= 3.0, $"{result} on {bg}");
    }

    [Fact]
    public void EnsureContrast_leaves_readable_colours_alone()
    {
        Rgb green = Rgb.Parse("#6CCB2E");
        Assert.Equal(green, ThemeResolver.EnsureContrast(green, ThemeResolver.BrandBackground, 3.0));
    }

    [Fact]
    public void Default_is_the_brand_look_whatever_windows_uses()
    {
        ResolvedTheme t = ThemeResolver.Resolve(AppearanceMode.TrainerDeck, BaseTheme.Light, "#E81123", systemUsesLightTheme: true, AccentPalette.FromBase(Rgb.Parse("#107C10")));
        Assert.Equal(NeutralSet.BrandDark, t.Neutrals);
        Assert.Equal(Rgb.Parse("#4CC2FF"), t.AccentFill);
        Assert.True(t.IsDark);
    }

    [Theory]
    [InlineData(true, NeutralSet.Light)]
    [InlineData(false, NeutralSet.Dark)]
    public void Windows_mode_follows_the_system(bool systemLight, NeutralSet expected)
    {
        AccentPalette green = AccentPalette.FromBase(Rgb.Parse("#107C10"));
        ResolvedTheme t = ThemeResolver.Resolve(AppearanceMode.Windows, BaseTheme.Dark, null, systemLight, green);
        Assert.Equal(expected, t.Neutrals);
        Assert.Equal(green, t.Palette);
        Assert.Equal(systemLight ? green.Dark1 : green.Light2, t.AccentFill);
    }

    [Theory]
    [InlineData(BaseTheme.Dark, true, NeutralSet.Dark)]
    [InlineData(BaseTheme.Light, false, NeutralSet.Light)]
    [InlineData(BaseTheme.System, true, NeutralSet.Light)]
    [InlineData(BaseTheme.System, false, NeutralSet.Dark)]
    public void Custom_mode_uses_the_chosen_base(BaseTheme chosen, bool systemLight, NeutralSet expected) =>
        Assert.Equal(expected, ThemeResolver.Resolve(AppearanceMode.Custom, chosen, "#E81123", systemLight, null).Neutrals);

    [Fact]
    public void Bad_custom_accent_falls_back_to_the_default_blue() =>
        Assert.Equal(AccentPalette.WindowsDefault, ThemeResolver.Resolve(AppearanceMode.Custom, BaseTheme.Dark, "nope", false, null).Palette);

    [Theory]
    [InlineData("#FFB900")]
    [InlineData("#767676")]
    [InlineData("#4C4A48")]
    [InlineData("#FFFFFF")]
    [InlineData("#000000")]
    [InlineData("#00CC6A")]
    public void Accent_shades_stay_readable(string accent)
    {
        foreach (NeutralSet n in Enum.GetValues<NeutralSet>())
        {
            ResolvedTheme t = ThemeResolver.Build(n, AccentPalette.FromBase(Rgb.Parse(accent)));
            Assert.True(Rgb.Contrast(t.AccentFill, t.Background) >= 3.0, $"{accent} fill on {n}");
            Assert.True(Rgb.Contrast(t.AccentText, t.Background) >= 4.5, $"{accent} text on {n}");
            Assert.True(Rgb.Contrast(t.TextOnAccent, t.AccentFill) >= 4.5, $"text on {accent} fill on {n}");
        }
    }
}
