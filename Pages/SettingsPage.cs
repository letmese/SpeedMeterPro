using SpeedMeterPro.Models;
using SpeedMeterPro.Services;
using SpeedMeterPro.UI;

namespace SpeedMeterPro.Pages;

public sealed class SettingsPage : UserControl
{
    private readonly AppSettingsVm _s;
    private readonly WidgetForm _widget;
    private readonly DashboardForm _dashboard;
    private Panel _swatchBg = null!;
    private Panel _swatchDlIcon = null!;
    private Panel _swatchDlLabel = null!;
    private Panel _swatchUlIcon = null!;
    private Panel _swatchUlLabel = null!;
    private CheckBox _transparent = null!;
    private ComboBox _preset = null!;

    public SettingsPage(AppSettingsVm settings, WidgetForm widget, DashboardForm dashboard)
    {
        _s = settings;
        _widget = widget;
        _dashboard = dashboard;
        BackColor = ThemeManager.Bg;
        AutoScroll = true;
        Controls.Add(PageStyle.Title("Settings"));

        int cardTop = 72;
        var appCard = BuildAppearanceCard(cardTop);
        Controls.Add(appCard);
        cardTop += appCard.Height + 24;

        var themeCard = BuildThemeCard(cardTop);
        Controls.Add(themeCard);
        cardTop += themeCard.Height + 24;

        Controls.Add(BuildInfoLine(cardTop));
        UpdateThemeControls();
    }

    private Control BuildAppearanceCard(int top)
    {
        var group = PageStyle.Card("Appearance & behavior");
        group.Location = new Point(24, top);
        int y = 56;

        var dark = PageStyle.Check("Dark mode", _s.IsDarkMode);
        dark.Location = new Point(24, y);
        group.Controls.Add(dark);
        dark.CheckedChanged += (_, _) => _s.IsDarkMode = dark.Checked;

        y += 34;
        var onTop = PageStyle.Check("Always on top (widget)", _s.AlwaysOnTop);
        onTop.Location = new Point(24, y);
        group.Controls.Add(onTop);
        onTop.CheckedChanged += (_, _) => _s.AlwaysOnTop = onTop.Checked;

        y += 34;
        var tray = PageStyle.Check("Minimize to tray on close", _s.MinimizeToTrayOnClose);
        tray.Location = new Point(24, y);
        group.Controls.Add(tray);
        tray.CheckedChanged += (_, _) => _s.MinimizeToTrayOnClose = tray.Checked;

        y += 34;
        var taskbar = PageStyle.Check("Keep widget in taskbar", _s.KeepInTaskbar);
        taskbar.Location = new Point(24, y);
        group.Controls.Add(taskbar);
        taskbar.CheckedChanged += (_, _) => _s.KeepInTaskbar = taskbar.Checked;

        y += 34;
        var startup = PageStyle.Check("Run at startup", _s.RunAtStartup);
        startup.Location = new Point(24, y);
        group.Controls.Add(startup);
        startup.CheckedChanged += (_, _) =>
        {
            _s.RunAtStartup = startup.Checked;
            try { StartupManager.SetEnabled(startup.Checked); } catch { }
        };

        y += 38;
        group.Controls.Add(new Label
        {
            Text = "Refresh interval (ms):",
            ForeColor = ThemeManager.Text,
            Location = new Point(24, y),
            AutoSize = true,
        });
        var interval = new NumericUpDown
        {
            Minimum = 300,
            Maximum = 60000,
            Value = Math.Clamp(_s.SpeedRefreshTime, 300, 60000),
            Increment = 100,
            Location = new Point(195, y - 2),
            Width = 130,
            BackColor = ThemeManager.BgAlt,
            ForeColor = ThemeManager.Text,
            BorderStyle = BorderStyle.FixedSingle,
        };
        group.Controls.Add(interval);
        interval.ValueChanged += (_, _) => _s.SpeedRefreshTime = (int)interval.Value;

        y += 38;
        group.Controls.Add(new Label
        {
            Text = "Font:",
            ForeColor = ThemeManager.Text,
            Location = new Point(24, y),
            AutoSize = true,
        });
        var fontBox = PageStyle.TextBox(_s.FontName);
        fontBox.Location = new Point(195, y - 2);
        fontBox.Width = 170;
        group.Controls.Add(fontBox);
        fontBox.TextChanged += (_, _) => _s.FontName = string.IsNullOrWhiteSpace(fontBox.Text) ? "Segoe UI" : fontBox.Text;

        y += 38;
        group.Controls.Add(new Label
        {
            Text = "Font size:",
            ForeColor = ThemeManager.Text,
            Location = new Point(24, y),
            AutoSize = true,
        });
        var fontSize = new NumericUpDown
        {
            Minimum = 8,
            Maximum = 24,
            Value = Math.Clamp(_s.FontSize, 8, 24),
            Location = new Point(195, y - 2),
            Width = 130,
            BackColor = ThemeManager.BgAlt,
            ForeColor = ThemeManager.Text,
            BorderStyle = BorderStyle.FixedSingle,
        };
        group.Controls.Add(fontSize);
        fontSize.ValueChanged += (_, _) => _s.FontSize = (int)fontSize.Value;

        y += 34;
        var bold = PageStyle.Check("Bold font", _s.IsFontBold);
        bold.Location = new Point(24, y);
        group.Controls.Add(bold);
        bold.CheckedChanged += (_, _) => _s.IsFontBold = bold.Checked;

        y += 38;
        group.Controls.Add(new Label
        {
            Text = "Speed display unit:",
            ForeColor = ThemeManager.Text,
            Location = new Point(24, y),
            AutoSize = true,
        });
        var unitCombo = PageStyle.Combo();
        unitCombo.Items.AddRange(new object[]
        {
            "Automatic (Kbps / Mbps / Gbps)",
            "Mbps (fixed)",
            "MB/s (KB/s / MB/s)",
        });
        unitCombo.SelectedIndex = Math.Clamp(_s.SpeedUnitMode, 0, 2);
        unitCombo.Location = new Point(195, y - 3);
        unitCombo.Width = 260;
        group.Controls.Add(unitCombo);
        unitCombo.SelectedIndexChanged += (_, _) =>
        {
            _s.SpeedUnitMode = unitCombo.SelectedIndex;
            _s.IsUnitSpeedByte = _s.SpeedUnitMode == 2;
            _s.IsAutomaticSpeedUnit = _s.SpeedUnitMode == 0;
            SettingsStore.Save(_s);
            _widget.ApplyWidgetTheme();
            Toast.Show("Settings", "Speed units applied to widget, Speed Test and Test History.", "#FF10B981");
        };

        y += 46;
        var save = PageStyle.Button("Save settings", PageStyle.ButtonKind.Primary);
        save.Location = new Point(24, y);
        group.Controls.Add(save);
        save.Click += (_, _) =>
        {
            SettingsStore.Save(_s);
            ThemeManager.IsDark = _s.IsDarkMode;
            _widget.ApplyWidgetTheme();
            _dashboard.ApplyUserSettings();
            Toast.Show("Settings", "Settings saved. Widget and pages updated.", "#FF10B981");
        };

        group.Size = new Size(620, y + 54);
        return group;
    }

    private Control BuildThemeCard(int top)
    {
        var group = PageStyle.Card("Widget theme — per-element colors & transparency");
        group.Location = new Point(24, top);
        int y = 56;

        group.Controls.Add(new Label
        {
            Text = "Preset:",
            ForeColor = ThemeManager.Text,
            Location = new Point(24, y),
            AutoSize = true,
        });
        _preset = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Items = { },
            Location = new Point(110, y - 4),
            Width = 260,
            BackColor = ThemeManager.BgAlt,
            ForeColor = ThemeManager.Text,
            FlatStyle = FlatStyle.Flat,
        };
        _preset.Items.AddRange(WidgetThemeService.GetThemeNames());
        group.Controls.Add(_preset);
        _preset.SelectedIndexChanged += (_, _) =>
        {
            if (_preset.SelectedItem is string name)
            {
                WidgetThemeService.ApplyTheme(_s, name);
                SettingsStore.Save(_s);
                UpdateThemeControls();
                _widget.ApplyWidgetTheme();
            }
        };

        y += 38;
        _transparent = PageStyle.Check("Transparent background (digits stay visible)", _s.MainUiTransparencyKey);
        _transparent.Location = new Point(24, y);
        group.Controls.Add(_transparent);
        _transparent.CheckedChanged += (_, _) =>
        {
            if (WidgetThemeService.IsCustomName(_s.ThemeColorName)) _s.CustomTransparencyKey = _transparent.Checked;
            else _s.MainUiTransparencyKey = _transparent.Checked;
            SettingsStore.Save(_s);
            _widget.ApplyWidgetTheme();
        };

        y += 40;
        group.Controls.Add(new Label
        {
            Text = "Element colors (click to pick):",
            ForeColor = ThemeManager.TextDim,
            Location = new Point(24, y),
            AutoSize = true,
        });
        y += 32;
        int x = 24;
        var cols = new (string Name, Action<Color> Setter)[]
        {
            ("Background", c => _s.CustomBackgroundColor = WidgetThemeService.ToHex(c)),
            ("Download Icon", c => _s.CustomDownloadIconColor = WidgetThemeService.ToHex(c)),
            ("Download Label", c => _s.CustomDownloadLabelColor = WidgetThemeService.ToHex(c)),
            ("Upload Icon", c => _s.CustomUploadIconColor = WidgetThemeService.ToHex(c)),
            ("Upload Label", c => _s.CustomUploadLabelColor = WidgetThemeService.ToHex(c)),
        };
        _swatchBg = AddSwatch(group, x, y, "Background", cols[0].Setter); x += 116;
        _swatchDlIcon = AddSwatch(group, x, y, "DL Icon", cols[1].Setter); x += 116;
        _swatchDlLabel = AddSwatch(group, x, y, "DL Label", cols[2].Setter); x += 116;
        _swatchUlIcon = AddSwatch(group, x, y, "UL Icon", cols[3].Setter); x += 116;
        _swatchUlLabel = AddSwatch(group, x, y, "UL Label", cols[4].Setter);

        y += 50;
        var hint = new Label
        {
            Text = "Custom colors are stored in the Custom theme. Preset themes reset them.",
            ForeColor = ThemeManager.TextDim,
            Location = new Point(24, y),
            AutoSize = true,
        };
        group.Controls.Add(hint);

        group.Size = new Size(620, y + 40);
        return group;
    }

    private Panel AddSwatch(Control group, int x, int y, string name, Action<Color> setter)
    {
        var swatch = new Panel
        {
            Location = new Point(x, y),
            Size = new Size(92, 30),
            BackColor = ThemeManager.BgAlt,
            BorderStyle = BorderStyle.FixedSingle,
            Cursor = Cursors.Hand,
        };
        swatch.Paint += (_, e) =>
        {
            using var b = new SolidBrush(swatch.BackColor);
            e.Graphics.FillRectangle(b, 2, 2, swatch.Width - 4, swatch.Height - 4);
            TextRenderer.DrawText(e.Graphics, name, swatch.Font, new Rectangle(2, 2, swatch.Width - 4, swatch.Height - 4),
                ThemeManager.Text, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        };
        swatch.Click += (_, _) =>
        {
            using var dlg = new ColorDialog { Color = swatch.BackColor, FullOpen = true };
            if (dlg.ShowDialog() != DialogResult.OK) return;
            if (!WidgetThemeService.IsCustomName(_s.ThemeColorName))
            {
                // Editing an element promotes the widget to the Custom theme.
                _s.ThemeColorName = "Custom";
                _s.CustomBackgroundColor = WidgetThemeService.ToHex(WidgetThemeService.WidgetColors.FromSettings(_s).Background);
                _s.CustomDownloadIconColor = WidgetThemeService.ToHex(WidgetThemeService.WidgetColors.FromSettings(_s).DownloadIcon);
                _s.CustomDownloadLabelColor = WidgetThemeService.ToHex(WidgetThemeService.WidgetColors.FromSettings(_s).DownloadLabel);
                _s.CustomUploadIconColor = WidgetThemeService.ToHex(WidgetThemeService.WidgetColors.FromSettings(_s).UploadIcon);
                _s.CustomUploadLabelColor = WidgetThemeService.ToHex(WidgetThemeService.WidgetColors.FromSettings(_s).UploadLabel);
                _s.CustomTransparencyKey = _s.MainUiTransparencyKey;
            }
            setter(dlg.Color);
            SettingsStore.Save(_s);
            UpdateThemeControls();
            _widget.ApplyWidgetTheme();
        };
        group.Controls.Add(swatch);
        return swatch;
    }

    private void UpdateThemeControls()
    {
        var c = WidgetThemeService.WidgetColors.FromSettings(_s);
        _swatchBg.BackColor = c.Background;
        _swatchDlIcon.BackColor = c.DownloadIcon;
        _swatchDlLabel.BackColor = c.DownloadLabel;
        _swatchUlIcon.BackColor = c.UploadIcon;
        _swatchUlLabel.BackColor = c.UploadLabel;
        _transparent.Checked = c.Transparent;
        if (_preset.Items.Count > 0 && _preset.SelectedItem as string != _s.ThemeColorName)
        {
            _preset.SelectedItem = _s.ThemeColorName;
        }
    }

    private Label BuildInfoLine(int top)
    {
        var info = PageStyle.Body(
            "Toasts (Speed Test finished, data plan alerts) can be disabled on the Data Plan page and in Test History.");
        info.Location = new Point(16, top);
        return info;
    }
}
