using FluentAssertions;
using HackerNews.Api.Application.Features.GetBestStories;
using HackerNews.Api.Application.Features.GetBestStories.Contracts;
using HackerNews.Api.Configuration;
using HackerNews.Api.Infrastructure;
using HackerNews.Api.Infrastructure.Interfaces;
using HackerNews.Api.Observability;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using System.Threading.RateLimiting;
using Xunit;

namespace HackerNewsApi.Tests.Api.Unit;

/// <summary>
/// Deep unit testing of the MediatR-style request handler. Ensures logic like partial failures, 
/// short-circuit caching, and sorting are executed accurately without HTTP or JSON overhead.
/// </summary>
public class GetBestStoriesHandlerTests
{
    private static IMemoryCache CreateMemoryCache() => new MemoryCache(new MemoryCacheOptions());

    private static HackerNewsSettings BaseSettings() =>
        new() { BaseUrl = "https://hacker-news.firebaseio.com/v0/", BestStoriesCacheMinutes = 5, StoryDetailsCacheMinutes = 15, MaxDegreeOfParallelism = 4, RequestsPerSecond = 100 };

    [Fact]
    public async Task HandleAsync_InvalidN_ReturnsFailure()
    {
        var hnClient = Substitute.For<IHackerNewsApiClient>();
        var cache = new CacheService(CreateMemoryCache());
        var settings = Options.Create(BaseSettings());
        var rateLimiter = new RequestRateLimiter(NullLogger<RequestRateLimiter>.Instance, settings);

        var handler = new GetBestStoriesHandler(hnClient, cache, settings, rateLimiter, NullLogger<GetBestStoriesHandler>.Instance);

        var result = await handler.HandleAsync(0, CancellationToken.None);

        result.IsFailed.Should().BeTrue();
    }

    [Fact]
    public async Task HandleAsync_UsesCacheFastPath_DoesNotCallRateLimiter()
    {
        var hnClient = Substitute.For<IHackerNewsApiClient>();
        hnClient.GetBestStoryIdsAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult<int[]?>(new[] { 101 }));
        hnClient.GetStoryAsync(101, Arg.Any<CancellationToken>()).Returns(Task.FromResult<HnStoryDto?>(new HnStoryDto(101, "TitleA", "http://a", "alice", DateTimeOffset.UtcNow.ToUnixTimeSeconds(), 5, 2)));

        var cache = new CacheService(CreateMemoryCache());
        var cached = new HnStoryDto(101, "TitleCached", "http://cached", "cache", DateTimeOffset.UtcNow.ToUnixTimeSeconds(), 99, 0);

        // Pre-seed cache so handler takes the fast path
        cache.Set($"{Constants.StoryCacheKeyPrefix}101", cached, TimeSpan.FromMinutes(5));

        var settings = Options.Create(BaseSettings());
        var rateLimiter = Substitute.For<IRequestRateLimiter>();

        // If AcquireAsync is called, throw an exception. Fast path should bypass the rate limiter completely.
        rateLimiter.AcquireAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns<ValueTask<RateLimitLease>>(_ => throw new InvalidOperationException("Acquire denied"));

        var handler = new GetBestStoriesHandler(hnClient, cache, settings, rateLimiter, NullLogger<GetBestStoriesHandler>.Instance);

        var result = await handler.HandleAsync(1, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.First().Title.Should().Be("TitleCached");
        await rateLimiter.DidNotReceiveWithAnyArgs().AcquireAsync(default, default);
    }

    [Fact]
    public async Task HandleAsync_WhenOneStoryFails_ReturnsRemainingSuccessfulStories()
    {
        var hnClient = Substitute.For<IHackerNewsApiClient>();
        hnClient.GetBestStoryIdsAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult<int[]?>(new[] { 1, 2, 3 }));

        // IDs 1 and 3 succeed, ID 2 throws a severe exception
        hnClient.GetStoryAsync(1, Arg.Any<CancellationToken>()).Returns(Task.FromResult<HnStoryDto?>(new HnStoryDto(1, "Title1", "", "", 0, 10, 0)));
        hnClient.GetStoryAsync(2, Arg.Any<CancellationToken>()).Throws(new Exception("Network failure"));
        hnClient.GetStoryAsync(3, Arg.Any<CancellationToken>()).Returns(Task.FromResult<HnStoryDto?>(new HnStoryDto(3, "Title3", "", "", 0, 20, 0)));

        var cache = new CacheService(CreateMemoryCache());
        var settings = Options.Create(BaseSettings());
        var rateLimiter = new RequestRateLimiter(NullLogger<RequestRateLimiter>.Instance, settings);
        var handler = new GetBestStoriesHandler(hnClient, cache, settings, rateLimiter, NullLogger<GetBestStoriesHandler>.Instance);

        var result = await handler.HandleAsync(3, CancellationToken.None);

        // Assert: The handler caught the exception for ID 2, allowed 1 & 3 to proceed, and mapped them correctly.
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        result.Value.Select(x => x.Title).Should().ContainInOrder("Title3", "Title1"); // Sorted by score descending (20 > 10)
    }

    [Fact]
    public async Task HandleAsync_WhenUpstreamFails_ReturnsStaleCacheData()
    {
        var cache = new CacheService(CreateMemoryCache());
        // Pre-seed stale cache with an old ID
        cache.Set(Constants.BestStoriesCacheKey, new[] { 999 });

        var hnClient = Substitute.For<IHackerNewsApiClient>();

        // Force the API to completely fail when asking for IDs
        hnClient.GetBestStoryIdsAsync(Arg.Any<CancellationToken>()).Throws(new Exception("API Down"));
        hnClient.GetStoryAsync(999, Arg.Any<CancellationToken>()).Returns(Task.FromResult<HnStoryDto?>(new HnStoryDto(999, "Stale Story", "", "", 0, 100, 0)));

        var settings = Options.Create(BaseSettings());
        var rateLimiter = new RequestRateLimiter(NullLogger<RequestRateLimiter>.Instance, settings);
        var handler = new GetBestStoriesHandler(hnClient, cache, settings, rateLimiter, NullLogger<GetBestStoriesHandler>.Instance);

        var result = await handler.HandleAsync(1, CancellationToken.None);

        // Assert: The application caught the exception, pulled the stale [999] from cache, and succeeded
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
        result.Value.First().Title.Should().Be("Stale Story");
    }

    [Fact]
    public async Task HandleAsync_RecordsAccurateMetrics()
    {
        var hnClient = Substitute.For<IHackerNewsApiClient>();
        hnClient.GetBestStoryIdsAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult<int[]?>(new[] { 1, 2 }));

        hnClient.GetStoryAsync(1, Arg.Any<CancellationToken>()).Returns(Task.FromResult<HnStoryDto?>(new HnStoryDto(1, "A", "", "", 0, 10, 0)));
        hnClient.GetStoryAsync(2, Arg.Any<CancellationToken>()).Throws(new Exception("Fail"));

        var cache = new CacheService(CreateMemoryCache());
        var settings = Options.Create(BaseSettings());
        var rateLimiter = new RequestRateLimiter(NullLogger<RequestRateLimiter>.Instance, settings);
        var metrics = Substitute.For<IMetricsService>();

        var handler = new GetBestStoriesHandler(hnClient, cache, settings, rateLimiter, NullLogger<GetBestStoriesHandler>.Instance, metrics);

        await handler.HandleAsync(2, CancellationToken.None);

        // Verify custom metrics are properly tracking successes and failures
        metrics.Received(1).IncrementTotalRequests();
        metrics.Received(1).IncrementFailedRequests();
    }

    [Fact]
    public async Task HandleAsync_WhenRequestedN_ExceedsAvailableIds_ReturnsAvailableStories()
    {
        // Arrange: User will request 5 stories, but HackerNews only has 2 best stories currently
        var hnClient = Substitute.For<IHackerNewsApiClient>();
        hnClient.GetBestStoryIdsAsync(Arg.Any<CancellationToken>())
                .Returns(Task.FromResult<int[]?>(new[] { 10, 20 }));

        hnClient.GetStoryAsync(10, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<HnStoryDto?>(new HnStoryDto(10, "Story 10", "", "", 0, 100, 0)));
        hnClient.GetStoryAsync(20, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<HnStoryDto?>(new HnStoryDto(20, "Story 20", "", "", 0, 90, 0)));

        var cache = new CacheService(CreateMemoryCache());
        var settings = Options.Create(BaseSettings());
        var rateLimiter = new RequestRateLimiter(NullLogger<RequestRateLimiter>.Instance, settings);

        var handler = new GetBestStoriesHandler(hnClient, cache, settings, rateLimiter, NullLogger<GetBestStoriesHandler>.Instance);

        // Act: Request n = 5
        var result = await handler.HandleAsync(5, CancellationToken.None);

        // Assert: Should succeed and return only the 2 available stories without throwing an IndexOutOfRangeException
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        result.Value.Select(x => x.Title).Should().ContainInOrder("Story 10", "Story 20");
    }
}