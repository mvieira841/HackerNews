using HackerNews.Api.Application.Features.GetBestStories;
using HackerNews.Api.Application.Features.GetBestStories.Interfaces;

namespace HackerNews.Api.Application;

/// <summary>
/// Registers Application-layer specific dependencies (Handlers, Services, MediatR logic).
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // Register the primary handler scoped to the lifetime of the HTTP request
        services.AddScoped<IGetBestStoriesHandler, GetBestStoriesHandler>();
        return services;
    }
}