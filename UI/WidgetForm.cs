using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
using SpeedMeterPro.Entitlement;
using SpeedMeterPro.Models;
using SpeedMeterPro.Pages;
using SpeedMeterPro.Services;
using Timer = System.Windows.Forms.Timer;

namespace SpeedMeterPro.UI;

/// <summary>
/// The always-on-top 2-row live speed widget. Draggable (snaps to top-right),
/// position lock, 12+ themes with per-element colors + transparency, font
/// settings, bps/Bps toggle, speed-zoned icon colors, the full 27-item tray
/// menu from the original dump (bound to both the tray icon and the widget
/// right-click), double-click opens the dashboard.
/// </summary>
public sealed class WidgetForm : Form
{
    private readonly AppSettingsVm _settings;
    private readonly IEntitlementProvider _entitlement = AllAccessEntitlementProvider.Instance;
    private readonly Timer _timer;
    private readonly Panel _accentBar;
    private readonly Label _downloadIcon;
    private readonly Label _downloadValue;
    private readonly Label _dividerLabel;
    private readonly Label _uploadIcon;
    private readonly Label _uploadValue;
    private readonly Label _latencyLabel;
    private SpeedSampler? _sampler;
    private string _interfaceId = "";
    private bool _dragging;
    private bool _paused;
    private bool _selecting;
    private int _measuring;
    private int _zeroCount;
    private Point _dragOffset;
    private NotifyIcon? _tray;
    private ContextMenuStrip? _trayMenu;
    private DashboardForm? _dashboard;
    private Font _valueFont = null!; // tracked so theme/font changes don't leak GDI objects

    // Tray menu entries we need to update check states on open.
    private ToolStripMenuItem _mShow = null!;
    private ToolStripMenuItem _mHide = null!;
    private ToolStripMenuItem _mTaskbar = null!;
    private ToolStripMenuItem _mLock = null!;
    private ToolStripMenuItem _mAutoNic = null!;
    private ToolStripMenuItem _mOnTop = null!;
    private ToolStripMenuItem _mRateFast = null!;
    private ToolStripMenuItem _mRateNormal = null!;
    private ToolStripMenuItem _mRateSlow = null!;
    private ToolStripMenuItem _mPause = null!;
    private ToolStripMenuItem _mResume = null!;

    public WidgetForm()
    {
        _settings = SettingsStore.Load();
        ThemeManager.IsDark = _settings.IsDarkMode;

        Text = "Speed Meter Pro";
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = _settings.KeepInTaskbar;
        TopMost = _settings.AlwaysOnTop;
        // FIX(display-1): the form used AutoScaleMode.Dpi together with a hard
        // coded 232x72 pixel size and labels pinned at (16,12)/(30,38). On
        // scaled laptop displays (125–200%, standard on modern laptops) the
        // scaled font made the two rows overlap each other and the speed text
        // wider than the 232px form, so the rounded region clipped the digits.
        // The layout is now computed from the real font metrics in Relayout()
        // (fonts in points already carry DPI), so auto-scaling must stay OFF
        // to avoid double scaling.
        AutoScaleMode = AutoScaleMode.None;
        BackColor = ThemeManager.Surface;
        Size = new Size(240, 76); // provisional — Relayout() sizes the form to fit
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor |
                 ControlStyles.StandardDoubleClick, true);
        DoubleClick += (_, _) => OpenDashboard();

        _accentBar = new Panel
        {
            Dock = DockStyle.Top,
            Height = 3,
            BackColor = ThemeManager.Accent,
        };
        Controls.Add(_accentBar);

        _downloadIcon = new Label
        {
            Text = "▼",
            ForeColor = ThemeManager.Metric.Download,
            AutoSize = true,
        };
        _downloadValue = new Label
        {
            Text = "—",
            ForeColor = ThemeManager.Text,
            // FIX(display-2): value labels used AutoSize = true, so the text
            // jumped sideways on every digit change and could run past the
            // form edge (clipping). The value slot is now a fixed rectangle
            // measured for the widest possible string of the current unit
            // mode — digits stay put and can never be clipped.
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleLeft,
        };
        _uploadIcon = new Label
        {
            Text = "▲",
            ForeColor = ThemeManager.Metric.Upload,
            AutoSize = true,
        };
        _uploadValue = new Label
        {
            Text = "—",
            ForeColor = ThemeManager.Text,
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleLeft,
        };
        _dividerLabel = new Label
        {
            Text = "|",
            ForeColor = Color.FromArgb(120, ThemeManager.TextDim),
            AutoSize = true,
            Visible = false,
        };
        _latencyLabel = new Label
        {
            Text = "• 12ms",
            ForeColor = Color.FromArgb(170, 190, 255),
            AutoSize = true,
            Visible = false,
        };
        Controls.Add(_downloadIcon);
        Controls.Add(_downloadValue);
        Controls.Add(_dividerLabel);
        Controls.Add(_uploadIcon);
        Controls.Add(_uploadValue);
        Controls.Add(_latencyLabel);

        _timer = new Timer { Interval = Math.Clamp(_settings.SpeedRefreshTime, 300, 60000) };
        _timer.Tick += (_, _) => Tick();

        SetupTray();
        SetupDrag();
        ApplyPosition();
        ConfigureStartupAndHotkeys();
        ApplyWidgetTheme();
    }

    private Font CreateFont()
    {
        var family = _settings.FontName;
        var size = Math.Clamp(_settings.FontSize, 8, 24);
        return new Font(family, size, _settings.IsFontBold ? FontStyle.Bold : FontStyle.Regular);
    }

    private void ConfigureStartupAndHotkeys()
    {
        try
        {
            StartupManager.SetEnabled(_settings.RunAtStartup);
        }
        catch (Exception ex)
        {
            Logger.Log($"Startup config failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Re-render the widget from the current theme settings: background,
    /// transparency (background hidden, digits still visible), accent bar and
    /// per-element colors (download icon / download label / upload icon /
    /// upload label).
    /// </summary>
    public void ApplyWidgetTheme()
    {
        var c = WidgetThemeService.WidgetColors.FromSettings(_settings);
        if (c.Transparent)
        {
            TransparencyKey = Color.Magenta;
            BackColor = Color.Magenta;
            _accentBar.Visible = false;
        }
        else
        {
            TransparencyKey = Color.Empty;
            BackColor = c.Background;
            _accentBar.Visible = true;
            _accentBar.BackColor = c.DownloadIcon;
        }
        _downloadIcon.ForeColor = c.DownloadIcon;
        _downloadValue.ForeColor = c.DownloadLabel;
        _uploadIcon.ForeColor = c.UploadIcon;
        _uploadValue.ForeColor = c.UploadLabel;
        // FIX(display-3): the old code only swapped fonts here and never
        // repositioned anything — after any font/size/unit change the labels
        // kept their old hard-coded coordinates and overlapped or clipped.
        Relayout();
    }

    /// <summary>
    /// FIX(display-1/2/3): compute the whole widget geometry from the actual
    /// font metrics instead of hard-coded pixels. Runs on startup, on every
    /// theme/font/unit change and on DPI changes, so the widget always fits
    /// its text at any font size (8–24pt) and any display scale (100–200%).
    /// The form grows leftward from its right edge, so a top-right widget
    /// stays anchored to the screen corner.
    /// </summary>
    private void Relayout()
    {
        var font = CreateFont();

        string probe = _settings.SpeedUnitMode switch
        {
            1 => "888.88 Mbps",
            2 => "888.88 GB/s",
            _ => "888.88 Gbps",
        };
        var textSz = TextRenderer.MeasureText(probe, font, Size.Empty, TextFormatFlags.NoPadding);
        var iconSz = TextRenderer.MeasureText("▼", font, Size.Empty, TextFormatFlags.NoPadding);

        int oldRight = Right;
        int oldTop = Top;

        SuspendLayout();
        ApplyFonts(font);

        if (_settings.WidgetStyle == WidgetStyle.KineticCapsule)
        {
            // Stitch Horizontal Capsule Pill [↓ 142.5 Mbps | ↑ 38.2 Mbps • 12ms]
            _accentBar.Visible = false;
            _dividerLabel.Visible = true;
            _latencyLabel.Visible = true;
            _downloadIcon.Text = "↓";
            _uploadIcon.Text = "↑";
            _downloadIcon.ForeColor = ThemeManager.Accent;
            _uploadIcon.ForeColor = ThemeManager.Info;

            int pad = 14;
            int gap = 4;
            int rowH = Math.Max(textSz.Height, font.Height);
            int height = Math.Max(rowH + 14, 38);
            int y = (height - rowH) / 2;

            int curX = pad;
            _downloadIcon.Location = new Point(curX, y);
            curX += _downloadIcon.PreferredWidth + gap;

            int valW = textSz.Width + 4;
            _downloadValue.Location = new Point(curX, y);
            _downloadValue.Size = new Size(valW, rowH);
            curX += valW + 6;

            _dividerLabel.Location = new Point(curX, y - 1);
            curX += _dividerLabel.PreferredWidth + 6;

            _uploadIcon.Location = new Point(curX, y);
            curX += _uploadIcon.PreferredWidth + gap;

            _uploadValue.Location = new Point(curX, y);
            _uploadValue.Size = new Size(valW, rowH);
            curX += valW + 8;

            _latencyLabel.Location = new Point(curX, y);
            curX += _latencyLabel.PreferredWidth + pad;

            int width = curX;
            Size = new Size(width, height);
        }
        else if (_settings.WidgetStyle == WidgetStyle.MinimalistBar)
        {
            _accentBar.Visible = false;
            _dividerLabel.Visible = false;
            _latencyLabel.Visible = false;
            _downloadIcon.Text = "↓";
            _uploadIcon.Text = "↑";
            _downloadIcon.ForeColor = ThemeManager.Accent;
            _uploadIcon.ForeColor = ThemeManager.Info;

            int pad = 8;
            int gap = 2;
            int rowH = Math.Max(textSz.Height, font.Height);
            int height = Math.Max(rowH + 6, 26);
            int y = (height - rowH) / 2;

            int curX = pad;
            _downloadIcon.Location = new Point(curX, y);
            curX += _downloadIcon.PreferredWidth + gap;

            int valW = textSz.Width + 4;
            _downloadValue.Location = new Point(curX, y);
            _downloadValue.Size = new Size(valW, rowH);
            curX += valW + 8;

            _uploadIcon.Location = new Point(curX, y);
            curX += _uploadIcon.PreferredWidth + gap;

            _uploadValue.Location = new Point(curX, y);
            _uploadValue.Size = new Size(valW, rowH);
            curX += valW + pad;

            int width = curX;
            Size = new Size(width, height);
        }
        else if (_settings.WidgetStyle == WidgetStyle.CyberpunkHud)
        {
            _accentBar.Visible = false;
            _dividerLabel.Visible = false;
            _latencyLabel.Visible = false;
            _downloadIcon.Text = "▼";
            _uploadIcon.Text = "▲";
            _downloadIcon.ForeColor = ThemeManager.Accent;
            _uploadIcon.ForeColor = ThemeManager.Info;

            int pad = 12;
            int gap = 8;
            int rowTextH = Math.Max(textSz.Height, font.Height);
            int rowH = rowTextH + 2;
            int top = 6;
            int iconX = pad;
            int valueX = iconX + Math.Max(iconSz.Width, 8) + gap;
            int textW = textSz.Width + 12;
            int width = valueX + textW + pad;
            int height = top + rowH * 2 + 8;

            _downloadIcon.Location = new Point(iconX, top);
            _downloadValue.Location = new Point(valueX, top);
            _downloadValue.Size = new Size(textW, rowTextH);
            _uploadIcon.Location = new Point(iconX, top + rowH);
            _uploadValue.Location = new Point(valueX, top + rowH);
            _uploadValue.Size = new Size(textW, rowTextH);
            Size = new Size(width, height);
        }
        else // ClassicTwoRow
        {
            _accentBar.Visible = true;
            _dividerLabel.Visible = false;
            _latencyLabel.Visible = false;
            _downloadIcon.Text = "▼";
            _uploadIcon.Text = "▲";

            int pad = Math.Max(10, font.Height / 3);
            int gap = Math.Max(12, font.Height / 3);
            int vPad = Math.Max(4, font.Height / 8);
            int accent = _accentBar.Visible ? _accentBar.Height : 0;
            int rowTextH = Math.Max(textSz.Height, font.Height);
            int rowH = Math.Max(rowTextH, iconSz.Height) + 2;
            int top = accent + vPad;
            int iconX = pad;
            int valueX = iconX + Math.Max(iconSz.Width, 8) + gap + 4;
            int textW = textSz.Width + 16;
            int width = valueX + textW + pad;
            int height = top + rowH * 2 + vPad;

            _downloadIcon.Location = new Point(iconX, top);
            _downloadValue.Location = new Point(valueX, top);
            _downloadValue.Size = new Size(textW, rowTextH);
            _uploadIcon.Location = new Point(iconX, top + rowH);
            _uploadValue.Location = new Point(valueX, top + rowH);
            _uploadValue.Size = new Size(textW, rowTextH);
            Size = new Size(width, height);
        }

        ResumeLayout();

        // Keep the right edge anchored
        Location = new Point(oldRight - Width, oldTop);
        KeepOnScreen();
        ApplyRegion();
        Invalidate();
    }

    /// <summary>Assign one tracked font to all labels and dispose the old one (no GDI leak).</summary>
    private void ApplyFonts(Font font)
    {
        var old = _valueFont;
        _downloadIcon.Font = font;
        _downloadValue.Font = font;
        _uploadIcon.Font = font;
        _uploadValue.Font = font;
        if (_dividerLabel is not null) _dividerLabel.Font = font;
        if (_latencyLabel is not null) _latencyLabel.Font = new Font(font.FontFamily, Math.Max(font.Size - 2, 7.5f), FontStyle.Bold);
        _valueFont = font;
        if (old is not null && !old.Equals(font)) old.Dispose();
    }

    /// <summary>The full 27-item tray menu from the original dump (icons, shortcut keys, check states).</summary>
    private void SetupTray()
    {
        _tray = new NotifyIcon
        {
            Text = "Speed Meter Pro",
            Icon = System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? SystemIcons.Application,
            Visible = true,
        };
        _trayMenu = BuildTrayMenu();
        _tray.ContextMenuStrip = _trayMenu;
        _tray.DoubleClick += (_, _) => OpenDashboard();

        // The widget itself shows the same menu on right-click.
        RightClickMenu();
        foreach (Control c in Controls)
        {
            c.MouseClick += (_, e) =>
            {
                if (e.Button == MouseButtons.Right && _trayMenu is not null)
                {
                    _trayMenu.Show(Cursor.Position);
                }
            };
        }
    }

    private void RightClickMenu()
    {
        MouseClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Right && _trayMenu is not null)
            {
                _trayMenu.Show(Cursor.Position);
            }
        };
    }

    private ContextMenuStrip BuildTrayMenu()
    {
        var menu = new ContextMenuStrip();
        menu.ShowImageMargin = true;
        menu.ImageScalingSize = new Size(32, 32);
        menu.Opening += (_, _) => RefreshMenuChecks();

        _mShow = Item(menu, "Show", IconChar.WindowRestore, ThemeManager.TextDim,
            Keys.Control | Keys.S, "Ctrl+S", (_, _) => { ShowWidget(); });
        _mHide = Item(menu, "Hide", IconChar.EyeSlash, ThemeManager.TextDim,
            Keys.Control | Keys.H, "Ctrl+H", (_, _) => { HideWidget(); });
        menu.Items.Add(new ToolStripSeparator());
        Item(menu, "Setting", IconChar.ScrewdriverWrench, ThemeManager.Metric.Download,
            Keys.None, "S", (_, _) => OpenDashboard("Settings"));
        Item(menu, "Dashboard", IconChar.Dashboard, ThemeManager.Metric.Download,
            Keys.None, "D", (_, _) => OpenDashboard());
        Item(menu, "Refresh Connection", IconChar.ArrowsRotate, ThemeManager.Metric.Upload,
            Keys.None, null, (_, _) => RefreshConnection());
        _mTaskbar = Item(menu, "Keep in Taskbar", IconChar.Thumbtack, ThemeManager.TextDim,
            Keys.None, null, (_, _) => ToggleKeepInTaskbar());
        _mLock = Item(menu, "Lock Position", IconChar.LockOpen, ThemeManager.TextDim,
            Keys.None, null, (_, _) => ToggleLockPosition());
        Item(menu, "Restore Default Setup", IconChar.RotateLeft, ThemeManager.Metric.Rose,
            Keys.None, null, (_, _) => RestoreDefaults());
        menu.Items.Add(new ToolStripSeparator());

        var rate = new ToolStripMenuItem("Speed Refresh Time")
        {
            Image = MenuIcon(IconChar.ClockRotateLeft, ThemeManager.Metric.Latency),
        };
        _mRateFast = Child(rate, "Fast (500 ms)", IconChar.Bolt, ThemeManager.Metric.Latency, (_, _) => SetRefreshRate(500));
        _mRateNormal = Child(rate, "Normal (1 Second)", IconChar.GaugeHigh, ThemeManager.Metric.Latency, (_, _) => SetRefreshRate(1000));
        _mRateSlow = Child(rate, "Slow (2 Seconds)", IconChar.HourGlass, ThemeManager.Metric.Latency, (_, _) => SetRefreshRate(2000));
        menu.Items.Add(rate);

        var looks = new ToolStripMenuItem("Widget Look / Style")
        {
            Image = MenuIcon(IconChar.Sliders, ThemeManager.Accent),
        };
        Child(looks, "Kinetic Capsule (Stitch Pill)", IconChar.Bolt, ThemeManager.Accent, (_, _) => SetWidgetStyle(WidgetStyle.KineticCapsule));
        Child(looks, "Classic Two-Row Box", IconChar.Desktop, ThemeManager.Metric.Upload, (_, _) => SetWidgetStyle(WidgetStyle.ClassicTwoRow));
        Child(looks, "Minimalist Micro Bar", IconChar.Compress, ThemeManager.Metric.Download, (_, _) => SetWidgetStyle(WidgetStyle.MinimalistBar));
        Child(looks, "Cyberpunk HUD", IconChar.Microchip, ThemeManager.Metric.Orange, (_, _) => SetWidgetStyle(WidgetStyle.CyberpunkHud));
        menu.Items.Add(looks);

        menu.Items.Add(new ToolStripSeparator());
        Item(menu, "Adapters(NIC) Setting", IconChar.Ethernet, ThemeManager.Metric.Upload,
            Keys.None, null, (_, _) => OpenDashboard("Adapter Config"));
        _mAutoNic = Item(menu, "Auto Select NIC", IconChar.NetworkWired, ThemeManager.Metric.Upload,
            Keys.None, null, (_, _) => ToggleAutoSelectNic());
        Item(menu, "Connection Info", IconChar.Wifi, ThemeManager.Metric.Upload,
            Keys.None, null, (_, _) => OpenDashboard("Network Info"));
        _mOnTop = Item(menu, "Always On Top", IconChar.ArrowUp, ThemeManager.TextDim,
            Keys.None, null, (_, _) => ToggleAlwaysOnTop());
        Item(menu, "Change UI Position", IconChar.UpDownLeftRight, ThemeManager.TextDim,
            Keys.None, null, (_, _) => ChangeUiPosition());
        menu.Items.Add(new ToolStripSeparator());

        Item(menu, "Data Usages Stat", IconChar.ChartColumn, ThemeManager.Metric.Jitter,
            Keys.None, null, (_, _) => OpenDashboard("Usage Stats"));
        Item(menu, "Test Speed", IconChar.GaugeHigh, ThemeManager.Metric.Jitter,
            Keys.None, null, (_, _) => OpenDashboard("Speed Test"));
        Item(menu, "Stopwatch", IconChar.Stopwatch, ThemeManager.Metric.Jitter,
            Keys.None, null, (_, _) => OpenDashboard("Stop Watch"));
        Item(menu, "Active Process", IconChar.Microchip, ThemeManager.Metric.Jitter,
            Keys.None, null, (_, _) => OpenDashboard("Active Process"));
        menu.Items.Add(new ToolStripSeparator());

        var quick = new ToolStripMenuItem("Quick Links")
        {
            Image = MenuIcon(IconChar.Link, ThemeManager.Metric.Download),
        };
        Child(quick, "Network Connections", IconChar.NetworkWired, ThemeManager.Metric.Download, (_, _) => Launch("ncpa.cpl"));
        Child(quick, "Network Status", IconChar.Wifi, ThemeManager.Metric.Download, (_, _) => Launch("ms-settings:network-status"));
        Child(quick, "Resource Monitor", IconChar.ChartLine, ThemeManager.Metric.Download, (_, _) => Launch("resmon"));
        Child(quick, "Task Manager", IconChar.ListCheck, ThemeManager.Metric.Download, (_, _) => Launch("taskmgr"));
        Child(quick, "System Information", IconChar.CircleInfo, ThemeManager.Metric.Download, (_, _) => Launch("msinfo32"));
        Child(quick, "Internet Properties", IconChar.Globe, ThemeManager.Metric.Download, (_, _) => Launch("inetcpl.cpl"));
        Child(quick, "System Properties", IconChar.Sliders, ThemeManager.Metric.Download, (_, _) => Launch("sysdm.cpl"));
        Child(quick, "Control Panel", IconChar.Gear, ThemeManager.Metric.Download, (_, _) => Launch("control"));
        Child(quick, "Add/Remove Programs", IconChar.Screwdriver, ThemeManager.Metric.Download, (_, _) => Launch("appwiz.cpl"));
        Child(quick, "Display Properties", IconChar.Desktop, ThemeManager.Metric.Download, (_, _) => Launch("desk.cpl"));
        Child(quick, "Downloads", IconChar.Download, ThemeManager.Metric.Download, (_, _) => Launch("explorer.exe", "shell:Downloads"));
        menu.Items.Add(quick);

        Item(menu, "Tools Hub", IconChar.ToolBox, ThemeManager.Metric.Orange,
            Keys.None, null, (_, _) => OpenDashboard("Network Tools"));
        Item(menu, "Network Properties", IconChar.Sliders, ThemeManager.Metric.Upload,
            Keys.None, null, (_, _) => OpenDashboard("Network Interface"));
        Item(menu, "Take Screenshot", IconChar.Camera, ThemeManager.Metric.Jitter,
            Keys.None, null, (_, _) => TakeScreenshot());
        Item(menu, "How To Use", IconChar.CircleQuestion, ThemeManager.TextDim,
            Keys.None, null, (_, _) => OpenDashboard("About"));
        menu.Items.Add(new ToolStripSeparator());

        _mPause = Item(menu, "Pause", IconChar.Pause, ThemeManager.Metric.Latency,
            Keys.None, null, (_, _) => SetPaused(true));
        _mResume = Item(menu, "Resume", IconChar.Play, ThemeManager.Metric.Upload,
            Keys.None, null, (_, _) => SetPaused(false));
        Item(menu, "About", IconChar.CircleInfo, ThemeManager.TextDim,
            Keys.None, null, (_, _) => OpenDashboard("About"));
        Item(menu, "Exit", IconChar.PowerOff, ThemeManager.Metric.Rose,
            Keys.None, "Q", (_, _) => ExitApp());

        return menu;
    }

    private static ToolStripMenuItem Item(
        ContextMenuStrip menu, string text, IconChar icon, Color color,
        Keys shortcut, string? shortcutText, EventHandler onClick)
    {
        var item = new ToolStripMenuItem(text)
        {
            Image = MenuIcon(icon, color),
            ShortcutKeys = shortcut,
            ShowShortcutKeys = shortcutText is not null,
            ShortcutKeyDisplayString = shortcutText,
        };
        item.Click += onClick;
        menu.Items.Add(item);
        return item;
    }

    /// <summary>32px tray icon: rounded colored tile + white glyph (original tray style).</summary>
    private static Bitmap MenuIcon(IconChar icon, Color background)
    {
        var bmp = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using var path = PageStyle.RoundedPath(new Rectangle(1, 1, 30, 30), 7);
            using (var b = new SolidBrush(background))
            {
                g.FillPath(b, path);
            }
            using var glyph = FaIcons.Bitmap(icon, 20, Color.White);
            g.DrawImage(glyph, (32 - glyph.Width) / 2, (32 - glyph.Height) / 2);
        }
        return bmp;
    }

    private static ToolStripMenuItem Child(
        ToolStripMenuItem parent, string text, IconChar icon, Color color, EventHandler onClick)
    {
        var item = new ToolStripMenuItem(text)
        {
            Image = MenuIcon(icon, color),
        };
        item.Click += onClick;
        parent.DropDownItems.Add(item);
        return item;
    }

    private void RefreshMenuChecks()
    {
        _mShow.Checked = Visible && !_paused;
        _mHide.Checked = !Visible;
        _mTaskbar.Checked = _settings.KeepInTaskbar;
        _mAutoNic.Checked = !_settings.IsNicManuallySelect;
        _mOnTop.Checked = _settings.AlwaysOnTop;
        _mPause.Checked = _paused;
        _mResume.Checked = !_paused;
        _mRateFast.Checked = _settings.SpeedRefreshTime == 500;
        _mRateNormal.Checked = _settings.SpeedRefreshTime == 1000;
        _mRateSlow.Checked = _settings.SpeedRefreshTime == 2000;
        _mLock.Text = _settings.IsPositionLocked ? "Unlock Position" : "Lock Position";
        _mLock.Image = MenuIcon(_settings.IsPositionLocked ? IconChar.Lock : IconChar.LockOpen, ThemeManager.TextDim);
    }

    // --- Tray menu actions ---------------------------------------------------

    private void ShowWidget()
    {
        Show();
        if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
        Activate();
        Toast.Show("Speed Meter Pro", "Widget is visible again.");
    }

    private void HideWidget()
    {
        Hide();
        Toast.Show("Speed Meter Pro", "Widget hidden. Right-click the tray icon to show it again.");
    }

    private void RefreshConnection()
    {
        SelectAdapter();
        Tick();
        Toast.Show("Speed Meter Pro", "Network connection refreshed.");
    }

    private void ToggleKeepInTaskbar()
    {
        _settings.KeepInTaskbar = !_settings.KeepInTaskbar;
        ShowInTaskbar = _settings.KeepInTaskbar;
        SettingsStore.Save(_settings);
    }

    private void ToggleLockPosition()
    {
        _settings.IsPositionLocked = !_settings.IsPositionLocked;
        SettingsStore.Save(_settings);
        Toast.Show("Speed Meter Pro",
            _settings.IsPositionLocked ? "Widget position locked." : "Widget unlocked — drag it anywhere.");
    }

    private void RestoreDefaults()
    {
        WidgetThemeService.ApplyTheme(_settings, "Black");
        _settings.FontName = "Segoe UI";
        _settings.FontSize = 10;
        _settings.IsFontBold = false;
        _settings.SpeedRefreshTime = 1000;
        _timer.Interval = 1000;
        _settings.IsPositionLocked = false;
        _settings.AlwaysOnTop = true;
        TopMost = true;
        SettingsStore.Save(_settings);
        ApplyWidgetTheme();
        SnapToTopRight();
        Toast.Show("Speed Meter Pro", "Default setup restored.");
    }

    private void SetRefreshRate(int ms)
    {
        _settings.SpeedRefreshTime = ms;
        _timer.Interval = ms;
        _sampler = string.IsNullOrEmpty(_interfaceId)
            ? null
            : new SpeedSampler(_interfaceId, _settings.SpeedRefreshTime);
        SettingsStore.Save(_settings);
    }

    private void ToggleAutoSelectNic()
    {
        _settings.IsNicManuallySelect = !_settings.IsNicManuallySelect;
        SettingsStore.Save(_settings);
        SelectAdapter();
    }

    private void ToggleAlwaysOnTop()
    {
        _settings.AlwaysOnTop = !_settings.AlwaysOnTop;
        TopMost = _settings.AlwaysOnTop;
        SettingsStore.Save(_settings);
    }

    private void ChangeUiPosition()
    {
        if (_settings.IsPositionLocked)
        {
            Toast.Show("Speed Meter Pro",
                "Widget is locked. Use tray menu → Unlock Position, then drag it.");
            return;
        }
        SnapToTopRight();
        _settings.UiPositionWidthPoint = Left;
        _settings.UiPositionHeightPoint = Top;
        SettingsStore.Save(_settings);
        Toast.Show("Speed Meter Pro", "Widget snapped to the top-right corner.");
    }

    private void SetPaused(bool paused)
    {
        _paused = paused;
        if (paused) _timer.Stop();
        else _timer.Start();
    }

    private void TakeScreenshot()
    {
        try
        {
            var bounds = Screen.PrimaryScreen!.Bounds;
            using var bmp = new Bitmap(bounds.Width, bounds.Height);
            using (var g = Graphics.FromImage(bmp))
            {
                g.CopyFromScreen(bounds.X, bounds.Y, 0, 0, bounds.Size);
            }
            var dir = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, $"SpeedMeterPro_{DateTime.Now:yyyyMMdd_HHmmss}.png");
            bmp.Save(path, System.Drawing.Imaging.ImageFormat.Png);
            Toast.Show("Speed Meter Pro", $"Screenshot saved to {path}");
        }
        catch (Exception ex)
        {
            Logger.Log($"Screenshot: {ex.Message}");
            Toast.Show("Speed Meter Pro", "Screenshot failed.");
        }
    }

    private static void Launch(string command, string? args = null)
    {
        try
        {
            if (args is null) Process.Start(new ProcessStartInfo(command) { UseShellExecute = true });
            else Process.Start(new ProcessStartInfo(command, args) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Logger.Log($"Launch {command}: {ex.Message}");
        }
    }

    // --- Drag / position / sampling -----------------------------------------

    private void SetupDrag()
    {
        MouseDown += (_, e) =>
        {
            if (_settings.IsPositionLocked) return;
            if (e.Button == MouseButtons.Left)
            {
                _dragging = true;
                _dragOffset = new Point(e.X, e.Y);
            }
        };
        MouseMove += (_, e) =>
        {
            if (!_dragging || _settings.IsPositionLocked) return;
            var target = new Point(Left + e.X - _dragOffset.X, Top + e.Y - _dragOffset.Y);
            Location = target;
        };
        MouseUp += (_, e) =>
        {
            if (!_dragging) return;
            _dragging = false;
            SnapToTopRight();
            _settings.UiPositionWidthPoint = Left;
            _settings.UiPositionHeightPoint = Top;
            SettingsStore.Save(_settings);
        };
        foreach (Control c in Controls)
        {
            c.MouseDown += (_, e) => OnMouseDown(e);
            c.MouseMove += (_, e) => OnMouseMove(e);
            c.MouseUp += (_, e) => OnMouseUp(e);
        }
    }

    private void SnapToTopRight()
    {
        var wa = CurrentScreen().WorkingArea;
        var target = new Point(wa.Right - Width - 8, wa.Top + 8);
        if (Math.Abs(Location.X - target.X) < 120 && Math.Abs(Location.Y - target.Y) < 120)
        {
            Location = target;
        }
    }

    private void ApplyPosition()
    {
        if (_settings.UiPositionWidthPoint != 0 || _settings.UiPositionHeightPoint != 0)
        {
            Location = new Point(_settings.UiPositionWidthPoint, _settings.UiPositionHeightPoint);
            // FIX(display-4): a saved position from a previous monitor setup
            // (or a bigger font) can land the widget off-screen — clamp it
            // back into the visible working area.
            KeepOnScreen();
            return;
        }
        var wa = CurrentScreen().WorkingArea;
        Location = new Point(wa.Right - Width - 8, wa.Top + 8);
    }

    private void KeepOnScreen()
    {
        try
        {
            var wa = CurrentScreen().WorkingArea;
            int x = Math.Min(Math.Max(Left, wa.Left), Math.Max(wa.Left, wa.Right - Width));
            int y = Math.Min(Math.Max(Top, wa.Top), Math.Max(wa.Top, wa.Bottom - Height));
            if (x != Left || y != Top) Location = new Point(x, y);
        }
        catch { }
    }

    /// <summary>Screen the widget currently lives on (multi-monitor safe).</summary>
    private Screen CurrentScreen()
    {
        try
        {
            return IsHandleCreated ? Screen.FromHandle(Handle) : Screen.PrimaryScreen!;
        }
        catch
        {
            return Screen.PrimaryScreen!;
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var c = WidgetThemeService.WidgetColors.FromSettings(_settings);
        if (c.Transparent) return;

        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        if (_settings.WidgetStyle == WidgetStyle.KineticCapsule)
        {
            var r = new Rectangle(0, 0, Width - 1, Height - 1);
            using var path = PageStyle.RoundedPath(r, Height / 2);
            using var borderPen = new Pen(Color.FromArgb(90, ThemeManager.Accent), 1.5f);
            g.DrawPath(borderPen, path);
        }
        else if (_settings.WidgetStyle == WidgetStyle.CyberpunkHud)
        {
            var r = new Rectangle(0, 0, Width - 1, Height - 1);
            using var p = new Pen(ThemeManager.Accent, 1);
            g.DrawRectangle(p, r);
            using var tickPen = new Pen(ThemeManager.Info, 2);
            g.DrawLine(tickPen, 0, 0, 10, 0);
            g.DrawLine(tickPen, 0, 0, 0, 10);
            g.DrawLine(tickPen, Width - 1, Height - 1, Width - 11, Height - 1);
            g.DrawLine(tickPen, Width - 1, Height - 1, Width - 1, Height - 11);
        }
        else
        {
            using var p = new Pen(ThemeManager.Border, 1);
            g.DrawRectangle(p, 0, 0, Width - 1, Height - 1);
        }
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        ApplyRegion();
        SelectAdapter();
        if (!_paused) _timer.Start();
        Logger.Log("Widget shown.");
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        ApplyRegion();
    }

    /// <summary>
    /// FIX(display-5): moving the widget to a monitor with a different scale
    /// (very common on laptops + external screens) used to leave the widget
    /// with stale geometry. Re-measure everything at the new DPI.
    /// </summary>
    protected override void OnDpiChanged(DpiChangedEventArgs e)
    {
        base.OnDpiChanged(e);
        Relayout();
    }

    private void ApplyRegion()
    {
        if (Width < 20 || Height < 20) return;
        int radius = _settings.WidgetStyle switch
        {
            WidgetStyle.KineticCapsule => Height / 2,
            WidgetStyle.MinimalistBar => 6,
            WidgetStyle.CyberpunkHud => 4,
            _ => 12,
        };
        using var path = PageStyle.RoundedPath(new Rectangle(0, 0, Width, Height), radius);
        var old = Region;
        Region = new Region(path);
        old?.Dispose();
    }

    public void SetWidgetStyle(WidgetStyle style)
    {
        _settings.WidgetStyle = style;
        SettingsStore.Save(_settings);
        ApplyWidgetTheme();
        Toast.Show("Widget Look", $"Widget look changed to {style}.", "#FF10B981");
    }

    /// <summary>
    /// FIX(speed-4): adapter selection (with ping validation, up to 3 pings ×
    /// 1500 ms) used to run synchronously ON THE UI THREAD — the widget froze
    /// for up to ~4.5 s on startup, on "Refresh Connection" and, worst, every
    /// 4th idle tick (the zero-traffic auto re-selection). Selection now runs
    /// on a background thread; the UI never blocks.
    /// </summary>
    private void SelectAdapter() => _ = SelectAdapterAsync();

    private async Task SelectAdapterAsync()
    {
        if (_selecting) return;
        _selecting = true;
        try
        {
            string id;
            if (_settings.IsNicManuallySelect && !string.IsNullOrEmpty(_settings.NicName))
            {
                var wanted = _settings.NicName;
                var nic = await Task.Run(() => NetworkInterface.GetAllNetworkInterfaces()
                        .FirstOrDefault(n => string.Equals(n.Name, wanted, StringComparison.OrdinalIgnoreCase)))
                    .ConfigureAwait(true);
                // FIX(speed-1): manual selection no longer depends on the strict
                // usability filter — the sampler reads by interface Id directly.
                id = nic?.Id
                     ?? await Task.Run(() => AdapterSelector.SelectAutoAdapter()?.Id ?? "").ConfigureAwait(true);
            }
            else
            {
                id = await Task.Run(() => AdapterSelector.SelectAutoAdapter(true)?.Id ?? "").ConfigureAwait(true);
            }

            if (IsDisposed || !IsHandleCreated) return;
            BeginInvoke(new Action(() =>
            {
                if (IsDisposed) return;
                _interfaceId = id;
                _sampler = string.IsNullOrEmpty(id) ? null : new SpeedSampler(id, _settings.SpeedRefreshTime);
                _zeroCount = 0;
                Logger.Log($"Adapter selected: {id}");
            }));
        }
        catch (Exception ex)
        {
            Logger.Log($"SelectAdapter: {ex.Message}");
            try
            {
                if (!IsDisposed && IsHandleCreated)
                {
                    BeginInvoke(new Action(() => _sampler = null));
                }
            }
            catch { }
        }
        finally
        {
            _selecting = false;
        }
    }

    private void Tick()
    {
        try
        {
            if (_sampler is null || string.IsNullOrEmpty(_interfaceId))
            {
                // While an adapter is being selected show a neutral placeholder
                // instead of a false "0" reading. If no adapter could be found at
                // all, retry the selection on a cooldown instead of every tick.
                string idle = _selecting ? "—" : "0" + UnitSuffix();
                _downloadValue.Text = idle;
                _uploadValue.Text = idle;
                if (_sampler is null && !_selecting)
                {
                    _zeroCount++;
                    if (_zeroCount % 4 == 0) SelectAdapter();
                }
                return;
            }
            if (Interlocked.Exchange(ref _measuring, 1) == 1) return;
            _ = MeasureAndRenderAsync();
        }
        catch (Exception ex)
        {
            Logger.Log($"Tick: {ex.Message}");
            Interlocked.Exchange(ref _measuring, 0);
        }
    }

    private async Task MeasureAndRenderAsync()
    {
        try
        {
            var sampler = _sampler;
            if (sampler is null) return;
            // FIX(speed-2): run the whole measurement (including any lazy
            // interface resolution) on the thread pool — the UI thread stays
            // free, which keeps the widget perfectly responsive.
            var avg = await Task.Run(() => sampler.MeasureAsync()).ConfigureAwait(false);
            if (IsDisposed || !IsHandleCreated) return;
            BeginInvoke(new Action(() =>
            {
                try
                {
                    if (IsDisposed) return;
                    _downloadValue.Text = UnitFormatter.FormatSpeedEx(avg.DownloadBps, _settings.SpeedUnitMode);
                    _uploadValue.Text = UnitFormatter.FormatSpeedEx(avg.UploadBps, _settings.SpeedUnitMode);
                    ApplySpeedZoneColors(avg.DownloadBps, avg.UploadBps);

                    // FIX(speed-3): the interface disappeared / driver reset —
                    // re-select immediately instead of displaying zeros.
                    if (_sampler is { InterfaceDead: true })
                    {
                        _sampler = null;
                        SelectAdapter();
                        return;
                    }

                    if (!_settings.IsNicManuallySelect && avg.DownloadBps == 0 && avg.UploadBps == 0)
                    {
                        _zeroCount++;
                        if (_zeroCount % 4 == 0) SelectAdapter();
                    }
                    else
                    {
                        _zeroCount = 0;
                    }
                }
                catch (Exception ex)
                {
                    Logger.Log($"RenderSample: {ex.Message}");
                }
            }));
        }
        catch (Exception ex)
        {
            Logger.Log($"MeasureAsync: {ex.Message}");
        }
        finally
        {
            Interlocked.Exchange(ref _measuring, 0);
        }
    }

    private string UnitSuffix() => _settings.SpeedUnitMode switch
    {
        2 => " B/s",
        1 => " Mbps",
        _ => " bps",
    };

    private void ApplySpeedZoneColors(double downloadBps, double uploadBps)
    {
        double dlMbps = downloadBps / 1_000_000.0;
        double ulMbps = uploadBps / 1_000_000.0;
        _downloadIcon.ForeColor = SpeedZone(dlMbps, WidgetThemeService.WidgetColors.FromSettings(_settings).DownloadIcon);
        _uploadIcon.ForeColor = SpeedZone(ulMbps, WidgetThemeService.WidgetColors.FromSettings(_settings).UploadIcon);
    }

    /// <summary>Speed-zoned colors per spec §3.1: idle dim, light green, busy amber, heavy rose.</summary>
    private static Color SpeedZone(double mbps, Color light)
    {
        if (mbps <= 0) return ThemeManager.TextDim;
        if (mbps >= 10) return ThemeManager.Metric.Rose;
        if (mbps >= 1) return ThemeManager.Metric.Orange;
        return light;
    }

    public void OpenDashboard(string page = "Dashboard")
    {
        if (_dashboard is null || _dashboard.IsDisposed)
        {
            _dashboard = new DashboardForm(this, _settings);
        }
        _dashboard.Show();
        _dashboard.Activate();
        _dashboard.NavigateTo(page);
    }

    private void ExitApp()
    {
        var r = MessageBox.Show("Exit Speed Meter Pro?", "Speed Meter Pro",
            MessageBoxButtons.OKCancel, MessageBoxIcon.Question);
        if (r != DialogResult.OK) return;
        SettingsStore.Save(_settings);
        _timer.Stop();
        _tray?.Dispose();
        Application.Exit();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (_settings.MinimizeToTrayOnClose && e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            Hide();
            Toast.Show("Speed Meter Pro", "Still running in the tray. Right-click the icon to exit.");
            return;
        }
        base.OnFormClosing(e);
        SettingsStore.Save(_settings);
        _timer.Stop();
        _tray?.Dispose();
    }
}