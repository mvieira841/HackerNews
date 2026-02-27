namespace HackerNews.Api.Application.Features.GetBestStories;

/// <summary>
/// Centralized string constants for the GetBestStories feature to avoid "magic strings" in code.
/// </summary>
public static class Constants
{
    public const string EndpointRoute = "/best-stories";
    public const string EndpointName = "GetBestStories";
    public const string BestStoriesCacheKey = "beststories_ids";
    public const string StoryCacheKeyPrefix = "story_";
    public const string BestStoriesPath = "beststories.json";
    public const string StoryItemPathFormat = "item/{0}.json";

    // Formatting standard required by the API specification
    public const string Iso8601DateFormat = "yyyy-MM-ddTHH:mm:sszzz";

    public const string InvalidNParameterMessage = "The parameter 'n' must be greater than 0.";
}