using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace TrainerDeck.Art;


public static class BrandArt
{
    private static readonly Color Background = Color.FromRgb(0x0D, 0x0F, 0x13);
    private static readonly Color Surface = Color.FromRgb(0x1E, 0x22, 0x2D);
    private static readonly Color SurfaceHigh = Color.FromRgb(0x2C, 0x32, 0x40);
    private static readonly Color Accent = Color.FromRgb(0x4C, 0xC2, 0xFF);
    private static readonly Color AccentDeep = Color.FromRgb(0x1F, 0x8F, 0xD6);
    private static readonly Color Ink = Color.FromRgb(0x06, 0x12, 0x1A);

    public static void Export(string folder)
    {
        Directory.CreateDirectory(folder);
        SavePng(Render(600, 900, DrawCover), Path.Combine(folder, "cover-600x900.png"));
        SavePng(Render(1920, 1080, DrawHero), Path.Combine(folder, "hero-1920x1080.png"));
        SavePng(Render(512, 512, (dc, w, h) => DrawLogo(dc, new Rect(0, 0, w, h))), Path.Combine(folder, "logo-512.png"));
        SaveIcon(Path.Combine(folder, "TrainerDeck.ico"));
    }

    public static void DrawLogo(DrawingContext dc, Rect bounds)
    {
        double s = Math.Min(bounds.Width, bounds.Height);
        double cx = bounds.X + (bounds.Width / 2), cy = bounds.Y + (bounds.Height / 2);
        double cw = s * 0.56, ch = s * 0.74, r = s * 0.09;
        Rect card = new(cx - (cw / 2), cy - (ch / 2) + (s * 0.03), cw, ch);

        DrawCard(dc, card, r, -14, new Point(cx, cy + (s * 0.4)), new SolidColorBrush(SurfaceHigh));
        DrawCard(dc, card, r, 14, new Point(cx, cy + (s * 0.4)), new SolidColorBrush(Surface));
        var accent = new LinearGradientBrush(Accent, AccentDeep, new Point(0, 0), new Point(1, 1));
        DrawCard(dc, card, r, 0, new Point(cx, cy), accent);

        double t = s * 0.2;
        var play = new StreamGeometry();
        using (StreamGeometryContext g = play.Open())
        {
            double px = cx - (t * 0.38), py = card.Y + (card.Height * 0.47);
            g.BeginFigure(new Point(px, py - (t / 2)), true, true);
            g.LineTo(new Point(px + (t * 0.87), py), true, false);
            g.LineTo(new Point(px, py + (t / 2)), true, false);
        }

        play.Freeze();
        dc.DrawGeometry(new SolidColorBrush(Ink), new Pen(new SolidColorBrush(Ink), s * 0.03) { LineJoin = PenLineJoin.Round }, play);
    }

    private static void DrawCard(DrawingContext dc, Rect card, double radius, double angle, Point pivot, Brush fill)
    {
        dc.PushTransform(new RotateTransform(angle, pivot.X, pivot.Y));
        dc.DrawRoundedRectangle(fill, new Pen(new SolidColorBrush(Color.FromArgb(0x50, 0, 0, 0)), radius * 0.12), card, radius, radius);
        dc.Pop();
    }

    private static void DrawBackdrop(DrawingContext dc, double w, double h, Point glowCenter, double glowRadius)
    {
        dc.DrawRectangle(new LinearGradientBrush(Color.FromRgb(0x15, 0x18, 0x20), Background, 90), null, new Rect(0, 0, w, h));
        var glow = new RadialGradientBrush(Color.FromArgb(0x55, Accent.R, Accent.G, Accent.B), Color.FromArgb(0, Accent.R, Accent.G, Accent.B))
        {
            Center = new Point(glowCenter.X / w, glowCenter.Y / h),
            GradientOrigin = new Point(glowCenter.X / w, glowCenter.Y / h),
            RadiusX = glowRadius / w,
            RadiusY = glowRadius / h,
        };
        dc.DrawRectangle(glow, null, new Rect(0, 0, w, h));
    }

    private static void DrawCardPattern(DrawingContext dc, Rect area, double cardWidth, double opacity)
    {
        double ch = cardWidth * 1.4, gap = cardWidth * 0.16;
        var pen = new Pen(new SolidColorBrush(Color.FromArgb((byte)(255 * opacity), 0x4C, 0xC2, 0xFF)), 2);
        var fill = new SolidColorBrush(Color.FromArgb((byte)(120 * opacity), 0x1E, 0x22, 0x2D));
        dc.PushClip(new RectangleGeometry(area));
        int row = 0;
        for (double y = area.Y - (ch / 2); y < area.Bottom; y += ch + gap, row++)
        {
            double shift = row % 2 == 0 ? 0 : (cardWidth + gap) / 2;
            for (double x = area.X - shift; x < area.Right; x += cardWidth + gap)
            {
                dc.DrawRoundedRectangle(fill, pen, new Rect(x, y, cardWidth, ch), cardWidth * 0.08, cardWidth * 0.08);
            }
        }

        dc.Pop();
    }

    private static void DrawCover(DrawingContext dc, double w, double h)
    {
        DrawBackdrop(dc, w, h, new Point(w / 2, h * 0.36), w * 0.9);
        dc.PushOpacityMask(new LinearGradientBrush(new GradientStopCollection { new(Colors.White, 0), new(Colors.Transparent, 0.55) }, 90));
        DrawCardPattern(dc, new Rect(0, 0, w, h), 110, 0.18);
        dc.Pop();
        DrawLogo(dc, new Rect((w / 2) - 170, (h * 0.36) - 170, 340, 340));
        DrawText(dc, "Trainer Deck", 64, FontWeights.SemiBold, Colors.White, new Point(w / 2, h * 0.68), TextAlignment.Center);
        DrawText(dc, "Game trainer launcher", 26, FontWeights.Normal, Color.FromRgb(0xA3, 0xAA, 0xB8), new Point(w / 2, (h * 0.68) + 86), TextAlignment.Center);
    }

    private static void DrawHero(DrawingContext dc, double w, double h)
    {
        DrawBackdrop(dc, w, h, new Point(w * 0.3, h * 0.5), w * 0.55);
        dc.PushOpacityMask(new LinearGradientBrush(new GradientStopCollection { new(Colors.Transparent, 0.35), new(Colors.White, 1) }, 0));
        DrawCardPattern(dc, new Rect(0, 0, w, h), 170, 0.22);
        dc.Pop();
        DrawLogo(dc, new Rect((w * 0.3) - 220, (h / 2) - 220, 440, 440));
        DrawText(dc, "Trainer Deck", 120, FontWeights.SemiBold, Colors.White, new Point(w * 0.48, (h / 2) - 104), TextAlignment.Left);
        DrawText(dc, "Your game trainers, one button away", 40, FontWeights.Normal, Color.FromRgb(0xA3, 0xAA, 0xB8), new Point((w * 0.48) + 6, (h / 2) + 52), TextAlignment.Left);
    }

    private static void DrawText(DrawingContext dc, string text, double size, FontWeight weight, Color color, Point origin, TextAlignment align)
    {
        var typeface = new Typeface(new FontFamily("Segoe UI Variable Display, Segoe UI"), FontStyles.Normal, weight, FontStretches.Normal);
        var ft = new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, size, new SolidColorBrush(color), 1.0)
        {
            TextAlignment = align,
        };
        dc.DrawText(ft, origin);
    }

    private static BitmapSource Render(int w, int h, Action<DrawingContext, double, double> draw)
    {
        var visual = new DrawingVisual();
        using (DrawingContext dc = visual.RenderOpen())
        {
            draw(dc, w, h);
        }

        var bmp = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
        bmp.Render(visual);
        bmp.Freeze();
        return bmp;
    }

    private static void SavePng(BitmapSource image, string path)
    {
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(image));
        using FileStream fs = File.Create(path);
        encoder.Save(fs);
    }

    private static void SaveIcon(string path)
    {
        int[] sizes = [16, 20, 24, 32, 40, 48, 64, 128, 256];
        var images = sizes.Select(size =>
        {
            BitmapSource bmp = Render(size, size, (dc, w, h) => DrawLogo(dc, new Rect(0, 0, w, h)));
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bmp));
            using var ms = new MemoryStream();
            encoder.Save(ms);
            return ms.ToArray();
        }).ToList();

        using var fs = File.Create(path);
        using var w = new BinaryWriter(fs);
        w.Write((short)0);
        w.Write((short)1);
        w.Write((short)sizes.Length);
        int offset = 6 + (16 * sizes.Length);
        for (int i = 0; i < sizes.Length; i++)
        {
            w.Write((byte)(sizes[i] >= 256 ? 0 : sizes[i]));
            w.Write((byte)(sizes[i] >= 256 ? 0 : sizes[i]));
            w.Write((byte)0);
            w.Write((byte)0);
            w.Write((short)1);
            w.Write((short)32);
            w.Write(images[i].Length);
            w.Write(offset);
            offset += images[i].Length;
        }

        foreach (byte[] png in images)
        {
            w.Write(png);
        }
    }
}
