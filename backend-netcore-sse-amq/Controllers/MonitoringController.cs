using Microsoft.AspNetCore.Mvc;
using MyProject.Services;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace MyProject.Controllers
{
    /// <summary>
    /// MonitoringController provides endpoints for connection statistics and monitoring
    /// </summary>
    [ApiController]
    [Route("api/monitoring")]
    public class MonitoringController : ControllerBase
    {
        private readonly ConnectionStatisticsManager _statsManager;

        public MonitoringController()
        {
            _statsManager = ConnectionStatisticsManager.Instance;
        }

        /// <summary>
        /// GET /api/monitoring/statistics
        /// Returns comprehensive statistics about SSE connections
        /// </summary>
        [HttpGet("statistics")]
        public IActionResult GetStatistics()
        {
            try
            {
                LoggerHelper.Info("[MONITORING] Statistics endpoint called");
                
                var statistics = _statsManager.GetStatistics();
                
                LoggerHelper.Debug($"[MONITORING] Statistics generated: {statistics.TotalActiveConnections} active connections");
                
                return Ok(statistics);
            }
            catch (Exception ex)
            {
                LoggerHelper.Error("[MONITORING] Error generating statistics", ex);
                _statsManager.RecordError(
                    HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    string.Empty,
                    "STATISTICS_ERROR",
                    "Failed to generate statistics",
                    ex
                );
                return StatusCode(500, new { error = "Failed to generate statistics", message = ex.Message });
            }
        }

        /// <summary>
        /// GET /api/monitoring/connections
        /// Returns a dump of all active connections with detailed information
        /// </summary>
        [HttpGet("connections")]
        public IActionResult GetConnectionsDump()
        {
            try
            {
                LoggerHelper.Info("[MONITORING] Connection dump endpoint called");
                
                var connections = _statsManager.GetConnectionDump();
                
                LoggerHelper.Debug($"[MONITORING] Connection dump generated: {connections.Count} connections");
                
                var response = new
                {
                    generatedAt = DateTime.UtcNow,
                    totalConnections = connections.Count,
                    connections = connections.Select(c => new
                    {
                        connectionId = c.ConnectionId,
                        host = c.Host,
                        infoId = c.InfoId,
                        broadcastGroup = c.BroadcastGroup,
                        broadcastGroup2 = c.BroadcastGroup2,
                        openedAt = c.OpenedAt,
                        duration = new
                        {
                            totalSeconds = c.Duration.TotalSeconds,
                            humanReadable = FormatDuration(c.Duration)
                        },
                        lastActivity = c.LastActivity,
                        idleTime = new
                        {
                            totalSeconds = c.IdleTime.TotalSeconds,
                            humanReadable = FormatDuration(c.IdleTime)
                        },
                        messageCount = c.MessageCount,
                        errorCount = c.ErrorCount,
                        lastError = c.LastError,
                        lastErrorAt = c.LastErrorAt,
                        isActive = c.IsActive
                    }).ToList()
                };
                
                return Ok(response);
            }
            catch (Exception ex)
            {
                LoggerHelper.Error("[MONITORING] Error generating connection dump", ex);
                _statsManager.RecordError(
                    HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    string.Empty,
                    "CONNECTION_DUMP_ERROR",
                    "Failed to generate connection dump",
                    ex
                );
                return StatusCode(500, new { error = "Failed to generate connection dump", message = ex.Message });
            }
        }

        /// <summary>
        /// GET /api/monitoring/health
        /// Basic health check endpoint with current connection count
        /// </summary>
        [HttpGet("health")]
        public IActionResult GetHealth()
        {
            try
            {
                var statistics = _statsManager.GetStatistics();
                
                var health = new
                {
                    status = "healthy",
                    timestamp = DateTime.UtcNow,
                    activeConnections = statistics.TotalActiveConnections,
                    totalErrors = statistics.TotalErrors,
                    uptime = GetUptime(),
                    memoryUsage = GC.GetTotalMemory(false) / 1024 / 1024 // MB
                };
                
                LoggerHelper.Debug($"[MONITORING] Health check: {statistics.TotalActiveConnections} active connections");
                
                return Ok(health);
            }
            catch (Exception ex)
            {
                LoggerHelper.Error("[MONITORING] Error in health check", ex);
                return StatusCode(500, new { status = "unhealthy", error = ex.Message });
            }
        }

        /// <summary>
        /// POST /api/monitoring/test-error
        /// Test endpoint to generate sample errors for testing monitoring
        /// </summary>
        [HttpPost("test-error")]
        public IActionResult TestError([FromBody] TestErrorRequest? request)
        {
            try
            {
                var errorType = request?.ErrorType ?? "TEST_ERROR";
                var errorMessage = request?.Message ?? "Test error for monitoring";
                
                var host = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "test-host";
                
                _statsManager.RecordError(host, string.Empty, errorType, errorMessage);
                
                LoggerHelper.Warn($"[MONITORING] Test error generated: {errorType} - {errorMessage}");
                
                return Ok(new { message = "Test error recorded", errorType, errorMessage });
            }
            catch (Exception ex)
            {
                LoggerHelper.Error("[MONITORING] Error generating test error", ex);
                return StatusCode(500, new { error = "Failed to generate test error", message = ex.Message });
            }
        }

        #region Helper Methods

        private string FormatDuration(TimeSpan duration)
        {
            if (duration.TotalDays >= 1)
                return $"{duration.Days}d {duration.Hours}h {duration.Minutes}m";
            if (duration.TotalHours >= 1)
                return $"{duration.Hours}h {duration.Minutes}m {duration.Seconds}s";
            if (duration.TotalMinutes >= 1)
                return $"{duration.Minutes}m {duration.Seconds}s";
            return $"{duration.TotalSeconds:F1}s";
        }

        private static DateTime _startTime = DateTime.UtcNow;
        private string GetUptime()
        {
            var uptime = DateTime.UtcNow - _startTime;
            return FormatDuration(uptime);
        }

        #endregion
    }

    #region Request Models

    public class TestErrorRequest
    {
        public string? ErrorType { get; set; }
        public string? Message { get; set; }
    }

    #endregion
}
