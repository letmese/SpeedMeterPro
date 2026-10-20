using System.Drawing.Imaging;
using System.IO;
using SpeedMeterPro.Models;
using SpeedMeterPro.Services;
using QRCoder;

namespace SpeedMeterPro.Pages;

public sealed class WifiPage : UserControl
{
    private readonly AppSettingsVm _s;
    private readonly ListView _list;
    private readonly Label _connected;
    private readonly PictureBox _qr;

    public WifiPage(AppSettingsVm settings)
    {
        _s = settings;
        BackColor = ThemeManager.Bg;
        AutoScroll = true;
        Controls.Add(PageStyle.Title("WiFi"));

        var group = PageStyle.Card("Saved wireless profiles (netsh wlan)");
        group.Location = new Point(24, 72);
        group.Size = new Size(540, 500);
        Controls.Add(group);

        _connected = new Label
        {
            Text = "Connected SSID: " + WifiHelper.GetConnectedSsid(),
            Font = new Font("Segoe UI", 10.5f, FontStyle.Bold),
            ForeColor = ThemeManager.Accent,
            Location = new Point(24, 56),
            AutoSize = true,
        };
        group.Controls.Add(_connected);

        _list = PageStyle.ListView();
        _list.Location = new Point(24, 92);
        _list.Size = new Size(492, 330);
        _list.Columns.Add("SSID", 180);
        _list.Columns.Add("Auth", 120);
        _list.Columns.Add("Encryption", 110);
        _list.Columns.Add("Channel", 80);
        group.Controls.Add(_list);

        var refresh = PageStyle.Button("Refresh", PageStyle.ButtonKind.Primary);
        refresh.Click += (_, _) => Populate();

        var details = PageStyle.Button("Details", PageStyle.ButtonKind.Secondary);
        details.Click += (_, _) => ShowDetails();

        var delete = PageStyle.Button("Delete profile", PageStyle.ButtonKind.Danger);
        delete.Click += (_, _) => DeleteSelected();

        var share = PageStyle.Button("Share QR", PageStyle.ButtonKind.Secondary);
        share.Click += (_, _) => ShowQr();

        group.Controls.Add(refresh);
        group.Controls.Add(details);
        group.Controls.Add(delete);
        group.Controls.Add(share);
        PageStyle.LayoutButtonBar(24, 440, 16, refresh, details, delete, share);

        var qrGroup = PageStyle.Card("QR credential share");
        qrGroup.Tag = "fixed"; // sits beside the stretched profile card
        qrGroup.Location = new Point(580, 72);
        qrGroup.Size = new Size(320, 500);
        Controls.Add(qrGroup);
        _qr = new PictureBox
        {
            SizeMode = PictureBoxSizeMode.Zoom,
            Location = new Point(32, 60),
            Size = new Size(256, 256),
            BackColor = Color.White,
        };
        qrGroup.Controls.Add(_qr);
        var qrHint = PageStyle.Body("Select a profile then click 'Share QR' to display a scannable WIFI: credential code.");
        qrHint.Location = new Point(24, 340);
        qrHint.MaximumSize = new Size(272, 0);
        qrGroup.Controls.Add(qrHint);

        Populate();
    }

    private void Populate()
    {
        var conn = WifiHelper.GetConnectedSsid();
        _connected.Text = "Connected SSID: " + (string.IsNullOrEmpty(conn) ? "(None / Not Connected)" : conn);
        _list.BeginUpdate();
        _list.Items.Clear();
        foreach (var p in WifiHelper.ListProfiles())
        {
            var item = new ListViewItem(new[]
            {
                p.Ssid + (p.IsConnected ? "  ●" : ""),
                p.Authentication,
                p.Encryption,
                p.Channel,
            });
            item.Tag = p;
            _list.Items.Add(item);
        }
        _list.EndUpdate();
    }

    private WifiProfile? Selected() => _list.SelectedItems.Count > 0 ? _list.SelectedItems[0].Tag as WifiProfile : null;

    private void ShowDetails()
    {
        var p = Selected();
        if (p is null)
        {
            Toast.Show("WiFi", "Select a profile first.");
            return;
        }
        var msg =
            $"SSID: {p.Ssid}\n" +
            $"Authentication: {p.Authentication}\n" +
            $"Encryption: {p.Encryption}\n" +
            $"Radio: {p.RadioType}\n" +
            $"Channel: {p.Channel}\n" +
            $"Key: {(string.IsNullOrEmpty(p.Key) ? "(hidden / open network)" : "•••••• (shown via Details+QR)")}";
        MessageBox.Show(msg, "WiFi profile: " + p.Name, MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void DeleteSelected()
    {
        var p = Selected();
        if (p is null)
        {
            Toast.Show("WiFi", "Select a profile first.");
            return;
        }
        var confirm = MessageBox.Show(
            $"Delete WiFi profile '{p.Name}'? This removes the saved network credentials.",
            "Delete profile", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
        if (confirm == DialogResult.Yes)
        {
            var ok = WifiHelper.DeleteProfile(p.Name);
            Toast.Show("WiFi", ok ? $"Profile '{p.Name}' deleted." : $"Failed to delete '{p.Name}'.", ok ? "#FF10B981" : "#FFEF4444");
            Populate();
        }
    }

    private void ShowQr()
    {
        var p = Selected();
        if (p is null)
        {
            Toast.Show("WiFi", "Select a profile first.");
            return;
        }
        var payload = WifiHelper.BuildQrPayload(p);
        try
        {
            using var generator = new QRCodeGenerator();
            var data = generator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.M);
            using var qr = new QRCode(data);
            using var bmp = qr.GetGraphic(12);
            var ms = new MemoryStream();
            bmp.Save(ms, ImageFormat.Png);
            ms.Position = 0;
            _qr.Image?.Dispose();
            _qr.Image = Image.FromStream(ms);
        }
        catch (Exception ex)
        {
            Toast.Show("WiFi", "QR generation failed: " + ex.Message, "#FFEF4444");
        }
    }
}
