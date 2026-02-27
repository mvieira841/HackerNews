using System.Threading;

namespace HackerNews.Api.Observability;

public class MetricsService : IMetricsService
{
    private long _total;
    private long _hits;
    private long _misses;
    private long _fails;
    private long _successes;

    public void IncrementTotalRequests() => Interlocked.Increment(ref _total);
    public void IncrementCacheHit() => Interlocked.Increment(ref _hits);
    public void IncrementCacheMiss() => Interlocked.Increment(ref _misses);
    public void IncrementFailedRequests() => Interlocked.Increment(ref _fails);
    public void IncrementSuccessfulUpstreamCalls() => Interlocked.Increment(ref _successes);

    public long TotalRequests => Interlocked.Read(ref _total);
    public long CacheHits => Interlocked.Read(ref _hits);
    public long CacheMisses => Interlocked.Read(ref _misses);
    public long FailedRequests => Interlocked.Read(ref _fails);
    public long SuccessfulUpstreamCalls => Interlocked.Read(ref _successes);
}
