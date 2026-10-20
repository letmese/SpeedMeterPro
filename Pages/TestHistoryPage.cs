using SpeedMeterPro.Models;
using SpeedMeterPro.Services;

namespace SpeedMeterPro.Pages;

public sealed class TestHistoryPage : UserControl
{
    private readonly AppSettingsVm _s;
    private readonly TestHistoryStore _history = TestHistoryStore.Load();
    private readonly ListView _list;
    private readonly Label _count;
    private int _sortColumn = 0;
    private SortOrder _sortOrder = SortOrder.Descending;
    private static readonly string[] BaseHeaders = { "Time", "Download", "Upload", "Ping (ms)", "Jitter (ms)", "P80 D/U" };

    public TestHistoryPage(AppSettingsVm settings)
    {
        _s = settings;
        BackColor = ThemeManager.Bg;
        AutoScroll = true;
        Controls.Add(PageStyle.Title("Test History"));

        var group = PageStyle.Card("Saved speed test results");
        group.Location = new Point(24, 72);
        group.Size = new Size(760, 530);
        Controls.Add(group);

        _list = PageStyle.ListView();
        _list.Location = new Point(24, 56);
        _list.Size = new Size(712, 390);
        _list.Columns.Add("Time", 160);
        _list.Columns.Add("Download", 120);
        _list.Columns.Add("Upload", 120);
        _list.Columns.Add("Ping (ms)", 100);
        _list.Columns.Add("Jitter (ms)", 100);
        _list.Columns.Add("P80 D/U", 150);
        _list.ColumnClick += OnColumnClick;
        group.Controls.Add(_list);

        _count = new Label
        {
            Text = "",
            ForeColor = ThemeManager.TextDim,
            Location = new Point(24, 456),
            AutoSize = true,
        };
        group.Controls.Add(_count);

        var delete = PageStyle.Button("Delete selected", PageStyle.ButtonKind.Secondary);
        delete.Click += (_, _) =>
        {
            if (_list.SelectedItems.Count == 0) return;
            if (_list.SelectedItems[0].Tag is SpeedTestRecord record)
            {
                _history.Delete(record);
                Populate();
            }
        };

        var clear = PageStyle.Button("Clear all", PageStyle.ButtonKind.Danger);
        clear.Click += (_, _) =>
        {
            _history.Clear();
            Populate();
            Toast.Show("Test History", "All records cleared.");
        };

        group.Controls.Add(delete);
        group.Controls.Add(clear);
        PageStyle.LayoutButtonBar(24, 482, 18, delete, clear);

        Populate();
    }

    private void OnColumnClick(object? sender, ColumnClickEventArgs e)
    {
        if (e.Column == _sortColumn)
        {
            _sortOrder = _sortOrder == SortOrder.Ascending ? SortOrder.Descending : SortOrder.Ascending;
        }
        else
        {
            _sortColumn = e.Column;
            _sortOrder = (_sortColumn == 0 || _sortColumn == 1 || _sortColumn == 2)
                ? SortOrder.Descending
                : SortOrder.Ascending;
        }

        ApplySort();
    }

    private void ApplySort()
    {
        _list.ListViewItemSorter = new SpeedTestRecordComparer(_sortColumn, _sortOrder);
        _list.Sort();

        for (int i = 0; i < _list.Columns.Count; i++)
        {
            var title = BaseHeaders[i];
            if (i == _sortColumn)
            {
                title += _sortOrder == SortOrder.Ascending ? "  ▲" : "  ▼";
            }
            _list.Columns[i].Text = title;
        }
    }

    private void Populate()
    {
        _list.BeginUpdate();
        _list.Items.Clear();
        foreach (var r in _history.Records)
        {
            var item = new ListViewItem(new[]
            {
                r.Timestamp.ToString("yyyy-MM-dd HH:mm:ss"),
                UnitFormatter.FormatSpeedEx(r.DownloadMbps * 1_000_000.0, _s.SpeedUnitMode),
                UnitFormatter.FormatSpeedEx(r.UploadMbps * 1_000_000.0, _s.SpeedUnitMode),
                r.PingMs.ToString("0.#"),
                r.JitterMs.ToString("0.#"),
                $"{UnitFormatter.FormatSpeedEx(r.DownloadP80 * 1_000_000.0, _s.SpeedUnitMode)} / {UnitFormatter.FormatSpeedEx(r.UploadP80 * 1_000_000.0, _s.SpeedUnitMode)}",
            })
            {
                Tag = r
            };
            _list.Items.Add(item);
        }
        _list.EndUpdate();
        ApplySort();
        _count.Text = $"{_history.Records.Count} / {TestHistoryStore.MaxRecords} records (stored at {AppPaths.HistoryFile})";
    }

    private sealed class SpeedTestRecordComparer : System.Collections.IComparer
    {
        private readonly int _col;
        private readonly SortOrder _order;

        public SpeedTestRecordComparer(int col, SortOrder order)
        {
            _col = col;
            _order = order;
        }

        public int Compare(object? x, object? y)
        {
            if (x is not ListViewItem itemX || y is not ListViewItem itemY) return 0;
            var rX = itemX.Tag as SpeedTestRecord;
            var rY = itemY.Tag as SpeedTestRecord;
            if (rX is null || rY is null) return 0;

            int result = _col switch
            {
                0 => rX.Timestamp.CompareTo(rY.Timestamp),
                1 => rX.DownloadMbps.CompareTo(rY.DownloadMbps),
                2 => rX.UploadMbps.CompareTo(rY.UploadMbps),
                3 => rX.PingMs.CompareTo(rY.PingMs),
                4 => rX.JitterMs.CompareTo(rY.JitterMs),
                5 => rX.DownloadP80.CompareTo(rY.DownloadP80),
                _ => 0
            };

            return _order == SortOrder.Descending ? -result : result;
        }
    }
}
