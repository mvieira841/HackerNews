using FluentAssertions;
using Polly;
using Polly.CircuitBreaker;
using System.Net;
using Xunit;

namespace HackerNewsApi.Tests.Api.Unit;

/// <summary>
/// Validates that our Polly Resilience policies accurately "Fail Fast" instead of continually
/// hanging or crashing under load when the external service is unavailable.
/// </summary>
public class CircuitBreakerPolicyTests
{
    [Fact]
    public async Task CircuitBreaker_OpensAfterConsecutiveResultFailures()
    {
        // Arrange: Break circuit after 3 consecutive failures
        var circuit = Policy<HttpResponseMessage>
            .HandleResult(r => (int)r.StatusCode >= 500)
            .CircuitBreakerAsync(3, TimeSpan.FromSeconds(5));

        Task<HttpResponseMessage> Fail() => Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError));

        // Act: Execute 3 times to intentionally trip the breaker
        var r1 = await circuit.ExecuteAsync(() => Fail());
        var r2 = await circuit.ExecuteAsync(() => Fail());
        var r3 = await circuit.ExecuteAsync(() => Fail());

        r1.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        r2.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        r3.StatusCode.Should().Be(HttpStatusCode.InternalServerError);

        // Next call should throw BrokenCircuitException because circuit is now OPEN
        await Assert.ThrowsAsync<BrokenCircuitException<HttpResponseMessage>>(() => circuit.ExecuteAsync(() => Fail()));
    }

    [Fact]
    public async Task CircuitBreaker_WithExceptionHandling_ThrowsBrokenCircuitAfterThreshold()
    {
        // Arrange
        var circuit = Policy
            .Handle<HttpRequestException>()
            .CircuitBreakerAsync(2, TimeSpan.FromSeconds(5));

        Task<HttpResponseMessage> Fail() => throw new HttpRequestException("upstream failure");

        // Act: Two failures should be observed and counted
        await Assert.ThrowsAsync<HttpRequestException>(() => circuit.ExecuteAsync(() => Fail()));
        await Assert.ThrowsAsync<HttpRequestException>(() => circuit.ExecuteAsync(() => Fail()));

        // The third call intercepts the connection before it's even made, throwing a BrokenCircuitException
        await Assert.ThrowsAsync<BrokenCircuitException>(() => circuit.ExecuteAsync(() => Fail()));
    }
}