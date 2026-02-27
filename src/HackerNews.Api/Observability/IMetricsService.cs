namespace HackerNews.Api.Observability;

public interface IMetricsService
{
    void IncrementTotalRequests();
    void IncrementCacheHit();
    void IncrementCacheMiss();
    void IncrementFailedRequests();
    void IncrementSuccessfulUpstreamCalls();

    long TotalRequests { get; }
    long CacheHits { get; }
    long CacheMisses { get; }
    long FailedRequests { get; }
    long SuccessfulUpstreamCalls { get; }
}
