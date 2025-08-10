using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MyProject.Services
{
    /// <summary>
    /// Background service that automatically closes idle SSE connections to prevent resource leaks
    /// </summary>
    public class ConnectionTimeoutService : BackgroundService
    {
        private readonly ConnectionStatisticsManager _statsManager;
        private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(2); // Check every 2 minutes
        private readonly TimeSpan _maxIdleTime = TimeSpan.FromMinutes(15);   // Close connections idle for >15 minutes
        private readonly TimeSpan _maxConnectionTime = TimeSpan.FromHours(2); // Force close connections open >2 hours

        public ConnectionTimeoutService()
        {
            _statsManager = ConnectionStatisticsManager.Instance;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            LoggerHelper.Info("ConnectionTimeoutService started - monitoring idle connections");
            
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await CheckAndCloseIdleConnections();
                    await Task.Delay(_checkInterval, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    LoggerHelper.Info("ConnectionTimeoutService stopping - cancellation requested");
                    break;
                }
                catch (Exception ex)
                {
                    LoggerHelper.Error("Error in ConnectionTimeoutService", ex);
                    await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken); // Wait a bit before retrying
                }
            }
            
            LoggerHelper.Info("ConnectionTimeoutService stopped");
        }

        private Task CheckAndCloseIdleConnections()
        {
            var connections = _statsManager.GetConnectionDump();
            var now = DateTime.UtcNow;
            int closedCount = 0;

            foreach (var connection in connections)
            {
                try
                {
                    bool shouldClose = false;
                    string closeReason = "";

                    // Check for idle timeout
                    if ((now - connection.LastActivity) > _maxIdleTime)
                    {
                        shouldClose = true;
                        closeReason = $"IDLE_TIMEOUT_{_maxIdleTime.TotalMinutes}MIN";
                    }
                    // Check for maximum connection time
                    else if ((now - connection.OpenedAt) > _maxConnectionTime)
                    {
                        shouldClose = true;
                        closeReason = $"MAX_DURATION_TIMEOUT_{_maxConnectionTime.TotalHours}HR";
                    }

                    if (shouldClose)
                    {
                        LoggerHelper.Info($"[TIMEOUT] Closing connection {connection.ConnectionId} from {connection.Host}. Reason: {closeReason}. " +
                                        $"Idle: {(now - connection.LastActivity).TotalMinutes:F1}min, Duration: {(now - connection.OpenedAt).TotalMinutes:F1}min");
                        
                        _statsManager.CloseConnection(connection.ConnectionId, closeReason);
                        closedCount++;
                    }
                }
                catch (Exception ex)
                {
                    LoggerHelper.Error($"Error processing connection {connection.ConnectionId} for timeout", ex);
                }
            }

            if (closedCount > 0)
            {
                LoggerHelper.Info($"[TIMEOUT] Closed {closedCount} idle/expired connections");
            }
            else
            {
                LoggerHelper.Debug($"[TIMEOUT] No connections to close. Active: {connections.Count}");
            }
            
            return Task.CompletedTask;
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            LoggerHelper.Info("ConnectionTimeoutService shutdown requested");
            await base.StopAsync(cancellationToken);
        }
    }
}
