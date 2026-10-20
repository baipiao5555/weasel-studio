using System.Drawing;
using Forms = System.Windows.Forms;

namespace WeaselStudio.Services;

public sealed class TrayService : IDisposable
{
    private readonly Forms.NotifyIcon _notifyIcon;
    private readonly Forms.ToolStripMenuItem _toggleItem;
    private bool _disposed;

    public TrayService(
        Action openSettings,
        Action toggleSync,
        Action syncNow,
        Action exit)
    {
        var menu = new Forms.ContextMenuStrip();

        var openItem = new Forms.ToolStripMenuItem("打开设置");
        openItem.Click += (_, _) => openSettings();

        _toggleItem = new Forms.ToolStripMenuItem("暂停同步");
        _toggleItem.Click += (_, _) => toggleSync();

        var syncNowItem = new Forms.ToolStripMenuItem("立即同步");
        syncNowItem.Click += (_, _) => syncNow();

        var exitItem = new Forms.ToolStripMenuItem("退出");
        exitItem.Click += (_, _) => exit();

        menu.Items.Add(openItem);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(_toggleItem);
        menu.Items.Add(syncNowItem);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(exitItem);

        _notifyIcon = new Forms.NotifyIcon
        {
            Icon = LoadIcon(),
            Text = "小狼毫设置中心",
            ContextMenuStrip = menu,
            Visible = true
        };

        _notifyIcon.DoubleClick += (_, _) => openSettings();
    }

    public void UpdateStatus(string message, bool running)
    {
        if (_disposed)
            return;

        var text = "小狼毫设置中心 · " + message;
        if (text.Length > 63)
            text = text[..60] + "...";

        _notifyIcon.Text = text;
        _toggleItem.Text = running ? "暂停同步" : "恢复同步";
    }

    public void ShowBalloon(string message)
    {
        if (_disposed)
            return;

        _notifyIcon.BalloonTipTitle = "小狼毫设置中心";
        _notifyIcon.BalloonTipText = message;
        _notifyIcon.BalloonTipIcon = Forms.ToolTipIcon.Info;
        _notifyIcon.ShowBalloonTip(2000);
    }

    private static Icon LoadIcon()
    {
        try
        {
            var path = Environment.ProcessPath;
            if (!string.IsNullOrWhiteSpace(path))
            {
                var icon = Icon.ExtractAssociatedIcon(path);
                if (icon is not null)
                    return icon;
            }
        }
        catch
        {
            // Fall back to the default application icon.
        }

        return SystemIcons.Application;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
    }
}
