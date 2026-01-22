using Microsoft.AspNetCore.SignalR;
using Prometheus;
using WebdoctorV.Checkers;
using WebdoctorV.Hubs;
using WebdoctorV.Models;
using WebdoctorV.Parsers;
using WebdoctorV.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddControllers();
builder.Services.AddSignalR();
var httpClientFactory = builder.Services.AddHttpClient();
builder.Services.AddSingleton<IChecker>(sp => 
{
    var httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient();
    return new HttpChecker(httpClient);
});
builder.Services.AddSingleton<IChecker, MySqlChecker>();
builder.Services.AddSingleton<IChecker, PostgreSqlChecker>();
builder.Services.AddSingleton<IChecker, SshChecker>();
builder.Services.AddSingleton<IChecker, TcpChecker>();
builder.Services.AddSingleton<IChecker, UdpChecker>();

// Load config - check stdin first (pipe input)
Config config;
string? configContent = null;
string? configSource = null;

// Check if input is redirected (pipe)
if (Console.IsInputRedirected)
{
    try
    {
        using var reader = new StreamReader(Console.OpenStandardInput());
        configContent = reader.ReadToEnd();
        if (!string.IsNullOrWhiteSpace(configContent))
        {
            configSource = "stdin (pipe)";
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Warning: Failed to read from stdin: {ex.Message}");
    }
}

// If no pipe input, try file
if (string.IsNullOrWhiteSpace(configContent))
{
    var configPath = builder.Configuration["ConfigPath"] ?? "/app/config.hcl";
    if (!System.IO.File.Exists(configPath))
    {
        // Try current directory
        var localConfig = Path.Combine(Directory.GetCurrentDirectory(), "config.hcl");
        if (System.IO.File.Exists(localConfig))
        {
            configPath = localConfig;
        }
        else
        {
            // Try examples directory (for local development)
            var examplesPath = Path.Combine("..", "examples", "example-health.hcl");
            if (System.IO.File.Exists(examplesPath))
            {
                configPath = examplesPath;
            }
        }
    }

    if (System.IO.File.Exists(configPath))
    {
        configContent = System.IO.File.ReadAllText(configPath);
        configSource = configPath;
    }
}

// Parse config
if (!string.IsNullOrWhiteSpace(configContent))
{
    try
    {
        config = StandardHclParser.ParseConfig(configContent);
        Console.WriteLine($"Loaded HCL config from {configSource}");
        Console.WriteLine($"Found {config.Services.Count} services");
        if (config.Services.Count == 0)
        {
            Console.WriteLine($"WARNING: No services found in config. Config content length: {configContent.Length}");
            Console.WriteLine($"First 200 chars: {configContent.Substring(0, Math.Min(200, configContent.Length))}");
        }
        foreach (var service in config.Services)
        {
            Console.WriteLine($"  - Service: {service.Id} ({service.Protocol}://{service.Host}:{service.Port}{service.Path})");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error parsing config: {ex.Message}");
        Console.WriteLine($"Stack trace: {ex.StackTrace}");
        config = new Config { Interval = TimeSpan.FromSeconds(60) };
    }
}
else
{
    Console.WriteLine($"No config found (neither from stdin nor file). Using empty config.");
    config = new Config { Interval = TimeSpan.FromSeconds(60) };
}

builder.Services.AddSingleton(config);
builder.Services.AddSingleton<HealthCheckService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<HealthCheckService>());

var app = builder.Build();

// Configure Prometheus
app.UseRouting();
app.UseHttpMetrics();
app.MapMetrics("/metrics");
app.MapControllers();

// SignalR Hub
app.MapHub<HealthCheckHub>("/healthhub");

// Static files for future frontend
app.UseStaticFiles();

// Health endpoint for WebdoctorV itself
app.MapGet("/health", () =>
{
    // Simple health check - just return OK if app is running
    return Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow });
});

// Default route - redirect to dashboard
app.MapGet("/", () => Results.Redirect("/index.html"));

app.Run();
