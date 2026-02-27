namespace HackerNews.Api.Application.Features.GetBestStories.Contracts;

/// <summary>
/// Represents a high-ranking story retrieved from Hacker News.
/// </summary>
/// <param name="Title">The title of the Hacker News story.</param>
/// <param name="Uri">The URL bridging to the original article or discussion.</param>
/// <param name="PostedBy">The username of the individual who posted the story.</param>
/// <param name="Time">The time the story was posted, formatted in ISO 8601 (yyyy-MM-ddTHH:mm:sszzz).</param>
/// <param name="Score">The current score (upvotes) the story has received.</param>
/// <param name="CommentCount">The total number of descendants/comments on the story.</param>
public record StoryResponse(
    string? Title,
    string? Uri,
    string? PostedBy,
    string Time,
    int Score,
    int CommentCount);