using System.Collections.Concurrent;

namespace Gateway.Services;

public class StatsService
{
    private readonly ConcurrentBag<double> _latenciesMs = new();

    public void Record(double latencyMs) => _latenciesMs.Add(latencyMs);

    public object GetStats()
    {
        var sorted = _latenciesMs.OrderBy(x => x).ToList();
        if (sorted.Count == 0)
            return new { count = 0, p50 = 0.0, p99 = 0.0, minMs = 0.0, maxMs = 0.0 };

        return new
        {
            count  = sorted.Count,
            p50    = Percentile(sorted, 50),
            p99    = Percentile(sorted, 99),
            minMs  = sorted[0],
            maxMs  = sorted[^1]
        };
    }

    private static double Percentile(List<double> sorted, double p)
    {
        double idx = p / 100.0 * (sorted.Count - 1);
        int lo = (int)idx;
        int hi = Math.Min(lo + 1, sorted.Count - 1);
        return sorted[lo] + (idx - lo) * (sorted[hi] - sorted[lo]);
    }
}
