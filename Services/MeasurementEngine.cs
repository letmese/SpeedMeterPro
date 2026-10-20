using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace SpeedMeterPro.Services;

/// <summary>Snapshot of one interface's byte counters.</summary>
public readonly struct NicSnapshot
{
    public NicSnapshot(string name, long rxBytes, long txBytes)
    {
        Name = name;
        RxBytes = rxBytes;
        TxBytes = txBytes;
    }

    public string Name { get; }
    public long RxBytes { get; }
    public long TxBytes { get; }
}

/// <summary>
/// Adapter selector replicating the reference behavior:
/// prefers an IPv4 interface that has a gateway, DNS servers and a unicast
/// address; falls back through candidates; optional active ping validation.
/// </summary>
public static class AdapterSelector
{
    private static readonly string[] ReliableTestServers =
        { "8.8.8.8", "1.1.1.1", "208.67.222.222" };

    private static readonly string[] VirtualIndicators =
        { "virtual", "vmware", "virtualbox", "hyper-v", "docker", "vethernet", "loopback",
          "teredo", "isatap", "6to4", "tap-windows", "openvpn", "wintun", "tunnel", "npcap", "kernel debug" };

    public static NetworkInterface? SelectAutoAdapter(bool pingCheck = false)
    {
        var valid = NetworkInterface.GetAllNetworkInterfaces()
            .Where(IsUsableInterface)
            .OrderByDescending(GetInterfacePriority)
            .ToArray();

        // FIX(speed-1): the strict filter can come back empty on real laptops —
        // e.g. adapters whose only DNS servers are IPv6, hotspots that hand out
        // no DNS list, or driver descriptions that contain a "virtual" keyword
        // (Hyper-V bridged Wi-Fi, some Killer/Intel/USB drivers). When that
        // happened, the widget showed 0 bps forever while traffic was flowing.
        // Fall back to best-effort: any Up, non-loopback/non-tunnel interface.
        if (valid.Length == 0)
        {
            valid = NetworkInterface.GetAllNetworkInterfaces()
                .Where(n => n.OperationalStatus == OperationalStatus.Up
                            && n.NetworkInterfaceType is not NetworkInterfaceType.Loopback
                            and not NetworkInterfaceType.Tunnel
                            and not NetworkInterfaceType.Unknown)
                .OrderByDescending(GetInterfacePriority)
                .ToArray();
            if (valid.Length == 0) return null;
            if (!pingCheck) return valid[0];
            foreach (var nic in valid)
            {
                if (HasInternetConnectivity(nic)) return nic;
            }
            return valid[0];
        }

        if (!pingCheck) return valid[0];

        foreach (var nic in valid)
        {
            if (HasInternetConnectivity(nic)) return nic;
        }
        return valid[0];
    }

    private static bool IsUsableInterface(NetworkInterface nic)
    {
        try
        {
            if (nic.OperationalStatus != OperationalStatus.Up) return false;
            if (nic.NetworkInterfaceType is NetworkInterfaceType.Loopback
                or NetworkInterfaceType.Tunnel
                or NetworkInterfaceType.Unknown) return false;
            if (!nic.Supports(NetworkInterfaceComponent.IPv4)) return false;
            if (IsVirtualInterface(nic)) return false;
            var p = nic.GetIPProperties();
            if (!p.GatewayAddresses.Any(g => g.Address.AddressFamily == AddressFamily.InterNetwork
                                             && !IPAddress.IsLoopback(g.Address))) return false;
            if (!p.DnsAddresses.Any(d => d.AddressFamily == AddressFamily.InterNetwork
                                         && !IPAddress.IsLoopback(d))) return false;
            return p.UnicastAddresses.Any(a => a.Address.AddressFamily == AddressFamily.InterNetwork
                                               && !IsLoopbackAddress(a.Address));
        }
        catch
        {
            return false;
        }
    }

    private static bool IsVirtualInterface(NetworkInterface nic)
    {
        string description = nic.Description?.ToLower() ?? "";
        string name = nic.Name?.ToLower() ?? "";
        return VirtualIndicators.Any(v => description.Contains(v) || name.Contains(v));
    }

    private static bool IsLoopbackAddress(IPAddress address)
    {
        if (address is null) return true;
        byte[] b = address.GetAddressBytes();
        if (b.Length != 4) return false;
        if (b[0] == 127) return true;
        if (b[0] == 169 && b[1] == 254) return true;
        if (b[0] >= 224 && b[0] <= 239) return true;
        return false;
    }

    private static int GetInterfacePriority(NetworkInterface nic)
    {
        int score;
        switch (nic.NetworkInterfaceType)
        {
            case NetworkInterfaceType.Ethernet:
            case NetworkInterfaceType.GigabitEthernet:
            case NetworkInterfaceType.FastEthernetFx:
                score = 100;
                break;
            case NetworkInterfaceType.Wireless80211:
                score = 80;
                break;
            case NetworkInterfaceType.Ppp:
                score = 60;
                break;
            default:
                score = 40;
                break;
        }
        string name = nic.Name?.ToLower() ?? "";
        if (name == "ethernet") score += 20;
        else if (name == "wi-fi") score += 15;
        if (nic.OperationalStatus == OperationalStatus.Up) score += 10;
        if (IsVirtualInterface(nic)) score -= 50;
        return score;
    }

    private static bool HasInternetConnectivity(NetworkInterface nic)
    {
        foreach (var server in ReliableTestServers)
        {
            try
            {
                using var ping = new Ping();
                var reply = ping.Send(IPAddress.Parse(server), 1500);
                if (reply is not null && reply.Status == IPStatus.Success) return true;
            }
            catch { }
        }
        return false;
    }

    /// <summary>
    /// FIX(speed-1): read counters for every readable interface keyed by Id,
    /// WITHOUT the strict usability filter. The old filtered version could
    /// exclude the adapter the widget had just selected (manual selection
    /// matched by Name, but snapshots were re-filtered by IsUsableInterface),
    /// so the sampler never saw its own interface and the widget stuck at 0.
    /// </summary>
    public static Dictionary<string, NicSnapshot> ReadAllSnapshots()
    {
        var result = new Dictionary<string, NicSnapshot>();
        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            try
            {
                var stats = nic.GetIPv4Statistics();
                if (stats.BytesReceived >= 0 && stats.BytesSent >= 0)
                {
                    result[nic.Id] = new NicSnapshot(nic.Name, stats.BytesReceived, stats.BytesSent);
                }
            }
            catch { }
        }
        return result;
    }

    /// <summary>FIX(speed-2): resolve one live interface by Id with a single lookup.</summary>
    public static NetworkInterface? FindById(string interfaceId)
    {
        if (string.IsNullOrEmpty(interfaceId)) return null;
        try
        {
            return NetworkInterface.GetAllNetworkInterfaces()
                .FirstOrDefault(n => string.Equals(n.Id, interfaceId, StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            return null;
        }
    }
}

/// <summary>
/// Fresh-window sampler mirroring the reference engine: each measurement
/// collects sampleCount sub-reads spaced ~100ms apart over a window of
/// 0.8×refresh (clamped 300..5000ms), then returns the ±2σ outlier-trimmed
/// mean — same cadence and statistics as the original SpeedTestService.
/// </summary>
public sealed class SpeedSampler
{
    private readonly string _interfaceId;
    private readonly int _windowMs;
    private readonly int _sampleCount;
    private readonly int _delayMs;

    // FIX(speed-2): cache the live NetworkInterface instead of re-enumerating
    // every adapter in the system for every sub-sample. The old code called
    // NetworkInterface.GetAllNetworkInterfaces() 9+ times per measurement
    // (each call costs 50–300 ms on Windows). That stretched one 800 ms
    // measurement window to 1.5–3.5 s, the re-entrancy guard then skipped
    // timer ticks, and the widget updated only every few seconds with stale,
    // "wrong" looking values while burning CPU.
    private NetworkInterface? _nic;
    private int _deadReads;

    public SpeedSampler(string interfaceId, int intervalMs)
    {
        _interfaceId = interfaceId;
        _windowMs = Math.Clamp((int)(intervalMs * 0.8), 300, 5000);
        _sampleCount = Math.Clamp(_windowMs / 100, 3, 10);
        _delayMs = _windowMs / _sampleCount;
        // NOTE: the interface is resolved lazily inside MeasureAsync so that
        // constructing a sampler on the UI thread never triggers the (slow)
        // network-interface enumeration there.
    }

    public record struct Sample(double DownloadBps, double UploadBps);

    /// <summary>
    /// FIX(speed-3): true when the cached interface could not be read several
    /// times in a row (adapter removed, driver reset, sleep/resume). The
    /// widget checks this and re-selects an adapter instead of showing 0.
    /// </summary>
    public bool InterfaceDead => Volatile.Read(ref _deadReads) >= 3;

    public void Reset()
    {
        // Each measurement is fresh; nothing to clear.
    }

    public async Task<Sample> MeasureAsync()
    {
        if (_nic is null) ResolveInterface();
        if (_nic is null)
        {
            Interlocked.Exchange(ref _deadReads, int.MaxValue);
            return new Sample(0, 0);
        }

        // Chained reads: take n+1 counter snapshots spaced ~delay apart and
        // diff consecutive ones. Elapsed time is measured with a Stopwatch so
        // the rate math stays exact even if a read takes a few milliseconds.
        int n = _sampleCount;
        var rx = new long[n + 1];
        var tx = new long[n + 1];
        var times = new double[n + 1];
        var ok = new bool[n + 1];

        var clock = Stopwatch.StartNew();
        for (int i = 0; i <= n; i++)
        {
            if (i > 0) await Task.Delay(_delayMs).ConfigureAwait(false);
            if (TryReadCounters(_nic, out long rxNow, out long txNow))
            {
                rx[i] = rxNow;
                tx[i] = txNow;
                ok[i] = true;
                Interlocked.Exchange(ref _deadReads, 0);
            }
            else
            {
                // Adapter may have power-cycled — refresh the cached object once.
                int dead = Interlocked.Increment(ref _deadReads);
                if (dead == 2) ResolveInterface();
            }
            times[i] = clock.Elapsed.TotalSeconds;
        }

        var download = new List<double>(n);
        var upload = new List<double>(n);
        for (int i = 1; i <= n; i++)
        {
            if (!ok[i - 1] || !ok[i]) continue;
            double seconds = times[i] - times[i - 1];
            if (seconds <= 0) continue;
            long drx = rx[i] - rx[i - 1];
            long dtx = tx[i] - tx[i - 1];
            // Negative deltas mean the driver reset its counters — skip that pair.
            if (drx >= 0) download.Add(drx * 8 / seconds);
            if (dtx >= 0) upload.Add(dtx * 8 / seconds);
        }

        return new Sample(
            RobustAverage(download),
            RobustAverage(upload));
    }

    private void ResolveInterface()
    {
        _nic = AdapterSelector.FindById(_interfaceId);
    }

    /// <summary>
    /// FIX(speed-2): read the two counters straight from the cached interface.
    /// GetIPStatistics() is a single lightweight native call (sub-millisecond)
    /// and, like GetIPv4Statistics(), returns the interface-level cumulative
    /// in/out octets — family-agnostic, so IPv6 traffic is included too.
    /// </summary>
    private static bool TryReadCounters(NetworkInterface nic, out long rx, out long tx)
    {
        rx = 0;
        tx = 0;
        try
        {
            var stats = nic.GetIPStatistics();
            rx = stats.BytesReceived;
            tx = stats.BytesSent;
            if (rx >= 0 && tx >= 0) return true;
        }
        catch { }
        try
        {
            var stats4 = nic.GetIPv4Statistics();
            rx = stats4.BytesReceived;
            tx = stats4.BytesSent;
            if (rx >= 0 && tx >= 0) return true;
        }
        catch { }
        rx = 0;
        tx = 0;
        return false;
    }

    /// <summary>
    /// Mean with a mild 2σ trim that can never collapse a window with real
    /// traffic: if trimming leaves a zero/empty result while raw samples are
    /// positive, or would drop most of the window, fall back to the raw mean.
    /// </summary>
    private static double RobustAverage(List<double> values)
    {
        if (values.Count == 0) return 0.0;
        if (values.Count <= 2) return values.Average();
        double mean = values.Average();
        double sd = Math.Sqrt(values.Sum(v => (v - mean) * (v - mean)) / values.Count);
        if (sd <= 0) return mean;
        var kept = values.Where(v => Math.Abs(v - mean) <= 2 * sd).ToList();
        if (kept.Count == 0) return mean;
        double keptAvg = kept.Average();
        if (keptAvg <= 0 && values.Any(v => v > 0)) return mean;
        if (kept.Count < values.Count / 2) return mean;
        return keptAvg;
    }
}

/// <summary>Cumulative per-session byte counters.</summary>
public sealed class CumulativeCounter
{
    private long _rx;
    private long _tx;

    public void Add(long rxDelta, long txDelta)
    {
        _rx = Math.Max(0, _rx + rxDelta);
        _tx = Math.Max(0, _tx + txDelta);
    }

    public void Reset()
    {
        _rx = 0;
        _tx = 0;
    }

    public long Rx => _rx;
    public long Tx => _tx;
}