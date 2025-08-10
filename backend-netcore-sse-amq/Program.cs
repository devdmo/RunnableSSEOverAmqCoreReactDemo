using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MyProject.Services;

var builder = WebApplication.CreateBuilder(args);

// Configure Kestrel server with timeouts to prevent connection leaks
builder.WebHost.ConfigureKestrel(serverOptions =>
{
    // ✅ CRITICAL: HTTP request timeout
    serverOptions.Limits.KeepAliveTimeout = TimeSpan.FromMinutes(10); // Close idle HTTP connections after 10 minutes
    
    // ✅ CRITICAL: Request header timeout
    serverOptions.Limits.RequestHeadersTimeout = TimeSpan.FromSeconds(30);
    
    // ✅ CRITICAL: Maximum request body size timeout
    serverOptions.Limits.MaxRequestBodySize = 10 * 1024 * 1024; // 10MB
    
    // ✅ CRITICAL: Connection lifetime limit
    serverOptions.Limits.MaxConcurrentConnections = 1000; // Prevent resource exhaustion
    
    LoggerHelper.Info("Kestrel configured with connection timeouts and limits");
});

// Register controllers and custom services in dependency injection.
builder.Services.AddControllers();
builder.Services.AddSingleton<AMQConnectionManager>();  // Manages shared ActiveMQ connection.
builder.Services.AddSingleton<AMQPublisher>();          // Publishes messages to ActiveMQ.
builder.Services.AddSingleton<AMQConsumerSse>();          // Handles SSE consumption from ActiveMQ.

// ✅ CRITICAL: Add connection timeout background service
builder.Services.AddHostedService<ConnectionTimeoutService>(); // Automatically closes idle connections

// Configure CORS to allow requests from any origin (adjust for production as needed).
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
});

var app = builder.Build();

// Log application startup.
LoggerHelper.Info("Application starting up...");

// Enable CORS.
app.UseCors("AllowAll");

// Map controller endpoints.
app.MapControllers();

// Add graceful shutdown handling to prevent connection leaks
app.Lifetime.ApplicationStopping.Register(() =>
{
    LoggerHelper.Info("Application shutdown initiated - disposing AMQ connections...");
    
    try
    {
        // Get and dispose the AMQ connection manager
        var amqManager = app.Services.GetService<AMQConnectionManager>();
        amqManager?.Dispose();
        
        LoggerHelper.Info("AMQ connections disposed successfully during shutdown.");
    }
    catch (Exception ex)
    {
        LoggerHelper.Error("Error during application shutdown", ex);
    }
});

// Start the application.
app.Run();
