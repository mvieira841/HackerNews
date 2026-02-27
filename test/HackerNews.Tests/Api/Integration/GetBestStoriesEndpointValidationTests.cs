using FluentAssertions;
using HackerNews.Api.Application.Features.GetBestStories;
using System.Net;
using System.Text.Json;
using Xunit;

namespace HackerNews.Tests.Api.Integration;

public class GetBestStoriesEndpointValidationTests : IClassFixture<TestStartupFactory>
{
    private readonly TestStartupFactory _factory;

    public GetBestStoriesEndpointValidationTests(TestStartupFactory factory) => _factory = factory;

    [Fact]
    public async Task GetBestStoriesEndpoint_ReturnsBadRequest_WhenNIsZero()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act: Provide an invalid 'n' parameter
        var response = await client.GetAsync("/api/v1/best-stories?n=0");

        // Assert: Ensure custom error message is returned cleanly
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var textRaw = await response.Content.ReadAsStringAsync();
        var text = textRaw.StartsWith('"') ? JsonSerializer.Deserialize<string>(textRaw)! : textRaw;
        text.Should().Be(Constants.InvalidNParameterMessage);
    }

    [Fact]
    public async Task GetBestStoriesEndpoint_ReturnsBadRequest_WhenNIsMissing()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act: Omit 'n' entirely
        var response = await client.GetAsync("/api/v1/best-stories");

        // Assert: Ensure application code handles missing parameters gracefully, not framework exceptions
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var textRaw = await response.Content.ReadAsStringAsync();
        var text = textRaw.StartsWith('"') ? JsonSerializer.Deserialize<string>(textRaw)! : textRaw;
        text.Should().Be(Constants.InvalidNParameterMessage);
    }
}