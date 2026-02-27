using Asp.Versioning;
using HackerNews.Api.Application.Features.GetBestStories;

namespace HackerNews.Api.Application;

/// <summary>
/// Central registry for mapping all Minimal API endpoints in the application.
/// </summary>
public static class EndpointMappings
{
    public static WebApplication MapApplicationEndpoints(this WebApplication app)
    {
        // 1. Define the API versions supported by the application
        var apiVersionSet = app.NewApiVersionSet()
            .HasApiVersion(new ApiVersion(1, 0))
            .ReportApiVersions()
            .Build();

        // 2. Create a global route group with the /api/v{version} prefix
        var apiGroup = app.MapGroup("/api/v{version:apiVersion}")
            .WithApiVersionSet(apiVersionSet);

        // 3. Map feature endpoints to the GROUP instead of directly to the app.
        // This automatically prepends "/api/v1" to Constants.EndpointRoute ("/best-stories")
        apiGroup.MapGetBestStoriesEndpoint();

        return app;
    }
}