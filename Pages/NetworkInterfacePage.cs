using System.Net.NetworkInformation;
using SpeedMeterPro.Models;
using SpeedMeterPro.Services;

namespace SpeedMeterPro.Pages;

public sealed class NetworkInterfacePage : UserControl
{
    private readonly AppSettingsVm _s;
    private readonly ListView _list;

    public NetworkInterfacePage(AppSettingsVm settings)
    {
        _s = settings;
        BackColor = ThemeManager.Bg;
        AutoScroll = true;
        Controls.Add(PageStyle.Title("Network Interface"));

        var group = PageStyle.Card("All interfaces");
        group.Location = new Point(24, 72);
        group.Size = new Size(760, 490);
        Controls.Add(group);

        _list = PageStyle.ListView();
        _list.Location = new Point(24, 56);
        _list.Size = new Size(712, 350);
        _list.Columns.Add("Name", 180);
        _list.Columns.Add("Type", 120);
        _list.Columns.Add("Status", 90);
        _list.Columns.Add("Speed", 100);
        _list.Columns.Add("IP", 140);
        _list.Columns.Add("MAC", 140);
        group.Controls.Add(_list);

        var refresh = PageStyle.Button("Refresh");
        refresh.Location = new Point(24, 428);
        group.Controls.Add(refresh);
        refresh.Click += (_, _) => Populate();

        Populate();
    }

    private void Populate()
    {
        _list.BeginUpdate();
        _list.Items.Clear();
        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces().OrderBy(n => n.Name))
        {
            try
            {
                var p = nic.GetIPProperties();
                var ip = p.UnicastAddresses
                    .FirstOrDefault(a => a.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                    ?.Address.ToString() ?? "-";
                var mac = nic.GetPhysicalAddress().ToString();
                mac = string.IsNullOrEmpty(mac) ? "-" : string.Join(":", Enumerable.Range(0, mac.Length / 2).Select(i => mac.Substring(i * 2, 2)));
                var speed = nic.Speed switch
                {
                    >= 1_000_000_000 => $"{nic.Speed / 1_000_000_000.0:0.#} Gbps",
                    > 0 => $"{nic.Speed / 1_000_000.0:0.#} Mbps",
                    _ => "-",
                };
                _list.Items.Add(new ListViewItem(new[]
                {
                    nic.Name, nic.NetworkInterfaceType.ToString(), nic.OperationalStatus.ToString(),
                    speed, ip, mac,
                }));
            }
            catch { }
        }
        _list.EndUpdate();
    }
}
