using System.Net.NetworkInformation;
using SpeedMeterPro.Models;
using SpeedMeterPro.Services;

namespace SpeedMeterPro.Pages;

public sealed class AdapterConfigPage : UserControl
{
    private readonly AppSettingsVm _s;
    private readonly ComboBox _combo;
    private readonly Label _detail;

    public AdapterConfigPage(AppSettingsVm settings)
    {
        _s = settings;
        BackColor = ThemeManager.Bg;
        AutoScroll = true;
        Controls.Add(PageStyle.Title("Adapter Config"));

        var group = PageStyle.Card("Network adapter selection");
        group.Location = new Point(24, 72);
        group.Size = new Size(620, 460);
        Controls.Add(group);

        var manual = PageStyle.Check("Manually select adapter", _s.IsNicManuallySelect);
        manual.Location = new Point(24, 56);
        group.Controls.Add(manual);
        manual.CheckedChanged += (_, _) => _s.IsNicManuallySelect = manual.Checked;

        _combo = PageStyle.Combo();
        _combo.Location = new Point(24, 96);
        _combo.Width = 380;
        var nics = NetworkInterface.GetAllNetworkInterfaces()
            .Where(n => n.NetworkInterfaceType is NetworkInterfaceType.Ethernet
                        or NetworkInterfaceType.Wireless80211)
            .OrderBy(n => n.Name).ToList();
        foreach (var n in nics)
        {
            _combo.Items.Add(n.Name);
        }
        if (_combo.Items.Count > 0)
        {
            _combo.SelectedItem = nics.FirstOrDefault(n => n.Name == _s.NicName)?.Name ?? nics[0].Name;
        }
        group.Controls.Add(_combo);
        _combo.SelectedIndexChanged += (_, _) =>
        {
            if (_combo.SelectedItem is string name) _s.NicName = name;
        };

        var autoHint = PageStyle.Body(
            "Automatic mode validates candidates by gateway / DNS / unicast availability and pings the gateway, " +
            "then picks the highest-scoring live interface. Manual mode uses the adapter selected above.");
        autoHint.Location = new Point(24, 145);
        autoHint.MaximumSize = new Size(570, 0);
        group.Controls.Add(autoHint);

        _detail = new Label
        {
            Text = "",
            Font = new Font("Segoe UI", 9),
            ForeColor = ThemeManager.Text,
            Location = new Point(24, 235),
            AutoSize = true,
            MaximumSize = new Size(570, 140),
        };
        group.Controls.Add(_detail);

        var scan = PageStyle.Button("Scan & validate");
        scan.Location = new Point(24, 395);
        group.Controls.Add(scan);
        scan.Click += (_, _) =>
        {
            _s.IsNicManuallySelect = manual.Checked;
            _s.NicName = _combo.SelectedItem as string ?? _s.NicName;
            SettingsStore.Save(_s);
            ScanDetail();
        };

        ScanDetail();
    }

    private void ScanDetail()
    {
        var text = new System.Text.StringBuilder();
        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces().OrderBy(n => n.Name))
        {
            try
            {
                var p = nic.GetIPProperties();
                int score = 0;
                if (p.GatewayAddresses.Count > 0) score += 4;
                if (p.DnsAddresses.Count > 0) score += 2;
                if (p.UnicastAddresses.Any(a => a.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)) score += 1;
                text.AppendLine($"{nic.Name} [{nic.NetworkInterfaceType}] {nic.OperationalStatus} score={score}" +
                                $"  gw={p.GatewayAddresses.FirstOrDefault()?.Address?.ToString() ?? "-"}");
            }
            catch { }
        }
        _detail.Text = text.ToString();
    }
}
