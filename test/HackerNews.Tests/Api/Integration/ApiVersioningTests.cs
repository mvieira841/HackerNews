using FluentAssertions;
using System.Net;
using Xunit;

namespace HackerNews.Tests.Api.Integration;

/// <summary>
/// Validates that Asp.Versioning.Http correctly maps and restricts routes based on version segments.
/// </summary>
public class ApiVersioningTests : IClassFixture<TestStartupFactory>
{
    private readonly TestStartupFactory _factory;

    public ApiVersioningTests(TestStartupFactory factory) => _factory = factory;

    [Fact]
    public async Task RequestingUnsupportedVersion_ReturnsNotFound()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act: Request v2 which does not exist (we only mapped v1.0)
        var response = await client.GetAsync("/api/v2/best-stories?n=1");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task RequestingMalformedVersionFormat_ReturnsNotFound()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act: Request with an invalid version string format that violates the URL segment reader
        var response = await client.GetAsync("/api/vABC/best-stories?n=1");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}