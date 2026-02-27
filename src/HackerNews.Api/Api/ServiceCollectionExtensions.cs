namespace HackerNews.Api.Api;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApi(this IServiceCollection services)
    {
        // --- API Versioning ---
        services.AddApiVersioning(options =>
        {
            options.DefaultApiVersion = new Asp.Versioning.ApiVersion(1, 0);
            options.AssumeDefaultVersionWhenUnspecified = true; // Defaults to v1.0 if no version is provided
            options.ReportApiVersions = true; // Returns supported versions in the HTTP headers
            options.ApiVersionReader = new Asp.Versioning.UrlSegmentApiVersionReader();
        }).AddApiExplorer(options =>
        {
            // Formats the version as "'v'major[.minor]" (e.g. v1) and substitutes it in the Swagger URL
            options.GroupNameFormat = "'v'V";
            options.SubstituteApiVersionInUrl = true;
        });

        return services;
    }
}
