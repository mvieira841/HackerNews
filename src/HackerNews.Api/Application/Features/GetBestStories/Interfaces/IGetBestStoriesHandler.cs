using FluentResults;
using HackerNews.Api.Application.Features.GetBestStories.Contracts;

namespace HackerNews.Api.Application.Features.GetBestStories.Interfaces;

/// <summary>
/// Interface for the MediatR-style handler that processes the Best Stories request.
/// </summary>
public interface IGetBestStoriesHandler
{
    Task<Result<IEnumerable<StoryResponse>>> HandleAsync(int n, CancellationToken cancellationToken);
}