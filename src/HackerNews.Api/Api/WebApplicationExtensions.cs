using HackerNews.Api.Application;
using HackerNews.Api.Observability;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Serilog;
using Serilog.Events;

namespace HackerNews.Api.Api;

public static class WebApplicationExtensions
{
    public static WebApplication UseApi(this WebApplication app)
    {
        // --- 4. HTTP PIPELINE ---
        if (app.Environment.IsDevelopment())
        {
            // Expose OpenAPI/Swagger definitions in development environments.
            app.UseSwagger();
            app.UseSwaggerUI();
        }
        else
        {
            app.UseHttpsRedirection();
        }
        
        // Custom Serilog request logging middleware.
        app.UseSerilogRequestLogging(options =>
        {
            // Define the exact format of the console/log output.
            options.MessageTemplate = "Handled {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";

            // Customize log levels based on HTTP status codes (e.g., 500s are Errors, 400s are Warnings).
            options.GetLevel = (httpContext, elapsed, ex) =>
            {
                if (ex != null) return LogEventLevel.Error;
                var statusCode = httpContext.Response?.StatusCode;
                if (statusCode >= 500) return LogEventLevel.Error;
                if (statusCode >= 400) return LogEventLevel.Warning;
                return LogEventLevel.Information;
            };

            // Push extra contextual data (like IP and actual routing pattern) into the log properties.
            options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
            {
                diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value);
                diagnosticContext.Set("RequestScheme", httpContext.Request.Scheme);
                diagnosticContext.Set("RequestPath", httpContext.Request.Path.Value);
                diagnosticContext.Set("RequestQueryString", httpContext.Request.QueryString.Value);
                diagnosticContext.Set("RemoteIpAddress", httpContext.Connection.RemoteIpAddress?.ToString());

                var endpoint = httpContext.GetEndpoint();
                if (endpoint != null)
                {
                    diagnosticContext.Set("EndpointName", endpoint.DisplayName);
                    // Attempt to log the matched route pattern (e.g., "/best-stories")
                    if (endpoint is RouteEndpoint re && re.RoutePattern?.RawText is string pattern)
                    {
                        diagnosticContext.Set("RoutePattern", pattern);
                    }
                }
            };
        });

        // Liveness probe: returns 200 OK as long as the API process is running.
        app.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = _ => false // Excludes all dependency checks
        });

        // Readiness probe: actively tests the Redis connection (and any other tagged dependencies).
        app.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("ready")
        });

        // Expose internal custom application metrics.
        app.MapGet("/metrics", (IMetricsService metrics) =>
        {
            return Results.Json(new
            {
                totalRequests = metrics.TotalRequests,
                cacheHits = metrics.CacheHits,
                cacheMisses = metrics.CacheMisses,
                successfulUpstreamCalls = metrics.SuccessfulUpstreamCalls,
                failedRequests = metrics.FailedRequests
            });
        });

        // Register all business endpoints (e.g., /best-stories)
        app.MapApplicationEndpoints();

        Log.Information("HackerNews.Api started");
        Log.Information("Environment: {Env}", app.Environment.EnvironmentName);

        // Delay logging the bound URLs until the Kestrel server has fully finished starting up.
        app.Lifetime.ApplicationStarted.Register(() =>
        {
            var urls = string.Join(", ", app.Urls);
            Log.Information("HackerNews.Api started and listening on: {Urls}", urls);
        });

        return app;
    }
}
