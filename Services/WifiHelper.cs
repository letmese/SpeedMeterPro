using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Text;
using System.Text.Json;

namespace SpeedMeterPro.Services;

public sealed class WifiProfile
{
    public string Name { get; set; } = "";
    public string Ssid { get; set; } = "";
    public string Authentication { get; set; } = "";
    public string Encryption { get; set; } = "";
    public string RadioType { get; set; } = "";
    public string Channel { get; set; } = "";
    public string Key { get; set; } = "";
    public bool IsConnected { get; set; }
}

/// <summary>
/// WiFi page helper: netsh wlan profile listing/detail/deletion and
/// QR credential sharing (WIFI:T:WPA;S:...;P:...;;).
/// </summary>
public static class WifiHelper
{
    public static string GetConnectedSsid()
    {
        try
        {
            var outText = RunNetsh("wlan show interfaces");
            foreach (var line in outText.Split('\n'))
            {
                var t = line.Trim();
                if (t.StartsWith("SSID", StringComparison.OrdinalIgnoreCase)
                    || t.StartsWith("SSID 名称", StringComparison.OrdinalIgnoreCase))
                {
                    var parts = t.Split(new[] { ':', '：' }, 2);
                    if (parts.Length == 2)
                    {
                        var ssid = parts[1].Trim().Trim('"');
                        if (!string.IsNullOrEmpty(ssid)) return ssid;
                    }
                }
            }
        }
        catch { }

        // Fallback for Windows 11: netsh wlan show interfaces requires location permission/admin.
        // Get-NetConnectionProfile provides the active connection name without elevation.
        try
        {
            using var proc = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "powershell",
                    Arguments = "-NoProfile -Command \"(Get-NetConnectionProfile | Where-Object { $_.InterfaceAlias -like '*Wi-Fi*' -or $_.InterfaceAlias -like '*Wireless*' -or $_.InterfaceAlias -like '*WLAN*' }).Name\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true,
                }
            };
            proc.Start();
            var outName = proc.StandardOutput.ReadToEnd().Trim();
            proc.WaitForExit(3000);
            if (!string.IsNullOrEmpty(outName))
            {
                return outName;
            }
        }
        catch { }

        return "";
    }

    public static List<WifiProfile> ListProfiles()
    {
        var profiles = new List<WifiProfile>();
        try
        {
            var names = ParseProfileNames(RunNetsh("wlan show profiles"));
            var connected = GetConnectedSsid();
            foreach (var name in names)
            {
                var p = GetProfileDetail(name);
                p.IsConnected = !string.IsNullOrEmpty(connected) &&
                    (string.Equals(p.Ssid, connected, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(p.Name, connected, StringComparison.OrdinalIgnoreCase)
                    || connected.StartsWith(p.Ssid, StringComparison.OrdinalIgnoreCase)
                    || connected.StartsWith(p.Name, StringComparison.OrdinalIgnoreCase)
                    || p.Ssid.StartsWith(connected, StringComparison.OrdinalIgnoreCase));
                profiles.Add(p);
            }
        }
        catch (Exception ex)
        {
            Logger.Log($"WifiHelper.ListProfiles: {ex.Message}");
        }
        return profiles;
    }

    private static List<string> ParseProfileNames(string output)
    {
        var names = new List<string>();
        foreach (var line in output.Split('\n'))
        {
            var t = line.Trim();
            if (t.Contains("User Profile", StringComparison.OrdinalIgnoreCase)
                || t.Contains("用户配置文件", StringComparison.OrdinalIgnoreCase)
                || t.StartsWith("Profile", StringComparison.OrdinalIgnoreCase))
            {
                var parts = t.Split(new[] { ':', '：' }, 2);
                if (parts.Length == 2)
                {
                    var name = parts[1].Trim().Trim('"');
                    if (!string.IsNullOrEmpty(name) && !names.Contains(name, StringComparer.OrdinalIgnoreCase))
                    {
                        names.Add(name);
                    }
                }
            }
        }
        return names;
    }

    public static WifiProfile GetProfileDetail(string profileName)
    {
        var p = new WifiProfile { Name = profileName, Ssid = profileName };
        try
        {
            var outText = RunNetsh($"wlan show profile name=\"{profileName}\" key=clear");
            foreach (var line in outText.Split('\n'))
            {
                var t = line.Trim();
                if (t.StartsWith("SSID name", StringComparison.OrdinalIgnoreCase)
                    || t.StartsWith("SSID 名称", StringComparison.OrdinalIgnoreCase))
                {
                    var parts = t.Split(new[] { ':', '：' }, 2);
                    if (parts.Length == 2) p.Ssid = parts[1].Trim().Trim('"');
                }
                else if (t.StartsWith("Authentication", StringComparison.OrdinalIgnoreCase)
                      || t.StartsWith("身份验证", StringComparison.OrdinalIgnoreCase))
                {
                    var parts = t.Split(new[] { ':', '：' }, 2);
                    if (parts.Length == 2) p.Authentication = parts[1].Trim();
                }
                else if (t.StartsWith("Encryption", StringComparison.OrdinalIgnoreCase)
                      || t.StartsWith("加密", StringComparison.OrdinalIgnoreCase)
                      || t.StartsWith("Cipher", StringComparison.OrdinalIgnoreCase))
                {
                    var parts = t.Split(new[] { ':', '：' }, 2);
                    if (parts.Length == 2) p.Encryption = parts[1].Trim();
                }
                else if (t.StartsWith("Radio type", StringComparison.OrdinalIgnoreCase)
                      || t.StartsWith("无线电类型", StringComparison.OrdinalIgnoreCase))
                {
                    var parts = t.Split(new[] { ':', '：' }, 2);
                    if (parts.Length == 2) p.RadioType = parts[1].Trim();
                }
                else if (t.StartsWith("Channel", StringComparison.OrdinalIgnoreCase)
                      || t.StartsWith("信道", StringComparison.OrdinalIgnoreCase)
                      || t.StartsWith("频道", StringComparison.OrdinalIgnoreCase))
                {
                    var parts = t.Split(new[] { ':', '：' }, 2);
                    if (parts.Length == 2) p.Channel = parts[1].Trim();
                }
            }

            // Key material shown with key=clear; decrypt via DPAPI-encrypted cache.
            if (outText.Contains("key content", StringComparison.OrdinalIgnoreCase)
                || outText.Contains("密钥内容", StringComparison.OrdinalIgnoreCase))
            {
                p.Key = WifiKeyVault.GetOrDecrypt(profileName, () => ExtractKey(outText));
            }
        }
        catch (Exception ex)
        {
            Logger.Log($"GetProfileDetail({profileName}): {ex.Message}");
        }
        return p;
    }

    private static string ExtractKey(string output)
    {
        foreach (var line in output.Split('\n'))
        {
            var t = line.Trim();
            if (t.StartsWith("Key Content", StringComparison.OrdinalIgnoreCase)
                || t.StartsWith("密钥内容", StringComparison.OrdinalIgnoreCase)
                || t.StartsWith("Key content", StringComparison.OrdinalIgnoreCase))
            {
                var parts = t.Split(new[] { ':', '：' }, 2);
                if (parts.Length == 2) return parts[1].Trim();
            }
        }
        return "";
    }

    public static bool DeleteProfile(string profileName)
    {
        try
        {
            var output = RunNetsh($"netsh wlan delete profile name=\"{profileName}\"");
            return output.Contains("deleted", StringComparison.OrdinalIgnoreCase)
                || output.Contains("已删除", StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            Logger.Log($"DeleteProfile: {ex.Message}");
            return false;
        }
    }

    public static string BuildQrPayload(WifiProfile profile)
    {
        var auth = profile.Authentication switch
        {
            "WPA2-Personal" or "WPA2" => "WPA",
            "WPA3-Personal" or "WPA3" => "WPA",
            "Open" or "None" => "nopass",
            _ => "WPA",
        };
        var key = string.IsNullOrEmpty(profile.Key) ? "" : profile.Key;
        return $"WIFI:T:{auth};S:{profile.Ssid};P:{key};;";
    }

    private static string RunNetsh(string arguments)
    {
        if (arguments.StartsWith("netsh ", StringComparison.OrdinalIgnoreCase))
        {
            arguments = arguments.Substring(6);
        }
        else if (arguments.StartsWith("netsh.exe ", StringComparison.OrdinalIgnoreCase))
        {
            arguments = arguments.Substring(10);
        }

        using var proc = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "netsh",
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
            }
        };
        proc.Start();
        string output = proc.StandardOutput.ReadToEnd();
        proc.WaitForExit(5000);
        return output + proc.StandardError.ReadToEnd();
    }
}

/// <summary>
/// DPAPI-protected cache for WiFi keys: plaintext keys never touch disk;
/// key=clear output is decrypted once, then encrypted with CurrentUser scope.
/// </summary>
public static class WifiKeyVault
{
    public static string GetOrDecrypt(string profileName, Func<string> decryptFunc)
    {
        try
        {
            if (File.Exists(AppPaths.WifiKeyCacheFile))
            {
                var map = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(AppPaths.WifiKeyCacheFile));
                if (map is not null && map.TryGetValue(profileName, out var enc))
                {
                    var bytes = Convert.FromBase64String(enc);
                    var plain = System.Security.Cryptography.ProtectedData.Unprotect(
                        bytes, null, System.Security.Cryptography.DataProtectionScope.CurrentUser);
                    return System.Text.Encoding.UTF8.GetString(plain);
                }
            }

            var key = decryptFunc();
            if (string.IsNullOrEmpty(key)) return key;

            var encBytes = System.Security.Cryptography.ProtectedData.Protect(
                System.Text.Encoding.UTF8.GetBytes(key), null, System.Security.Cryptography.DataProtectionScope.CurrentUser);
            var dict = new Dictionary<string, string>();
            if (File.Exists(AppPaths.WifiKeyCacheFile))
            {
                try
                {
                    dict = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(AppPaths.WifiKeyCacheFile))
                           ?? dict;
                }
                catch { }
            }
            dict[profileName] = Convert.ToBase64String(encBytes);
            Directory.CreateDirectory(AppPaths.DataDir);
            File.WriteAllText(AppPaths.WifiKeyCacheFile, JsonSerializer.Serialize(dict));
            return key;
        }
        catch (Exception ex)
        {
            Logger.Log($"WifiKeyVault: {ex.Message}");
            return "";
        }
    }
}
