using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MyProject.Services;

var builder = WebApplication.CreateBuilder(args);

// Register controllers and custom services in dependency injection.
builder.Services.AddControllers();
builder.Services.AddSingleton<AMQConnectionManager>();  // Manages shared ActiveMQ connection.
builder.Services.AddSingleton<AMQPublisher>();          // Publishes messages to ActiveMQ.
builder.Services.AddSingleton<AMQConsumerSse>();          // Handles SSE consumption from ActiveMQ.

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

// Start the application.
app.Run();
