using System.Runtime.InteropServices;
using System.Text;

namespace SpeedMeterPro.Services;

public sealed class ProcessInfo
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Title { get; set; } = "";
    public long WorkingSetMb { get; set; }
    public bool HasWindow { get; set; }
    public IntPtr MainWindowHandle { get; set; }
}

/// <summary>Enumerate processes with visible windows via user32 (no WMI).</summary>
public static class ProcessListHelper
{
    public static List<ProcessInfo> GetVisibleProcesses()
    {
        var result = new List<ProcessInfo>();
        var processes = System.Diagnostics.Process.GetProcesses();
        foreach (var p in processes)
        {
            try
            {
                if (p.MainWindowHandle == IntPtr.Zero) continue;
                var title = p.MainWindowTitle;
                if (string.IsNullOrWhiteSpace(title) && !IsWindowVisible(p.MainWindowHandle)) continue;
                var info = new ProcessInfo
                {
                    Id = p.Id,
                    Name = p.ProcessName,
                    Title = string.IsNullOrWhiteSpace(title) ? p.ProcessName : title,
                    WorkingSetMb = p.WorkingSet64 / 1024 / 1024,
                    HasWindow = true,
                    MainWindowHandle = p.MainWindowHandle,
                };
                result.Add(info);
            }
            catch { }
        }
        return result.OrderBy(x => x.Name).ToList();
    }

    public static void SetForeground(IntPtr handle)
    {
        if (handle != IntPtr.Zero)
        {
            SetForegroundWindow(handle);
        }
    }

    public static void EndProcess(ProcessInfo info)
    {
        try
        {
            using var p = System.Diagnostics.Process.GetProcessById(info.Id);
            p.Kill();
        }
        catch (Exception ex)
        {
            Logger.Log($"EndProcess {info.Name}: {ex.Message}");
        }
    }

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);
}
