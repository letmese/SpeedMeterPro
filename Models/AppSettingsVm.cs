namespace SpeedMeterPro.Models;

/// <summary>Full settings model, persisted as JSON (atomic write + backup).</summary>
public sealed class AppSettingsVm
{
    public int AppVersion { get; set; } = 1000;
    public int LaunchCount { get; set; }
    public string LastAppVersion { get; set; } = string.Empty;
    public bool IsNewInstallation { get; set; } = true;

    // Appearance / theme
    public bool IsDarkMode { get; set; } = true;
    public string ThemeColorName { get; set; } = "Custom";
    public bool MainUiTransparencyKey { get; set; }
    public string MainUiBackgroundColor { get; set; } = "FF1A1D29";
    public string MainUiDownloadIconColor { get; set; } = "FF3B82F6";
    public string MainUiDownloadLabelColor { get; set; } = "FFE5E7EB";
    public string MainUiUploadIconColor { get; set; } = "FFE5E7EB";
    public string MainUiUploadLabelColor { get; set; } = "FFE5E7EB";
    public bool CustomTransparencyKey { get; set; }
    public string CustomBackgroundColor { get; set; } = "FF1A1D29";
    public string CustomDownloadIconColor { get; set; } = "FF3B82F6";
    public string CustomDownloadLabelColor { get; set; } = "FFE5E7EB";
    public string CustomUploadIconColor { get; set; } = "FF10B981";
    public string CustomUploadLabelColor { get; set; } = "FFE5E7EB";

    // Font
    public string FontName { get; set; } = "Segoe UI";
    public int FontSize { get; set; } = 11;
    public bool IsFontBold { get; set; }

    // Units
    public bool IsUnitSpeedByte { get; set; }
    public bool IsAutomaticSpeedUnit { get; set; } = true;
    public string UnitSpeedName { get; set; } = "Kbps";
    /// <summary>0 = automatic bits (Kbps/Mbps/Gbps), 1 = fixed Mbps, 2 = bytes (KB/s/MB/s/GB/s).</summary>
    public int SpeedUnitMode { get; set; }

    // Behavior
    public bool RunAtStartup { get; set; } = true;
    public int SpeedRefreshTime { get; set; } = 1000;
    public bool AlwaysOnTop { get; set; } = true;
    public bool KeepInTaskbar { get; set; }
    public bool MinimizeToTrayOnClose { get; set; } = true;

    // Alerts
    public bool IsAlertService { get; set; }
    public double DownloadAlert { get; set; }
    public double UploadAlert { get; set; }
    public bool IsDataPlanAlertsEnabled { get; set; } = true;

    // Adapter
    public bool IsNicManuallySelect { get; set; }
    public string NicName { get; set; } = "Ethernet";

    // Widget position
    public int UiPositionWidthPoint { get; set; }
    public int UiPositionHeightPoint { get; set; }
    public bool IsPositionLocked { get; set; }
    public int LockedPositionX { get; set; }
    public int LockedPositionY { get; set; }
    public string UiPositionAnchor { get; set; } = "TopRight";
    public bool IsSidebarCollapsed { get; set; }

    /// <summary>Visual design / layout mode for the floating widget.</summary>
    public WidgetStyle WidgetStyle { get; set; } = WidgetStyle.KineticCapsule;

    /// <summary>Selected regional server identifier for speed testing.</summary>
    public string SpeedTestServerId { get; set; } = "auto";
}

public enum WidgetStyle
{
    KineticCapsule = 0, // Stitch design: sleek horizontal pill [↓ DL | ↑ UL | • Ping]
    ClassicTwoRow = 1,  // Traditional 2-row rectangular card with accent bar
    MinimalistBar = 2,  // Ultra-compact single-line micro bar
    CyberpunkHud = 3,   // High-contrast HUD with dual meters & monospaced digits
}
