using Forms = System.Windows.Forms;

namespace TrainerDeck.UI;

public sealed class TrayIcon : IDisposable
{
    private readonly Forms.NotifyIcon _icon;
    private bool _toldOnce;

    public TrayIcon(Action open, Action openOverlay, Action exit)
    {
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Open Trainer Deck", null, (_, _) => open());
        menu.Items.Add("Open overlay", null, (_, _) => openOverlay());
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => exit());

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
