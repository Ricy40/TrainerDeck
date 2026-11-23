using System.Globalization;

namespace TrainerDeck.Core.Theming;

public readonly record struct Rgb(byte R, byte G, byte B)
{
    public static readonly Rgb Black = new(0, 0, 0);
    public static readonly Rgb White = new(255, 255, 255);

    public string ToHex() => $"#{R:X2}{G:X2}{B:X2}";

    public override string ToString() => ToHex();

    public static bool TryParse(string? text, out Rgb rgb)
    {
        rgb = default;
        string s = (text ?? "").Trim().TrimStart('#');
        if (s.Length == 3)
        {
            s = string.Concat(s.Select(c => new string(c, 2)));
        }

        if (s.Length != 6 || !int.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int v))
        {
            return false;
        }

        rgb = new Rgb((byte)(v >> 16), (byte)(v >> 8), (byte)v);
        return true;
    }

    public static Rgb Parse(string text) =>
        TryParse(text, out Rgb rgb) ? rgb : throw new FormatException($"Not a colour: {text}");

    public double Luminance
    {
        get
        {
            static double Channel(byte c)
            {
                double v = c / 255.0;
                return v <= 0.03928 ? v / 12.92 : Math.Pow((v + 0.055) / 1.055, 2.4);
            }

            return (0.2126 * Channel(R)) + (0.7152 * Channel(G)) + (0.0722 * Channel(B));
        }
    }

    public static double Contrast(Rgb a, Rgb b)
    {
        double la = a.Luminance, lb = b.Luminance;
        return (Math.Max(la, lb) + 0.05) / (Math.Min(la, lb) + 0.05);
    }

    public Rgb Mix(Rgb other, double amount)
    {
        amount = Math.Clamp(amount, 0, 1);
        static byte Lerp(byte a, byte b, double t) => (byte)Math.Round(a + ((b - a) * t));
        return new Rgb(Lerp(R, other.R, amount), Lerp(G, other.G, amount), Lerp(B, other.B, amount));
    }

    public (double H, double S, double L) ToHsl()
    {
        double r = R / 255.0, g = G / 255.0, b = B / 255.0;
        double max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b));
        double l = (max + min) / 2, h = 0, s = 0;
        double d = max - min;
        if (d > 0)
        {
            s = l > 0.5 ? d / (2 - max - min) : d / (max + min);
            h = max == r ? ((g - b) / d) + (g < b ? 6 : 0)
                : max == g ? ((b - r) / d) + 2
                : ((r - g) / d) + 4;
            h *= 60;
        }

        return (h, s, l);
    }

    public static Rgb FromHsl(double h, double s, double l)
    {
        h = ((h % 360) + 360) % 360;
        s = Math.Clamp(s, 0, 1);
        l = Math.Clamp(l, 0, 1);
        double c = (1 - Math.Abs((2 * l) - 1)) * s;
        double x = c * (1 - Math.Abs((h / 60 % 2) - 1));
        double m = l - (c / 2);
        (double r, double g, double b) = h switch
        {
            < 60 => (c, x, 0.0),
            < 120 => (x, c, 0.0),
            < 180 => (0.0, c, x),
            < 240 => (0.0, x, c),
            < 300 => (x, 0.0, c),
            _ => (c, 0.0, x),
        };
        static byte B(double v) => (byte)Math.Round(Math.Clamp(v, 0, 1) * 255);
        return new Rgb(B(r + m), B(g + m), B(b + m));
    }
}
