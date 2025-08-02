using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace MyProject.Services
{
    /// <summary>
    /// Global singleton for tracking SSE connection statistics and monitoring
    /// </summary>
    public class ConnectionStatisticsManager
    {
        private static readonly Lazy<ConnectionStatisticsManager> _instance = new(() => new ConnectionStatisticsManager());
        public static ConnectionStatisticsManager Instance => _instance.Value;

        // Active connections tracking
        private readonly ConcurrentDictionary<string, ConnectionInfo> _activeConnections = new();
        
        // Statistics tracking
        private readonly ConcurrentQueue<ConnectionEvent> _connectionEvents = new();
        private readonly ConcurrentQueue<ErrorEvent> _errorEvents = new();
        
        // Counters
        private long _totalConnectionsOpened = 0;
        private long _totalConnectionsClosed = 0;
        private long _totalMessagesProcessed = 0;
        private long _totalErrors = 0;

        private ConnectionStatisticsManager() { }

        #region Connection Management

        public string OpenConnection(string host, string infoId, string broadcastGroup, string broadcastGroup2)
        {
            var connectionId = Guid.NewGuid().ToString();
            var connectionInfo = new ConnectionInfo
            {
                ConnectionId = connectionId,
                Host = host,
                InfoId = infoId,
                BroadcastGroup = broadcastGroup,
                BroadcastGroup2 = broadcastGroup2,
                OpenedAt = DateTime.UtcNow,
                LastActivity = DateTime.UtcNow,
                MessageCount = 0,
                IsActive = true
            };

            _activeConnections[connectionId] = connectionInfo;
            Interlocked.Increment(ref _totalConnectionsOpened);

            // Add to events
            _connectionEvents.Enqueue(new ConnectionEvent
            {
                Timestamp = DateTime.UtcNow,
                Host = host,
                ConnectionId = connectionId,
                EventType = "OPENED",
                Details = $"InfoId: {infoId}, BG1: {broadcastGroup}, BG2: {broadcastGroup2}"
            });

            // Keep only last 100 events
            TrimEventQueue(_connectionEvents, 100);

            LoggerHelper.Info($"[STATS] Connection opened: {connectionId} from {host}. Total active: {_activeConnections.Count}");
            return connectionId;
        }

        public void CloseConnection(string connectionId, string reason = "Normal")
        {
            if (_activeConnections.TryRemove(connectionId, out var connectionInfo))
            {
                connectionInfo.IsActive = false;
                connectionInfo.ClosedAt = DateTime.UtcNow;
                connectionInfo.CloseReason = reason;

                Interlocked.Increment(ref _totalConnectionsClosed);

                _connectionEvents.Enqueue(new ConnectionEvent
                {
                    Timestamp = DateTime.UtcNow,
                    Host = connectionInfo.Host,
                    ConnectionId = connectionId,
                    EventType = "CLOSED",
                    Details = $"Reason: {reason}, Duration: {connectionInfo.Duration.TotalSeconds:F1}s, Messages: {connectionInfo.MessageCount}"
                });

                TrimEventQueue(_connectionEvents, 100);

                LoggerHelper.Info($"[STATS] Connection closed: {connectionId} from {connectionInfo.Host}. Reason: {reason}. Total active: {_activeConnections.Count}");
            }
        }

        public void RecordMessage(string connectionId)
        {
            if (_activeConnections.TryGetValue(connectionId, out var connectionInfo))
            {
                Interlocked.Increment(ref connectionInfo.MessageCount);
                connectionInfo.LastActivity = DateTime.UtcNow;
                Interlocked.Increment(ref _totalMessagesProcessed);
            }
        }

        public void RecordError(string host, string connectionId, string errorType, string errorMessage, Exception exception = null)
        {
            var errorEvent = new ErrorEvent
            {
                Timestamp = DateTime.UtcNow,
                Host = host,
                ConnectionId = connectionId,
                ErrorType = errorType,
                ErrorMessage = errorMessage,
                ExceptionDetails = exception?.ToString()
            };

            _errorEvents.Enqueue(errorEvent);
            Interlocked.Increment(ref _totalErrors);

            // Keep only last 100 errors
            TrimEventQueue(_errorEvents, 100);

            // Update connection info if exists
            if (!string.IsNullOrEmpty(connectionId) && _activeConnections.TryGetValue(connectionId, out var connectionInfo))
            {
                connectionInfo.ErrorCount++;
                connectionInfo.LastError = errorMessage;
                connectionInfo.LastErrorAt = DateTime.UtcNow;
            }

            LoggerHelper.Error($"[STATS] Error recorded for {host}: {errorType} - {errorMessage}", exception);
        }

        #endregion

        #region Statistics Retrieval

        public StatisticsSnapshot GetStatistics()
        {
            var activeConnections = _activeConnections.Values.ToList();
            var now = DateTime.UtcNow;

            // Calculate idle connections (no activity in last 5 minutes)
            var idleConnections = activeConnections
                .Where(c => (now - c.LastActivity).TotalMinutes > 5)
                .OrderByDescending(c => now - c.LastActivity)
                .Take(5)
                .ToList();

            // Get host statistics
            var hostStats = activeConnections
                .GroupBy(c => c.Host)
                .Select(g => new HostStatistic
                {
                    Host = g.Key,
                    ActiveConnections = g.Count(),
                    TotalMessages = g.Sum(c => c.MessageCount),
                    OldestConnection = g.Min(c => c.OpenedAt),
                    NewestConnection = g.Max(c => c.OpenedAt)
                })
                .OrderByDescending(h => h.ActiveConnections)
                .ToList();

            // Get recent events
            var recentEvents = _connectionEvents.TakeLast(25).ToList();
            var recentErrors = _errorEvents.TakeLast(10).ToList();

            return new StatisticsSnapshot
            {
                GeneratedAt = now,
                TotalActiveConnections = activeConnections.Count,
                TotalConnectionsOpened = _totalConnectionsOpened,
                TotalConnectionsClosed = _totalConnectionsClosed,
                TotalMessagesProcessed = _totalMessagesProcessed,
                TotalErrors = _totalErrors,
                HostStatistics = hostStats,
                Last5ConnectionOpened = recentEvents.Where(e => e.EventType == "OPENED").TakeLast(5).ToList(),
                Last5ConnectionClosed = recentEvents.Where(e => e.EventType == "CLOSED").TakeLast(5).ToList(),
                Last5ErrorHosts = recentErrors.TakeLast(5).ToList(),
                IdleConnections = idleConnections.Take(5).ToList()
            };
        }

        public List<ConnectionInfo> GetConnectionDump()
        {
            return _activeConnections.Values
                .OrderBy(c => c.OpenedAt)  // Sort from older to newer
                .ToList();
        }

        #endregion

        #region Helper Methods

        private void TrimEventQueue<T>(ConcurrentQueue<T> queue, int maxSize)
        {
            while (queue.Count > maxSize)
            {
                queue.TryDequeue(out _);
            }
        }

        #endregion
    }

    #region Data Models

    public class ConnectionInfo
    {
        public string ConnectionId { get; set; } = string.Empty;
        public string Host { get; set; } = string.Empty;
        public string InfoId { get; set; } = string.Empty;
        public string BroadcastGroup { get; set; } = string.Empty;
        public string BroadcastGroup2 { get; set; } = string.Empty;
        public DateTime OpenedAt { get; set; }
        public DateTime? ClosedAt { get; set; }
        public DateTime LastActivity { get; set; }
        public long MessageCount { get; set; }
        public int ErrorCount { get; set; }
        public string? LastError { get; set; }
        public DateTime? LastErrorAt { get; set; }
        public bool IsActive { get; set; }
        public string? CloseReason { get; set; }

        public TimeSpan Duration => (ClosedAt ?? DateTime.UtcNow) - OpenedAt;
        public TimeSpan IdleTime => DateTime.UtcNow - LastActivity;
    }

    public class ConnectionEvent
    {
        public DateTime Timestamp { get; set; }
        public string Host { get; set; } = string.Empty;
        public string ConnectionId { get; set; } = string.Empty;
        public string EventType { get; set; } = string.Empty; // OPENED, CLOSED
        public string Details { get; set; } = string.Empty;
    }

    public class ErrorEvent
    {
        public DateTime Timestamp { get; set; }
        public string Host { get; set; } = string.Empty;
        public string ConnectionId { get; set; } = string.Empty;
        public string ErrorType { get; set; } = string.Empty;
        public string ErrorMessage { get; set; } = string.Empty;
        public string? ExceptionDetails { get; set; }
    }

    public class HostStatistic
    {
        public string Host { get; set; } = string.Empty;
        public int ActiveConnections { get; set; }
        public long TotalMessages { get; set; }
        public DateTime OldestConnection { get; set; }
        public DateTime NewestConnection { get; set; }
    }

    public class StatisticsSnapshot
    {
        public DateTime GeneratedAt { get; set; }
        public int TotalActiveConnections { get; set; }
        public long TotalConnectionsOpened { get; set; }
        public long TotalConnectionsClosed { get; set; }
        public long TotalMessagesProcessed { get; set; }
        public long TotalErrors { get; set; }
        public List<HostStatistic> HostStatistics { get; set; } = new();
        public List<ConnectionEvent> Last5ConnectionOpened { get; set; } = new();
        public List<ConnectionEvent> Last5ConnectionClosed { get; set; } = new();
        public List<ErrorEvent> Last5ErrorHosts { get; set; } = new();
        public List<ConnectionInfo> IdleConnections { get; set; } = new();
    }

    #endregion
}
