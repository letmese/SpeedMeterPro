using System.Net.NetworkInformation;
using SpeedMeterPro.Models;
using SpeedMeterPro.Services;
using Timer = System.Windows.Forms.Timer;

namespace SpeedMeterPro.Pages;

public sealed class UsageStatsPage : UserControl
{
    private readonly AppSettingsVm _s;
    private readonly UsageStore _usage = UsageStore.Load();
    private readonly ListView _list;
    private readonly Label _summary;
    private readonly Timer _monitor;

    public UsageStatsPage(AppSettingsVm settings)
    {
        _s = settings;
        BackColor = ThemeManager.Bg;
        AutoScroll = true;
        Controls.Add(PageStyle.Title("Usage Stats"));

        var group = PageStyle.Card("Per-interface monthly usage");
        group.Location = new Point(24, 72);
        group.Size = new Size(760, 510);
        Controls.Add(group);

        _list = PageStyle.ListView();
        _list.Location = new Point(24, 56);
        _list.Size = new Size(712, 340);
        _list.Columns.Add("Interface", 200);
        _list.Columns.Add("Month", 110);
        _list.Columns.Add("Download", 120);
        _list.Columns.Add("Upload", 120);
        _list.Columns.Add("Total", 120);
        _list.Columns.Add("Days", 70);
        group.Controls.Add(_list);

        _summary = new Label
        {
            Text = "",
            ForeColor = ThemeManager.TextDim,
            Location = new Point(24, 412),
            AutoSize = true,
        };
        group.Controls.Add(_summary);

        var refresh = PageStyle.Button("Refresh");
        refresh.Location = new Point(24, 448);
        group.Controls.Add(refresh);
        refresh.Click += (_, _) => Populate();

        var sample = PageStyle.Button("Sample now");
        sample.Location = new Point(145, 448);
        group.Controls.Add(sample);
        sample.Click += (_, _) => RecordSample();

        Populate();

        // 5-minute monitor: auto records usage deltas every 5 minutes.
        _monitor = new Timer { Interval = 5 * 60 * 1000 };
        _monitor.Tick += (_, _) => RecordSample();
        _monitor.Start();
        Disposed += (_, _) => _monitor.Stop();
    }

    private void RecordSample()
    {
        try
        {
            var nic = AdapterSelector.SelectAutoAdapter();
            if (nic is null) return;
            var stats = nic.GetIPStatistics();
            _usage.AddSample(nic.Name, DateTime.Now, stats.BytesReceived, stats.BytesSent);
            Populate();
        }
        catch { }
    }

    private void Populate()
    {
        _list.BeginUpdate();
        _list.Items.Clear();
        foreach (var u in _usage.Items.OrderByDescending(x => x.YearMonth))
        {
            var total = u.RxBytes + u.TxBytes;
            _list.Items.Add(new ListViewItem(new[]
            {
                u.InterfaceName,
                u.YearMonth,
                UnitFormatter.FormatBytes(u.RxBytes),
                UnitFormatter.FormatBytes(u.TxBytes),
                UnitFormatter.FormatBytes(total),
                u.DailyRx.Count.ToString(),
            }));
        }
        _list.EndUpdate();

        long totalRx = _usage.Items.Sum(x => x.RxBytes);
        long totalTx = _usage.Items.Sum(x => x.TxBytes);
        _summary.Text = $"Total tracked: {UnitFormatter.FormatBytes(totalRx)} down / {UnitFormatter.FormatBytes(totalTx)} up " +
                        $"(monitored every 5 minutes; stored at {AppPaths.UsageFile})";
    }
}
