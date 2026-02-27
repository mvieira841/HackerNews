using FluentAssertions;
using HackerNews.Api.Application.Features.GetBestStories.Contracts;
using HackerNews.Api.Infrastructure.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace HackerNews.Tests.Api.Integration;

/// <summary>
/// High-level Integration Tests evaluating the full HTTP pipeline, Routing, and Handlers.
/// </summary>
public class GetBestStoriesEndpointTests : IClassFixture<TestStartupFactory>
{
    private readonly TestStartupFactory _factory;

    public GetBestStoriesEndpointTests(TestStartupFactory factory) => _factory = factory;

    [Fact]
    public async Task GetBestStoriesEndpoint_ReturnsOk_WithExpectedPayload()
    {
        // Arrange: Mock the external HTTP client dependency to prevent hitting the real HackerNews API
        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                var hnClient = Substitute.For<IHackerNewsApiClient>();

                // Setup mock return values
                hnClient.GetBestStoryIdsAsync(Arg.Any<CancellationToken>()).Returns(new[] { 1, 2 });
                hnClient.GetStoryAsync(1, Arg.Any<CancellationToken>()).Returns(new HnStoryDto(1, "A", "http://a", "a", DateTimeOffset.UtcNow.ToUnixTimeSeconds(), 1, 0));
                hnClient.GetStoryAsync(2, Arg.Any<CancellationToken>()).Returns(new HnStoryDto(2, "B", "http://b", "b", DateTimeOffset.UtcNow.ToUnixTimeSeconds(), 2, 1));

                // Override the real client with our mock
                services.AddSingleton(hnClient);
            });
        }).CreateClient();

        // Act: Make a real HTTP GET to our test server
        var response = await client.GetAsync("/api/v1/best-stories?n=2");

        // Assert: Verify HTTP Status, Deserialization, and Ordering rules
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<StoryResponse[]>();

        body.Should().NotBeNull();
        body!.Should().HaveCount(2);
        // Verify business logic: must be returned ordered descending by score
        body[0].Score.Should().BeGreaterThanOrEqualTo(body[1].Score);
    }
}