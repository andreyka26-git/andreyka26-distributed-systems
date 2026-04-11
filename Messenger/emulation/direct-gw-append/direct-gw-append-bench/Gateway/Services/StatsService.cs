using System.Collections.Concurrent;
using Gateway.Models;

namespace Gateway.Services;

public class StatsService
{
    private readonly ConcurrentBag<double> _latenciesMs = new();

    public void Record(double latencyMs) => _latenciesMs.Add(latencyMs);

    public void Reset() => _latenciesMs.Clear();

    public LatencyStats GetLatencyStats()
    {
        var sorted = _latenciesMs.OrderBy(x => x).ToList();
        if (sorted.Count == 0)
            return new LatencyStats(0, 0, 0, 0, 0);

        return new LatencyStats(
            OperationsPerformed: sorted.Count,
            P50Ms: Percentile(sorted, 50),
            P99Ms: Percentile(sorted, 99),
            MinMs: sorted[0],
            MaxMs: sorted[^1]
        );
    }

    private static double Percentile(List<double> sorted, double p)
    {
        double idx = p / 100.0 * (sorted.Count - 1);
        int lo = (int)idx;
        int hi = Math.Min(lo + 1, sorted.Count - 1);
        return sorted[lo] + (idx - lo) * (sorted[hi] - sorted[lo]);
    }
}
