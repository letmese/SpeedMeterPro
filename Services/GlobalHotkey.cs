using System.Runtime.InteropServices;

namespace SpeedMeterPro.Services;

/// <summary>
/// Global (system-wide) keyboard shortcuts registered with RegisterHotKey,
/// matching the original app's KeyboardShortcutService:
/// Ctrl+S show, Ctrl+H hide, S settings, D dashboard, Q quit,
/// C screenshot, M smaller font, Z bigger font.
/// </summary>
public sealed class GlobalHotkey : IDisposable
{
    private const uint MOD_CONTROL = 0x0002;
    private const uint MOD_NOREPEAT = 0x4000;

    public const int WM_HOTKEY = 0x0312;

    private readonly IntPtr _hwnd;
    private readonly HashSet<int> _ids = new();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    public GlobalHotkey(IntPtr hwnd) => _hwnd = hwnd;

    /// <summary>Register one hotkey. id is returned in WM_HOTKEY's wParam.</summary>
    public void Register(int id, bool ctrl, Keys key)
    {
        uint mods = MOD_NOREPEAT | (ctrl ? MOD_CONTROL : 0);
        if (!RegisterHotKey(_hwnd, id, mods, (uint)key))
        {
            Logger.Log($"Global hotkey id={id} registration failed: {Marshal.GetLastWin32Error()}");
            return;
        }
        _ids.Add(id);
    }

    public void Dispose()
    {
        foreach (var id in _ids) UnregisterHotKey(_hwnd, id);
        _ids.Clear();
    }
}
