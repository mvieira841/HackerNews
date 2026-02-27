using HackerNews.Api.Infrastructure.Interfaces;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace HackerNews.Api.Infrastructure;

/// <summary>
/// Implementation of ICacheService that uses Redis (IDistributedCache) for multi-server, distributed caching.
/// Handles object serialization to and from JSON, with graceful degradation if Redis goes offline mid-operation.
/// </summary>
public class RedisCacheService : ICacheService
{
    private readonly IDistributedCache _distributedCache;
    private readonly ILogger<RedisCacheService> _logger;
    private readonly Observability.IMetricsService? _metrics;

    public RedisCacheService(
        IDistributedCache distributedCache,
        ILogger<RedisCacheService> logger,
        Observability.IMetricsService? metrics = null)
    {
        _distributedCache = distributedCache;
        _logger = logger;
        _metrics = metrics;
    }

    public async Task<T?> GetOrCreateAsync<T>(string key, Func<Task<T?>> factory, TimeSpan? absoluteExpirationRelativeToNow = null)
    {
        byte[]? data = null;

        // 1. Attempt to read from Redis safely
        try
        {
            data = await _distributedCache.GetAsync(key);
        }
        catch (Exception ex)
        {
            // RESILIENCE: Redis is down or unreachable. Log and proceed to factory.
            _logger.LogWarning(ex, "Redis cache read failed for key {Key}. Bypassing cache.", key);
        }

        if (data != null && data.Length > 0)
        {
            try
            {
                var str = System.Text.Encoding.UTF8.GetString(data);
                var obj = JsonSerializer.Deserialize<T>(str);
                if (obj is not null)
                {
                    _metrics?.IncrementCacheHit();
                    return obj;
                }
            }
            catch
            {
                // EDGE CASE: Corrupted JSON
            }
        }

        // 2. Cache Miss or Cache Failure - Execute the factory delegate to get fresh data
        var value = await factory();
        _metrics?.IncrementCacheMiss();

        // 3. Attempt to write to Redis safely
        if (value is not null)
        {
            try
            {
                var json = JsonSerializer.Serialize(value);
                var bytes = System.Text.Encoding.UTF8.GetBytes(json);
                var options = new DistributedCacheEntryOptions();

                if (absoluteExpirationRelativeToNow.HasValue)
                {
                    options.SetAbsoluteExpiration(absoluteExpirationRelativeToNow.Value);
                }

                await _distributedCache.SetAsync(key, bytes, options);
                _metrics?.IncrementSuccessfulUpstreamCalls();
            }
            catch (Exception ex)
            {
                // RESILIENCE: Redis died right before write. Log and return data anyway.
                _logger.LogWarning(ex, "Redis cache write failed for key {Key}. Proceeding without caching.", key);
            }
        }

        return value;
    }

    public T? Get<T>(string key)
    {
        try
        {
            var data = _distributedCache.Get(key);
            if (data != null && data.Length > 0)
            {
                var str = System.Text.Encoding.UTF8.GetString(data);
                var obj = JsonSerializer.Deserialize<T>(str);
                if (obj is not null)
                {
                    _metrics?.IncrementCacheHit();
                    return obj;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis cache Get failed for key {Key}. Treating as Cache Miss.", key);
        }

        _metrics?.IncrementCacheMiss();
        return default;
    }

    public void Set<T>(string key, T value, TimeSpan? absoluteExpirationRelativeToNow = null)
    {
        try
        {
            var json = JsonSerializer.Serialize(value);
            var bytes = System.Text.Encoding.UTF8.GetBytes(json);
            var options = new DistributedCacheEntryOptions();
            if (absoluteExpirationRelativeToNow.HasValue)
            {
                options.SetAbsoluteExpiration(absoluteExpirationRelativeToNow.Value);
            }
            _distributedCache.Set(key, bytes, options);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis cache Set failed for key {Key}.", key);
        }
    }

    public void Remove(string key)
    {
        try
        {
            _distributedCache.Remove(key);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis cache Remove failed for key {Key}.", key);
        }
    }
}