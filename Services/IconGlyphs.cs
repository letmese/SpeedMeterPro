using System.Drawing;

namespace SpeedMeterPro.Services;

/// <summary>
/// Glyph set matching the original app's icon style (the dump's IconChar /
/// FontAwesome-style chrome icons). These are real Unicode glyphs that render
/// on Windows 11 via Segoe UI Symbol (BMP symbols), so no extra font/asset
/// files are needed. Sidebar/tray use ForNav/ForTray maps; tray icons are
/// additionally drawn as colored glyph bitmaps (see WidgetForm).
/// </summary>
public static class IconGlyphs
{
    // --- Sidebar nav --------------------------------------------------------
    public const string Dashboard = "\u2302";        // ⌂ house
    public const string Settings = "\u2699";         // ⚙ gear
    public const string AdapterConfig = "\u21C5";    // ⇅ up/down arrows
    public const string SpeedTest = "\u26A1";        // ⚡ bolt
    public const string TestHistory = "\u2630";      // ☰ trigram
    public const string UsageStats = "\u25D4";       // ◔ circle upper-right quadrant
    public const string DataPlan = "\u2637";         // ☷ trigram earth
    public const string Wifi = "\u25C9";             // ◉ fisheye
    public const string NetworkInfo = "\u2316";      // ⌖ position indicator
    public const string NetworkInterface = "\u2301"; // ⌁ electric arrow
    public const string ActiveProcess = "\u25A3";    // ▣ white square w/ small square
    public const string NetworkTools = "\u2692";     // ⚒ hammer and pick
    public const string StopWatch = "\u25F7";        // ◷ circle upper-right quadrant
    public const string OurApps = "\u25C6";          // ◆ black diamond
    public const string About = "\u24D8";            // ⓘ info

    // --- Tray menu (BMP-only so they render in Segoe UI Symbol) -------------
    public const string ShowWidget = "\u25C9";       // ◉
    public const string HideWidget = "\u25CC";       // ◌ dotted circle
    public const string Refresh = "\u27F3";          // ⟳ clockwise gapped arrow
    public const string KeepInTaskbar = "\u25A6";    // ▦ square w/ crosshatch
    public const string Lock = "\u25D9";             // ◙ lock-state circle
    public const string RestoreDefault = "\u21BA";   // ↺ anticlockwise arrow
    public const string SpeedRefresh = "\u23F1";     // ⏱ stopwatch (refresh rate)
    public const string Adapter = "\u21C5";          // ⇅
    public const string AutoSelect = "\u25CE";       // ◎ bullseye
    public const string ConnectionInfo = "\u2316";   // ⌖
    public const string AlwaysOnTop = "\u2191";      // ↑ up arrow
    public const string ChangePosition = "\u2195";   // ↕ up/down arrow
    public const string DataUsage = "\u25D4";        // ◔
    public const string TestSpeed = "\u26A1";        // ⚡ bolt
    public const string Stopwatch = "\u25F7";        // ◷
    public const string ActiveProcessTray = "\u25A3";// ▣
    public const string QuickLinks = "\u2261";       // ≡ chain
    public const string ToolsHub = "\u2692";         // ⚒
    public const string NetworkProperties = "\u25A4";// ▤ square w/ horizontal fill
    public const string Screenshot = "\u25A1";       // □ white square
    public const string HowToUse = "\u2753";         // ❓ question mark
    public const string Pause = "\u275A\u275A";      // ❚❚
    public const string Play = "\u25B6";             // ▶
    public const string Exit = "\u2715";             // ✕ multiplication X
    public const string AboutTray = "\u24D8";        // ⓘ

    // --- Quick Links (11) ----------------------------------------------------
    public const string QlNetworkConnections = "\u21C5"; // ⇅
    public const string QlNetworkStatus = "\u25CE";      // ◎
    public const string QlResourceMonitor = "\u25A4";    // ▤
    public const string QlTaskManager = "\u25A3";        // ▣
    public const string QlSystemInformation = "\u24D8";  // ⓘ
    public const string QlInternetProperties = "\u25C9"; // ◉
    public const string QlSystemProperties = "\u2699";   // ⚙
    public const string QlControlPanel = "\u25A6";       // ▦
    public const string QlAddRemovePrograms = "\u2715";  // ✕
    public const string QlDisplayProperties = "\u25A1";  // □
    public const string QlDownloads = "\u2193";          // ↓

    /// <summary>Font that can render the glyph set (falls back to Latin for labels).</summary>
    public static Font GlyphFont(float size = 9f) => new("Segoe UI Symbol", size);

    public static string ForNav(string label) => label switch
    {
        "Dashboard" => Dashboard,
        "Settings" => Settings,
        "Adapter Config" => AdapterConfig,
        "Speed Test" => SpeedTest,
        "Test History" => TestHistory,
        "Usage Stats" => UsageStats,
        "Data Plan" => DataPlan,
        "WiFi" => Wifi,
        "Network Info" => NetworkInfo,
        "Network Interface" => NetworkInterface,
        "Active Process" => ActiveProcess,
        "Network Tools" => NetworkTools,
        "Stop Watch" => StopWatch,
        "Our Apps" => OurApps,
        "About" => About,
        _ => About,
    };

    /// <summary>Glyph for a 27-entry tray item (also used by the widget context menu).</summary>
    public static string ForTray(string label) => label switch
    {
        "Show Widget" => ShowWidget,
        "Hide Widget" => HideWidget,
        "Setting" or "Settings" => Settings,
        "Dashboard" => Dashboard,
        "Refresh Connection" or "Refresh" => Refresh,
        "Keep in Taskbar" => KeepInTaskbar,
        "Lock Position" => Lock,
        "Restore Default Setup" or "Restore Default" => RestoreDefault,
        "Speed Refresh Time" => SpeedRefresh,
        "Adapter(s) Setting" or "Adapters" => Adapter,
        "Auto Select NIC" => AutoSelect,
        "Connection Info" => ConnectionInfo,
        "Always On Top" => AlwaysOnTop,
        "Change UI Position" => ChangePosition,
        "Data Usages Stat" or "Data Usages" => DataUsage,
        "Test Speed" => TestSpeed,
        "Stopwatch" => Stopwatch,
        "Active Process" => ActiveProcessTray,
        "Quick Links" => QuickLinks,
        "Tools Hub" => ToolsHub,
        "Network Properties" => NetworkProperties,
        "Take Screenshot" or "Screenshot" => Screenshot,
        "How To Use" => HowToUse,
        "Pause" => Pause,
        "Resume" => Play,
        "About" => AboutTray,
        "Exit" => Exit,
        _ => AboutTray,
    };

    /// <summary>Glyph for a Quick Links sub-item.</summary>
    public static string ForQuickLink(string label) => label switch
    {
        "Network Connections" => QlNetworkConnections,
        "Network Status" => QlNetworkStatus,
        "Resource Monitor" => QlResourceMonitor,
        "Task Manager" => QlTaskManager,
        "System Information" => QlSystemInformation,
        "Internet Properties" => QlInternetProperties,
        "System Properties" => QlSystemProperties,
        "Control Panel" => QlControlPanel,
        "Add/Remove Programs" => QlAddRemovePrograms,
        "Display Properties" => QlDisplayProperties,
        "Downloads" => QlDownloads,
        _ => QlDownloads,
    };

    /// <summary>Accent color per tray item, matching the original's per-entry accent.</summary>
    public static Color TrayAccent(string label) => label switch
    {
        "Show Widget" => ThemeManager.Metric.Download,
        "Hide Widget" => Color.Gray,
        "Setting" or "Settings" => ThemeManager.Metric.BlueDark,
        "Dashboard" => ThemeManager.Metric.Teal,
        "Refresh Connection" or "Refresh" => ThemeManager.Metric.Upload,
        "Keep in Taskbar" => ThemeManager.Metric.Indigo,
        "Lock Position" => ThemeManager.Metric.Orange,
        "Restore Default Setup" or "Restore Default" => ThemeManager.Metric.Latency,
        "Speed Refresh Time" => ThemeManager.Metric.Latency,
        "Adapter(s) Setting" or "Adapters" => ThemeManager.Metric.BlueDark,
        "Auto Select NIC" => ThemeManager.Metric.Sky,
        "Connection Info" => ThemeManager.Metric.Sky,
        "Always On Top" => ThemeManager.Metric.Upload,
        "Change UI Position" => ThemeManager.Metric.Orange,
        "Data Usages Stat" or "Data Usages" => ThemeManager.Metric.Fuchsia,
        "Test Speed" => ThemeManager.Metric.Download,
        "Stopwatch" => ThemeManager.Metric.Jitter,
        "Active Process" => ThemeManager.Metric.Rose,
        "Quick Links" => ThemeManager.Metric.Indigo,
        "Tools Hub" => ThemeManager.Metric.Upload,
        "Network Properties" => ThemeManager.Metric.Teal,
        "Take Screenshot" or "Screenshot" => ThemeManager.Metric.Sky,
        "How To Use" => ThemeManager.Metric.Latency,
        "Pause" => ThemeManager.Metric.Warning,
        "Resume" => ThemeManager.Metric.Success,
        "About" => ThemeManager.Metric.Teal,
        "Exit" => ThemeManager.Metric.Error,
        _ => ThemeManager.TextDim,
    };
}
