using System;

namespace HackerNews.Api.Configuration;

/// <summary>
/// Strongly-typed configuration class mapped to the "HackerNewsSettings" section in appsettings.json.
/// </summary>
public class HackerNewsSettings
{
    public const string SectionName = "HackerNewsSettings";

    /// <summary>The base URL for the external Hacker News API.</summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>How long to cache the array of the 'best' story IDs.</summary>
    public int BestStoriesCacheMinutes { get; set; } = 5;

    /// <summary>How long to cache the individual details of a specific story.</summary>
    public int StoryDetailsCacheMinutes { get; set; } = 15;

    /// <summary>
    /// Controls how many external HTTP requests to Hacker News can run simultaneously.
    /// Defaults to double the logical processor count to maximize network IO without thrashing CPU.
    /// </summary>
    public int MaxDegreeOfParallelism { get; set; } = Math.Max(1, Environment.ProcessorCount * 2);

    /// <summary>
    /// Global token bucket rate limit specifying the maximum steady-state requests per second allowed 
    /// to hit the external HackerNews API.
    /// </summary>
    public int RequestsPerSecond { get; set; } = 20;

    /// <summary>
    /// Redis connection string (e.g., 'localhost:6379'). If left blank, the app gracefully degrades to an In-Memory cache.
    /// </summary>
    public string RedisConnectionString { get; set; } = string.Empty;
}