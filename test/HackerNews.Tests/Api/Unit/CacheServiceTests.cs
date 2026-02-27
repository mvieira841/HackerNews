using FluentAssertions;
using HackerNews.Api.Infrastructure;
using Microsoft.Extensions.Caching.Memory;
using Xunit;

namespace HackerNewsApi.Tests.Api.Unit;

/// <summary>
/// Unit tests for our lightweight local In-Memory caching fallback implementation.
/// </summary>
public class CacheServiceTests
{
    [Fact]
    public async Task GetSetRemove_WorksAsExpected()
    {
        var memory = new MemoryCache(new MemoryCacheOptions());
        var cache = new CacheService(memory);

        var key = "test_key";

        // initially null
        cache.Get<string>(key).Should().BeNull();

        // Set and get
        cache.Set(key, "value", TimeSpan.FromMinutes(5));
        cache.Get<string>(key).Should().Be("value");

        // Remove
        cache.Remove(key);
        cache.Get<string>(key).Should().BeNull();

        // GetOrCreateAsync factory path evaluation
        var factoryInvoked = false;
        var value = await cache.GetOrCreateAsync<string>(key, async () =>
        {
            factoryInvoked = true;
            return await Task.FromResult<string?>("created");
        }, TimeSpan.FromMinutes(1));

        factoryInvoked.Should().BeTrue();
        value.Should().Be("created");
    }

    [Fact]
    public async Task Set_WithExpiration_RemovesAfterDelay()
    {
        var memory = new MemoryCache(new MemoryCacheOptions());
        var cache = new CacheService(memory);

        var key = "temp_key";
        // Configure an immediate timeout
        cache.Set(key, "v", TimeSpan.FromMilliseconds(100));
        cache.Get<string>(key).Should().Be("v");

        // Wait for the memory cache object expiration sweep to naturally occur
        await Task.Delay(250);
        cache.Get<string>(key).Should().BeNull();
    }
}