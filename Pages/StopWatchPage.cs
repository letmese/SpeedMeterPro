using System.Diagnostics;
using System.IO;
using SpeedMeterPro.Models;
using SpeedMeterPro.Services;
using Timer = System.Windows.Forms.Timer;

namespace SpeedMeterPro.Pages;

public sealed class StopWatchPage : UserControl
{
    private readonly AppSettingsVm _s;
    private readonly Stopwatch _sw = new();
    private readonly List<double> _laps = new();
    private readonly Timer _timer;
    private readonly Label _display;
    private readonly Label _stats;
    private readonly Button _toggle;

    public StopWatchPage(AppSettingsVm settings)
    {
        _s = settings;
        BackColor = ThemeManager.Bg;
        AutoScroll = true;
        Controls.Add(PageStyle.Title("Stop Watch"));

        var group = PageStyle.Card("Cumulative timing with lap speed");
        group.Location = new Point(24, 72);
        group.Size = new Size(620, 440);
        Controls.Add(group);

        _display = new Label
        {
            Text = "00:00:00.0",
            Font = new Font("Consolas", 36, FontStyle.Bold),
            ForeColor = ThemeManager.Accent,
            Location = new Point(28, 56),
            AutoSize = true,
        };
        group.Controls.Add(_display);

        _toggle = PageStyle.Button("Start");
        _toggle.Location = new Point(28, 145);
        group.Controls.Add(_toggle);
        _toggle.Click += (_, _) =>
        {
            if (_sw.IsRunning)
            {
                _sw.Stop();
                _toggle.Text = "Start";
            }
            else
            {
                _sw.Start();
                _toggle.Text = "Pause";
            }
        };

        var lap = PageStyle.Button("Lap");
        lap.Location = new Point(140, 145);
        group.Controls.Add(lap);
        lap.Click += (_, _) =>
        {
            if (_sw.IsRunning) _laps.Add(_sw.Elapsed.TotalSeconds);
        };

        var reset = PageStyle.Button("Reset");
        reset.Location = new Point(245, 145);
        group.Controls.Add(reset);
        reset.Click += (_, _) =>
        {
            _sw.Reset();
            _laps.Clear();
            _display!.Text = "00:00:00.0";
            _stats!.Text = "Laps: none";
        };

        _stats = new Label
        {
            Text = "Laps: none",
            Font = new Font("Segoe UI", 10),
            ForeColor = ThemeManager.Text,
            Location = new Point(28, 205),
            AutoSize = true,
        };
        group.Controls.Add(_stats);

        var export = PageStyle.Button("Export to Downloads (.txt)");
        export.Location = new Point(28, 260);
        group.Controls.Add(export);
        export.Click += (_, _) => Export();

        _timer = new Timer { Interval = 100 };
        _timer.Tick += (_, _) =>
        {
            if (!_sw.IsRunning) return;
            var t = _sw.Elapsed;
            _display.Text = $"{t.Hours:00}:{t.Minutes:00}:{t.Seconds:00}.{t.Milliseconds / 100}";
        };
        _timer.Start();
        Disposed += (_, _) => _timer.Stop();
    }

    private void Export()
    {
        try
        {
            var downloads = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var path = Path.Combine(downloads, "Downloads", $"stopwatch_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Speed Meter Pro - Stop Watch export");
            sb.AppendLine($"Date: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"Total: {Format(_sw.Elapsed.TotalSeconds)}");
            if (_laps.Count > 0)
            {
                var min = _laps.Min();
                var max = _laps.Max();
                var avg = _laps.Average();
                sb.AppendLine();
                sb.AppendLine("Laps (seconds):");
                for (int i = 0; i < _laps.Count; i++)
                {
                    sb.AppendLine($"{i + 1,3}. {_laps[i]:0.0}");
                }
                sb.AppendLine();
                sb.AppendLine($"Min: {min:0.0}  Max: {max:0.0}  Avg: {avg:0.0}");
            }
            File.WriteAllText(path, sb.ToString());
            Toast.Show("Stop Watch", $"Exported to {path}", "#FF10B981");
        }
        catch (Exception ex)
        {
            Toast.Show("Stop Watch", "Export failed: " + ex.Message, "#FFEF4444");
        }
    }

    private static string Format(double seconds)
    {
        var t = TimeSpan.FromSeconds(seconds);
        return $"{t.Hours:00}:{t.Minutes:00}:{t.Seconds:00}.{t.Milliseconds / 100}";
    }
}
