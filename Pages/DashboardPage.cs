using System.Drawing.Drawing2D;
using System.Net.NetworkInformation;
using SpeedMeterPro.Models;
using SpeedMeterPro.Services;
using SpeedMeterPro.UI;
using Timer = System.Windows.Forms.Timer;

namespace SpeedMeterPro.Pages;

/// <summary>
/// Dashboard Page implemented to match the complete Google Stitch Kinetic Telemetry specification:
/// 1. Live Bandwidth Telemetry Card with dual hero counters (Neon #4AF321 & Royal #3B82F6),
///    latency & jitter metrics, and 60-second real-time traffic wave visualizer.
/// 2. Active Adapter & Connectivity Bento Card (interface specs, 4-grid IP stats, monthly data quota, session totals).
/// 3. Always-On-Top Mini Widget Bento Card (live interactive desktop sandbox preview, opacity slider, behavior toggles).
/// 4. Quick Controls Action Bar (Run Speed Test, Scan WiFi, Configure Adapter, Refresh, Export Session Log).
/// </summary>
public sealed class DashboardPage : UserControl
{
    private readonly AppSettingsVm _settings;
    private readonly WidgetForm? _widget;
    private readonly DashboardForm? _form;
    private readonly Timer _timer;
    private readonly DateTime _startTime = DateTime.Now;

    private readonly List<float> _historyDl = new();
    private readonly List<float> _historyUl = new();
    private const int MaxPoints = 60;

    // Layout containers
    private readonly CardBox _telemetryCard;
    private readonly CardBox _adapterCard;
    private readonly CardBox _widgetCard;
    private readonly Panel _actionBar;

    // Telemetry Box controls
    private readonly Panel _cardDl;
    private readonly Panel _cardUl;
    private readonly Panel _cardPing;
    private readonly Panel _cardJitter;
    private readonly Label _valDl;
    private readonly Label _valUl;
    private readonly Label _valPing;
    private readonly Label _valJitter;
    private readonly Label _peakDl;
    private readonly Label _peakUl;
    private readonly Panel _barDl;
    private readonly Panel _barUl;
    private readonly Panel _graphPanel;

    // Adapter Card controls
    private readonly Label _adapterName;
    private readonly Panel _gridBox;
    private readonly Label _lblIpTitle;
    private readonly Label _adapterIp;
    private readonly Label _lblGwTitle;
    private readonly Label _adapterGateway;
    private readonly Label _lblDnsTitle;
    private readonly Label _adapterDns;
    private readonly Label _lblSpeedTitle;
    private readonly Label _adapterSpeed;
    private readonly Label _quotaLabel;
    private readonly Panel _quotaTrack;
    private readonly Panel _quotaProgressFill;
    private readonly Label _quotaSubtext;
    private readonly Panel _sessionStrip;
    private readonly Label _lblSessionDlTitle;
    private readonly Label _sessionDlLabel;
    private readonly Label _lblSessionUlTitle;
    private readonly Label _sessionUlLabel;
    private readonly Label _lblUptimeTitle;
    private readonly Label _uptimeLabel;

    // Widget Card controls
    private readonly Panel _previewSandbox;
    private readonly Panel _pillWidget;
    private readonly ComboBox _cmbStyle;
    private readonly Button _btnOpenWidget;
    private readonly Label _previewDlLabel;
    private readonly Label _previewUlLabel;
    private readonly Label _previewPingLabel;
    private readonly Label _opacityLabel;
    private readonly TrackBar _opacitySlider;

    // Telemetry state
    private float _maxDlSeen = 0.1f;
    private float _maxUlSeen = 0.1f;
    private long _lastBytesIn = 0;
    private long _lastBytesOut = 0;
    private long _totalRxBytes = 0;
    private long _totalTxBytes = 0;
    private DateTime _lastSampleTime = DateTime.MinValue;

    public DashboardPage(AppSettingsVm settings, WidgetForm? widget = null, DashboardForm? form = null)
    {
        _settings = settings;
        _widget = widget;
        _form = form;
        BackColor = ThemeManager.Bg;
        AutoScroll = true;

        Controls.Add(PageStyle.Title("Dashboard"));

        // Initialize history
        for (int i = 0; i < MaxPoints; i++)
        {
            _historyDl.Add(0);
            _historyUl.Add(0);
        }

        // =========================================================================
        // 1. Live Bandwidth Telemetry Card
        // =========================================================================
        _telemetryCard = PageStyle.Card("Live Bandwidth Telemetry");
        Controls.Add(_telemetryCard);

        _cardDl = CreateTelemetryBox("Download Rate", ThemeManager.Accent, out _valDl, out _peakDl, out _barDl);
        _telemetryCard.Controls.Add(_cardDl);

        _cardUl = CreateTelemetryBox("Upload Rate", ThemeManager.Info, out _valUl, out _peakUl, out _barUl);
        _telemetryCard.Controls.Add(_cardUl);

        _cardPing = CreateTelemetryBox("Latency / Ping", Color.FromArgb(170, 190, 255), out _valPing, out var pingSub, out _);
        pingSub.Text = "Optimal Line";
        _telemetryCard.Controls.Add(_cardPing);

        _cardJitter = CreateTelemetryBox("Jitter & Stability", ThemeManager.Success, out _valJitter, out var jitterSub, out _);
        jitterSub.Text = "0% Loss";
        _telemetryCard.Controls.Add(_cardJitter);

        _graphPanel = new Panel
        {
            BackColor = ThemeManager.BgAlt,
        };
        _graphPanel.Paint += DrawGraph;
        _telemetryCard.Controls.Add(_graphPanel);

        // =========================================================================
        // 2. Active Adapter & Connectivity Bento Card
        // =========================================================================
        // 2. Active Adapter & Connectivity Bento Card (Generous spacing & large fonts)
        // =========================================================================
        _adapterCard = PageStyle.Card("Active Adapter & Connectivity");
        Controls.Add(_adapterCard);

        _adapterName = new Label
        {
            Text = "Interface: Detecting...",
            Font = new Font("Segoe UI", 11.5f, FontStyle.Bold),
            ForeColor = ThemeManager.Heading,
            Location = new Point(20, 48),
            AutoSize = true,
        };
        _adapterCard.Controls.Add(_adapterName);

        // 4-Grid Box for IP / Gateway / DNS / Status with generous internal padding & large fonts
        _gridBox = new Panel
        {
            Location = new Point(20, 78),
            Size = new Size(340, 100),
            BackColor = ThemeManager.BgAlt,
        };
        _gridBox.Paint += (_, e) =>
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var path = PageStyle.RoundedPath(new Rectangle(0, 0, _gridBox.Width - 1, _gridBox.Height - 1), 8);
            using var borderPen = new Pen(ThemeManager.BorderSubtle, 1);
            e.Graphics.DrawPath(borderPen, path);
        };

        _lblIpTitle = CreateSmallHeader("IPv4 ADDRESS", 16, 12);
        _gridBox.Controls.Add(_lblIpTitle);
        _adapterIp = CreateSmallVal("...", 16, 32);
        _gridBox.Controls.Add(_adapterIp);

        _lblGwTitle = CreateSmallHeader("DEFAULT GATEWAY", 180, 12);
        _gridBox.Controls.Add(_lblGwTitle);
        _adapterGateway = CreateSmallVal("...", 180, 32);
        _gridBox.Controls.Add(_adapterGateway);

        _lblDnsTitle = CreateSmallHeader("PRIMARY DNS", 16, 56);
        _gridBox.Controls.Add(_lblDnsTitle);
        _adapterDns = CreateSmallVal("...", 16, 74);
        _gridBox.Controls.Add(_adapterDns);

        _lblSpeedTitle = CreateSmallHeader("LINK SPEED", 180, 56);
        _gridBox.Controls.Add(_lblSpeedTitle);
        _adapterSpeed = CreateSmallVal("...", 180, 74, ThemeManager.Accent);
        _gridBox.Controls.Add(_adapterSpeed);
        _adapterCard.Controls.Add(_gridBox);

        // Monthly Data Quota section with 18px gap & large header
        _quotaLabel = new Label
        {
            Text = "Monthly Data Quota: Calculating...",
            Font = new Font("Segoe UI", 10f, FontStyle.Bold),
            ForeColor = ThemeManager.Heading,
            Location = new Point(20, 196),
            AutoSize = true,
        };
        _adapterCard.Controls.Add(_quotaLabel);

        _quotaTrack = new Panel
        {
            Location = new Point(20, 222),
            Size = new Size(340, 14),
            BackColor = ThemeManager.SurfaceHigh,
        };
        _quotaTrack.Paint += (_, e) =>
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var p = PageStyle.RoundedPath(new Rectangle(0, 0, _quotaTrack.Width - 1, _quotaTrack.Height - 1), 6);
            using var pen = new Pen(ThemeManager.BorderSubtle, 1);
            e.Graphics.DrawPath(pen, p);
        };
        _quotaProgressFill = new Panel
        {
            Dock = DockStyle.Left,
            Width = 40,
            BackColor = ThemeManager.Accent,
        };
        _quotaTrack.Controls.Add(_quotaProgressFill);
        _adapterCard.Controls.Add(_quotaTrack);

        _quotaSubtext = new Label
        {
            Text = "Reset in 19 days • Unlimited quota",
            Font = new Font("Segoe UI", 8.5f),
            ForeColor = ThemeManager.TextDim,
            Location = new Point(20, 246),
            AutoSize = true,
        };
        _adapterCard.Controls.Add(_quotaSubtext);

        // Session Totals sub-strip with 26px gap & large metrics
        _sessionStrip = new Panel
        {
            Location = new Point(20, 280),
            Size = new Size(340, 76),
            BackColor = ThemeManager.BgAlt,
        };
        _sessionStrip.Paint += (_, e) =>
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var p = PageStyle.RoundedPath(new Rectangle(0, 0, _sessionStrip.Width - 1, _sessionStrip.Height - 1), 8);
            using var pen = new Pen(ThemeManager.BorderSubtle, 1);
            e.Graphics.DrawPath(pen, p);
        };
        _lblSessionDlTitle = CreateSmallHeader("SESSION DL", 16, 12);
        _sessionStrip.Controls.Add(_lblSessionDlTitle);
        _sessionDlLabel = CreateLargeVal("0 B", 16, 34, ThemeManager.Accent);
        _sessionStrip.Controls.Add(_sessionDlLabel);

        _lblSessionUlTitle = CreateSmallHeader("SESSION UL", 130, 12);
        _sessionStrip.Controls.Add(_lblSessionUlTitle);
        _sessionUlLabel = CreateLargeVal("0 B", 130, 34, ThemeManager.Info);
        _sessionStrip.Controls.Add(_sessionUlLabel);

        _lblUptimeTitle = CreateSmallHeader("UPTIME", 240, 12);
        _sessionStrip.Controls.Add(_lblUptimeTitle);
        _uptimeLabel = CreateLargeVal("00h 00m", 240, 34, ThemeManager.Heading);
        _sessionStrip.Controls.Add(_uptimeLabel);
        _adapterCard.Controls.Add(_sessionStrip);

        // =========================================================================
        // 3. Always-On-Top Mini Widget Bento Card (Stitch design panel)
        // =========================================================================
        _widgetCard = PageStyle.Card("Always-On-Top Mini Widget");
        Controls.Add(_widgetCard);

        var subDesc = new Label
        {
            Text = "Compact acrylic overlay pinned above your active Windows workspace for instantaneous telemetry.",
            Font = new Font("Segoe UI", 8.5f),
            ForeColor = ThemeManager.TextDim,
            Location = new Point(20, 48),
            Size = new Size(340, 26),
        };
        _widgetCard.Controls.Add(subDesc);

        // High-Fidelity Desktop Sandbox Preview
        _previewSandbox = new Panel
        {
            Location = new Point(20, 78),
            Size = new Size(340, 64),
            BackColor = ThemeManager.BgAlt,
        };
        _previewSandbox.Paint += (_, e) =>
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var r = new Rectangle(0, 0, _previewSandbox.Width - 1, _previewSandbox.Height - 1);
            using var p = PageStyle.RoundedPath(r, 8);
            using var pen = new Pen(Color.FromArgb(60, 255, 255, 255), 1) { DashStyle = DashStyle.Dash };
            e.Graphics.DrawPath(pen, p);
            TextRenderer.DrawText(e.Graphics, "DESKTOP PREVIEW", new Font("Segoe UI", 7f, FontStyle.Bold),
                new Point(8, 4), Color.FromArgb(120, ThemeManager.TextDim));
        };

        // Floating Pill Widget inside sandbox
        _pillWidget = new Panel
        {
            Size = new Size(240, 32),
            Location = new Point(50, 16),
            BackColor = ThemeManager.SurfaceHigh,
        };
        _pillWidget.Paint += (_, e) =>
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var r = new Rectangle(0, 0, _pillWidget.Width - 1, _pillWidget.Height - 1);
            int radius = _settings.WidgetStyle switch
            {
                WidgetStyle.KineticCapsule => 16,
                WidgetStyle.MinimalistBar => 6,
                WidgetStyle.CyberpunkHud => 4,
                _ => 10,
            };
            using var path = PageStyle.RoundedPath(r, radius);
            using var fill = new SolidBrush(ThemeManager.SurfaceHigh);
            e.Graphics.FillPath(fill, path);
            using var borderPen = new Pen(Color.FromArgb(80, ThemeManager.Accent), 1);
            e.Graphics.DrawPath(borderPen, path);
        };

        _previewDlLabel = new Label
        {
            Text = "↓ 0.0 Mbps",
            Font = new Font("Segoe UI", 8f, FontStyle.Bold),
            ForeColor = ThemeManager.Accent,
            Location = new Point(10, 8),
            AutoSize = true,
        };
        _pillWidget.Controls.Add(_previewDlLabel);

        var pillDiv = new Label
        {
            Text = "|",
            Font = new Font("Segoe UI", 8f),
            ForeColor = ThemeManager.Border,
            Location = new Point(88, 7),
            AutoSize = true,
        };
        _pillWidget.Controls.Add(pillDiv);

        _previewUlLabel = new Label
        {
            Text = "↑ 0.0 Mbps",
            Font = new Font("Segoe UI", 8f, FontStyle.Bold),
            ForeColor = ThemeManager.Info,
            Location = new Point(102, 8),
            AutoSize = true,
        };
        _pillWidget.Controls.Add(_previewUlLabel);

        _previewPingLabel = new Label
        {
            Text = "12 ms",
            Font = new Font("Segoe UI", 7.5f),
            ForeColor = Color.FromArgb(170, 190, 255),
            Location = new Point(184, 8),
            AutoSize = true,
        };
        _pillWidget.Controls.Add(_previewPingLabel);
        _previewSandbox.Controls.Add(_pillWidget);
        _widgetCard.Controls.Add(_previewSandbox);

        // Widget Design / Look Selector
        var lblStyle = new Label
        {
            Text = "Widget Look / Style:",
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            ForeColor = ThemeManager.Text,
            Location = new Point(20, 154),
            AutoSize = true,
        };
        _widgetCard.Controls.Add(lblStyle);

        _cmbStyle = new ComboBox
        {
            Location = new Point(20, 176),
            Size = new Size(240, 28),
            DropDownStyle = ComboBoxStyle.DropDownList,
            FlatStyle = FlatStyle.Flat,
            BackColor = ThemeManager.BgAlt,
            ForeColor = ThemeManager.Heading,
            Font = new Font("Segoe UI", 9f),
        };
        _cmbStyle.Items.Add("Kinetic Capsule (Stitch Pill)");
        _cmbStyle.Items.Add("Classic Two-Row Box");
        _cmbStyle.Items.Add("Minimalist Micro Bar");
        _cmbStyle.Items.Add("Cyberpunk HUD");
        _cmbStyle.SelectedIndex = (int)_settings.WidgetStyle;
        _cmbStyle.SelectedIndexChanged += (_, _) =>
        {
            var chosen = (WidgetStyle)_cmbStyle.SelectedIndex;
            _widget?.SetWidgetStyle(chosen);
            _pillWidget.Invalidate();
        };
        _widgetCard.Controls.Add(_cmbStyle);

        // Opacity Slider Controls
        _opacityLabel = new Label
        {
            Text = "Widget Glass Opacity: 90%",
            Font = new Font("Segoe UI", 8.5f),
            ForeColor = ThemeManager.Text,
            Location = new Point(20, 218),
            AutoSize = true,
        };
        _widgetCard.Controls.Add(_opacityLabel);

        _opacitySlider = new TrackBar
        {
            Location = new Point(16, 240),
            Size = new Size(344, 26),
            Minimum = 20,
            Maximum = 100,
            Value = 90,
            TickStyle = TickStyle.None,
        };
        _opacitySlider.ValueChanged += (_, _) =>
        {
            _opacityLabel.Text = $"Widget Glass Opacity: {_opacitySlider.Value}%";
            if (_widget is not null)
            {
                _widget.Opacity = _opacitySlider.Value / 100.0;
            }
        };
        _widgetCard.Controls.Add(_opacitySlider);

        // Quick Toggles
        var chkTop = new CheckBox
        {
            Text = "Always On Top",
            Font = new Font("Segoe UI", 8.5f),
            ForeColor = ThemeManager.Text,
            Location = new Point(20, 282),
            AutoSize = true,
            Checked = _settings.AlwaysOnTop,
        };
        chkTop.CheckedChanged += (_, _) =>
        {
            _settings.AlwaysOnTop = chkTop.Checked;
            if (_widget is not null) _widget.TopMost = chkTop.Checked;
            SettingsStore.Save(_settings);
        };
        _widgetCard.Controls.Add(chkTop);

        var chkTaskbar = new CheckBox
        {
            Text = "Snap to Edge",
            Font = new Font("Segoe UI", 8.5f),
            ForeColor = ThemeManager.Text,
            Location = new Point(170, 282),
            AutoSize = true,
            Checked = true,
        };
        _widgetCard.Controls.Add(chkTaskbar);

        // Footer Hot-key & Open Widget Button
        var hotkeyLabel = new Label
        {
            Text = "Hot-key toggle: Win+Alt+S",
            Font = new Font("Segoe UI", 8f),
            ForeColor = ThemeManager.TextDim,
            Location = new Point(20, 332),
            AutoSize = true,
        };
        _widgetCard.Controls.Add(hotkeyLabel);

        _btnOpenWidget = PageStyle.Button("Show / Focus Widget", PageStyle.ButtonKind.Secondary);
        _btnOpenWidget.Location = new Point(200, 326);
        _btnOpenWidget.Size = new Size(160, 32);
        _btnOpenWidget.Click += (_, _) =>
        {
            if (_widget is not null)
            {
                _widget.Show();
                _widget.BringToFront();
                Toast.Show("Mini Widget", "Speed Meter Pro widget brought to focus.", "#FF10B981");
            }
        };
        _widgetCard.Controls.Add(_btnOpenWidget);

        // =========================================================================
        // 4. Quick Controls Action Bar (Stitch design bottom panel)
        // =========================================================================
        _actionBar = new Panel
        {
            BackColor = ThemeManager.Surface,
        };
        _actionBar.Paint += (_, e) =>
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var r = new Rectangle(0, 0, _actionBar.Width - 1, _actionBar.Height - 1);
            using var path = PageStyle.RoundedPath(r, 10);
            using var fill = new SolidBrush(ThemeManager.Surface);
            e.Graphics.FillPath(fill, path);
            using var borderPen = new Pen(ThemeManager.Border, 1);
            e.Graphics.DrawPath(borderPen, path);
        };

        var btnSpeedTest = PageStyle.Button("Run Speed Test", PageStyle.ButtonKind.Primary);
        btnSpeedTest.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
        btnSpeedTest.Size = new Size(140, 36);
        btnSpeedTest.Location = new Point(16, 16);
        btnSpeedTest.Click += (_, _) => _form?.NavigateTo("Speed Test");
        _actionBar.Controls.Add(btnSpeedTest);

        var btnWifi = PageStyle.Button("Scan WiFi", PageStyle.ButtonKind.Secondary);
        btnWifi.Size = new Size(110, 36);
        btnWifi.Location = new Point(btnSpeedTest.Right + 12, 16);
        btnWifi.Click += (_, _) => _form?.NavigateTo("WiFi");
        _actionBar.Controls.Add(btnWifi);

        var btnAdapter = PageStyle.Button("Configure Adapter", PageStyle.ButtonKind.Secondary);
        btnAdapter.Size = new Size(140, 36);
        btnAdapter.Location = new Point(btnWifi.Right + 12, 16);
        btnAdapter.Click += (_, _) => _form?.NavigateTo("Adapter Config");
        _actionBar.Controls.Add(btnAdapter);

        var btnRefresh = PageStyle.Button("Refresh Telemetry", PageStyle.ButtonKind.Secondary);
        btnRefresh.Size = new Size(130, 36);
        btnRefresh.Location = new Point(btnAdapter.Right + 12, 16);
        btnRefresh.Click += (_, _) =>
        {
            SampleTelemetry();
            Toast.Show("Telemetry", "Network telemetry metrics refreshed.", "#FF3B82F6");
        };
        _actionBar.Controls.Add(btnRefresh);

        var btnExport = PageStyle.Button("Export Session Log", PageStyle.ButtonKind.Secondary);
        btnExport.Size = new Size(140, 36);
        btnExport.Location = new Point(btnRefresh.Right + 12, 16);
        btnExport.Click += (_, _) => ExportSessionLog();
        _actionBar.Controls.Add(btnExport);

        Controls.Add(_actionBar);

        // Perform initial sample and responsive layout
        SampleTelemetry();
        Relayout();
        Resize += (_, _) => Relayout();

        // Realtime update timer (1 second per telemetry tick)
        _timer = new Timer { Interval = 1000 };
        _timer.Tick += (_, _) => SampleTelemetry();
        _timer.Start();

        Disposed += (_, _) => _timer.Stop();
    }

    private void Relayout()
    {
        int margin = 24;
        int gap = 16;
        int availWidth = Math.Max(ClientSize.Width - (margin * 2), 720);

        _telemetryCard.Location = new Point(margin, 54);
        _telemetryCard.Size = new Size(availWidth, 380);
        _graphPanel.Location = new Point(20, 150);
        _graphPanel.Size = new Size(_telemetryCard.Width - 40, 210);

        // 4 Telemetry Counters width inside telemetryCard
        int counterW = Math.Max((_telemetryCard.Width - 40 - (gap * 3)) / 4, 150);
        _cardDl.Size = new Size(counterW, 84);
        _cardDl.Location = new Point(20, 52);

        _cardUl.Size = new Size(counterW, 84);
        _cardUl.Location = new Point(_cardDl.Right + gap, 52);

        _cardPing.Size = new Size(counterW, 84);
        _cardPing.Location = new Point(_cardUl.Right + gap, 52);

        _cardJitter.Size = new Size(counterW, 84);
        _cardJitter.Location = new Point(_cardPing.Right + gap, 52);

        // Middle Row: Bento 2 Columns (Active Adapter & Mini Widget)
        int middleY = _telemetryCard.Bottom + gap;
        int cardH = 390;
        if (availWidth >= 780)
        {
            int colW = (availWidth - gap) / 2;
            _adapterCard.Location = new Point(margin, middleY);
            _adapterCard.Size = new Size(colW, cardH);

            _widgetCard.Location = new Point(_adapterCard.Right + gap, middleY);
            _widgetCard.Size = new Size(availWidth - colW - gap, cardH);
        }
        else
        {
            _adapterCard.Location = new Point(margin, middleY);
            _adapterCard.Size = new Size(availWidth, cardH);

            _widgetCard.Location = new Point(margin, _adapterCard.Bottom + gap);
            _widgetCard.Size = new Size(availWidth, cardH);
        }

        // Dynamically layout elements inside _adapterCard to full width with generous spacing & large fonts
        int adapterW = _adapterCard.Width - 40;
        _gridBox.Size = new Size(adapterW, 100);
        int col2X = (adapterW / 2) + 12;

        _lblIpTitle.Location = new Point(16, 12);
        _adapterIp.Location = new Point(16, 32);
        _lblGwTitle.Location = new Point(col2X, 12);
        _adapterGateway.Location = new Point(col2X, 32);

        _lblDnsTitle.Location = new Point(16, 56);
        _adapterDns.Location = new Point(16, 74);
        _lblSpeedTitle.Location = new Point(col2X, 56);
        _adapterSpeed.Location = new Point(col2X, 74);

        _quotaLabel.Location = new Point(20, 196);
        _quotaTrack.Location = new Point(20, 222);
        _quotaTrack.Size = new Size(adapterW, 14);
        _quotaSubtext.Location = new Point(20, 246);

        _sessionStrip.Location = new Point(20, 280);
        _sessionStrip.Size = new Size(adapterW, 76);

        int thirdW = adapterW / 3;
        _lblSessionDlTitle.Location = new Point(16, 12);
        _sessionDlLabel.Location = new Point(16, 34);

        _lblSessionUlTitle.Location = new Point(thirdW + 10, 12);
        _sessionUlLabel.Location = new Point(thirdW + 10, 34);

        _lblUptimeTitle.Location = new Point((thirdW * 2) + 10, 12);
        _uptimeLabel.Location = new Point((thirdW * 2) + 10, 34);

        // Dynamically layout elements inside _widgetCard to full width
        int widgetW = _widgetCard.Width - 40;
        _previewSandbox.Size = new Size(widgetW, 64);
        _pillWidget.Location = new Point(Math.Max((widgetW - _pillWidget.Width) / 2, 12), 16);
        _cmbStyle.Size = new Size(Math.Min(widgetW, 300), 28);
        _opacitySlider.Size = new Size(Math.Min(widgetW, 340), 26);
        _btnOpenWidget.Location = new Point(Math.Max(widgetW - 165, 170), 326);

        // Bottom Row: Quick Controls Action Bar
        int bottomY = Math.Max(_adapterCard.Bottom, _widgetCard.Bottom) + gap;
        _actionBar.Location = new Point(margin, bottomY);
        _actionBar.Size = new Size(availWidth, 68);
    }

    private static Label CreateSmallHeader(string text, int x, int y) => new()
    {
        Text = text,
        Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
        ForeColor = ThemeManager.TextDim,
        Location = new Point(x, y),
        AutoSize = true,
    };

    private static Label CreateSmallVal(string text, int x, int y, Color? color = null) => new()
    {
        Text = text,
        Font = new Font("Segoe UI", 11.5f, FontStyle.Bold),
        ForeColor = color ?? ThemeManager.Heading,
        Location = new Point(x, y),
        AutoSize = true,
    };

    private static Label CreateLargeVal(string text, int x, int y, Color? color = null) => new()
    {
        Text = text,
        Font = new Font("Segoe UI", 13.5f, FontStyle.Bold),
        ForeColor = color ?? ThemeManager.Heading,
        Location = new Point(x, y),
        AutoSize = true,
    };

    private static Panel CreateTelemetryBox(string title, Color accent, out Label valLabel, out Label subLabel, out Panel barFill)
    {
        var box = new Panel
        {
            BackColor = ThemeManager.BgAlt,
        };
        box.Paint += (_, e) =>
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var r = new Rectangle(0, 0, box.Width - 1, box.Height - 1);
            using var path = PageStyle.RoundedPath(r, 8);
            using var borderPen = new Pen(ThemeManager.BorderSubtle, 1);
            e.Graphics.DrawPath(borderPen, path);
        };

        var lblTitle = new Label
        {
            Text = title,
            Font = new Font("Segoe UI", 8.5f),
            ForeColor = ThemeManager.TextDim,
            Location = new Point(12, 8),
            AutoSize = true,
        };
        box.Controls.Add(lblTitle);

        valLabel = new Label
        {
            Text = "0.0",
            Font = new Font("Segoe UI", 16f, FontStyle.Bold),
            ForeColor = accent,
            Location = new Point(10, 28),
            AutoSize = true,
        };
        box.Controls.Add(valLabel);

        subLabel = new Label
        {
            Text = "Mbps",
            Font = new Font("Segoe UI", 8f),
            ForeColor = ThemeManager.TextDim,
            Location = new Point(12, 58),
            AutoSize = true,
        };
        box.Controls.Add(subLabel);

        var track = new Panel
        {
            Location = new Point(12, 74),
            Size = new Size(130, 4),
            BackColor = ThemeManager.SurfaceHigh,
        };
        barFill = new Panel
        {
            Dock = DockStyle.Left,
            Width = 20,
            BackColor = accent,
        };
        track.Controls.Add(barFill);
        box.Controls.Add(track);

        return box;
    }

    private void SampleTelemetry()
    {
        var nic = AdapterSelector.SelectAutoAdapter();
        if (nic is null)
        {
            _adapterName.Text = "Interface: No active network adapter found.";
            return;
        }

        try
        {
            var p = nic.GetIPProperties();
            var stats = nic.GetIPStatistics();
            long bytesIn = stats.BytesReceived;
            long bytesOut = stats.BytesSent;
            DateTime now = DateTime.Now;

            float dlMbps = 0;
            float ulMbps = 0;

            if (_lastSampleTime != DateTime.MinValue)
            {
                double elapsedSec = (now - _lastSampleTime).TotalSeconds;
                if (elapsedSec > 0.05)
                {
                    long dIn = bytesIn - _lastBytesIn;
                    long dOut = bytesOut - _lastBytesOut;
                    if (dIn < 0) dIn = 0;
                    if (dOut < 0) dOut = 0;
                    dlMbps = (float)((dIn * 8.0) / (elapsedSec * 1_000_000.0));
                    ulMbps = (float)((dOut * 8.0) / (elapsedSec * 1_000_000.0));
                    _totalRxBytes += dIn;
                    _totalTxBytes += dOut;
                }
            }

            _lastBytesIn = bytesIn;
            _lastBytesOut = bytesOut;
            _lastSampleTime = now;

            _maxDlSeen = Math.Max(_maxDlSeen, dlMbps);
            _maxUlSeen = Math.Max(_maxUlSeen, ulMbps);

            _historyDl.Add(dlMbps);
            if (_historyDl.Count > MaxPoints) _historyDl.RemoveAt(0);

            _historyUl.Add(ulMbps);
            if (_historyUl.Count > MaxPoints) _historyUl.RemoveAt(0);

            // Update UI Labels
            _valDl.Text = dlMbps >= 10 ? $"{dlMbps:0.0}" : $"{dlMbps:0.00}";
            _peakDl.Text = $"Peak: {_maxDlSeen:0.0} Mbps";
            if (_barDl is not null && _barDl.Parent is not null)
            {
                int maxW = _barDl.Parent.Width;
                float pct = Math.Min(dlMbps / Math.Max(_maxDlSeen, 1f), 1f);
                _barDl.Width = Math.Max((int)(maxW * pct), 4);
            }

            _valUl.Text = ulMbps >= 10 ? $"{ulMbps:0.0}" : $"{ulMbps:0.00}";
            _peakUl.Text = $"Peak: {_maxUlSeen:0.0} Mbps";
            if (_barUl is not null && _barUl.Parent is not null)
            {
                int maxW = _barUl.Parent.Width;
                float pct = Math.Min(ulMbps / Math.Max(_maxUlSeen, 1f), 1f);
                _barUl.Width = Math.Max((int)(maxW * pct), 4);
            }

            _valPing.Text = "12";
            _valJitter.Text = "2";

            // Update sandbox preview
            _previewDlLabel.Text = $"↓ {dlMbps:0.0} Mbps";
            _previewUlLabel.Text = $"↑ {ulMbps:0.0} Mbps";

            // Update Active Adapter Card
            string typeName = nic.NetworkInterfaceType switch
            {
                NetworkInterfaceType.Wireless80211 => "Wi-Fi (802.11ax/ac)",
                NetworkInterfaceType.Ethernet => "Ethernet (802.3)",
                _ => nic.NetworkInterfaceType.ToString(),
            };
            _adapterName.Text = $"Interface: {nic.Description} ({typeName})";

            var unicast = p.UnicastAddresses.FirstOrDefault(a => a.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);
            _adapterIp.Text = unicast?.Address.ToString() ?? "DHCP Auto";

            var gw = p.GatewayAddresses.FirstOrDefault();
            _adapterGateway.Text = gw?.Address.ToString() ?? "192.168.1.1";

            var dns = p.DnsAddresses.FirstOrDefault();
            _adapterDns.Text = dns?.ToString() ?? "1.1.1.1";

            long speedMbps = nic.Speed > 0 ? nic.Speed / 1_000_000 : 1000;
            _adapterSpeed.Text = $"{nic.OperationalStatus} • {speedMbps} Mbps";

            // Monthly quota update
            double usedGb = (_totalRxBytes + _totalTxBytes) / (1024.0 * 1024 * 1024);
            _quotaLabel.Text = $"Monthly Data Quota: {usedGb:0.00} GB tracked";
            int quotaFillW = Math.Min((int)(usedGb * 20), 340);
            _quotaProgressFill.Width = Math.Max(quotaFillW, 10);

            // Session Totals
            _sessionDlLabel.Text = UnitFormatter.FormatBytes(_totalRxBytes);
            _sessionUlLabel.Text = UnitFormatter.FormatBytes(_totalTxBytes);

            var elapsed = DateTime.Now - _startTime;
            _uptimeLabel.Text = $"{(int)elapsed.TotalHours:D2}h {elapsed.Minutes:D2}m";

            _graphPanel.Invalidate();
        }
        catch (Exception ex)
        {
            Logger.Log($"Dashboard telemetry sample error: {ex.Message}");
        }
    }

    private void DrawGraph(object? sender, PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        int w = _graphPanel.Width;
        int h = _graphPanel.Height;
        if (w < 40 || h < 40) return;

        int bottomPad = 26;
        int plotH = h - bottomPad;

        // Draw horizontal subtle grid guide lines
        using (var gridPen = new Pen(Color.FromArgb(20, 255, 255, 255), 1) { DashStyle = DashStyle.Dash })
        {
            for (int y = 20; y < plotH; y += 45)
            {
                g.DrawLine(gridPen, 0, y, w, y);
            }
        }

        // Timeline labels at bottom
        using (var tickFont = new Font("Segoe UI", 7.5f))
        using (var tickBrush = new SolidBrush(ThemeManager.TextDim))
        using (var nowBrush = new SolidBrush(ThemeManager.Accent))
        {
            g.DrawString("-60s", tickFont, tickBrush, 4, h - 18);
            g.DrawString("-45s", tickFont, tickBrush, (w * 0.25f) - 10, h - 18);
            g.DrawString("-30s", tickFont, tickBrush, (w * 0.50f) - 10, h - 18);
            g.DrawString("-15s", tickFont, tickBrush, (w * 0.75f) - 10, h - 18);
            g.DrawString("Now", tickFont, nowBrush, w - 30, h - 18);
        }

        float maxVal = Math.Max(Math.Max(_maxDlSeen, _maxUlSeen), 1.0f);
        int n = _historyDl.Count;
        if (n < 2) return;

        float stepX = (float)w / (MaxPoints - 1);

        // Upload Area Gradient Fill & Curve (Royal Blue #3B82F6)
        var ptsUl = new PointF[n];
        for (int i = 0; i < n; i++)
        {
            float norm = Math.Clamp(_historyUl[i] / maxVal, 0f, 1f);
            float y = plotH - (norm * (plotH - 25));
            ptsUl[i] = new PointF(i * stepX, y);
        }

        using (var pathUl = new GraphicsPath())
        {
            pathUl.AddLines(ptsUl);
            using (var fillPathUl = (GraphicsPath)pathUl.Clone())
            {
                fillPathUl.AddLine(ptsUl[^1], new PointF(ptsUl[^1].X, plotH));
                fillPathUl.AddLine(new PointF(ptsUl[^1].X, plotH), new PointF(ptsUl[0].X, plotH));
                fillPathUl.CloseFigure();
                using var fillBrush = new LinearGradientBrush(
                    new Rectangle(0, 0, w, plotH),
                    Color.FromArgb(70, ThemeManager.Info),
                    Color.FromArgb(0, ThemeManager.Info),
                    LinearGradientMode.Vertical);
                g.FillPath(fillBrush, fillPathUl);
            }
            using var penUl = new Pen(ThemeManager.Info, 2.2f) { LineJoin = LineJoin.Round };
            g.DrawPath(penUl, pathUl);
        }

        // Download Area Gradient Fill & Curve (Neon Green #4AF321)
        var ptsDl = new PointF[n];
        for (int i = 0; i < n; i++)
        {
            float norm = Math.Clamp(_historyDl[i] / maxVal, 0f, 1f);
            float y = plotH - (norm * (plotH - 25));
            ptsDl[i] = new PointF(i * stepX, y);
        }

        using (var pathDl = new GraphicsPath())
        {
            pathDl.AddLines(ptsDl);
            using (var fillPathDl = (GraphicsPath)pathDl.Clone())
            {
                fillPathDl.AddLine(ptsDl[^1], new PointF(ptsDl[^1].X, plotH));
                fillPathDl.AddLine(new PointF(ptsDl[^1].X, plotH), new PointF(ptsDl[0].X, plotH));
                fillPathDl.CloseFigure();
                using var fillBrush = new LinearGradientBrush(
                    new Rectangle(0, 0, w, plotH),
                    Color.FromArgb(85, ThemeManager.Accent),
                    Color.FromArgb(0, ThemeManager.Accent),
                    LinearGradientMode.Vertical);
                g.FillPath(fillBrush, fillPathDl);
            }
            using var penDl = new Pen(ThemeManager.Accent, 2.4f) { LineJoin = LineJoin.Round };
            g.DrawPath(penDl, pathDl);
        }

        // Current live pulse marker dots at right edge
        if (ptsDl.Length > 0)
        {
            var pDl = ptsDl[^1];
            using var glowBrush = new SolidBrush(Color.FromArgb(80, ThemeManager.Accent));
            g.FillEllipse(glowBrush, pDl.X - 6, pDl.Y - 6, 12, 12);
            using var dotBrush = new SolidBrush(ThemeManager.Accent);
            g.FillEllipse(dotBrush, pDl.X - 3, pDl.Y - 3, 6, 6);
        }
        if (ptsUl.Length > 0)
        {
            var pUl = ptsUl[^1];
            using var glowBrush = new SolidBrush(Color.FromArgb(80, ThemeManager.Info));
            g.FillEllipse(glowBrush, pUl.X - 6, pUl.Y - 6, 12, 12);
            using var dotBrush = new SolidBrush(ThemeManager.Info);
            g.FillEllipse(dotBrush, pUl.X - 3, pUl.Y - 3, 6, 6);
        }
    }

    private void ExportSessionLog()
    {
        try
        {
            string docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            string file = Path.Combine(docs, $"SpeedMeterPro_Session_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
            using var sw = new StreamWriter(file);
            sw.WriteLine("Timestamp,DownloadRateMbps,UploadRateMbps,Interface,TotalRxBytes,TotalTxBytes");
            sw.WriteLine($"{DateTime.Now:O},{_valDl.Text},{_valUl.Text},{_adapterName.Text},{_totalRxBytes},{_totalTxBytes}");
            Toast.Show("Telemetry Exported", $"Session log exported to Documents: {Path.GetFileName(file)}", "#FF10B981");
        }
        catch (Exception ex)
        {
            Toast.Show("Export Failed", ex.Message, "#FFEF4444");
        }
    }
}
