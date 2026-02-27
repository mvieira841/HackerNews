using HackerNews.Api.Application.Features.GetBestStories.Contracts;
using HackerNews.Api.Application.Features.GetBestStories.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace HackerNews.Api.Application.Features.GetBestStories;

/// <summary>
/// Minimal API endpoint definition for fetching the best stories.
/// </summary>
public static class Endpoint
{
    public static void MapGetBestStoriesEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet(Constants.EndpointRoute, async (
            [FromQuery(Name = "n")] int? n,
            IGetBestStoriesHandler handler,
            CancellationToken cancellationToken) =>
        {
            // Validate 'n' explicitly. 
            // We use nullable int? so the framework doesn't throw a default 400 error if 'n' is missing.
            // This lets us control the exact error message returned to the client.
            if (!n.HasValue || n.Value <= 0)
            {
                return Results.BadRequest(Constants.InvalidNParameterMessage);
            }

            // Execute core business logic via the injected handler
            var result = await handler.HandleAsync(n.Value, cancellationToken);

            if (result.IsFailed)
            {
                return Results.BadRequest(result.Errors.First().Message);
            }

            return Results.Ok(result.Value);
        })
        .WithName(Constants.EndpointName)
        .WithSummary("Retrieves the best stories from Hacker News.")
        .WithDescription("Fetches the top 'n' best stories from the Hacker News API, ensuring they are returned in descending order based on their score.")
        .Produces<IEnumerable<StoryResponse>>(StatusCodes.Status200OK, "application/json")
        .ProducesProblem(StatusCodes.Status400BadRequest);
    }
}