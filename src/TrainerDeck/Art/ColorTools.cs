using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace TrainerDeck.Art;

internal static class ColorTools
{
    public static Color DominantColor(BitmapSource source)
    {
        BitmapSource bmp = source.Format == PixelFormats.Bgra32 ? source : new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
        int w = bmp.PixelWidth, h = bmp.PixelHeight, stride = w * 4;
        var px = new byte[stride * h];
        bmp.CopyPixels(px, stride, 0);

        double r = 0, g = 0, b = 0, total = 0;
        for (int i = 0; i < px.Length; i += 4 * 3) 
        {
            byte a = px[i + 3];
            if (a < 160)
            {
                continue;
            }

            double pb = px[i] / 255.0, pg = px[i + 1] / 255.0, pr = px[i + 2] / 255.0;
            double max = Math.Max(pr, Math.Max(pg, pb)), min = Math.Min(pr, Math.Min(pg, pb));
            double sat = max <= 0 ? 0 : (max - min) / max;
            double weight = 0.05 + (sat * sat) * (0.3 + max);
            r += pr * weight;
            g += pg * weight;
            b += pb * weight;
            total += weight;
        }

        if (total <= 0)
        {
            return Color.FromRgb(0x3A, 0x44, 0x58);
        }

        return Color.FromRgb((byte)(r / total * 255), (byte)(g / total * 255), (byte)(b / total * 255));
    }

    public static Color FromName(string name)
    {
        int hash = 17;
        foreach (char c in name.ToUpperInvariant())
        {
            hash = unchecked((hash * 31) + c);
        }

        double hue = (uint)hash % 360;
        return FromHsl(hue, 0.45, 0.42);
    }

    public static Brush CardGradient(Color accent)
    {
        ToHsl(accent, out double hue, out double sat, out double light);
        sat = Math.Clamp(sat, 0.25, 0.75);
        var top = FromHsl(hue, sat, Math.Clamp(light, 0.32, 0.46));
        var bottom = FromHsl(hue, sat * 0.85, 0.12);
        var brush = new LinearGradientBrush(top, bottom, new System.Windows.Point(0.2, 0), new System.Windows.Point(0.6, 1));
        brush.Freeze();
        return brush;
    }

    private static void ToHsl(Color c, out double h, out double s, out double l)
    {
        double r = c.R / 255.0, g = c.G / 255.0, b = c.B / 255.0;
        double max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b));
        l = (max + min) / 2;
        if (max == min)
        {
            h = s = 0;
            return;
        }

        double d = max - min;
        s = l > 0.5 ? d / (2 - max - min) : d / (max + min);
        if (max == r)
        {
            h = ((g - b) / d) + (g < b ? 6 : 0);
        }
        else if (max == g)
        {
            h = ((b - r) / d) + 2;
        }
        else
        {
            h = ((r - g) / d) + 4;
        }

        h *= 60;
    }

    private static Color FromHsl(double h, double s, double l)
    {
        double c = (1 - Math.Abs((2 * l) - 1)) * s;
        double x = c * (1 - Math.Abs((h / 60 % 2) - 1));
        double m = l - (c / 2);
        (double r, double g, double b) = (h % 360) switch
        {
            < 60 => (c, x, 0d),
            < 120 => (x, c, 0d),
            < 180 => (0d, c, x),
            < 240 => (0d, x, c),
            < 300 => (x, 0d, c),
            _ => (c, 0d, x),
        };
        return Color.FromRgb((byte)((r + m) * 255), (byte)((g + m) * 255), (byte)((b + m) * 255));
    }
}
