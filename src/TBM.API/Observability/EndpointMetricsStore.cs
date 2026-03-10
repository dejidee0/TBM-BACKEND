using System.Collections.Concurrent;

namespace TBM.API.Observability;

public interface IEndpointMetricsStore
{
    void Record(
        string domain,
        string method,
        string path,
        int statusCode,
        double latencyMs,
        DateTime startedAtUtc);

    EndpointMetricsSnapshot GetSnapshot();
    EndpointDomainMetrics? GetDomainSnapshot(string domain);
}

public sealed class InMemoryEndpointMetricsStore : IEndpointMetricsStore
{
    private readonly ConcurrentDictionary<string, DomainAccumulator> _domains =
        new(StringComparer.OrdinalIgnoreCase);

    public void Record(
        string domain,
        string method,
        string path,
        int statusCode,
        double latencyMs,
        DateTime startedAtUtc)
    {
        var key = NormalizeDomain(domain);
        var accumulator = _domains.GetOrAdd(key, _ => new DomainAccumulator(key));
        accumulator.Record(method, path, statusCode, latencyMs, startedAtUtc);
    }

    public EndpointMetricsSnapshot GetSnapshot()
    {
        var domainRows = _domains.Values
            .Select(x => x.ToSnapshot())
            .OrderBy(x => x.Domain, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new EndpointMetricsSnapshot
        {
            GeneratedAtUtc = DateTime.UtcNow,
            Domains = domainRows
        };
    }

    public EndpointDomainMetrics? GetDomainSnapshot(string domain)
    {
        var key = NormalizeDomain(domain);
        return _domains.TryGetValue(key, out var accumulator)
            ? accumulator.ToSnapshot()
            : null;
    }

    private static string NormalizeDomain(string domain)
    {
        return string.IsNullOrWhiteSpace(domain) ? "other" : domain.Trim().ToLowerInvariant();
    }

    private sealed class DomainAccumulator
    {
        private const int MaxSamples = 300;
        private readonly object _sync = new();
        private readonly Queue<double> _latencySamples = new();
        private readonly string _domain;

        private long _totalRequests;
        private long _totalErrors;
        private double _totalLatencyMs;
        private DateTime _lastSeenUtc;
        private int _lastStatusCode;
        private string _lastPath = "/";
        private string _lastMethod = "GET";

        public DomainAccumulator(string domain)
        {
            _domain = domain;
        }

        public void Record(
            string method,
            string path,
            int statusCode,
            double latencyMs,
            DateTime startedAtUtc)
        {
            lock (_sync)
            {
                _totalRequests++;
                if (statusCode >= 500 || statusCode == StatusCodes.Status429TooManyRequests)
                {
                    _totalErrors++;
                }

                _totalLatencyMs += latencyMs;
                _lastSeenUtc = startedAtUtc;
                _lastStatusCode = statusCode;
                _lastPath = path;
                _lastMethod = method;

                _latencySamples.Enqueue(latencyMs);
                while (_latencySamples.Count > MaxSamples)
                {
                    _latencySamples.Dequeue();
                }
            }
        }

        public EndpointDomainMetrics ToSnapshot()
        {
            lock (_sync)
            {
                var totalRequests = _totalRequests;
                var totalErrors = _totalErrors;
                var errorRate = totalRequests == 0
                    ? 0d
                    : Math.Round((double)totalErrors / totalRequests * 100d, 2);

                var averageLatency = totalRequests == 0
                    ? 0d
                    : Math.Round(_totalLatencyMs / totalRequests, 2);

                var p95Latency = CalculateP95(_latencySamples);

                return new EndpointDomainMetrics
                {
                    Domain = _domain,
                    TotalRequests = totalRequests,
                    TotalErrors = totalErrors,
                    ErrorRatePercent = errorRate,
                    AverageLatencyMs = averageLatency,
                    P95LatencyMs = p95Latency,
                    LastSeenUtc = _lastSeenUtc,
                    LastStatusCode = _lastStatusCode,
                    LastPath = _lastPath,
                    LastMethod = _lastMethod
                };
            }
        }

        private static double CalculateP95(IEnumerable<double> samples)
        {
            var list = samples.ToList();
            if (list.Count == 0)
            {
                return 0d;
            }

            list.Sort();
            var index = (int)Math.Ceiling(list.Count * 0.95d) - 1;
            index = Math.Clamp(index, 0, list.Count - 1);
            return Math.Round(list[index], 2);
        }
    }
}

public sealed class EndpointMetricsSnapshot
{
    public DateTime GeneratedAtUtc { get; set; }
    public List<EndpointDomainMetrics> Domains { get; set; } = new();
}

public sealed class EndpointDomainMetrics
{
    public string Domain { get; set; } = "other";
    public long TotalRequests { get; set; }
    public long TotalErrors { get; set; }
    public double ErrorRatePercent { get; set; }
    public double AverageLatencyMs { get; set; }
    public double P95LatencyMs { get; set; }
    public DateTime LastSeenUtc { get; set; }
    public int LastStatusCode { get; set; }
    public string LastPath { get; set; } = "/";
    public string LastMethod { get; set; } = "GET";
}
