using System.Drawing;
using System.Runtime.InteropServices;
using SpeedMeterPro.Models;

namespace SpeedMeterPro.Services;

/// <summary>One built-in widget theme: colors for every widget element + transparency flag.</summary>
public sealed record WidgetThemePreset(
    string Name,
    Color Background,
    Color DownloadIcon,
    Color DownloadLabel,
    Color UploadIcon,
    Color UploadLabel,
    bool Transparent);

/// <summary>
/// The 12 built-in widget themes from the original dump's WidgetThemeService
/// plus "Windows Taskbar Color", "Desktop Background Color" and "Custom".
/// Applying a preset writes the MainUI_* settings (or Custom_* for custom) and
/// persists them, so the widget can re-render live.
/// </summary>
public static class WidgetThemeService
{
    public static readonly WidgetThemePreset[] Presets =
    {
        new("Black", Color.FromArgb(0xFF, 0x12, 0x12, 0x12), Color.FromArgb(0xFF, 0x4A, 0xF3, 0x21), Color.FromArgb(0xFF, 0xE8, 0xEA, 0xED), Color.FromArgb(0xFF, 0x10, 0xB9, 0x81), Color.FromArgb(0xFF, 0xE8, 0xEA, 0xED), false),
        new("White", Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF), Color.FromArgb(0xFF, 0x25, 0x63, 0xEB), Color.FromArgb(0xFF, 0x1A, 0x1D, 0x29), Color.FromArgb(0xFF, 0x10, 0xB9, 0x81), Color.FromArgb(0xFF, 0x1A, 0x1D, 0x29), false),
        new("Blue", Color.FromArgb(0xFF, 0x0F, 0x2E, 0x5C), Color.FromArgb(0xFF, 0x4A, 0xF3, 0x21), Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF), Color.FromArgb(0xFF, 0x10, 0xB9, 0x81), Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF), false),
        new("Transparent", Color.FromArgb(0xFF, 0x00, 0x00, 0x00), Color.FromArgb(0xFF, 0x4A, 0xF3, 0x21), Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF), Color.FromArgb(0xFF, 0x10, 0xB9, 0x81), Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF), true),
        new("Transparent Dark", Color.FromArgb(0xFF, 0x00, 0x00, 0x00), Color.FromArgb(0xFF, 0x4A, 0xF3, 0x21), Color.FromArgb(0xFF, 0xE8, 0xEA, 0xED), Color.FromArgb(0xFF, 0x10, 0xB9, 0x81), Color.FromArgb(0xFF, 0xE8, 0xEA, 0xED), true),
        new("Transparent Light", Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF), Color.FromArgb(0xFF, 0x25, 0x63, 0xEB), Color.FromArgb(0xFF, 0x1A, 0x1D, 0x29), Color.FromArgb(0xFF, 0x10, 0xB9, 0x81), Color.FromArgb(0xFF, 0x1A, 0x1D, 0x29), true),
        new("Dim Gray", Color.FromArgb(0xFF, 0x2B, 0x2B, 0x2B), Color.FromArgb(0xFF, 0x8A, 0xB4, 0xF8), Color.FromArgb(0xFF, 0xE8, 0xEA, 0xED), Color.FromArgb(0xFF, 0x9C, 0xE8, 0xC0), Color.FromArgb(0xFF, 0xE8, 0xEA, 0xED), false),
        new("Midnight", Color.FromArgb(0xFF, 0x0B, 0x0F, 0x1A), Color.FromArgb(0xFF, 0x81, 0x8C, 0xF8), Color.FromArgb(0xFF, 0xE5, 0xE7, 0xEB), Color.FromArgb(0xFF, 0x38, 0xBD, 0xF8), Color.FromArgb(0xFF, 0xE5, 0xE7, 0xEB), false),
        new("Neon", Color.FromArgb(0xFF, 0x14, 0x14, 0x28), Color.FromArgb(0xFF, 0xD9, 0x46, 0xEF), Color.FromArgb(0xFF, 0xF4, 0x72, 0xB6), Color.FromArgb(0xFF, 0x4A, 0xF3, 0x21), Color.FromArgb(0xFF, 0xF4, 0x72, 0xB6), false),
        new("Sunset", Color.FromArgb(0xFF, 0x2A, 0x14, 0x10), Color.FromArgb(0xFF, 0xFB, 0x71, 0x85), Color.FromArgb(0xFF, 0xFF, 0xE3, 0xB0), Color.FromArgb(0xFF, 0xF5, 0x9E, 0x0B), Color.FromArgb(0xFF, 0xFF, 0xE3, 0xB0), false),
        new("Ocean", Color.FromArgb(0xFF, 0x0C, 0x24, 0x36), Color.FromArgb(0xFF, 0x38, 0xBD, 0xF8), Color.FromArgb(0xFF, 0xE0, 0xF2, 0xFE), Color.FromArgb(0xFF, 0x14, 0xB8, 0xA6), Color.FromArgb(0xFF, 0xE0, 0xF2, 0xFE), false),
        new("Candy", Color.FromArgb(0xFF, 0x2E, 0x10, 0x28), Color.FromArgb(0xFF, 0xF4, 0x72, 0xB6), Color.FromArgb(0xFF, 0xFF, 0xD6, 0xF2), Color.FromArgb(0xFF, 0x4A, 0xF3, 0x21), Color.FromArgb(0xFF, 0xFF, 0xD6, 0xF2), false),
    };

    private const string SystemTaskbar = "Windows Taskbar Color";
    private const string SystemDesktop = "Desktop Background Color";
    private const string CustomName = "Custom";

    public static string[] GetThemeNames()
    {
        var names = Presets.Select(p => p.Name).ToList();
        names.Add(SystemTaskbar);
        names.Add(SystemDesktop);
        names.Add(CustomName);
        return names.ToArray();
    }

    public static bool IsCustomName(string? name) => name == CustomName;
    public static bool IsSystemName(string? name) => name == SystemTaskbar || name == SystemDesktop;

    /// <summary>Apply a theme (preset / system snapshot / custom) to settings and persist.</summary>
    public static void ApplyTheme(AppSettingsVm s, string themeName)
    {
        if (IsSystemName(themeName))
        {
            var sys = GetSystemThemeColors(themeName);
            s.MainUiTransparencyKey = false;
            s.MainUiBackgroundColor = ToHex(sys.Background);
            s.MainUiDownloadIconColor = ToHex(sys.DownloadIcon);
            s.MainUiDownloadLabelColor = ToHex(sys.DownloadLabel);
            s.MainUiUploadIconColor = ToHex(sys.UploadIcon);
            s.MainUiUploadLabelColor = ToHex(sys.UploadLabel);
            s.ThemeColorName = themeName;
            SettingsStore.Save(s);
            return;
        }

        if (IsCustomName(themeName))
        {
            s.ThemeColorName = CustomName;
            SettingsStore.Save(s);
            return;
        }

        var preset = Presets.FirstOrDefault(p => p.Name == themeName);
        if (preset is null) return;
        s.MainUiTransparencyKey = preset.Transparent;
        s.MainUiBackgroundColor = ToHex(preset.Background);
        s.MainUiDownloadIconColor = ToHex(preset.DownloadIcon);
        s.MainUiDownloadLabelColor = ToHex(preset.DownloadLabel);
        s.MainUiUploadIconColor = ToHex(preset.UploadIcon);
        s.MainUiUploadLabelColor = ToHex(preset.UploadLabel);
        s.ThemeColorName = preset.Name;
        SettingsStore.Save(s);
    }

    /// <summary>Resolved colors the widget should render right now.</summary>
    public readonly record struct WidgetColors(
        Color Background, Color DownloadIcon, Color DownloadLabel,
        Color UploadIcon, Color UploadLabel, bool Transparent)
    {
        public static WidgetColors FromSettings(AppSettingsVm s)
        {
            if (IsCustomName(s.ThemeColorName))
            {
                return new WidgetColors(
                    ColorFromHex(s.CustomBackgroundColor, Color.FromArgb(0xFF, 0x1A, 0x1D, 0x29)),
                    ColorFromHex(s.CustomDownloadIconColor, Color.FromArgb(0xFF, 0x3B, 0x82, 0xF6)),
                    ColorFromHex(s.CustomDownloadLabelColor, Color.FromArgb(0xFF, 0xE5, 0xE7, 0xEB)),
                    ColorFromHex(s.CustomUploadIconColor, Color.FromArgb(0xFF, 0x10, 0xB9, 0x81)),
                    ColorFromHex(s.CustomUploadLabelColor, Color.FromArgb(0xFF, 0xE5, 0xE7, 0xEB)),
                    s.CustomTransparencyKey);
            }
            return new WidgetColors(
                ColorFromHex(s.MainUiBackgroundColor, Color.FromArgb(0xFF, 0x12, 0x12, 0x12)),
                ColorFromHex(s.MainUiDownloadIconColor, Color.FromArgb(0xFF, 0x4A, 0xF3, 0x21)),
                ColorFromHex(s.MainUiDownloadLabelColor, Color.FromArgb(0xFF, 0xE8, 0xEA, 0xED)),
                ColorFromHex(s.MainUiUploadIconColor, Color.FromArgb(0xFF, 0x10, 0xB9, 0x81)),
                ColorFromHex(s.MainUiUploadLabelColor, Color.FromArgb(0xFF, 0xE8, 0xEA, 0xED)),
                s.MainUiTransparencyKey);
        }
    }

    // --- Helpers ------------------------------------------------------------

    public static string ToHex(Color c) => $"#{c.A:X2}{c.R:X2}{c.G:X2}{c.B:X2}";

    public static Color ColorFromHex(string? hex, Color fallback)
    {
        if (string.IsNullOrWhiteSpace(hex)) return fallback;
        try
        {
            var t = hex.TrimStart('#');
            if (t.Length == 6) t = "FF" + t;
            if (t.Length != 8) return fallback;
            return Color.FromArgb(
                Convert.ToInt32(t.Substring(0, 2), 16),
                Convert.ToInt32(t.Substring(2, 2), 16),
                Convert.ToInt32(t.Substring(4, 2), 16),
                Convert.ToInt32(t.Substring(6, 2), 16));
        }
        catch
        {
            return fallback;
        }
    }

    [DllImport("dwmapi.dll", PreserveSig = false)]
    private static extern int DwmGetColorizationColor(out uint colorization, out bool colorizationAlphaBlend);

    private static (Color Background, Color DownloadIcon, Color DownloadLabel, Color UploadIcon, Color UploadLabel) GetSystemThemeColors(string kind)
    {
        // Try the actual taskbar colorization color (Windows accent), then fall back.
        Color accent = SystemColors.Highlight;
        try
        {
            if (DwmGetColorizationColor(out uint colorization, out _) == 0)
            {
                accent = Color.FromArgb(
                    (byte)(colorization >> 24), (byte)colorization,
                    (byte)(colorization >> 8), (byte)(colorization >> 16));
            }
        }
        catch
        {
            // fall through
        }

        bool dark = ThemeManager.IsDark;
        Color bg;
        Color text;
        if (kind == SystemTaskbar)
        {
            bg = dark ? Color.FromArgb(0xFF, 0x20, 0x20, 0x20) : Color.FromArgb(0xFF, 0xF0, 0xF0, 0xF0);
            text = dark ? Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF) : Color.FromArgb(0xFF, 0x1A, 0x1A, 0x1A);
        }
        else
        {
            // Desktop background: sample a dark/light neutral matching system mode.
            bg = dark ? Color.FromArgb(0xFF, 0x12, 0x12, 0x16) : Color.FromArgb(0xFF, 0xE8, 0xE8, 0xEE);
            text = dark ? Color.FromArgb(0xFF, 0xE8, 0xEA, 0xED) : Color.FromArgb(0xFF, 0x20, 0x20, 0x20);
        }
        return (bg, accent, text, Color.FromArgb(0xFF, 0x10, 0xB9, 0x81), text);
    }
}
