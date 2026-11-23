using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using Forms = System.Windows.Forms;
using Media = System.Windows.Media;

namespace TrainerDeck.UI;

internal sealed class TrayMenuRenderer : Forms.ToolStripRenderer
{
    private const int GlyphLeft = 14;
    private const int TextLeft = 44;

    private static readonly Font GlyphFont = new("Segoe Fluent Icons", 11f, GraphicsUnit.Point);

    private Color _back = Color.FromArgb(0x1E, 0x22, 0x2D);
    private Color _text = Color.White;
    private Color _disabled = Color.Gray;
    private Color _hover = Color.FromArgb(0x0F, 0xFF, 0xFF, 0xFF);
    private Color _divider = Color.FromArgb(0x15, 0xFF, 0xFF, 0xFF);

    public static Forms.Padding ItemPadding => new(TextLeft - 8, 7, 28, 7);

    public void Prepare(Forms.ToolStripDropDown menu)
    {
        _back = Read("SurfaceHighColor", _back);
        _text = Read("TextPrimaryColor", _text);
        _disabled = Read("TextTertiaryColor", _disabled);
        _hover = Read("SubtleFillColorSecondaryColor", _hover);
        _divider = Read("DividerStrokeColorDefaultColor", _divider);
        menu.BackColor = _back;

        int round = 2;
        _ = DwmSetWindowAttribute(menu.Handle, 33, ref round, sizeof(int));
    }

    protected override void OnRenderToolStripBackground(Forms.ToolStripRenderEventArgs e) => e.Graphics.Clear(_back);

    protected override void OnRenderToolStripBorder(Forms.ToolStripRenderEventArgs e)
    {
    }

    protected override void OnRenderMenuItemBackground(Forms.ToolStripItemRenderEventArgs e)
    {
        if (!e.Item.Selected || !e.Item.Enabled)
        {
            return;
        }

        var r = new Rectangle(4, 1, e.Item.Width - 8, e.Item.Height - 2);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var path = RoundedRect(r, 4);
        using var brush = new SolidBrush(_hover);
        e.Graphics.FillPath(brush, path);
    }

    protected override void OnRenderItemText(Forms.ToolStripItemTextRenderEventArgs e)
    {
        Color color = e.Item.Enabled ? _text : _disabled;
        var bounds = new Rectangle(0, 0, e.Item.Width, e.Item.Height);
        if (e.Item.Tag is string glyph)
        {
            var glyphBox = new Rectangle(GlyphLeft, 0, 20, bounds.Height);
            Forms.TextRenderer.DrawText(e.Graphics, glyph, GlyphFont, glyphBox, color,
                Forms.TextFormatFlags.Left | Forms.TextFormatFlags.VerticalCenter | Forms.TextFormatFlags.NoPadding);
        }

        var textBox = new Rectangle(TextLeft, 0, bounds.Width - TextLeft, bounds.Height);
        Forms.TextRenderer.DrawText(e.Graphics, e.Text, e.TextFont, textBox, color,
            Forms.TextFormatFlags.Left | Forms.TextFormatFlags.VerticalCenter | Forms.TextFormatFlags.EndEllipsis);
    }

    protected override void OnRenderSeparator(Forms.ToolStripSeparatorRenderEventArgs e)
    {
        int y = e.Item.Height / 2;
        using var pen = new Pen(_divider);
        e.Graphics.DrawLine(pen, 4, y, e.Item.Width - 4, y);
    }

    private static Color Read(string key, Color fallback) =>
        System.Windows.Application.Current?.TryFindResource(key) is Media.Color c ? Color.FromArgb(c.A, c.R, c.G, c.B) : fallback;

    private static GraphicsPath RoundedRect(Rectangle r, int radius)
    {
        int d = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(r.Left, r.Top, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Top, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.Left, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
}
