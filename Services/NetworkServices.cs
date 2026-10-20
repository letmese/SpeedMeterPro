using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;

namespace SpeedMeterPro.Services;

public static class UnitFormatter
{
    public static string FormatSpeed(double bps, bool useBytes, bool automatic)
    {
        // FIX(speed-5): the legacy overload had two unit-math bugs that made
        // every caller that still used it show wrong speeds:
        //   1) automatic + byte units: it divided BITS by 1000 and labelled
        //      them B/s / KB/s / MB/s without ever converting bits->bytes,
        //      over-reporting the speed by exactly 8x (a 100 Mbps download
        //      displayed as "100 MB/s").
        //   2) fixed (non-automatic) bit units: it never scaled at all, so a
        //      25 Mbps link displayed as "25000000 bps".
        // Delegate to the corrected FormatSpeedEx so all speed text in the app
        // is produced by one consistent implementation.
        return FormatSpeedEx(bps, useBytes ? 2 : automatic ? 0 : 1);
    }

    /// <summary>Speed formatting driven by the user's unit mode (settings SpeedUnitMode).</summary>
    public static string FormatSpeedEx(double bps, int mode)
    {
        if (bps <= 0) return mode == 2 ? "0 B/s" : mode == 1 ? "0 Mbps" : "0 bps";
        if (mode == 1) return $"{bps / 1_000_000.0:0.##} Mbps";
        if (mode == 2)
        {
            double v = bps / 8.0;
            string[] units = { "B/s", "KB/s", "MB/s", "GB/s" };
            int idx = 0;
            while (v >= 1024 && idx < units.Length - 1)
            {
                v /= 1024;
                idx++;
            }
            return $"{v:0.##} {units[idx]}";
        }
        double value = bps;
        string[] bitUnits = { "bps", "Kbps", "Mbps", "Gbps" };
        int i = 0;
        while (value >= 1000 && i < bitUnits.Length - 1)
        {
            value /= 1000;
            i++;
        }
        return $"{value:0.##} {bitUnits[i]}";
    }

    public static string FormatBytes(long bytes)
    {
        double v = bytes;
        string[] units = { "B", "KB", "MB", "GB", "TB" };
        int idx = 0;
        while (v >= 1024 && idx < units.Length - 1)
        {
            v /= 1024;
            idx++;
        }
        return $"{v:0.##} {units[idx]}";
    }
}

/// <summary>Hotkey registration via RegisterHotKey (global while app runs).</summary>
public sealed class HotkeyManager : IDisposable
{
    private const int WM_HOTKEY = 0x0312;
    private readonly Dictionary<int, Action> _actions = new();
    private int _nextId = 0x6000;

    public void Register(Keys key, Action action)
    {
        var id = _nextId++;
        _actions[id] = action;
    }

    public void HandleWindowMessage(ref Message m)
    {
        if (m.Msg == WM_HOTKEY && _actions.TryGetValue(m.WParam.ToInt32(), out var action))
        {
            action();
        }
    }

    public void Dispose()
    {
        _actions.Clear();
    }
}

/// <summary>Run-at-startup via HKCU Run registry value (low risk, per-user).</summary>
public static class StartupManager
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "SpeedMeterPro";

    public static void SetEnabled(bool enabled)
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RunKey, true)
                ?? Microsoft.Win32.Registry.CurrentUser.CreateSubKey(RunKey);
            if (key is null) return;
            if (enabled)
            {
                var exe = Environment.ProcessPath ?? Application.ExecutablePath;
                key.SetValue(ValueName, $"\"{exe}\"");
            }
            else
            {
                key.DeleteValue(ValueName, false);
            }
        }
        catch (Exception ex)
        {
            Logger.Log($"StartupManager: {ex.Message}");
        }
    }

    public static bool IsEnabled()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RunKey);
            return key?.GetValue(ValueName) is not null;
        }
        catch
        {
            return false;
        }
    }
}

/// <summary>Ping wrapper used by adapter validation and tools.</summary>
public static class PingHelper
{
    public static PingReply? Send(string host, int timeoutMs = 1000)
    {
        using var ping = new Ping();
        try
        {
            return ping.Send(host, timeoutMs);
        }
        catch
        {
            return null; // treat as unreachable
        }
    }
}

/// <summary>DNS lookup helper.</summary>
public static class DnsHelper
{
    public static string Resolve(string host)
    {
        try
        {
            var entry = Dns.GetHostAddresses(host);
            return string.Join(", ", entry.Select(a => a.ToString()));
        }
        catch
        {
            return "Unable to resolve";
        }
    }
}

/// <summary>Traceroute via ICMP with incremental TTL.</summary>
public static class TraceRouteHelper
{
    public static List<string> Run(string host, int maxHops = 30)
    {
        var results = new List<string>();
        for (int ttl = 1; ttl <= maxHops; ttl++)
        {
            using var ping = new Ping();
            var options = new PingOptions(ttl, true);
            var reply = ping.Send(host, 1000, new byte[32], options);
            string line;
            if (reply is null)
            {
                line = $"{ttl,2}  *";
            }
            else if (reply.Status == IPStatus.Success)
            {
                line = $"{ttl,2}  {reply.Address}  {reply.RoundtripTime,4} ms";
                results.Add(line);
                break;
            }
            else if (reply.Status == IPStatus.TtlExpired)
            {
                line = $"{ttl,2}  {reply.Address}  {reply.RoundtripTime,4} ms";
            }
            else
            {
                line = $"{ttl,2}  {reply.Status}";
            }
            results.Add(line);
        }
        return results;
    }
}