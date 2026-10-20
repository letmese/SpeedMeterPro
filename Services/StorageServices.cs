using System.IO;
using System.Text.Json;
using SpeedMeterPro.Models;

namespace SpeedMeterPro.Services;

public sealed class SpeedTestRecord
{
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public double DownloadMbps { get; set; }
    public double UploadMbps { get; set; }
    public double PingMs { get; set; }
    public double JitterMs { get; set; }
    public double DownloadP80 { get; set; }
    public double UploadP80 { get; set; }
    public string Server { get; set; } = "Cloudflare";
}

/// <summary>JSON-backed speed test history with a hard cap of 200 records.</summary>
public sealed class TestHistoryStore
{
    public const int MaxRecords = 200;
    private readonly List<SpeedTestRecord> _records = new();

    public IReadOnlyList<SpeedTestRecord> Records => _records;

    public static TestHistoryStore Load()
    {
        var store = new TestHistoryStore();
        try
        {
            if (File.Exists(AppPaths.HistoryFile))
            {
                var list = JsonSerializer.Deserialize<List<SpeedTestRecord>>(File.ReadAllText(AppPaths.HistoryFile));
                if (list is not null)
                {
                    store._records.AddRange(list.OrderByDescending(r => r.Timestamp).Take(MaxRecords));
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Log($"History load failed: {ex.Message}");
        }
        return store;
    }

    public void Add(SpeedTestRecord record)
    {
        _records.Insert(0, record);
        if (_records.Count > MaxRecords)
        {
            _records.RemoveRange(MaxRecords, _records.Count - MaxRecords);
        }
        Save();
    }

    public void Delete(SpeedTestRecord record)
    {
        _records.Remove(record);
        Save();
    }

    public void Clear()
    {
        _records.Clear();
        Save();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(AppPaths.DataDir);
            var tmp = AppPaths.HistoryFile + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(_records, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(tmp, AppPaths.HistoryFile, true);
        }
        catch (Exception ex)
        {
            Logger.Log($"History save failed: {ex.Message}");
        }
    }
}

public sealed class UsageRecord
{
    public long RxBytes { get; set; }
    public long TxBytes { get; set; }
}

/// <summary>Per-interface, per-month usage with daily breakdown.</summary>
public sealed class UsageStats
{
    public string InterfaceName { get; set; } = "";
    public string YearMonth { get; set; } = "";
    public long RxBytes { get; set; }
    public long TxBytes { get; set; }
    public Dictionary<string, long> DailyRx { get; set; } = new();
    public Dictionary<string, long> DailyTx { get; set; } = new();
}

public sealed class UsageStore
{
    private List<UsageStats> _items = new();

    public IReadOnlyList<UsageStats> Items => _items;

    public static UsageStore Load()
    {
        var store = new UsageStore();
        try
        {
            if (File.Exists(AppPaths.UsageFile))
            {
                var list = JsonSerializer.Deserialize<List<UsageStats>>(File.ReadAllText(AppPaths.UsageFile));
                if (list is not null) store._items = list;
            }
        }
        catch (Exception ex)
        {
            Logger.Log($"Usage load failed: {ex.Message}");
        }
        return store;
    }

    public void AddSample(string nicName, DateTime at, long rxDelta, long txDelta)
    {
        var ym = $"{at:yyyy-MM}";
        var day = $"{at:yyyy-MM-dd}";
        var stats = _items.FirstOrDefault(x => x.InterfaceName == nicName && x.YearMonth == ym);
        if (stats is null)
        {
            stats = new UsageStats { InterfaceName = nicName, YearMonth = ym };
            _items.Add(stats);
        }
        stats.RxBytes += Math.Max(0, rxDelta);
        stats.TxBytes += Math.Max(0, txDelta);
        stats.DailyRx.TryAdd(day, 0);
        stats.DailyTx.TryAdd(day, 0);
        stats.DailyRx[day] += Math.Max(0, rxDelta);
        stats.DailyTx[day] += Math.Max(0, txDelta);

        // Prune months older than 13 months
        var cutoff = DateTime.Now.AddMonths(-13).ToString("yyyy-MM");
        _items.RemoveAll(x => string.CompareOrdinal(x.YearMonth, cutoff) < 0);
        Save();
    }

    public UsageStats? GetCurrent(string nicName)
    {
        var ym = DateTime.Now.ToString("yyyy-MM");
        return _items.FirstOrDefault(x => x.InterfaceName == nicName && x.YearMonth == ym);
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(AppPaths.DataDir);
            var tmp = AppPaths.UsageFile + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(_items, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(tmp, AppPaths.UsageFile, true);
        }
        catch (Exception ex)
        {
            Logger.Log($"Usage save failed: {ex.Message}");
        }
    }
}

public sealed class DataPlan
{
    public long MonthlyLimitBytes { get; set; }
    public double ThresholdPercent { get; set; } = 80;
    public bool AlertsEnabled { get; set; } = true;
    public DateTime? LastAlertAt { get; set; }
}

public static class DataPlanStore
{
    public static DataPlan Load()
    {
        try
        {
            if (File.Exists(AppPaths.DataPlanFile))
            {
                var p = JsonSerializer.Deserialize<DataPlan>(File.ReadAllText(AppPaths.DataPlanFile));
                return p ?? new DataPlan();
            }
        }
        catch (Exception ex)
        {
            Logger.Log($"DataPlan load failed: {ex.Message}");
        }
        return new DataPlan();
    }

    public static void Save(DataPlan plan)
    {
        try
        {
            Directory.CreateDirectory(AppPaths.DataDir);
            var tmp = AppPaths.DataPlanFile + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(plan, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(tmp, AppPaths.DataPlanFile, true);
        }
        catch (Exception ex)
        {
            Logger.Log($"DataPlan save failed: {ex.Message}");
        }
    }
}
