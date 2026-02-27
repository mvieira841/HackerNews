using FluentAssertions;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HackerNews.Tests.Api.Integration;

public class EndpointRoutingTests : IClassFixture<TestStartupFactory>
{
    private readonly TestStartupFactory _factory;

    public EndpointRoutingTests(TestStartupFactory factory) => _factory = factory;

    [Fact]
    public void EndpointDataSource_ContainsBestStoriesRoute()
    {
        // Arrange: Pull the routing table directly from the Dependency Injection container
        var server = _factory.Server;
        var endpoints = server.Services.GetRequiredService<EndpointDataSource>();

        // Act: Verify our custom Minimal API mappings were registered properly into the table
        var hasBestStories = endpoints.Endpoints
            .OfType<RouteEndpoint>()
            .Any(e => e?.RoutePattern?.RawText?.Contains("api/v{version:apiVersion}/best-stories") == true);

        // Assert
        hasBestStories.Should().BeTrue();
    }
}