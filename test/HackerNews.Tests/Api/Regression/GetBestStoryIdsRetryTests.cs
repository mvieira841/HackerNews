using FluentAssertions;
using HackerNews.Api.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using System.Net;
using Xunit;

namespace HackerNews.Tests.Api.Regression;

internal class FlakyIdsHandler : HttpMessageHandler
{
    private int _callCount;

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        _callCount++;
        // First call -> Simulate failure
        if (_callCount == 1)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError));
        }

        // Second call -> Return valid JSON array
        var msg = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("[101,202]", System.Text.Encoding.UTF8, "application/json")
        };
        return Task.FromResult(msg);
    }
}

public class HackerNewsApiClientIdsRetryTests
{
    [Fact]
    public async Task GetBestStoryIdsAsync_RetriesOnTransientFailure_ThenSucceeds()
    {
        // Arrange
        var handler = new FlakyIdsHandler();
        var client = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") };

        var apiClient = new HackerNewsApiClient(client, NullLogger<HackerNewsApiClient>.Instance);

        // Act
        var result = await apiClient.GetBestStoryIdsAsync(CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(2);
        result.Should().Contain(new[] { 101, 202 });
    }
}