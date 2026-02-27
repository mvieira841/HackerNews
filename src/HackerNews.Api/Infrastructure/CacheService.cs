using HackerNews.Api.Infrastructure.Interfaces;
using Microsoft.Extensions.Caching.Memory;

namespace HackerNews.Api.Infrastructure;

/// <summary>
/// A lightweight In-Memory cache implementation used as a fallback if Redis is not configured.
/// Data is stored directly in the application's RAM.
/// </summary>
public class CacheService : ICacheService
{
    private readonly IMemoryCache _memoryCache;
    private readonly Observability.IMetricsService? _metrics;

    public CacheService(IMemoryCache memoryCache, Observability.IMetricsService? metrics = null)
    {
        _memoryCache = memoryCache;
        _metrics = metrics;
    }

    public Task<T?> GetOrCreateAsync<T>(string key, Func<Task<T?>> factory, TimeSpan? absoluteExpirationRelativeToNow = null)
    {
        if (_memoryCache.TryGetValue(key, out T? existing) && existing is not null)
        {
            _metrics?.IncrementCacheHit();
            return Task.FromResult<T?>(existing);
        }

        return CreateAndCacheAsync();

        async Task<T?> CreateAndCacheAsync()
        {
            var value = await factory();
            _metrics?.IncrementCacheMiss();

            if (value is not null)
            {
                var entry = _memoryCache.CreateEntry(key);
                if (absoluteExpirationRelativeToNow.HasValue)
                {
                    entry.AbsoluteExpirationRelativeToNow = absoluteExpirationRelativeToNow;
                }
                entry.Value = value!;
                entry.Dispose(); // Commits the entry to the cache
                _metrics?.IncrementSuccessfulUpstreamCalls();
            }

            return value;
        }
    }

    public T? Get<T>(string key)
    {
        return _memoryCache.TryGetValue(key, out T? value) ? value : default;
    }

    public void Set<T>(string key, T value, TimeSpan? absoluteExpirationRelativeToNow = null)
    {
        var entry = _memoryCache.CreateEntry(key);
        if (absoluteExpirationRelativeToNow.HasValue)
        {
            entry.AbsoluteExpirationRelativeToNow = absoluteExpirationRelativeToNow;
        }
        entry.Value = value!;
        entry.Dispose();
    }

    public void Remove(string key)
    {
        _memoryCache.Remove(key);
    }
}