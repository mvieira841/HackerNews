using HackerNews.Api.Configuration;
using HackerNews.Api.Infrastructure.Interfaces;
using Microsoft.Extensions.Options;
using Polly;
using StackExchange.Redis;
using System.Net;
using System.Net.Http.Headers;

namespace HackerNews.Api.Infrastructure;

/// <summary>
/// Registers all external dependencies (HttpClients, Caching, Rate Limiters).
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        // 1. Configure the typed HttpClient for the external Hacker News API
        services.AddHttpClient<IHackerNewsApiClient, HackerNewsApiClient>((sp, client) =>
        {
            var settings = sp.GetRequiredService<IOptions<HackerNewsSettings>>().Value;
            client.BaseAddress = new Uri(settings.BaseUrl);
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        })
        // Wrap the client in Polly Resilience policies
        .AddPolicyHandler((sp, request) => Policy.WrapAsync(GetRetryPolicy(), GetCircuitBreakerPolicy()));

        // 2. Register the Rate Limiter as a Singleton so the Token Bucket persists across all requests
        services.AddSingleton<IRequestRateLimiter, RequestRateLimiter>();

        // 3. Setup Caching Strategy
        // We use an intermediate provider to check if a Redis Connection string was provided.
        var provider = services.BuildServiceProvider();
        var settings = provider.GetRequiredService<IOptions<HackerNewsSettings>>().Value;

        if (!string.IsNullOrWhiteSpace(settings.RedisConnectionString))
        {
            try
            {
                // actively test the connection at startup with a fast 2-second timeout
                var redisOptions = ConfigurationOptions.Parse(settings.RedisConnectionString);
                redisOptions.AbortOnConnectFail = true;
                redisOptions.ConnectTimeout = 2000;

                // If this fails, it throws a RedisConnectionException
                using var multiplexer = ConnectionMultiplexer.Connect(redisOptions);

                // Connection successful! Register Redis.
                services.AddStackExchangeRedisCache(opts => opts.Configuration = settings.RedisConnectionString);
                services.AddSingleton<ICacheService, RedisCacheService>();
            }
            catch (RedisConnectionException)
            {
                // RESILIENCE: Redis is configured but unavailable. Fallback safely.
                Console.WriteLine("⚠️ WARNING: Redis is unavailable at startup. Gracefully defaulting to In-Memory Cache.");
                services.AddMemoryCache();
                services.AddSingleton<ICacheService, CacheService>();
            }
        }
        else
        {
            // Fallback to local memory cache if Redis is unconfigured
            services.AddMemoryCache();
            services.AddSingleton<ICacheService, CacheService>();
        }

        return services;

        return services;
    }

    /// <summary>
    /// Creates a Polly retry policy to handle transient HTTP failures (5xx, Timeouts).
    /// </summary>
    private static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
    {
        return Policy<HttpResponseMessage>
            .Handle<HttpRequestException>()
            .Or<TaskCanceledException>()
            .OrResult(r => (int)r.StatusCode >= 500 || r.StatusCode == HttpStatusCode.RequestTimeout)
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)),
                onRetry: (outcome, timespan, attempt, context) => { /* Logging handled inside the typed client */ });
    }

    /// <summary>
    /// Creates a Polly circuit breaker policy. Prevents the app from continuously slamming 
    /// a totally dead upstream API, failing fast instead.
    /// </summary>
    private static IAsyncPolicy<HttpResponseMessage> GetCircuitBreakerPolicy()
    {
        return Policy<HttpResponseMessage>
            .Handle<HttpRequestException>()
            .OrResult(r => (int)r.StatusCode >= 500 || r.StatusCode == HttpStatusCode.RequestTimeout)
            // Break the circuit for 30 seconds after 5 consecutive failures
            .CircuitBreakerAsync(5, TimeSpan.FromSeconds(30));
    }
}