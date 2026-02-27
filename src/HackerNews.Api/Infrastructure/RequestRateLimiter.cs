using HackerNews.Api.Configuration;
using HackerNews.Api.Infrastructure.Interfaces;
using Microsoft.Extensions.Options;
using System.Threading.RateLimiting;

namespace HackerNews.Api.Infrastructure;

/// <summary>
/// Global rate limiter to ensure our application does not abuse and get blocked by the external HackerNews API.
/// Uses a Token Bucket algorithm to enforce a steady request rate while allowing tiny bursts of traffic.
/// </summary>
public class RequestRateLimiter : IRequestRateLimiter
{
    private readonly TokenBucketRateLimiter _rateLimiter;

    public RequestRateLimiter(ILogger<RequestRateLimiter> logger, IOptions<HackerNewsSettings> options)
    {
        var settings = options.Value;

        // Configuration of the Token Bucket
        _rateLimiter = new TokenBucketRateLimiter(new TokenBucketRateLimiterOptions
        {
            // TokenLimit: Maximum tokens the bucket can hold. Set to 2x the per-second rate to allow up to 2 seconds of "burst" traffic.
            TokenLimit = Math.Max(1, settings.RequestsPerSecond * 2),

            // TokensPerPeriod: The steady-state replenishment rate.
            TokensPerPeriod = Math.Max(1, settings.RequestsPerSecond),

            // Replenish the bucket every 1 second.
            ReplenishmentPeriod = TimeSpan.FromSeconds(1),

            // If the bucket is empty, queue up requests (oldest first).
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,

            // Allow a queue large enough to hold 20 seconds worth of traffic before rejecting requests outright.
            QueueLimit = Math.Max(0, settings.RequestsPerSecond * 20),

            AutoReplenishment = true
        });

        logger.LogDebug("RequestRateLimiter initialized: {TokensPerSecond}", settings.RequestsPerSecond);
    }

    public ValueTask<RateLimitLease> AcquireAsync(int permitCount = 1, CancellationToken cancellationToken = default) =>
        _rateLimiter.AcquireAsync(permitCount, cancellationToken);
}