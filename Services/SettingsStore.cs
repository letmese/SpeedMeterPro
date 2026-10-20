using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using SpeedMeterPro.Models;

namespace SpeedMeterPro.Services;

/// <summary>
/// JSON settings persistence with atomic write + timestamped backup,
/// matching the reference app's robustness goals (no partial writes).
/// </summary>
public static class SettingsStore
{
    /// <summary>Raised after a successful save so live surfaces (widget, dashboard) can re-render.</summary>
    public static event Action<AppSettingsVm>? Saved;

    public static string SettingsPath => Path.Combine(AppPaths.DataDir, "settings.json");

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static AppSettingsVm Load()
    {
        try
        {
            if (!File.Exists(SettingsPath))
            {
                var fresh = new AppSettingsVm();
                Save(fresh);
                return fresh;
            }
            var json = File.ReadAllText(SettingsPath);
            var vm = JsonSerializer.Deserialize<AppSettingsVm>(json, Options);
            return vm ?? new AppSettingsVm();
        }
        catch (Exception ex)
        {
            Logger.Log($"Settings load failed ({ex.Message}); restoring from backup if present.");
            var backup = FindBackup();
            if (backup is not null)
            {
                try
                {
                    var vm = JsonSerializer.Deserialize<AppSettingsVm>(File.ReadAllText(backup), Options);
                    if (vm is not null)
                    {
                        Save(vm);
                        return vm;
                    }
                }
                catch (Exception inner)
                {
                    Logger.Log($"Backup restore failed: {inner.Message}");
                }
            }
            return new AppSettingsVm();
        }
    }

    public static void Save(AppSettingsVm vm)
    {
        Directory.CreateDirectory(AppPaths.DataDir);
        var tmp = SettingsPath + ".tmp";
        var json = JsonSerializer.Serialize(vm, Options);
        File.WriteAllText(tmp, json);
        if (File.Exists(SettingsPath))
        {
            var backup = SettingsPath + ".bak";
            try { File.Copy(SettingsPath, backup, true); } catch { /* best effort */ }
        }
        File.Move(tmp, SettingsPath, true);
        try { Saved?.Invoke(vm); } catch (Exception ex) { Logger.Log($"Saved event error: {ex.Message}"); }
    }

    private static string? FindBackup()
    {
        var bak = SettingsPath + ".bak";
        return File.Exists(bak) ? bak : null;
    }
}

/// <summary>Well-known app directories under %LocalAppData%.</summary>
public static class AppPaths
{
    public static string Root => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SpeedMeterPro");

    public static string DataDir => Path.Combine(Root, "Data");

    public static string LogDir => Path.Combine(Root, "Logs");

    public static string HistoryFile => Path.Combine(DataDir, "speed_test_history.json");

    public static string UsageFile => Path.Combine(DataDir, "usage.json");

    public static string DataPlanFile => Path.Combine(DataDir, "data_plan.json");

    public static string WifiKeyCacheFile => Path.Combine(DataDir, "wifi_keys.dat");

    public static void Ensure()
    {
        Directory.CreateDirectory(DataDir);
        Directory.CreateDirectory(LogDir);
    }
}

public static class Logger
{
    private static readonly object Gate = new();

    public static void Init()
    {
        AppPaths.Ensure();
        try
        {
            var old = Directory.GetFiles(AppPaths.LogDir, "*.log");
            foreach (var f in old)
            {
                if (File.GetLastWriteTime(f) < DateTime.Now.AddDays(-7))
                {
                    try { File.Delete(f); } catch { }
                }
            }
        }
        catch { }
    }

    public static void Log(string message)
    {
        try
        {
            lock (Gate)
            {
                var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {message}{Environment.NewLine}";
                File.AppendAllText(Path.Combine(AppPaths.LogDir, "app.log"), line);
            }
        }
        catch { }
    }
}
