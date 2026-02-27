using System.Text.Json.Serialization;

namespace HackerNews.Api.Application.Features.GetBestStories.Contracts;

/// <summary>
/// Represents the raw JSON payload returned by the Hacker News Firebase API.
/// </summary>
public record HnStoryDto(
    int Id,
    string? Title,
    string? Url,
    string? By,
    long Time,
    int Score,
    [property: JsonPropertyName("descendants")] int CommentCount);