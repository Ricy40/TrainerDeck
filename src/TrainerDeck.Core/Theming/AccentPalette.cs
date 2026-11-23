namespace TrainerDeck.Core.Theming;

public sealed record AccentPalette(Rgb Light3, Rgb Light2, Rgb Light1, Rgb Base, Rgb Dark1, Rgb Dark2, Rgb Dark3)
{
    public static readonly AccentPalette WindowsDefault = new(
        Rgb.Parse("#99EBFF"), Rgb.Parse("#4CC2FF"), Rgb.Parse("#0091F8"), Rgb.Parse("#0078D4"),
        Rgb.Parse("#0067C0"), Rgb.Parse("#003E92"), Rgb.Parse("#001A68"));

    public IEnumerable<Rgb> LightestToDarkest => [Light3, Light2, Light1, Base, Dark1, Dark2, Dark3];

    public static AccentPalette FromBase(Rgb accent)
    {
        var (h, s, l) = accent.ToHsl();
        Rgb Lighter(double t) => Rgb.FromHsl(h, s, l + ((1 - l) * t));
        Rgb Darker(double t) => Rgb.FromHsl(h, s, l * (1 - t));
        return new AccentPalette(Lighter(0.66), Lighter(0.40), Lighter(0.12), accent, Darker(0.10), Darker(0.31), Darker(0.51));
    }

    public static AccentPalette? FromRegistry(byte[]? data)
    {
        if (data is null || data.Length < 28)
        {
            return null;
        }

        Rgb At(int i) => new(data[i * 4], data[(i * 4) + 1], data[(i * 4) + 2]);
        return new AccentPalette(At(0), At(1), At(2), At(3), At(4), At(5), At(6));
    }
}

public static class WindowsAccentColors
{
    public static readonly IReadOnlyList<Rgb> All = new[]
    {
        "#FFB900", "#FF8C00", "#F7630C", "#CA5010", "#DA3B01", "#EF6950",
        "#D13438", "#FF4343", "#E74856", "#E81123", "#EA005E", "#C30052",
        "#E3008C", "#BF0077", "#C239B3", "#9A0089", "#0078D4", "#0063B1",
        "#8E8CD8", "#6B69D6", "#8764B8", "#744DA9", "#B146C2", "#881798",
        "#0099BC", "#2D7D9A", "#00B7C3", "#038387", "#00B294", "#018574",
        "#00CC6A", "#10893E", "#7A7574", "#5D5A58", "#68768A", "#515C6B",
        "#567C73", "#486860", "#498205", "#107C10", "#767676", "#4C4A48",
        "#69797E", "#4A5459", "#647C64", "#525E54", "#847545", "#7E735F",
    }.Select(Rgb.Parse).ToArray();
}
