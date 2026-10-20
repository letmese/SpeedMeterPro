using SpeedMeterPro.Models;
using SpeedMeterPro.Services;

namespace SpeedMeterPro.Pages;

public sealed class NetworkToolsPage : UserControl
{
    private readonly AppSettingsVm _s;
    private readonly TextBox _host;
    private readonly ComboBox _tool;
    private readonly TextBox _output;

    public NetworkToolsPage(AppSettingsVm settings)
    {
        _s = settings;
        BackColor = ThemeManager.Bg;
        AutoScroll = true;
        Controls.Add(PageStyle.Title("Network Tools"));

        var group = PageStyle.Card("Ping / DNS / IP lookup / Traceroute");
        group.Location = new Point(24, 72);
        group.Size = new Size(680, 560);
        Controls.Add(group);

        group.Controls.Add(new Label
        {
            Text = "Host / domain:",
            ForeColor = ThemeManager.Text,
            Location = new Point(24, 60),
            AutoSize = true,
        });
        _host = PageStyle.TextBox("1.1.1.1");
        _host.Location = new Point(135, 58);
        _host.Width = 200;
        group.Controls.Add(_host);

        _tool = PageStyle.Combo();
        _tool.Items.AddRange(new object[] { "Ping", "DNS lookup", "Public IP", "Traceroute" });
        _tool.SelectedIndex = 0;
        _tool.Location = new Point(355, 58);
        _tool.Width = 140;
        group.Controls.Add(_tool);

        var run = PageStyle.Button("Run");
        run.Location = new Point(515, 56);
        group.Controls.Add(run);
        run.Click += async (_, _) => await RunToolAsync();

        _output = new TextBox
        {
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            Font = new Font("Consolas", 9.5f),
            BackColor = ThemeManager.BgAlt,
            ForeColor = ThemeManager.Text,
            BorderStyle = BorderStyle.FixedSingle,
            Location = new Point(24, 110),
            Size = new Size(632, 420),
        };
        group.Controls.Add(_output);
    }

    private async Task RunToolAsync()
    {
        var host = string.IsNullOrWhiteSpace(_host.Text) ? "1.1.1.1" : _host.Text.Trim();
        _output.Text = "Running...";
        try
        {
            switch (_tool.SelectedItem?.ToString())
            {
                case "Ping":
                    var reply = PingHelper.Send(host, 2000);
                    _output.Text = reply is null
                        ? $"Ping {host}: unreachable"
                        : reply.Status == System.Net.NetworkInformation.IPStatus.Success
                            ? $"Ping {host}: {reply.RoundtripTime} ms ({reply.Address})"
                            : $"Ping {host}: {reply.Status}";
                    break;
                case "DNS lookup":
                    _output.Text = $"{host} -> {DnsHelper.Resolve(host)}";
                    break;
                case "Public IP":
                    await ShowPublicIpAsync();
                    break;
                case "Traceroute":
                    _output.Text = string.Join(Environment.NewLine, TraceRouteHelper.Run(host));
                    break;
            }
        }
        catch (Exception ex)
        {
            _output.Text = "Error: " + ex.Message;
        }
    }

    private async Task ShowPublicIpAsync()
    {
        using var client = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(8) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("SpeedMeterPro/1.0");
        try
        {
            _output.Text = (await client.GetStringAsync("https://api.ipify.org")).Trim();
        }
        catch
        {
            try
            {
                _output.Text = (await client.GetStringAsync("https://icanhazip.com")).Trim();
            }
            catch (Exception ex)
            {
                _output.Text = "Error resolving public IP: " + ex.Message;
            }
        }
    }
}
