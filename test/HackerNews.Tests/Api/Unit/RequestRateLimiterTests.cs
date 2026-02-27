using FluentAssertions;
using HackerNews.Api.Configuration;
using HackerNews.Api.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace HackerNewsApi.Tests.Api.Unit;

public class RequestRateLimiterTests
{
    [Fact]
    public async Task AcquireAsync_AllowsImmediateToken()
    {
        var settings = Options.Create(new HackerNewsSettings { RequestsPerSecond = 1 });
        var limiter = new RequestRateLimiter(NullLogger<RequestRateLimiter>.Instance, settings);

        // Given a limit of 1 per second, the first immediate request must be granted
        var lease = await limiter.AcquireAsync(1);
        lease.IsAcquired.Should().BeTrue();
    }

    [Fact]
    public async Task AcquireAsync_Denies_WhenRequestExceedsCapacity()
    {
        var settings = Options.Create(new HackerNewsSettings { RequestsPerSecond = 1 });
        var limiter = new RequestRateLimiter(NullLogger<RequestRateLimiter>.Instance, settings);

        // Request a very large number of tokens (greater than maximum TokenLimit bucket size).
        // This causes the underlying TokenBucketRateLimiter to throw immediately.
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => limiter.AcquireAsync(100).AsTask());
    }

    [Fact]
    public async Task AcquireAsync_Allows_BurstUpToTokenLimit()
    {
        var settings = Options.Create(new HackerNewsSettings { RequestsPerSecond = 2 });
        var limiter = new RequestRateLimiter(NullLogger<RequestRateLimiter>.Instance, settings);

        // With RequestsPerSecond = 2, we configured the TokenLimit to be double (4). 
        // Acquiring a burst of up to 4 should succeed.
        var lease = await limiter.AcquireAsync(4);
        lease.IsAcquired.Should().BeTrue();

        // Acquiring more than the burst capacity should throw ArgumentOutOfRangeException
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => limiter.AcquireAsync(5).AsTask());
    }
}