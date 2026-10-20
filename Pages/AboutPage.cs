using SpeedMeterPro.Models;
using SpeedMeterPro.Services;

namespace SpeedMeterPro.Pages;

public sealed class AboutPage : UserControl
{
    public AboutPage(AppSettingsVm settings)
    {
        BackColor = ThemeManager.Bg;
        AutoScroll = true;
        Controls.Add(PageStyle.Title("About"));

        var group = PageStyle.Card("Speed Meter Pro");
        group.Location = new Point(24, 72);
        group.Size = new Size(620, 280);
        Controls.Add(group);

        var info = new Label
        {
            Text =
                "Version: 1.0.0\n" +
                "Platform: .NET 8 WinForms, Windows 11\n" +
                "License: Free (all features unlocked)\n\n" +
                "An original implementation of the Net Speed Meter feature set: live widget, speed test, " +
                "usage stats, WiFi manager, network tools and more.\n\n" +
                "No telemetry. No third-party analytics. No store integration.",
            Font = new Font("Segoe UI", 10),
            ForeColor = ThemeManager.Text,
            Location = new Point(24, 56),
            AutoSize = true,
        };
        group.Controls.Add(info);
    }
}

public sealed class OurAppsPage : UserControl
{
    public OurAppsPage(AppSettingsVm settings)
    {
        BackColor = ThemeManager.Bg;
        AutoScroll = true;
        Controls.Add(PageStyle.Title("Our Apps"));

        var body = PageStyle.Body(
            "This space is reserved for showcasing companion apps. Speed Meter Pro is currently the only " +
            "product. No promotional or store content is bundled — matching the telemetry-free, promo-free goal.");
        body.Location = new Point(24, 76);
        body.MaximumSize = new Size(720, 0);
        Controls.Add(body);
    }
}
