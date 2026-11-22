using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.Win32;

namespace Lumenet;

/// <summary>
/// Lumenet's icon in the notification area of the taskbar. A click opens the page in the browser;
/// the menu opens the page, shows the log of the desk program, and ends Lumenet.
/// </summary>
internal sealed class Tray : ApplicationContext
{
    private readonly string _page;
    private readonly string _version;
    private readonly LogBook _log;
    private readonly Icon _icon;
    private readonly ContextMenuStrip _menu;
    private readonly NotifyIcon _notifyIcon;

    // The thread of the windows, for a call from another thread.
    private readonly SynchronizationContext _windows;

    private LogWindow? _logWindow;

    /// <param name="page">The address of the page: "http://localhost:5080/".</param>
    /// <param name="version">Lumenet's version, for the icon's tip.</param>
    /// <param name="log">The log of the desk program.</param>
    public Tray(string page, string version, LogBook log)
    {
        _page = page;
        _version = version;
        _log = log;
        _icon = BuiltIn.Icon(SystemInformation.SmallIconSize);

        _menu = new ContextMenuStrip();
        var open = new ToolStripMenuItem("Open Lumenet", null, (_, _) => OpenPage());
        open.Font = new Font(open.Font, FontStyle.Bold); // what a click on the icon does
        _menu.Items.AddRange(
        [
            open,
            new ToolStripMenuItem("Show the log", null, (_, _) => ShowLog()),
            new ToolStripSeparator(),
            new ToolStripMenuItem("Exit", null, (_, _) => Exit()),
        ]);

        // Making the menu gave this thread its place to post to.
        _windows = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();

        _notifyIcon = new NotifyIcon
        {
            Icon = _icon,
            Text = $"Lumenet {version}\n{page}",
            ContextMenuStrip = _menu,
            Visible = true,
        };
        _notifyIcon.MouseClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Left)
                OpenPage();
        };

        // Windows shuts down or the user signs out: Lumenet ends as with Exit.
        SystemEvents.SessionEnded += OnSessionEnded;
    }

    /// <summary>Opens the page in the browser.</summary>
    public void OpenPage()
    {
        try
        {
            Process.Start(new ProcessStartInfo(_page) { UseShellExecute = true });
        }
        catch (Exception e) when (e is Win32Exception or InvalidOperationException)
        {
            _log.Add(LogLevel.Warning, BuiltIn.LogName, $"The browser could not be opened at {_page}. {e.Message}");
        }
    }

    /// <summary>Opens the page, from another thread (Lumenet was started a second time).</summary>
    public void OpenPageSoon()
    {
        _windows.Post(_ => OpenPage(), null);
    }

    private void ShowLog()
    {
        if (_logWindow is null || _logWindow.IsDisposed)
            _logWindow = new LogWindow(_log, $"Lumenet {_version}: log", BuiltIn.Icon(SystemInformation.IconSize));

        if (_logWindow.WindowState == FormWindowState.Minimized)
            _logWindow.WindowState = FormWindowState.Normal;

        _logWindow.Show();
        _logWindow.Activate();
    }

    private void Exit()
    {
        _notifyIcon.Visible = false;
        _logWindow?.Dispose();
        ExitThread();
    }

    private void OnSessionEnded(object? sender, SessionEndedEventArgs e)
    {
        _windows.Post(_ => Exit(), null);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            SystemEvents.SessionEnded -= OnSessionEnded;
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _menu.Dispose();
            _logWindow?.Dispose();
            _icon.Dispose();
        }

        base.Dispose(disposing);
    }
}
