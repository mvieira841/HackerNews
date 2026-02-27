using FluentAssertions;
using System.Net;
using System.Text.Json;
using Xunit;

namespace HackerNews.Tests.Api.Integration;

/// <summary>
/// Ensures our infrastructural diagnostic endpoints (Health Checks and Metrics) are functioning correctly.
/// </summary>
public class ObservabilityEndpointsTests : IClassFixture<TestStartupFactory>
{
    private readonly TestStartupFactory _factory;

    public ObservabilityEndpointsTests(TestStartupFactory factory) => _factory = factory;

    [Fact]
    public async Task LivenessEndpoint_ReturnsOk()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act: Hit the lightweight liveness probe
        var response = await client.GetAsync("/health/live");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Be("Healthy");
    }

    [Fact]
    public async Task ReadinessEndpoint_ReturnsOk_WhenDependenciesAreHealthy()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act: Hit the readiness probe (which actively pings Redis via Testcontainers)
        var response = await client.GetAsync("/health/ready");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Be("Healthy");
    }

    [Fact]
    public async Task MetricsEndpoint_ReturnsValidJson_MatchingSchema()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act: Access the strongly-typed metrics endpoint
        var response = await client.GetAsync("/metrics");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);

        // Assert the specific metric properties from MetricsResponse exist in the JSON payload
        doc.RootElement.TryGetProperty("totalRequests", out _).Should().BeTrue();
        doc.RootElement.TryGetProperty("cacheHits", out _).Should().BeTrue();
        doc.RootElement.TryGetProperty("cacheMisses", out _).Should().BeTrue();
        doc.RootElement.TryGetProperty("successfulUpstreamCalls", out _).Should().BeTrue();
        doc.RootElement.TryGetProperty("failedRequests", out _).Should().BeTrue();
    }
}