using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using SpeedMeterPro.Models;
using SpeedMeterPro.Pages;
using SpeedMeterPro.Services;

namespace SpeedMeterPro.UI;

/// <summary>
/// Main shell (spec §3.2): header with title/subtitle + accent, left nav
/// sidebar (200→60 collapsible, icon+label, active/hover states, lock badge,
/// DPI-aware) hosting all 15 pages; main content area swaps pages.
/// Custom themed window chrome matching the dark neon/cyberpunk aesthetic.
/// </summary>
public sealed class DashboardForm : Form
{
    [DllImport("user32.dll")]
    private static extern bool ReleaseCapture();

    [DllImport("user32.dll")]
    private static extern nint SendMessage(nint hWnd, int msg, nint wParam, nint lParam);

    private const int WM_NCLBUTTONDOWN = 0xA1;
    private const int HTCAPTION = 0x2;
    private const int WM_NCHITTEST = 0x84;
    private const int HTLEFT = 10;
    private const int HTRIGHT = 11;
    private const int HTTOP = 12;
    private const int HTTOPLEFT = 13;
    private const int HTTOPRIGHT = 14;
    private const int HTBOTTOM = 15;
    private const int HTBOTTOMLEFT = 16;
    private const int HTBOTTOMRIGHT = 17;
    private const int ResizeBorder = 8;

    private readonly WidgetForm _widget;
    private readonly AppSettingsVm _settings;
    private readonly Panel _sidebar;
    private Panel? _navPanel;
    private readonly Panel _contentHost;
    private readonly Panel _pageHost;
    private readonly Panel _header;
    private readonly Label _logoTitle;
    private readonly List<(NavItem Item, NavButton Btn)> _nav = new();
    private bool _collapsed;
    private NavItem? _current;
    private const int ExpandedWidth = 220;
    private const int CollapsedWidth = 60;
    private const int HeaderHeight = 64;

    // Window controls
    private Panel? _windowControls;
    private Panel? _btnMin;
    private Panel? _btnMax;
    private Panel? _btnClose;
    private Button? _quickTestBtn;

    public DashboardForm(WidgetForm widget, AppSettingsVm settings)
    {
        _widget = widget;
        _settings = settings;
        Text = "Speed Meter Pro - Dashboard";
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.None;
        DoubleBuffered = true;
        SetStyle(ControlStyles.ResizeRedraw, true);

        ClientSize = new Size(1080, 680);
        MinimumSize = new Size(860, 560);
        ThemeManager.IsDark = _settings.IsDarkMode;
        BackColor = ThemeManager.Bg;
        ForeColor = ThemeManager.Text;
        _collapsed = _settings.IsSidebarCollapsed;

        _sidebar = new Panel
        {
            Dock = DockStyle.Left,
            Width = _collapsed ? CollapsedWidth : ExpandedWidth,
            BackColor = ThemeManager.Sidebar,
        };
        _contentHost = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = ThemeManager.Bg,
        };
        _header = new Panel
        {
            Dock = DockStyle.Top,
            Height = HeaderHeight,
            BackColor = ThemeManager.Surface,
        };
        _pageHost = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = ThemeManager.Bg,
        };

        _contentHost.Controls.Add(_pageHost);
        _contentHost.Controls.Add(_header);
        Controls.Add(_contentHost);
        Controls.Add(_sidebar);

        _logoTitle = new Label
        {
            Text = "SpeedMeterPro",
            Font = new Font("Segoe UI", 10.5f, FontStyle.Bold),
            ForeColor = ThemeManager.Heading,
            Location = new Point(42, 20),
            AutoSize = true,
        };

        BuildHeader();
        BuildSidebar();
        SelectPage(BuildNav()[0]);
    }

    protected override CreateParams CreateParams
    {
        get
        {
            const int CS_DROPSHADOW = 0x20000;
            const int WS_MINIMIZEBOX = 0x20000;
            const int WS_MAXIMIZEBOX = 0x10000;

            var cp = base.CreateParams;
            cp.ClassStyle |= CS_DROPSHADOW;
            cp.Style |= WS_MINIMIZEBOX | WS_MAXIMIZEBOX;
            return cp;
        }
    }

    private void DragWindow()
    {
        if (WindowState == FormWindowState.Maximized) return;
        ReleaseCapture();
        SendMessage(Handle, WM_NCLBUTTONDOWN, HTCAPTION, 0);
    }

    public void ToggleMaximize()
    {
        if (WindowState == FormWindowState.Maximized)
        {
            WindowState = FormWindowState.Normal;
        }
        else
        {
            MaximizedBounds = Screen.FromHandle(Handle).WorkingArea;
            WindowState = FormWindowState.Maximized;
        }
        _btnMax?.Invalidate();
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_NCHITTEST && WindowState == FormWindowState.Normal)
        {
            base.WndProc(ref m);
            var pos = PointToClient(new Point(m.LParam.ToInt32()));

            bool left = pos.X <= ResizeBorder;
            bool right = pos.X >= ClientSize.Width - ResizeBorder;
            bool top = pos.Y <= ResizeBorder;
            bool bottom = pos.Y >= ClientSize.Height - ResizeBorder;

            if (top && left) { m.Result = HTTOPLEFT; return; }
            if (top && right) { m.Result = HTTOPRIGHT; return; }
            if (bottom && left) { m.Result = HTBOTTOMLEFT; return; }
            if (bottom && right) { m.Result = HTBOTTOMRIGHT; return; }
            if (left) { m.Result = HTLEFT; return; }
            if (right) { m.Result = HTRIGHT; return; }
            if (top) { m.Result = HTTOP; return; }
            if (bottom) { m.Result = HTBOTTOM; return; }
            return;
        }

        base.WndProc(ref m);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (WindowState != FormWindowState.Maximized)
        {
            using var p = new Pen(ThemeManager.Border, 1);
            e.Graphics.DrawRectangle(p, 0, 0, Width - 1, Height - 1);
        }
    }

    private void BuildHeader()
    {
        _header.Paint += (_, e) =>
        {
            // Bottom dividing border
            using var p = new Pen(ThemeManager.Border);
            e.Graphics.DrawLine(p, 0, _header.Height - 1, _header.Width, _header.Height - 1);

            // Subtle neon accent indicator at the very top edge of the header
            using var accentPen = new Pen(Color.FromArgb(140, ThemeManager.Accent), 2);
            e.Graphics.DrawLine(accentPen, 0, 0, _header.Width, 0);
        };

        // Window drag handlers for header area
        _header.MouseDown += (_, e) =>
        {
            if (e.Button == MouseButtons.Left) DragWindow();
        };
        _header.DoubleClick += (_, _) => ToggleMaximize();

        var icon = new PictureBox
        {
            Image = FaIcons.Bitmap(IconChar.GaugeHigh, 28, ThemeManager.Accent),
            SizeMode = PictureBoxSizeMode.Zoom,
            Size = new Size(28, 28),
            Location = new Point(22, 18),
            BackColor = Color.Transparent,
            Cursor = Cursors.Hand,
        };
        icon.MouseDown += (_, e) => { if (e.Button == MouseButtons.Left) DragWindow(); };

        var title = new Label
        {
            Text = "Speed Meter Pro",
            Font = new Font("Segoe UI", 13, FontStyle.Bold),
            ForeColor = ThemeManager.Heading,
            Location = new Point(56, 12),
            AutoSize = true,
        };
        title.MouseDown += (_, e) => { if (e.Button == MouseButtons.Left) DragWindow(); };
        title.DoubleClick += (_, _) => ToggleMaximize();

        var sub = new Label
        {
            Text = "Windows 11 Network Monitor • Telemetry",
            Font = new Font("Segoe UI", 8.5f),
            ForeColor = ThemeManager.TextDim,
            Location = new Point(57, 36),
            AutoSize = true,
        };
        sub.MouseDown += (_, e) => { if (e.Button == MouseButtons.Left) DragWindow(); };
        sub.DoubleClick += (_, _) => ToggleMaximize();

        // Stitch Live Status Pill
        var statusBadge = new Panel
        {
            Location = new Point(350, 18),
            Size = new Size(185, 28),
            BackColor = ThemeManager.SurfaceHigh,
        };
        statusBadge.Paint += (_, e) =>
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var path = PageStyle.RoundedPath(new Rectangle(0, 0, statusBadge.Width - 1, statusBadge.Height - 1), 14);
            using var borderPen = new Pen(Color.FromArgb(40, ThemeManager.Accent), 1);
            e.Graphics.DrawPath(borderPen, path);
            using var dot = new SolidBrush(ThemeManager.Accent);
            e.Graphics.FillEllipse(dot, 10, 10, 8, 8);
        };
        var statusText = new Label
        {
            Text = "Live Monitoring Active",
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            ForeColor = ThemeManager.Accent,
            Location = new Point(24, 6),
            AutoSize = true,
        };
        statusBadge.Controls.Add(statusText);

        // Custom Window Control Buttons (Minimize, Maximize/Restore, Close)
        _windowControls = new Panel
        {
            Size = new Size(138, 36),
            BackColor = Color.Transparent,
        };

        _btnMin = CreateWindowControlButton(WindowControlAction.Minimize);
        _btnMax = CreateWindowControlButton(WindowControlAction.Maximize);
        _btnClose = CreateWindowControlButton(WindowControlAction.Close);

        _btnMin.Location = new Point(0, 0);
        _btnMax.Location = new Point(46, 0);
        _btnClose.Location = new Point(92, 0);

        _windowControls.Controls.Add(_btnMin);
        _windowControls.Controls.Add(_btnMax);
        _windowControls.Controls.Add(_btnClose);

        // Stitch Quick Action: Run Speed Test button on top bar
        _quickTestBtn = PageStyle.Button("Run Speed Test", PageStyle.ButtonKind.Primary);
        _quickTestBtn.Height = 32;
        _quickTestBtn.Click += (_, _) => NavigateTo("Speed Test");

        void RepositionHeaderControls()
        {
            if (_windowControls != null)
            {
                _windowControls.Location = new Point(_header.Width - _windowControls.Width, 0);
            }
            if (_quickTestBtn != null && _windowControls != null)
            {
                _quickTestBtn.Location = new Point(_windowControls.Left - _quickTestBtn.Width - 16, 16);
            }
        }

        _header.Resize += (_, _) => RepositionHeaderControls();
        RepositionHeaderControls();

        _header.Controls.Add(icon);
        _header.Controls.Add(title);
        _header.Controls.Add(sub);
        _header.Controls.Add(statusBadge);
        _header.Controls.Add(_quickTestBtn);
        _header.Controls.Add(_windowControls);
    }

    private enum WindowControlAction { Minimize, Maximize, Close }

    private Panel CreateWindowControlButton(WindowControlAction action)
    {
        var btn = new Panel
        {
            Size = new Size(46, 36),
            BackColor = Color.Transparent,
            Cursor = Cursors.Hand,
        };

        bool isHovered = false;

        btn.MouseEnter += (_, _) =>
        {
            isHovered = true;
            btn.BackColor = action == WindowControlAction.Close
                ? Color.FromArgb(232, 17, 35) // Windows 11 Fluent Crimson
                : ThemeManager.SurfaceHigh;
            btn.Invalidate();
        };

        btn.MouseLeave += (_, _) =>
        {
            isHovered = false;
            btn.BackColor = Color.Transparent;
            btn.Invalidate();
        };

        btn.Paint += (_, e) =>
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var iconColor = (action == WindowControlAction.Close && isHovered)
                ? Color.White
                : (isHovered ? ThemeManager.Heading : ThemeManager.TextDim);

            using var pen = new Pen(iconColor, 1.8f);

            switch (action)
            {
                case WindowControlAction.Minimize:
                    // Horizontal line
                    e.Graphics.DrawLine(pen, 17, 18, 29, 18);
                    break;

                case WindowControlAction.Maximize:
                    if (WindowState == FormWindowState.Maximized)
                    {
                        // Restore icon: two overlapping squares
                        using var thinPen = new Pen(iconColor, 1.4f);
                        // Back square
                        e.Graphics.DrawRectangle(thinPen, 20, 13, 8, 8);
                        // Front square
                        e.Graphics.FillRectangle(new SolidBrush(isHovered ? ThemeManager.SurfaceHigh : ThemeManager.Surface), 17, 16, 8, 8);
                        e.Graphics.DrawRectangle(thinPen, 17, 16, 8, 8);
                    }
                    else
                    {
                        // Maximize icon: single square
                        e.Graphics.DrawRectangle(pen, 18, 13, 10, 10);
                    }
                    break;

                case WindowControlAction.Close:
                    // 'X' icon
                    e.Graphics.DrawLine(pen, 18, 13, 28, 23);
                    e.Graphics.DrawLine(pen, 28, 13, 18, 23);
                    break;
            }
        };

        btn.Click += (_, _) =>
        {
            switch (action)
            {
                case WindowControlAction.Minimize:
                    WindowState = FormWindowState.Minimized;
                    break;
                case WindowControlAction.Maximize:
                    ToggleMaximize();
                    break;
                case WindowControlAction.Close:
                    Close();
                    break;
            }
        };

        return btn;
    }
    private List<NavItem> BuildNav()    {
        var entitlement = Entitlement.AllAccessEntitlementProvider.Instance;
        var items = new List<NavItem>        {
            new() {
 Label = "Dashboard", Icon = "", Glyph = IconChar.GaugeHigh, AlwaysFree = true }
,            new() {
 Label = "Settings", Icon = "", Glyph = IconChar.Gear, AlwaysFree = true }
,            new() {
 Label = "Adapter Config", Icon = "", Glyph = IconChar.Ethernet, AlwaysFree = true }
,            new() {
 Label = "Speed Test", Icon = "", Glyph = IconChar.Bolt, AlwaysFree = true }
,            new() {
 Label = "Test History", Icon = "", Glyph = IconChar.ClockRotateLeft, AlwaysFree = true }
,            new() {
 Label = "Usage Stats", Icon = "", Glyph = IconChar.ChartColumn, AlwaysFree = true }
,            new() {
 Label = "Data Plan", Icon = "", Glyph = IconChar.FileInvoiceDollar, AlwaysFree = true }
,            new() {
 Label = "WiFi", Icon = "", Glyph = IconChar.Wifi, AlwaysFree = true }
,            new() {
 Label = "Network Info", Icon = "", Glyph = IconChar.CircleInfo, AlwaysFree = true }
,            new() {
 Label = "Network Interface", Icon = "", Glyph = IconChar.NetworkWired, AlwaysFree = true }
,            new() {
 Label = "Active Process", Icon = "", Glyph = IconChar.Microchip, AlwaysFree = true }
,            new() {
 Label = "Network Tools", Icon = "", Glyph = IconChar.ToolBox, AlwaysFree = true }
,            new() {
 Label = "Stop Watch", Icon = "", Glyph = IconChar.Stopwatch, AlwaysFree = true }
,            new() {
 Label = "Our Apps", Icon = "", Glyph = IconChar.Store, AlwaysFree = true }
,            new() {
 Label = "About", Icon = "", Glyph = IconChar.CircleQuestion, AlwaysFree = true }
,        };
        foreach (var item in items)        {
            item.ControlType = item.Label switch            {
                "Dashboard" => typeof(DashboardPage),                "Settings" => typeof(SettingsPage),                "Adapter Config" => typeof(AdapterConfigPage),                "Speed Test" => typeof(SpeedTestPage),                "Test History" => typeof(TestHistoryPage),                "Usage Stats" => typeof(UsageStatsPage),                "Data Plan" => typeof(DataPlanPage),                "WiFi" => typeof(WifiPage),                "Network Info" => typeof(NetworkInfoPage),                "Network Interface" => typeof(NetworkInterfacePage),                "Active Process" => typeof(ActiveProcessPage),                "Network Tools" => typeof(NetworkToolsPage),                "Stop Watch" => typeof(StopWatchPage),                "Our Apps" => typeof(OurAppsPage),                "About" => typeof(AboutPage),                _ => null,            };        }
        return items;    }
    private void BuildSidebar()    {
        _sidebar.Paint += (_, e) =>        {
            using var p = new Pen(ThemeManager.Border);
            e.Graphics.DrawLine(p, _sidebar.Width - 1, 0, _sidebar.Width - 1, _sidebar.Height);        };
        var logo = new Panel        {
            Dock = DockStyle.Top,            Height = 64,            BackColor = ThemeManager.Sidebar,        };
        var logoIcon = new PictureBox        {
            Image = FaIcons.Bitmap(IconChar.GaugeHigh, 26, ThemeManager.Accent),            SizeMode = PictureBoxSizeMode.Zoom,            Size = new Size(26, 26),            Location = new Point(16, 18),            BackColor = Color.Transparent,        };
        logo.Controls.Add(logoIcon);
        logo.Controls.Add(_logoTitle);
        _logoTitle.Visible = !_collapsed;
        var collapse = new Button        {
            Text = "«",            Dock = DockStyle.Bottom,            Height = 34,            FlatStyle = FlatStyle.Flat,            BackColor = ThemeManager.BgAlt,            ForeColor = ThemeManager.TextDim,        };
        collapse.FlatAppearance.BorderSize = 0;
        collapse.Click += (_, _) => ToggleCollapse();
        _navPanel = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            BackColor = ThemeManager.Sidebar,
        };
        _sidebar.Controls.Add(collapse);
        _sidebar.Controls.Add(logo);
        _sidebar.Controls.Add(_navPanel);
        _navPanel.BringToFront();

        int btnWidth = _collapsed ? CollapsedWidth - 16 : ExpandedWidth - 28;
        int topY = 8;
        foreach (var item in BuildNav())
        {
            var icon = item.Glyph == IconChar.None ? null : FaIcons.Bitmap(item.Glyph, 24, NavColor(item.Label));
            var btn = PageStyle.NavButton(_collapsed ? "" : item.Label, icon);
            btn.Location = new Point(8, topY);
            btn.Size = new Size(btnWidth, 40);
            btn.TextAlign = _collapsed ? ContentAlignment.MiddleCenter : ContentAlignment.MiddleLeft;
            btn.Click += (_, _) => SelectPage(item);
            _navPanel.Controls.Add(btn);
            _nav.Add((item, btn));
            topY += 44;
        }
    }

    private static Color NavColor(string label) => label switch
    {
        "Dashboard" => ThemeManager.Accent,
        "Settings" => ThemeManager.Metric.Latency,
        "Adapter Config" => ThemeManager.Metric.Upload,
        "Speed Test" => ThemeManager.Metric.Orange,
        "Test History" => ThemeManager.Metric.Jitter,
        "Usage Stats" => ThemeManager.Metric.Download,
        "Data Plan" => ThemeManager.Metric.Rose,
        "WiFi" => ThemeManager.Metric.Upload,
        "Network Info" => ThemeManager.Metric.Latency,
        "Network Interface" => ThemeManager.Metric.Download,
        "Active Process" => ThemeManager.Metric.Orange,
        "Network Tools" => ThemeManager.Metric.Jitter,
        "Stop Watch" => ThemeManager.Metric.Download,
        "Our Apps" => ThemeManager.Metric.Upload,
        "About" => ThemeManager.Metric.Latency,
        _ => ThemeManager.Text,
    };

    /// <summary>Re-apply theme + rebuild current page after settings changed.</summary>
    public void ApplyUserSettings()
    {
        ThemeManager.IsDark = _settings.IsDarkMode;
        BackColor = ThemeManager.Bg;
        ForeColor = ThemeManager.Text;
        _sidebar.BackColor = ThemeManager.Sidebar;
        if (_navPanel is not null) _navPanel.BackColor = ThemeManager.Sidebar;
        _header.BackColor = ThemeManager.Surface;
        _contentHost.BackColor = ThemeManager.Bg;
        _pageHost.BackColor = ThemeManager.Bg;
        foreach (var (_, btn) in _nav)
        {
            var it = _nav.First(x => x.Btn == btn).Item;
            btn.Icon = it.Glyph == IconChar.None ? null : FaIcons.Bitmap(it.Glyph, 24, NavColor(it.Label));
        }
        if (_current is not null) SelectPage(_current);
        ApplyUserFont();
        Invalidate();
    }

    /// <summary>Push the user font (family/size/bold) onto every control of the current page.</summary>
    public void ApplyUserFont()
    {
        Font font;
        try
        {
            // FIX(layout-1): decouple the dashboard from the widget FontSize.
            // Every page is authored with fixed pixel geometry (card sizes,
            // label coordinates); previously this method scaled every control
            // to the widget's 8-24pt size without moving siblings, so larger
            // sizes made text overlap and crowd the dashboard. The dashboard
            // now uses a fixed comfortable size (10pt) and honors only the
            // font family / bold flag; FontSize remains a widget-only setting.
            font = new Font(_settings.FontName, 10f,
                _settings.IsFontBold ? FontStyle.Bold : FontStyle.Regular);
        }
        catch
        {
            return;
        }
        foreach (Control c in _pageHost.Controls)
        {
            ApplyFontRecursive(c, font);
        }
    }

    private static void ApplyFontRecursive(Control c, Font font)
    {
        c.Font = font;
        foreach (Control child in c.Controls) ApplyFontRecursive(child, font);
    }

    private void ToggleCollapse()    {
        _collapsed = !_collapsed;
        _sidebar.Width = _collapsed ? CollapsedWidth : ExpandedWidth;
        int btnWidth = _collapsed ? CollapsedWidth - 16 : ExpandedWidth - 28;
        foreach (var (_, btn) in _nav)        {
            var item = _nav.First(x => x.Btn == btn).Item;
            btn.Text = _collapsed ? "" : item.Label;
            btn.Width = btnWidth;
            btn.TextAlign = _collapsed ? ContentAlignment.MiddleCenter : ContentAlignment.MiddleLeft;        }
        _logoTitle.Visible = !_collapsed;
        _settings.IsSidebarCollapsed = _collapsed;
        SettingsStore.Save(_settings);    }
    public void NavigateTo(string pageLabel)    {
        var item = _nav.Select(x => x.Item).FirstOrDefault(i => i.Label == pageLabel);
        if (item is not null) SelectPage(item);    }
    private void SelectPage(NavItem item)    {
        if (item.ControlType is null) return;
        _current = item;
        foreach (var (it, btn) in _nav)        {
            btn.Selected = ReferenceEquals(it, item);
            btn.Invalidate();        }
        _pageHost.Controls.Clear();
        UserControl page;
        if (item.ControlType == typeof(SettingsPage))
        {
            page = new SettingsPage(_settings, _widget, this);
        }
        else if (item.ControlType == typeof(DashboardPage))
        {
            page = new DashboardPage(_settings, _widget, this);
        }
        else
        {
            page = (UserControl)Activator.CreateInstance(item.ControlType, _settings)!;
        }
        page.Dock = DockStyle.Fill;
        _pageHost.Controls.Add(page);
        if (item.ControlType != typeof(DashboardPage))
        {
            var cards = new List<Control>();
            CollectCards(page, cards);
            if (cards.Count > 0) PageStyle.FillLayout(page, cards.ToArray());
        }
        Text = $"Speed Meter Pro - {item.Label}";    }

    /// <summary>Collect top-level CardBox surfaces so pages can be centered/stretched to the screen width.</summary>
    private static void CollectCards(Control root, List<Control> cards)
    {
        foreach (Control c in root.Controls)
        {
            if (c is CardBox) { cards.Add(c); continue; }
            CollectCards(c, cards);
        }
    }
    protected override void OnFormClosed(FormClosedEventArgs e)    {
        base.OnFormClosed(e);
        SettingsStore.Save(_settings);
        _widget.Show();    }}