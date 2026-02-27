using FluentAssertions;
using HackerNews.Api.Application.Features.GetBestStories.Contracts;
using HackerNews.Api.Infrastructure;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using System.Text;
using Xunit;

namespace HackerNews.Tests.Api.Unit;

public class RedisCacheServiceTests
{
    [Fact]
    public async Task GetOrCreateAsync_WhenRedisHasCorruptedJson_IgnoresAndProceedsToFactory()
    {
        // Arrange: Mock the interface to return a corrupted JSON byte array directly from Redis
        var mockCache = Substitute.For<IDistributedCache>();
        mockCache.GetAsync("bad_key", Arg.Any<CancellationToken>())
            .Returns(Encoding.UTF8.GetBytes("{ this_is_invalid_json: "));

        // FIX: Inject NullLogger
        var cacheService = new RedisCacheService(mockCache, NullLogger<RedisCacheService>.Instance);

        // Act: Because the JSON is bad, it should silently swallow the deserialization exception and run the factory logic.
        var result = await cacheService.GetOrCreateAsync("bad_key", () => Task.FromResult<string?>("fallback_value"));

        // Assert
        result.Should().Be("fallback_value");
    }

    [Fact]
    public void SetAndGet_SuccessfullySerializesAndDeserializes()
    {
        // Arrange: We use MemoryDistributedCache (which behaves exactly like Redis cache in memory via bytes) 
        // to easily test the Set/Get JSON serialization logic
        var opts = Options.Create(new MemoryDistributedCacheOptions());
        var memoryCache = new MemoryDistributedCache(opts);

        // FIX: Inject NullLogger
        var cacheService = new RedisCacheService(memoryCache, NullLogger<RedisCacheService>.Instance);

        var testObj = new HnStoryDto(1, "Test Serialization", "http://test", "tester", 123456789, 100, 5);

        // Act: Pushing an object to distributed cache automatically triggers our JsonSerializer code
        cacheService.Set("test_key", testObj);
        var retrieved = cacheService.Get<HnStoryDto>("test_key");

        // Assert
        retrieved.Should().NotBeNull();
        retrieved!.Title.Should().Be("Test Serialization");
        retrieved.Score.Should().Be(100);
        retrieved.CommentCount.Should().Be(5);
    }
}