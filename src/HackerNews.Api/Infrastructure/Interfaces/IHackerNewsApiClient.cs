using HackerNews.Api.Application.Features.GetBestStories.Contracts;

namespace HackerNews.Api.Infrastructure.Interfaces;

public interface IHackerNewsApiClient
{
    Task<int[]?> GetBestStoryIdsAsync(CancellationToken cancellationToken = default);
    Task<HnStoryDto?> GetStoryAsync(int id, CancellationToken cancellationToken = default);
}