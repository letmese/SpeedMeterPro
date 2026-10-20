using System.Diagnostics;
using System.Net.Http;
using System.Threading.Tasks;
using SpeedMeterPro.Models;

namespace SpeedMeterPro.Services;

public sealed class SpeedTestOptions
{
    public int DurationSeconds { get; set; } = 8;
    public bool TestDownload { get; set; } = true;
    public bool TestUpload { get; set; } = true;
    public bool TestLatency { get; set; } = true;
    public SpeedTestServerInfo Server { get; set; } = SpeedTestServers.All[0];
}

public sealed class SpeedTestResult
{
    public bool Success { get; set; }
    public double DownloadMbps { get; set; }
    public double UploadMbps { get; set; }
    public double PingMs { get; set; }
    public double JitterMs { get; set; }
    public double DownloadP80 { get; set; }
    public double UploadP80 { get; set; }
    public List<double> DownloadSamples { get; set; } = new();
    public List<double> UploadSamples { get; set; } = new();
    public string Error { get; set; } = "";
    public string ServerName { get; set; } = "";
    public string ServerLocation { get; set; } = "";
}

/// <summary>
/// Parallel download/upload against Cloudflare edge servers and global regional endpoints,
/// mirroring the original app with multi-server support (Asia, Europe, US, Australia).
/// </summary>
public sealed class SpeedTestRunner
{
    private static readonly string[] PingEndpoints =
    {
        "https://speed.cloudflare.com/__down?bytes=1",
        "https://www.cloudflare.com",
        "https://1.1.1.1",
        "https://www.google.com",
        "https://www.microsoft.com",
        "https://www.bing.com",
    };

    private const int DownloadStreams = 2;
    private const int UploadStreams = 2;
    private const int UploadChunkBytes = 4 * 1024 * 1024; // 4 MB payload per POST
    private const int DownloadSampleMs = 200;
    private const int UploadBucketMs = 500;

    private readonly HttpClient _http;
    private readonly CancellationTokenSource _cts = new();

    public SpeedTestRunner()
    {
        var handler = new HttpClientHandler { AutomaticDecompression = System.Net.DecompressionMethods.None };
        _http = new HttpClient(handler);
        _http.Timeout = TimeSpan.FromSeconds(40);
    }

    public void Cancel() => _cts.Cancel();

    public async Task<SpeedTestResult> RunAsync(SpeedTestOptions options, IProgress<(string Stage, double Value)>? progress)
    {
        var result = new SpeedTestResult
        {
            ServerName = options.Server.Name,
            ServerLocation = options.Server.Region
        };

        try
        {
            if (options.TestLatency)
            {
                progress?.Report(("Latency", 0));
                await MeasurePingAsync(options, result);
            }

            if (options.TestDownload)
            {
                progress?.Report(("Download", 0));
                result.DownloadSamples = await MeasureDownloadAsync(options, progress);
                result.DownloadMbps = Top80Average(result.DownloadSamples);
                result.DownloadP80 = result.DownloadMbps;
            }

            if (options.TestUpload)
            {
                progress?.Report(("Upload", 0));
                result.UploadSamples = await MeasureUploadAsync(options, progress);
                result.UploadMbps = Top80Average(result.UploadSamples);
                result.UploadP80 = result.UploadMbps;
            }

            result.Success = true;
        }
        catch (OperationCanceledException)
        {
            result.Error = "Test cancelled";
        }
        catch (Exception ex)
        {
            result.Error = ex.Message;
        }
        return result;
    }

    // Latency: Probe target server, detect edge location if Cloudflare, take 6 samples, jitter = stddev
    private async Task MeasurePingAsync(SpeedTestOptions options, SpeedTestResult result)
    {
        string targetUrl = options.Server.PingUrl;

        // If Cloudflare auto, discover detected edge location from response headers
        if (options.Server.IsCloudflareAnycast)
        {
            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Head, "https://speed.cloudflare.com/__down?bytes=1");
                using var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, _cts.Token);
                string city = resp.Headers.TryGetValues("city", out var cv) ? cv.FirstOrDefault() ?? "" : "";
                string country = resp.Headers.TryGetValues("country", out var cnt) ? cnt.FirstOrDefault() ?? "" : "";
                string colo = resp.Headers.TryGetValues("colo", out var cl) ? cl.FirstOrDefault() ?? "" : "";
                if (!string.IsNullOrEmpty(city) || !string.IsNullOrEmpty(colo))
                {
                    result.ServerLocation = $"{city}, {country} (Edge: {colo})";
                }
            }
            catch { }
        }

        var samples = new List<double>();
        for (int i = 0; i < 6; i++)
        {
            try
            {
                var sw = Stopwatch.StartNew();
                using var req = new HttpRequestMessage(HttpMethod.Head, targetUrl);
                using var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, _cts.Token);
                sw.Stop();
                if (resp.IsSuccessStatusCode) samples.Add(sw.Elapsed.TotalMilliseconds);
            }
            catch { }
            try { await Task.Delay(50, _cts.Token); } catch { break; }
        }

        if (samples.Count > 0)
        {
            result.PingMs = Math.Round(samples.Average(), 1);
            result.JitterMs = Math.Round(StdDev(samples), 1);
        }
    }

    private async Task<List<double>> MeasureDownloadAsync(SpeedTestOptions options, IProgress<(string, double)>? progress)
    {
        try
        {
            await DownloadStreamAsync(options.Server.WarmupUrl, TimeSpan.FromSeconds(1.5), _cts.Token, null);
        }
        catch { /* non-fatal */ }

        var agg = new ByteAggregator();
        var samples = new List<double>();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token);

        var tasks = Enumerable.Range(0, DownloadStreams)
            .Select(_ => DownloadStreamAsync(options.Server.DownloadUrl, TimeSpan.FromSeconds(options.DurationSeconds), cts.Token, agg)).ToList();

        // Sample the combined byte counter every 200ms: each sample is the
        // sum of both streams in that window, so the top-80% average is the
        // real aggregate throughput (not per-stream speed).
        var sw = Stopwatch.StartNew();
        long last = 0;
        while (!cts.IsCancellationRequested && sw.ElapsedMilliseconds < options.DurationSeconds * 1000L - DownloadSampleMs)
        {
            try { await Task.Delay(DownloadSampleMs, cts.Token); } catch { break; }
            long cur = agg.Total;
            long delta = cur - last;
            last = cur;
            samples.Add(delta * 8.0 / DownloadSampleMs / 1000.0);
            double curTop = Top80Average(samples);
            progress?.Report(("Download", curTop));
        }
        cts.Cancel();
        try { await Task.WhenAll(tasks); } catch { }
        return samples;
    }

    private sealed class ByteAggregator
    {
        private long _total;
        public long Total => Interlocked.Read(ref _total);
        public void Add(int n) => Interlocked.Add(ref _total, n);
    }

    private async Task DownloadStreamAsync(string url, TimeSpan duration, CancellationToken token, ByteAggregator? agg)
    {
        var buffer = new byte[81920];
        var end = DateTime.UtcNow + duration;
        int requests = 0;
        // One continuous transfer per stream, re-opening only when the server
        // closes the stream early so a fast link keeps counting until the
        // window ends. The cap is generous (16 re-opens).
        while (!token.IsCancellationRequested && DateTime.UtcNow < end && requests < 16)
        {
            requests++;
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            using var resp = await SendWithRetryAsync(req, token);
            if ((int)resp.StatusCode == 429)
            {
                try { await Task.Delay(1000, token); } catch { return; }
                continue;
            }
            if (!resp.IsSuccessStatusCode)
            {
                try { await Task.Delay(200, token); } catch { return; }
                continue;
            }
            await using var stream = await resp.Content.ReadAsStreamAsync(token);
            while (!token.IsCancellationRequested && DateTime.UtcNow < end)
            {
                int n;
                try
                {
                    n = await stream.ReadAsync(buffer, token);
                }
                catch (IOException) { break; }
                if (n <= 0) break;
                agg?.Add(n);
            }
        }
    }

    private async Task<HttpResponseMessage> SendWithRetryAsync(HttpRequestMessage req, CancellationToken token)
    {
        try
        {
            return await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, token);
        }
        catch (HttpRequestException) when (!token.IsCancellationRequested)
        {
            try { await Task.Delay(300, token); } catch { throw; }
            return await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, token);
        }
    }

    private readonly record struct UploadEvent(double StartMs, double EndMs, long Bytes);

    private async Task<List<double>> MeasureUploadAsync(SpeedTestOptions options, IProgress<(string, double)>? progress)
    {
        // Phase-relative clock for upload events: timestamps are measured from
        // here so bucket index = (StartMs / bucketMs) stays inside the window.
        var phaseStart = Stopwatch.GetTimestamp();

        // Warm-up with a small 256KB upload first (original behavior).
        try
        {
            await UploadChunkAsync(options.Server.UploadUrl, 256 * 1024, _cts.Token, phaseStart);
        }
        catch { /* non-fatal */ }

        var events = new List<UploadEvent>();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token);
        var tasks = Enumerable.Range(0, UploadStreams)
            .Select(_ => UploadLoopAsync(options.Server.UploadUrl, options.DurationSeconds, cts.Token, events, phaseStart)).ToList();

        var reporting = Task.Run(async () =>
        {
            while (!cts.IsCancellationRequested)
            {
                try { await Task.Delay(500, cts.Token); } catch { break; }
                double v;
                lock (events) v = Top80Average(BucketAggregate(events, options.DurationSeconds, UploadBucketMs));
                progress?.Report(("Upload", v));
            }
        });

        try { await Task.WhenAll(tasks); } catch { }
        finally { cts.Cancel(); }
        return BucketAggregate(events, options.DurationSeconds, UploadBucketMs);
    }

    private async Task UploadLoopAsync(string uploadUrl, int durationSeconds, CancellationToken token, List<UploadEvent> events, long phaseStart)
    {
        var end = DateTime.UtcNow.AddSeconds(durationSeconds);
        while (!token.IsCancellationRequested && DateTime.UtcNow < end)
        {
            var ev = await UploadChunkAsync(uploadUrl, UploadChunkBytes, token, phaseStart);
            if (ev.Bytes > 0) lock (events) events.Add(ev);
        }
    }

    private async Task<UploadEvent> UploadChunkAsync(string uploadUrl, int size, CancellationToken token, long phaseStart)
    {
        var payload = BuildPayload(size);
        var start = Stopwatch.GetTimestamp();
        using var content = new ByteArrayContent(payload);
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
        using var resp = await _http.PostAsync(uploadUrl, content, token);
        if ((int)resp.StatusCode == 429)
        {
            throw new HttpRequestException("The speed test server is rate-limiting requests right now. Please wait a bit before testing again.");
        }
        if (resp.IsSuccessStatusCode)
        {
            _ = await resp.Content.ReadAsStringAsync(token);
            var end = Stopwatch.GetTimestamp();
            var startMs = (start - phaseStart) * 1000.0 / Stopwatch.Frequency;
            var endMs = (end - phaseStart) * 1000.0 / Stopwatch.Frequency;
            return new UploadEvent(startMs, endMs, payload.Length);
        }
        return default;
    }

    /// <summary>Bucket upload events into fixed windows; concurrent streams' rates add up per bucket.</summary>
    private static List<double> BucketAggregate(List<UploadEvent> events, int durationSeconds, int bucketMs)
    {
        int buckets = (int)Math.Ceiling(durationSeconds * 1000.0 / bucketMs);
        var values = new double[buckets];
        foreach (var ev in events)
        {
            double span = ev.EndMs - ev.StartMs;
            if (span <= 0) continue;
            double rate = ev.Bytes * 8.0 / span / 1000.0; // this POST's instantaneous Mbps
            int b0 = (int)(ev.StartMs / bucketMs);
            int b1 = (int)(ev.EndMs / bucketMs);
            for (int b = b0; b <= b1 && b < buckets; b++)
            {
                double oStart = Math.Max(ev.StartMs, b * bucketMs);
                double oEnd = Math.Min(ev.EndMs, (b + 1) * bucketMs);
                double overlap = Math.Max(0, oEnd - oStart);
                if (overlap <= 0) continue;
                values[b] += rate * overlap / bucketMs; // weighted contribution
            }
        }
        return values.Where(v => v > 0).ToList();
    }

    private static byte[] BuildPayload(int size)
    {
        var payload = new byte[size];
        new Random(42).NextBytes(payload);
        return payload;
    }

    /// <summary>Average of the top 80% samples — the original app's result formula.</summary>
    private static double Top80Average(List<double> samples)
    {
        if (samples.Count == 0) return 0;
        if (samples.Count <= 4) return samples.Average();
        return samples.OrderDescending().Take((int)(samples.Count * 0.8)).Average();
    }

    private static double StdDev(List<double> samples)
    {
        if (samples.Count < 2) return 0;
        double avg = samples.Average();
        return Math.Sqrt(samples.Sum(s => Math.Pow(s - avg, 2.0)) / samples.Count);
    }
}
