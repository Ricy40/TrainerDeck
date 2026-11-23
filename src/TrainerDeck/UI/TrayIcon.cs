using Forms = System.Windows.Forms;

namespace TrainerDeck.UI;

public sealed class TrayIcon : IDisposable
{
    private readonly Forms.NotifyIcon _icon;
    private bool _toldOnce;

    public TrayIcon(Action open, Action openOverlay, Action exit)
    {
        var renderer = new TrayMenuRenderer();
        var menu = new Forms.ContextMenuStrip
        {
            Renderer = renderer,
            ShowImageMargin = false,
            Padding = new Forms.Padding(0, 4, 0, 4),
            Font = new System.Drawing.Font("Segoe UI Variable Text", 10f),
        };
        menu.Items.Add(Item("Open Trainer Deck", "", open));
        menu.Items.Add(Item("Open overlay", "", openOverlay));
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(Item("Exit", "", exit));
        menu.Opening += (_, _) => renderer.Prepare(menu);

        _icon = new Forms.NotifyIcon
        {
            Text = "Trainer Deck",
            Icon = LoadIcon(),
            ContextMenuStrip = menu,
            Visible = false,
        };
        _icon.MouseClick += (_, e) =>
        {
            if (e.Button == Forms.MouseButtons.Left)
            {
                open();
            }
        };
    }

    private static Forms.ToolStripMenuItem Item(string text, string glyph, Action click)
    {
        var item = new Forms.ToolStripMenuItem(text) { Tag = glyph, Padding = TrayMenuRenderer.ItemPadding };
        item.Click += (_, _) => click();
        return item;
    }

    public bool Visible
    {
        get => _icon.Visible;
        set => _icon.Visible = value;
    }

    public void ShowHiddenNotice(string text)
    {
        Visible = true;
        if (!_toldOnce)
        {
            _toldOnce = true;
            _icon.ShowBalloonTip(4000, "Trainer Deck is still running", text, Forms.ToolTipIcon.None);
        }
    }

    private static System.Drawing.Icon LoadIcon()
    {
        try
        {
            if (Environment.ProcessPath is string exe && System.Drawing.Icon.ExtractAssociatedIcon(exe) is { } icon)
            {
                return icon;
            }
        }
        catch (Exception ex) when (ex is ArgumentException or System.IO.IOException)
        {
        }

        return System.Drawing.SystemIcons.Application;
    }

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
    }
}
