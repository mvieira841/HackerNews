using HackerNews.Api.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using System.Reflection;

namespace HackerNews.Api.Observability;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddObservability(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            // Set up basic document info
            options.SwaggerDoc("v1", new Microsoft.OpenApi.OpenApiInfo
            {
                Title = "HackerNews API",
                Version = "v1",
                Description = "A RESTful API to retrieve the best stories from Hacker News."
            });

            // Instruct Swagger to include the generated XML comments
            var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
            var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
            options.IncludeXmlComments(xmlPath);
        });

        // 1. Start building the health checks
        var healthChecksBuilder = services.AddHealthChecks();

        // 2. Fetch the current settings to check if Redis is configured
        var provider = services.BuildServiceProvider();
        var settings = provider.GetRequiredService<IOptions<HackerNewsSettings>>().Value;

        // 3. Conditionally add the Redis health check (Readiness probe)
        if (!string.IsNullOrWhiteSpace(settings.RedisConnectionString))
        {
            healthChecksBuilder.AddRedis(
                redisConnectionString: settings.RedisConnectionString,
                name: "redis_cache",
                failureStatus: HealthStatus.Unhealthy, // Forces 503 on failure
                tags: new[] { "ready" },
                timeout: TimeSpan.FromSeconds(1)); // Don't wait too long for a dead container
        }

        // Must be a Singleton so metrics persist globally across the app lifecycle
        services.AddSingleton<IMetricsService, MetricsService>();

        return services;
    }
}