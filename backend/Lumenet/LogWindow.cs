using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Lumenet;

/// <summary>
/// The log of the desk program, as its console would show it, in a window of its own: closing
/// it (✕ or Esc) only hides it, Lumenet goes on. New lines come at the bottom; while an older
/// part is being read, the text stays where it is.
/// </summary>
internal sealed class LogWindow : Form
{
    // The colors of the desk (frontend/src/styles.css).
    private static readonly Color Well = ColorTranslator.FromHtml("#0e1013");
    private static readonly Color Legend = ColorTranslator.FromHtml("#d3d8df");
    private static readonly Color Dim = ColorTranslator.FromHtml("#8b93a1");
    private static readonly Color Yellow = ColorTranslator.FromHtml("#ffd426");
    private static readonly Color Red = ColorTranslator.FromHtml("#ff5247");

    // A line that goes on (an error's stack) starts under the text of its first line.
    private static readonly string Indent = new(' ', "00:00:00 info ".Length);

    // So many lines more than the log keeps, and the window starts again with those it keeps.
    private const int Slack = 200;

    private readonly LogBook _log;
    private readonly RichTextBox _text;

    // The number of the last line shown, and how many the window holds.
    private long _shown;
    private int _lines;

    // 1 while new lines are on their way to the window's thread.
    private int _pending;

    public LogWindow(LogBook log, string title, Icon icon)
    {
        _log = log;
        Text = title;
        Icon = icon;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = LogicalToDeviceUnits(new Size(960, 540));
        MinimumSize = LogicalToDeviceUnits(new Size(420, 220));
        BackColor = Well;
        Padding = new Padding(LogicalToDeviceUnits(8));

        _text = new RichTextBox
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            WordWrap = false,
            BorderStyle = BorderStyle.None,
            BackColor = Well,
            ForeColor = Legend,
            Font = new Font(MonospacedFont(), 10f),
            HideSelection = false,
            ScrollBars = RichTextBoxScrollBars.Both,
            DetectUrls = false,
            AccessibleName = "Log",
        };
        Controls.Add(_text);

        // Dark scroll bars too (Windows 10 1809 and later; the light ones otherwise).
        _text.HandleCreated += (_, _) => SetWindowTheme(_text.Handle, "DarkMode_Explorer", null);

        _log.Added += OnAdded;
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);

        // A dark title bar, as the desk is dark (Windows 10 2004 and later; 19 before that).
        int on = 1;
        if (DwmSetWindowAttribute(Handle, 20, ref on, sizeof(int)) != 0)
            DwmSetWindowAttribute(Handle, 19, ref on, sizeof(int));
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        ShowNew();
        _text.Focus();
    }

    // Shown again after being hidden: the lines that came meanwhile.
    protected override void OnVisibleChanged(EventArgs e)
    {
        base.OnVisibleChanged(e);
        if (Visible && IsHandleCreated)
            ShowNew();
    }

    // ✕ hides the window; Lumenet goes on, and so does its log.
    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            Hide();
        }

        base.OnFormClosing(e);
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.Escape)
        {
            Hide();
            return true;
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _log.Added -= OnAdded;

        base.Dispose(disposing);
    }

    // On the thread that wrote the line. A hidden window catches up when it is shown.
    private void OnAdded()
    {
        if (Interlocked.Exchange(ref _pending, 1) == 1)
            return;

        try
        {
            if (IsHandleCreated && !IsDisposed)
            {
                BeginInvoke(ShowNew);
                return;
            }
        }
        catch (InvalidOperationException)
        {
            // closed meanwhile
        }

        _pending = 0;
    }

    private void ShowNew()
    {
        _pending = 0;
        if (IsDisposed || !Visible)
            return;

        IReadOnlyList<LogLine> lines = _log.LinesAfter(_shown);
        if (lines.Count == 0)
            return;

        bool follow = AtBottom();
        if (_lines + lines.Count > LogBook.Capacity + Slack)
        {
            // Too long: start again with the lines the log keeps.
            _text.Clear();
            _lines = 0;
            lines = _log.LinesAfter(0);
            follow = true;
        }

        var scroll = default(Point);
        int start = _text.SelectionStart;
        int length = _text.SelectionLength;
        if (!follow)
            SendMessage(_text.Handle, EmGetScrollPos, 0, ref scroll);

        SendMessage(_text.Handle, WmSetRedraw, 0, 0);
        try
        {
            foreach (LogLine line in lines)
            {
                _text.SelectionStart = _text.TextLength;
                _text.SelectionLength = 0;
                _text.SelectionColor = ColorOf(line.Level);
                _text.AppendText(Format(line));
            }
        }
        finally
        {
            if (follow)
            {
                _text.SelectionStart = _text.TextLength;
                _text.ScrollToCaret();
            }
            else
            {
                _text.Select(start, length);
                SendMessage(_text.Handle, EmSetScrollPos, 0, ref scroll);
            }

            SendMessage(_text.Handle, WmSetRedraw, 1, 0);
            _text.Invalidate();
        }

        _shown = lines[^1].Number;
        _lines += lines.Count;
    }

    // Whether the last line is in sight (or nothing scrolls yet).
    private bool AtBottom()
    {
        var info = new ScrollInfo { Size = Marshal.SizeOf<ScrollInfo>(), Mask = SifAll };
        if (!GetScrollInfo(_text.Handle, SbVert, ref info) || info.Page == 0)
            return true;

        return info.Position + (int)info.Page >= info.Max;
    }

    // "14:02:11 warn BoxLink: The box at 192.168.7.1 is not connected", as the console has it.
    private static string Format(LogLine line)
    {
        string text = line.Text.Replace("\r\n", "\n").Replace("\n", "\n" + Indent);
        return $"{line.Time:HH:mm:ss} {LevelName(line.Level)} {line.Category}: {text}\n";
    }

    private static string LevelName(LogLevel level)
    {
        return level switch
        {
            LogLevel.Trace => "trce",
            LogLevel.Debug => "dbug",
            LogLevel.Information => "info",
            LogLevel.Warning => "warn",
            LogLevel.Error => "fail",
            _ => "crit",
        };
    }

    private static Color ColorOf(LogLevel level)
    {
        return level switch
        {
            LogLevel.Trace or LogLevel.Debug => Dim,
            LogLevel.Information => Legend,
            LogLevel.Warning => Yellow,
            _ => Red,
        };
    }

    // Cascadia Mono where Windows has it (11), Consolas otherwise.
    private static string MonospacedFont()
    {
        using var installed = new System.Drawing.Text.InstalledFontCollection();
        return installed.Families.Any(family => family.Name == "Cascadia Mono") ? "Cascadia Mono" : "Consolas";
    }

    private const int WmSetRedraw = 0x000B;
    private const int EmGetScrollPos = 0x0400 + 221;
    private const int EmSetScrollPos = 0x0400 + 222;
    private const int SbVert = 1;
    private const uint SifAll = 0x17;

    [StructLayout(LayoutKind.Sequential)]
    private struct ScrollInfo
    {
        public int Size;
        public uint Mask;
        public int Min;
        public int Max;
        public uint Page;
        public int Position;
        public int TrackPosition;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr window, int message, int wParam, int lParam);

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr window, int message, int wParam, ref Point lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetScrollInfo(IntPtr window, int bar, ref ScrollInfo info);

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);

    [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
    private static extern int SetWindowTheme(IntPtr window, string? subAppName, string? subIdList);
}
