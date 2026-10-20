using System.Drawing.Drawing2D;
using Timer = System.Windows.Forms.Timer;

namespace SpeedMeterPro.Services;

/// <summary>
/// Dark/light theme palette provider (spec §3.3 design system):
/// dark #121212 background, #232323 surface, brand accent #4AF321,
/// MetricColors palette, header/sidebar/card tokens.
/// </summary>
public static class ThemeManager
{
    public static bool IsDark { get; set; } = true;

    // --- Core surfaces (Stitch Kinetic Telemetry tokens) -------------------
    public static Color Bg => FromArgb(IsDark ? "#FF10131E" : "#FFF8FAFC");
    public static Color BgAlt => FromArgb(IsDark ? "#FF181B27" : "#FFF1F5F9");
    public static Color Surface => FromArgb(IsDark ? "#FF1C1F2B" : "#FFFFFFFF");
    public static Color SurfaceHigh => FromArgb(IsDark ? "#FF262936" : "#FFF8FAFC");
    public static Color Sidebar => FromArgb(IsDark ? "#FF141724" : "#FFF8FAFC");
    public static Color SidebarHover => FromArgb(IsDark ? "#FF222638" : "#FFE2E8F0");

    // --- Text (Stitch tokens: #E0E1F2 on-surface, #94A3B8 muted) ------------
    public static Color Text => FromArgb(IsDark ? "#FFE0E1F2" : "#FF0F172A");
    public static Color TextDim => FromArgb(IsDark ? "#FF94A3B8" : "#FF64748B");
    public static Color Heading => FromArgb(IsDark ? "#FFFFFFFF" : "#FF020617");

    // --- Brand accent (Stitch Primary Neon #4AF321 & Secondary #3B82F6) -----
    public static Color Accent => FromArgb("#FF4AF321");
    public static Color AccentHover => FromArgb("#FF68FF42");
    public static Color AccentDown => FromArgb("#FF38E504");
    public static Color AccentAlt => FromArgb("#FF3B82F6");

    // --- Lines & status ----------------------------------------------------
    public static Color Border => FromArgb(IsDark ? "#FF2B2E3D" : "#FFE2E8F0");
    public static Color BorderSubtle => FromArgb(IsDark ? "#FF222533" : "#FFF1F5F9");
    public static Color Danger => FromArgb("#FFFF5252");
    public static Color Success => FromArgb("#FF4AF321");
    public static Color Warning => FromArgb("#FFF59E0B");
    public static Color Info => FromArgb("#FF3B82F6");

    // --- MetricColors palette (spec §3.3) ----------------------------------
    public static class Metric
    {
        public static readonly Color Sky = FromArgb("#FF38BDF8");
        public static readonly Color Teal = FromArgb("#FF14B8A6");
        public static readonly Color Download = FromArgb("#FF4AF321");
        public static readonly Color Upload = FromArgb("#FF10B981");
        public static readonly Color Fuchsia = FromArgb("#FFD946EF");
        public static readonly Color Orange = FromArgb("#FFF59E0B");
        public static readonly Color BlueDark = FromArgb("#FF2563EB");
        public static readonly Color Latency = FromArgb("#FF818CF8");
        public static readonly Color Indigo = FromArgb("#FF6366F1");
        public static readonly Color Jitter = FromArgb("#FFF472B6");
        public static readonly Color Rose = FromArgb("#FFFB7185");
        public static readonly Color Error = FromArgb("#FFEF4444");
        public static readonly Color Warning = FromArgb("#FFF59E0B");
        public static readonly Color Success = FromArgb("#FF10B981");
    }

    public static void ApplyTo(Control c, bool dark)
    {
        IsDark = dark;
        c.BackColor = Bg;
        c.ForeColor = Text;
    }

    public static Color FromArgb(string hex)
    {
        hex = hex.TrimStart('#');
        if (hex.Length == 6) hex = "FF" + hex;
        return Color.FromArgb(
            Convert.ToInt32(hex.Substring(0, 2), 16),
            Convert.ToInt32(hex.Substring(2, 2), 16),
            Convert.ToInt32(hex.Substring(4, 2), 16),
            Convert.ToInt32(hex.Substring(6, 2), 16));
    }
}

/// <summary>
/// Custom stacked toast notifications: fade in/out, auto-dismiss,
/// slide-up stack, click to dismiss, top-right placement.
/// </summary>
public sealed class ToastForm : Form
{
    private readonly Label _title;
    private readonly Label _body;
    private readonly Color _accent;
    private readonly Timer _fadeTimer;
    private readonly Timer _lifeTimer;
    private int _lifeMs;
    private static readonly List<ToastForm> Active = new();

    public ToastForm(string title, string body, Color accent, int lifeMs = 4000)
    {
        _accent = accent;
        _lifeMs = lifeMs;
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        ShowInTaskbar = false;
        TopMost = true;
        BackColor = ThemeManager.Surface;
        Size = new Size(340, 86);

        var bar = new Panel { Dock = DockStyle.Left, Width = 4, BackColor = accent };
        Controls.Add(bar);

        _title = new Label
        {
            Text = title,
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            ForeColor = ThemeManager.Heading,
            Location = new Point(16, 10),
            AutoSize = true,
        };
        _body = new Label
        {
            Text = body,
            Font = new Font("Segoe UI", 9),
            ForeColor = ThemeManager.TextDim,
            Location = new Point(16, 34),
            MaximumSize = new Size(308, 44),
            AutoSize = true,
        };
        Controls.Add(_title);
        Controls.Add(_body);

        _fadeTimer = new Timer { Interval = 30 };
        _fadeTimer.Tick += (_, _) => FadeStep();
        _lifeTimer = new Timer { Interval = 300 };
        _lifeTimer.Tick += (_, _) =>
        {
            _lifeMs -= 300;
            if (_lifeMs <= 0) BeginClose();
        };

        MouseClick += (_, _) => Close();
        _title.MouseClick += (_, _) => Close();
        _body.MouseClick += (_, _) => Close();

        var screens = Screen.AllScreens;
        var screen = screens.Length > 0 ? screens[0] : Screen.PrimaryScreen!;
        var wa = screen.WorkingArea;
        Active.RemoveAll(t => t.IsDisposed);
        int offset = 12 + Active.Count * 96;
        Location = new Point(wa.Right - Width - 12, wa.Top + offset);
        Active.Add(this);
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        Opacity = 0;
        _fadeTimer.Start();
        _lifeTimer.Start();
    }

    private void FadeStep()
    {
        Opacity = Math.Min(1, Opacity + 0.12);
        if (Opacity >= 1) _fadeTimer.Stop();
    }

    private void BeginClose()
    {
        _lifeTimer.Stop();
        _fadeTimer.Stop();
        Opacity = 0;
        Close();
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        base.OnFormClosed(e);
        Active.Remove(this);
        Reposition();
    }

    private static void Reposition()
    {
        var screens = Screen.AllScreens;
        var screen = screens.Length > 0 ? screens[0] : Screen.PrimaryScreen!;
        var wa = screen.WorkingArea;
        int i = 0;
        foreach (var t in Active.Where(x => !x.IsDisposed))
        {
            t.Location = new Point(wa.Right - t.Width - 12, wa.Top + 12 + i * 96);
            i++;
        }
    }
}

public static class Toast
{
    public static void Show(string title, string body, string accentHex = "#FF4AF321", int lifeMs = 4000)
    {
        try
        {
            var form = new ToastForm(title, body, ThemeManager.FromArgb(accentHex), lifeMs);
            form.Show();
        }
        catch (Exception ex)
        {
            Logger.Log($"Toast failed: {ex.Message}");
        }
    }
}
