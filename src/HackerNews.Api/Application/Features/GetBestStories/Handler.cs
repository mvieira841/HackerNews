using FluentResults;
using HackerNews.Api.Application.Features.GetBestStories.Contracts;
using HackerNews.Api.Application.Features.GetBestStories.Interfaces;
using HackerNews.Api.Configuration;
using HackerNews.Api.Infrastructure.Interfaces;
using Microsoft.Extensions.Options;

namespace HackerNews.Api.Application.Features.GetBestStories;

/// <summary>
/// Core application logic orchestrator for fetching best stories. 
/// Handles caching strategies, concurrency, rate limiting, and partial failures.
/// </summary>
public class GetBestStoriesHandler(
    IHackerNewsApiClient hnClient,
    ICacheService cache,
    IOptions<HackerNewsSettings> options,
    IRequestRateLimiter rateLimiter,
    ILogger<GetBestStoriesHandler> logger,
    Observability.IMetricsService? metrics = null) : IGetBestStoriesHandler
{
    private readonly HackerNewsSettings _settings = options.Value;
    private readonly Observability.IMetricsService? _metrics = metrics;

    public async Task<Result<IEnumerable<StoryResponse>>> HandleAsync(int n, CancellationToken cancellationToken)
    {
        _metrics?.IncrementTotalRequests();

        // 1. Validate Input
        if (n <= 0)
        {
            return Result.Fail(Constants.InvalidNParameterMessage);
        }

        // 2. Fetch Master List of Best Story IDs (Wrapped in Cache)
        var bestStoryIds = await cache.GetOrCreateAsync<int[]>(Constants.BestStoriesCacheKey, async () =>
        {
            try
            {
                return await hnClient.GetBestStoryIdsAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                // RESILIENCE FALLBACK:
                // If the upstream HackerNews API is fully down, we catch the exception and 
                // attempt to return STALE data from the cache so our API stays operational.
                logger.LogWarning(ex, "Failed to fetch best story IDs, serving stale cache if available.");
                return cache.Get<int[]>(Constants.BestStoriesCacheKey);
            }
        }, TimeSpan.FromMinutes(_settings.BestStoriesCacheMinutes));

        if (bestStoryIds is null || bestStoryIds.Length == 0)
        {
            return Result.Ok(Enumerable.Empty<StoryResponse>());
        }

        // We only want to process up to 'n' stories.
        var idsToFetch = bestStoryIds.Take(Math.Min(n, bestStoryIds.Length)).ToArray();

        // Prepare a thread-safe array to hold the results of our parallel processing
        var fetchedStories = new HnStoryDto?[idsToFetch.Length];

        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = Math.Max(1, _settings.MaxDegreeOfParallelism),
            CancellationToken = cancellationToken
        };

        // 3. Fetch Individual Story Details Concurrently
        await Parallel.ForEachAsync(Enumerable.Range(0, idsToFetch.Length), parallelOptions, async (i, ct) =>
        {
            var id = idsToFetch[i];

            try
            {
                var cacheKey = $"{Constants.StoryCacheKeyPrefix}{id}";

                // FAST PATH: Check the cache first. 
                // If found, we skip the rate limiter entirely to maximize performance.
                var cached = cache.Get<HnStoryDto>(cacheKey);
                if (cached is not null)
                {
                    fetchedStories[i] = cached;
                    return;
                }

                // SLOW PATH: We must call the external API.
                // Acquire a lease from the token bucket to prevent overwhelming the upstream service.
                using var lease = await rateLimiter.AcquireAsync(1, ct);
                if (!lease.IsAcquired)
                {
                    logger.LogWarning("Rate limiter denied token for story id {Id}", id);
                    return; // Skip fetching; it will remain null in the array
                }

                // Fetch data and save it into the cache for future requests
                var story = await cache.GetOrCreateAsync<HnStoryDto?>(cacheKey, async () =>
                {
                    return await hnClient.GetStoryAsync(id, ct);
                }, TimeSpan.FromMinutes(_settings.StoryDetailsCacheMinutes));

                fetchedStories[i] = story;
            }
            catch (OperationCanceledException)
            {
                throw; // Do not swallow cancellation exceptions
            }
            catch (Exception ex)
            {
                // PARTIAL FAILURE RESILIENCE:
                // If one specific story fails to load (e.g. 404 or transient timeout), 
                // we log the error but allow the Parallel loop to continue processing the other stories.
                logger.LogWarning(ex, "Failed to fetch story details for id {Id}", id);
                _metrics?.IncrementFailedRequests();
            }
        });

        // 4. Transform and Return
        // Filter out any nulls (failed requests / rate limited), map to the required output format, and sort.
        var stories = fetchedStories
            .Where(s => s is not null)
            .Select(s => new StoryResponse(
                Title: s!.Title,
                Uri: s.Url,
                PostedBy: s.By,
                Time: DateTimeOffset.FromUnixTimeSeconds(s.Time).ToString(Constants.Iso8601DateFormat),
                Score: s.Score,
                CommentCount: s.CommentCount
            ))
            .OrderByDescending(s => s.Score) // Ensure descending score order as required by spec
            .ToList();

        return Result.Ok<IEnumerable<StoryResponse>>(stories);
    }
}