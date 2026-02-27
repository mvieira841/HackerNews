using HackerNews.Api.Api;
using HackerNews.Api.Application;
using HackerNews.Api.Configuration;
using HackerNews.Api.Infrastructure;
using HackerNews.Api.Observability;
using Microsoft.Extensions.Options;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// --- 1. EARLY LOGGING INITIALIZATION ---
// Initialize Serilog immediately. This ensures that if the application crashes during 
// Dependency Injection setup or while reading config, the fatal error is still logged.
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .CreateLogger();

// Hook Serilog into the ASP.NET Core host pipeline, allowing it to access DI services later.
builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext());

// --- 2. CONFIGURATION ---
// Bind the "HackerNewsSettings" JSON section to our strongly-typed settings class.
builder.Services.Configure<HackerNewsSettings>(
    builder.Configuration.GetSection(HackerNewsSettings.SectionName));

// Expose the bound settings as a Singleton so standard DI constructors can easily inject it.
builder.Services.AddSingleton(sp => sp.GetRequiredService<IOptions<HackerNewsSettings>>().Value);

// --- 3. DEPENDENCY INJECTION ---
// Clean architecture separation via extension methods.
builder.Services.AddApi();              // Registers API versioning
builder.Services.AddInfrastructure();   // Registers HttpClients, Caches (Redis/Memory), Rate Limiter
builder.Services.AddApplication();      // Registers business logic handlers
builder.Services.AddObservability();    // Registers Health Checks, Swagger, Metrics

var app = builder.Build();

app.UseApi(); // Configure the HTTP request pipeline (Swagger, Serilog, Health Checks, etc.)

try
{
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Host terminated unexpectedly");
    throw;
}
finally
{
    // Ensure all logs are flushed to sinks (like files or external systems) before the app dies.
    Log.CloseAndFlush();
}