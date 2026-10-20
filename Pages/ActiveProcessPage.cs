using SpeedMeterPro.Models;
using SpeedMeterPro.Services;
using Timer = System.Windows.Forms.Timer;

namespace SpeedMeterPro.Pages;

public sealed class ActiveProcessPage : UserControl
{
    private readonly AppSettingsVm _s;
    private readonly ListView _list;
    private readonly Timer _timer;

    public ActiveProcessPage(AppSettingsVm settings)
    {
        _s = settings;
        BackColor = ThemeManager.Bg;
        AutoScroll = true;
        Controls.Add(PageStyle.Title("Active Process"));

        var group = PageStyle.Card("Processes with visible windows (user32)");
        group.Location = new Point(24, 72);
        group.Size = new Size(760, 510);
        Controls.Add(group);

        _list = PageStyle.ListView();
        _list.Location = new Point(24, 56);
        _list.Size = new Size(712, 370);
        _list.Columns.Add("Name", 180);
        _list.Columns.Add("Title", 300);
        _list.Columns.Add("PID", 80);
        _list.Columns.Add("Working Set", 130);
        group.Controls.Add(_list);

        var refresh = PageStyle.Button("Refresh");
        refresh.Location = new Point(24, 448);
        group.Controls.Add(refresh);
        refresh.Click += (_, _) => Populate();

        var focus = PageStyle.Button("Focus");
        focus.Location = new Point(140, 448);
        group.Controls.Add(focus);
        focus.Click += (_, _) =>
        {
            var p = Selected();
            if (p is not null) ProcessListHelper.SetForeground(p.MainWindowHandle);
        };

        var end = PageStyle.Button("End process");
        end.Location = new Point(245, 448);
        group.Controls.Add(end);
        end.Click += (_, _) =>
        {
            var p = Selected();
            if (p is null) return;
            if (MessageBox.Show($"End process '{p.Name}' (PID {p.Id})?", "End process",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
            {
                ProcessListHelper.EndProcess(p);
                Populate();
            }
        };

        Populate();
        _timer = new Timer { Interval = 5000 };
        _timer.Tick += (_, _) => Populate();
        _timer.Start();
        Disposed += (_, _) => _timer.Stop();
    }

    private ProcessInfo? Selected() => _list.SelectedItems.Count > 0 ? _list.SelectedItems[0].Tag as ProcessInfo : null;

    private void Populate()
    {
        var processes = ProcessListHelper.GetVisibleProcesses();
        _list.BeginUpdate();
        _list.Items.Clear();
        foreach (var p in processes)
        {
            var item = new ListViewItem(new[]
            {
                p.Name,
                p.Title,
                p.Id.ToString(),
                UnitFormatter.FormatBytes((long)p.WorkingSetMb * 1024 * 1024),
            });
            item.Tag = p;
            _list.Items.Add(item);
        }
        _list.EndUpdate();
    }
}
