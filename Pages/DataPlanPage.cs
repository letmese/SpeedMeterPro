using SpeedMeterPro.Models;
using SpeedMeterPro.Services;
using Timer = System.Windows.Forms.Timer;

namespace SpeedMeterPro.Pages;

public sealed class DataPlanPage : UserControl
{
    private readonly AppSettingsVm _s;
    private readonly DataPlan _plan;
    private readonly UsageStore _usage = UsageStore.Load();
    private readonly Label _current;
    private readonly Label _percent;
    private readonly Timer _monitor;

    public DataPlanPage(AppSettingsVm settings)
    {
        _s = settings;
        _plan = DataPlanStore.Load();
        BackColor = ThemeManager.Bg;
        AutoScroll = true;
        Controls.Add(PageStyle.Title("Data Plan"));

        var group = PageStyle.Card("Monthly data cap");
        group.Location = new Point(24, 72);
        group.Size = new Size(620, 460);
        Controls.Add(group);

        int y = 56;
        group.Controls.Add(new Label
        {
            Text = "Monthly limit (GB):",
            ForeColor = ThemeManager.Text,
            Location = new Point(24, y),
            AutoSize = true,
        });
        var limit = new NumericUpDown
        {
            Minimum = 1,
            Maximum = 10000,
            Value = (decimal)Math.Max(1, _plan.MonthlyLimitBytes / 1024.0 / 1024.0 / 1024.0),
            Increment = 1,
            Location = new Point(200, y - 2),
            Width = 150,
            BackColor = ThemeManager.BgAlt,
            ForeColor = ThemeManager.Text,
            BorderStyle = BorderStyle.FixedSingle,
        };
        group.Controls.Add(limit);
        limit.ValueChanged += (_, _) =>
        {
            _plan.MonthlyLimitBytes = (long)((double)limit.Value * 1024 * 1024 * 1024);
            DataPlanStore.Save(_plan);
        };

        y += 42;
        group.Controls.Add(new Label
        {
            Text = "Alert threshold (%):",
            ForeColor = ThemeManager.Text,
            Location = new Point(24, y),
            AutoSize = true,
        });
        var threshold = new NumericUpDown
        {
            Minimum = 10,
            Maximum = 100,
            Value = (decimal)_plan.ThresholdPercent,
            Increment = 5,
            Location = new Point(200, y - 2),
            Width = 150,
            BackColor = ThemeManager.BgAlt,
            ForeColor = ThemeManager.Text,
            BorderStyle = BorderStyle.FixedSingle,
        };
        group.Controls.Add(threshold);
        threshold.ValueChanged += (_, _) =>
        {
            _plan.ThresholdPercent = (double)threshold.Value;
            DataPlanStore.Save(_plan);
        };

        y += 42;
        var alerts = PageStyle.Check("Enable data plan alerts", _plan.AlertsEnabled && _s.IsDataPlanAlertsEnabled);
        alerts.Location = new Point(24, y);
        group.Controls.Add(alerts);
        alerts.CheckedChanged += (_, _) =>
        {
            _plan.AlertsEnabled = alerts.Checked;
            _s.IsDataPlanAlertsEnabled = alerts.Checked;
            DataPlanStore.Save(_plan);
            SettingsStore.Save(_s);
        };

        y += 46;
        _current = new Label
        {
            Text = "",
            Font = new Font("Segoe UI", 10.5f, FontStyle.Bold),
            ForeColor = ThemeManager.Text,
            Location = new Point(24, y),
            AutoSize = true,
        };
        group.Controls.Add(_current);

        y += 36;
        _percent = new Label
        {
            Text = "",
            ForeColor = ThemeManager.TextDim,
            Location = new Point(24, y),
            AutoSize = true,
        };
        group.Controls.Add(_percent);

        y += 44;
        var hint = PageStyle.Body(
            "Usage is tracked per interface and per month (see Usage Stats). A toast alert fires once per month " +
            "when usage crosses the threshold, and again at 100%.");
        hint.Location = new Point(24, y);
        hint.MaximumSize = new Size(570, 0);
        group.Controls.Add(hint);

        RefreshDisplay();

        _monitor = new Timer { Interval = 60_000 };
        _monitor.Tick += (_, _) => CheckAndAlert();
        _monitor.Start();
        Disposed += (_, _) => _monitor.Stop();
    }

    private void RefreshDisplay()
    {
        var nic = Services.AdapterSelector.SelectAutoAdapter();
        if (nic is null)
        {
            _current.Text = "No active adapter.";
            _percent.Text = "";
            return;
        }
        var usage = _usage.GetCurrent(nic.Name);
        long used = usage?.RxBytes ?? 0;
        var limit = _plan.MonthlyLimitBytes;
        if (limit <= 0) limit = 1;
        double pct = used * 100.0 / limit;
        _current.Text = $"Used this month ({nic.Name}): {UnitFormatter.FormatBytes(used)} of {UnitFormatter.FormatBytes(limit)}";
        _percent.Text = $"{pct:0.#}% of monthly cap. Threshold alert at {_plan.ThresholdPercent:0}%.";
    }

    private void CheckAndAlert()
    {
        RefreshDisplay();
        if (!_plan.AlertsEnabled || !_s.IsDataPlanAlertsEnabled || _plan.MonthlyLimitBytes <= 0) return;
        var nic = Services.AdapterSelector.SelectAutoAdapter();
        if (nic is null) return;
        var usage = _usage.GetCurrent(nic.Name);
        long used = usage?.RxBytes ?? 0;
        double pct = used * 100.0 / _plan.MonthlyLimitBytes;
        var monthKey = DateTime.Now.ToString("yyyy-MM");
        var alertKey = $"{monthKey}:{_plan.ThresholdPercent:0}:{pct >= 100}";
        if (pct >= 100 && _plan.LastAlertAt?.ToString("yyyy-MM") != monthKey + ":full")
        {
            _plan.LastAlertAt = DateTime.Now;
            DataPlanStore.Save(_plan);
            Toast.Show("Data Plan", $"You have used 100% of your monthly cap ({UnitFormatter.FormatBytes(used)}).", "#FFEF4444", 8000);
        }
        else if (pct >= _plan.ThresholdPercent && _plan.LastAlertAt?.ToString("yyyy-MM") != monthKey)
        {
            _plan.LastAlertAt = DateTime.Now;
            DataPlanStore.Save(_plan);
            Toast.Show("Data Plan", $"Usage reached {pct:0.#}% of the monthly cap.", "#FFF59E0B", 8000);
        }
    }
}
