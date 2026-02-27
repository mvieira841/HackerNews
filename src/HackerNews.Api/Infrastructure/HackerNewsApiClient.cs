using HackerNews.Api.Application.Features.GetBestStories;
using HackerNews.Api.Application.Features.GetBestStories.Contracts;
using HackerNews.Api.Infrastructure.Interfaces;
using Polly;
using Polly.Retry;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace HackerNews.Api.Infrastructure;

/// <summary>
/// Source generator context for JSON deserialization. By using Source Generators, 
/// we avoid reflection overhead at runtime, resulting in faster parsing and lower memory usage.
/// </summary>
[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
[JsonSerializable(typeof(int[]))]
[JsonSerializable(typeof(HnStoryDto))]
internal partial class HackerNewsJsonContext : JsonSerializerContext
{
}

/// <summary>
/// Typed HTTP Client for communicating specifically with the HackerNews Firebase API.
/// </summary>
public class HackerNewsApiClient(HttpClient httpClient, ILogger<HackerNewsApiClient> logger) : IHackerNewsApiClient
{
    /// <summary>
    /// A local Polly retry policy used for specifically handling JSON deserialization 
    /// or localized HTTP errors gracefully.
    /// </summary>
    private AsyncRetryPolicy<T?> CreateRetryPolicy<T>() where T : class?
    {
        return Policy<T?>
            .Handle<HttpRequestException>()
            .Or<TaskCanceledException>()
            .Or<JsonException>() // In case the API returns partial/corrupted JSON temporarily
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: attempt => TimeSpan.FromMilliseconds(200 * Math.Pow(2, attempt)),
                onRetry: (outcome, timespan, attempt, context) =>
                {
                    context.TryGetValue("requestUri", out var requestUri);
                    logger.LogWarning(
                        outcome.Exception,
                        "Retry {Attempt} for {RequestUri} after {Delay}ms",
                        attempt,
                        requestUri ?? "(unknown)",
                        timespan.TotalMilliseconds);
                });
    }

    public Task<int[]?> GetBestStoryIdsAsync(CancellationToken cancellationToken = default)
    {
        var policy = CreateRetryPolicy<int[]?>();
        var context = new Context { ["requestUri"] = Constants.BestStoriesPath };

        // Execute request utilizing Source Generators for ultra-fast deserialization
        return policy.ExecuteAsync((ctx, ct) =>
            httpClient.GetFromJsonAsync(Constants.BestStoriesPath, HackerNewsJsonContext.Default.Int32Array, ct),
            context,
            cancellationToken);
    }

    public Task<HnStoryDto?> GetStoryAsync(int id, CancellationToken cancellationToken = default)
    {
        var apiPath = string.Format(Constants.StoryItemPathFormat, id);
        var policy = CreateRetryPolicy<HnStoryDto?>();
        var context = new Context { ["requestUri"] = apiPath };

        return policy.ExecuteAsync((ctx, ct) =>
            httpClient.GetFromJsonAsync(apiPath, HackerNewsJsonContext.Default.HnStoryDto, ct),
            context,
            cancellationToken);
    }
}