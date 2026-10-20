namespace SpeedMeterPro.Models;

public sealed class SpeedTestServerInfo
{
    public string Id { get; set; } = "auto";
    public string Name { get; set; } = "Auto: Nearest Edge";
    public string Region { get; set; } = "Auto (Asia / Local)";
    public string Provider { get; set; } = "Cloudflare Global Edge";
    public string PingUrl { get; set; } = "https://speed.cloudflare.com/__down?bytes=1";
    public string DownloadUrl { get; set; } = "https://speed.cloudflare.com/__down?bytes=52428800";
    public string WarmupUrl { get; set; } = "https://speed.cloudflare.com/__down?bytes=524288";
    public string UploadUrl { get; set; } = "https://speed.cloudflare.com/__up";
    public bool IsCloudflareAnycast { get; set; } = true;
}

public static class SpeedTestServers
{
    public static readonly SpeedTestServerInfo[] All = new[]
    {
        new SpeedTestServerInfo
        {
            Id = "auto",
            Name = "⚡ Auto: Nearest Edge (Lowest Latency)",
            Region = "Auto-detect (Asia / Local)",
            Provider = "Cloudflare Global Edge · 300+ Cities",
            PingUrl = "https://speed.cloudflare.com/__down?bytes=1",
            DownloadUrl = "https://speed.cloudflare.com/__down?bytes=52428800",
            WarmupUrl = "https://speed.cloudflare.com/__down?bytes=524288",
            UploadUrl = "https://speed.cloudflare.com/__up",
            IsCloudflareAnycast = true,
        },
        new SpeedTestServerInfo
        {
            Id = "singapore",
            Name = "🇸🇬 Singapore (Southeast Asia)",
            Region = "Singapore · Southeast Asia Hub",
            Provider = "Equinix SG1 / Akamai Edge",
            PingUrl = "https://speedtest.singapore.linode.com/",
            DownloadUrl = "https://speedtest.singapore.linode.com/100MB-singapore.bin",
            WarmupUrl = "https://speedtest.singapore.linode.com/100MB-singapore.bin",
            UploadUrl = "https://speed.cloudflare.com/__up",
            IsCloudflareAnycast = false,
        },
        new SpeedTestServerInfo
        {
            Id = "tokyo",
            Name = "🇯🇵 Tokyo, Japan (East Asia)",
            Region = "Tokyo · East Asia Hub",
            Provider = "Linode Tokyo 2 / Akamai Edge",
            PingUrl = "https://speedtest.tokyo2.linode.com/",
            DownloadUrl = "https://speedtest.tokyo2.linode.com/100MB-tokyo2.bin",
            WarmupUrl = "https://speedtest.tokyo2.linode.com/100MB-tokyo2.bin",
            UploadUrl = "https://speed.cloudflare.com/__up",
            IsCloudflareAnycast = false,
        },
        new SpeedTestServerInfo
        {
            Id = "mumbai",
            Name = "🇮🇳 Mumbai, India (South Asia)",
            Region = "Mumbai · South Asia Hub",
            Provider = "Linode Mumbai 1 / Akamai Edge",
            PingUrl = "https://speedtest.mumbai1.linode.com/",
            DownloadUrl = "https://speedtest.mumbai1.linode.com/100MB-mumbai.bin",
            WarmupUrl = "https://speedtest.mumbai1.linode.com/100MB-mumbai.bin",
            UploadUrl = "https://speed.cloudflare.com/__up",
            IsCloudflareAnycast = false,
        },
        new SpeedTestServerInfo
        {
            Id = "frankfurt",
            Name = "🇩🇪 Frankfurt, Germany (Europe Central)",
            Region = "Frankfurt · Europe Central Hub",
            Provider = "Telehouse / Akamai Edge",
            PingUrl = "https://speedtest.frankfurt.linode.com/",
            DownloadUrl = "https://speedtest.frankfurt.linode.com/100MB-frankfurt.bin",
            WarmupUrl = "https://speedtest.frankfurt.linode.com/100MB-frankfurt.bin",
            UploadUrl = "https://speed.cloudflare.com/__up",
            IsCloudflareAnycast = false,
        },
        new SpeedTestServerInfo
        {
            Id = "london",
            Name = "🇬🇧 London, UK (Europe West)",
            Region = "London · Western Europe Hub",
            Provider = "Linode London / Akamai Edge",
            PingUrl = "https://speedtest.london.linode.com/",
            DownloadUrl = "https://speedtest.london.linode.com/100MB-london.bin",
            WarmupUrl = "https://speedtest.london.linode.com/100MB-london.bin",
            UploadUrl = "https://speed.cloudflare.com/__up",
            IsCloudflareAnycast = false,
        },
        new SpeedTestServerInfo
        {
            Id = "newark",
            Name = "🇺🇸 Newark, NJ, US (North America East)",
            Region = "Newark, NJ · US East Coast",
            Provider = "Linode Newark / Akamai Edge",
            PingUrl = "https://speedtest.newark.linode.com/",
            DownloadUrl = "https://speedtest.newark.linode.com/100MB-newark.bin",
            WarmupUrl = "https://speedtest.newark.linode.com/100MB-newark.bin",
            UploadUrl = "https://speed.cloudflare.com/__up",
            IsCloudflareAnycast = false,
        },
        new SpeedTestServerInfo
        {
            Id = "fremont",
            Name = "🇺🇸 Fremont, CA, US (North America West)",
            Region = "Fremont, CA · US Silicon Valley",
            Provider = "Linode Fremont / Akamai Edge",
            PingUrl = "https://speedtest.fremont.linode.com/",
            DownloadUrl = "https://speedtest.fremont.linode.com/100MB-fremont.bin",
            WarmupUrl = "https://speedtest.fremont.linode.com/100MB-fremont.bin",
            UploadUrl = "https://speed.cloudflare.com/__up",
            IsCloudflareAnycast = false,
        },
        new SpeedTestServerInfo
        {
            Id = "sydney",
            Name = "🇦🇺 Sydney, Australia (Oceania)",
            Region = "Sydney · Oceania Hub",
            Provider = "Equinix SY1 / Akamai Edge",
            PingUrl = "https://speedtest.syd1.linode.com/",
            DownloadUrl = "https://speedtest.syd1.linode.com/100MB-syd1.bin",
            WarmupUrl = "https://speedtest.syd1.linode.com/100MB-syd1.bin",
            UploadUrl = "https://speed.cloudflare.com/__up",
            IsCloudflareAnycast = false,
        },
    };

    public static SpeedTestServerInfo GetById(string? id)
    {
        if (string.IsNullOrWhiteSpace(id)) return All[0];
        foreach (var s in All)
        {
            if (string.Equals(s.Id, id, StringComparison.OrdinalIgnoreCase)) return s;
        }
        return All[0];
    }
}
