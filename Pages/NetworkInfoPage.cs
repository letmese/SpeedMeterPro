using System.Net;
using System.Text.Json;
using SpeedMeterPro.Models;
using SpeedMeterPro.Services;

namespace SpeedMeterPro.Pages;

public sealed class NetworkInfoPage : UserControl
{
    private readonly AppSettingsVm _s;
    private readonly Label _local;
    private readonly Label _public;
    private readonly Button _refresh;

    public NetworkInfoPage(AppSettingsVm settings)
    {
        _s = settings;
        BackColor = ThemeManager.Bg;
        AutoScroll = true;
        Controls.Add(PageStyle.Title("Network Info"));

        var group = PageStyle.Card("Local & public information");
        group.Location = new Point(24, 72);
        group.Size = new Size(620, 480);
        Controls.Add(group);

        _local = new Label
        {
            Text = "Loading local info...",
            Font = new Font("Segoe UI", 9.5f),
            ForeColor = ThemeManager.Text,
            Location = new Point(24, 56),
            AutoSize = true,
        };
        group.Controls.Add(_local);

        _public = new Label
        {
            Text = "Loading public IP...",
            Font = new Font("Segoe UI", 9.5f),
            ForeColor = ThemeManager.Text,
            Location = new Point(24, 210),
            AutoSize = true,
        };
        group.Controls.Add(_public);

        _refresh = PageStyle.Button("Refresh");
        _refresh.Location = new Point(24, 410);
        group.Controls.Add(_refresh);
        _refresh.Click += async (_, _) => await RefreshAllAsync();

        HandleCreated += async (_, _) => await RefreshAllAsync();
        ShowLocal();
        _ = RefreshAllAsync();
    }

    private void ShowLocal()
    {
        var text = new System.Text.StringBuilder();
        text.AppendLine("Local adapters:");
        var nics = System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces()
            .Where(n => n.OperationalStatus == System.Net.NetworkInformation.OperationalStatus.Up);
        foreach (var nic in nics)
        {
            var p = nic.GetIPProperties();
            var ips = string.Join(", ", p.UnicastAddresses
                .Where(a => a.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                .Select(a => a.Address.ToString()));
            if (string.IsNullOrEmpty(ips)) continue;
            text.AppendLine($"  • {nic.Name}: {ips}");
        }
        _local.Text = text.ToString();
    }

    private async Task RefreshAllAsync()
    {
        if (_refresh.Disposing || _refresh.IsDisposed) return;
        _refresh.Enabled = false;
        _public.Text = "Querying public IP...";
        ShowLocal();

        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("SpeedMeterPro/1.0 (Windows 11; x64)");

            string? ip = null;
            string? city = null;
            string? region = null;
            string? country = null;
            string? org = null;

            // Primary endpoint: ipinfo.io
            try
            {
                var json = await client.GetStringAsync("https://ipinfo.io/json");
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                if (root.TryGetProperty("ip", out var ipP)) ip = ipP.GetString();
                if (root.TryGetProperty("city", out var cityP)) city = cityP.GetString();
                if (root.TryGetProperty("region", out var regP)) region = regP.GetString();
                if (root.TryGetProperty("country", out var cntP)) country = cntP.GetString();
                if (root.TryGetProperty("org", out var orgP)) org = orgP.GetString();
            }
            catch { }

            // Secondary fallback: api.ipify.org
            if (string.IsNullOrEmpty(ip))
            {
                try
                {
                    var json = await client.GetStringAsync("https://api.ipify.org?format=json");
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("ip", out var ipP)) ip = ipP.GetString();
                }
                catch { }
            }

            // Tertiary fallback: icanhazip.com
            if (string.IsNullOrEmpty(ip))
            {
                try
                {
                    ip = (await client.GetStringAsync("https://icanhazip.com")).Trim();
                }
                catch { }
            }

            if (!string.IsNullOrEmpty(ip))
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"Public IP: {ip}");
                if (!string.IsNullOrEmpty(city) || !string.IsNullOrEmpty(region))
                    sb.AppendLine($"City / Region: {(city ?? "-")}, {(region ?? "-")}");
                if (!string.IsNullOrEmpty(country))
                    sb.AppendLine($"Country: {country}");
                if (!string.IsNullOrEmpty(org))
                    sb.AppendLine($"ISP / Org: {org}");
                _public.Text = sb.ToString().TrimEnd();
            }
            else
            {
                _public.Text = "Could not retrieve public IP (network offline or service unavailable).";
            }
        }
        catch (Exception ex)
        {
            _public.Text = $"Public IP lookup failed: {ex.Message}";
        }
        finally
        {
            if (!_refresh.Disposing && !_refresh.IsDisposed)
            {
                _refresh.Enabled = true;
            }
        }
    }
}
