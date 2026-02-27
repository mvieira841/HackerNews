using FluentAssertions;
using HackerNews.Api.Application.Features.GetBestStories.Contracts;
using HackerNews.Api.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace HackerNews.Tests.Api.Regression;

/// <summary>
/// A custom HTTP Message Handler used to artificially inject transient failures into the HttpClient.
/// </summary>
internal class FlakyHandler : HttpMessageHandler
{
    private int _callCount;

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        _callCount++;

        // First call -> Simulate transient 500 error
        if (_callCount == 1)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError));
        }

        // Second call -> Simulate success with payload
        var dto = new HnStoryDto(1, "R", "http://r", "rex", DateTimeOffset.UtcNow.ToUnixTimeSeconds(), 42, 7);
        var msg = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(dto)
        };
        return Task.FromResult(msg);
    }
}

public class HackerNewsApiClientRetryTests
{
    [Fact]
    public async Task GetStoryAsync_RetriesOnTransientFailure_ThenSucceeds()
    {
        // Arrange
        var handler = new FlakyHandler();
        var client = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") };
        var apiClient = new HackerNewsApiClient(client, NullLogger<HackerNewsApiClient>.Instance);

        // Act
        // Polly will internally hit the 500 error, wait for backoff, retry, and hit the 200 OK.
        var result = await apiClient.GetStoryAsync(1, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.Title.Should().Be("R");
        result.Score.Should().Be(42);
    }
}