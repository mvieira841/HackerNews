using HackerNews.Api.Configuration;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.Redis;
using Xunit;

namespace HackerNews.Tests.Api.Integration;

/// <summary>
/// Custom WebApplicationFactory utilized by integration tests. 
/// Automatically provisions and configures an ephemeral Docker Redis instance via Testcontainers.
/// </summary>
public class TestStartupFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    // Defines a Docker container utilizing the exact redis image used in production
    private readonly RedisContainer _redisContainer = new RedisBuilder("redis:7")
        .Build();

    /// <summary>
    /// Invoked automatically by xUnit BEFORE tests run. We spin up Docker here.
    /// </summary>
    public async Task InitializeAsync()
    {
        await _redisContainer.StartAsync();
    }

    /// <summary>
    /// Invoked automatically by xUnit AFTER tests run. We clean up Docker here.
    /// </summary>
    public new async Task DisposeAsync()
    {
        await _redisContainer.DisposeAsync();
    }

    /// <summary>
    /// Explicitly stops the Redis container to simulate infrastructure failure.
    /// </summary>
    public async Task StopRedisAsync() => await _redisContainer.StopAsync();

    /// <summary>
    /// Restarts the Redis container after a failure test is completed.
    /// </summary>
    public async Task StartRedisAsync() => await _redisContainer.StartAsync();

    /// <summary>
    /// Intercepts the API startup pipeline to inject our dynamic Testcontainer settings.
    /// </summary>
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureTestServices(services =>
        {
            // 1. Forcefully override the core Redis library connection string configuration
            services.PostConfigure<RedisCacheOptions>(options =>
            {
                options.Configuration = _redisContainer.GetConnectionString();
            });

            // 2. Overwrite our strongly-typed application settings to match, ensuring total sync
            services.PostConfigure<HackerNewsSettings>(settings =>
            {
                settings.RedisConnectionString = _redisContainer.GetConnectionString();
            });
        });

        base.ConfigureWebHost(builder);
    }
}