using Microsoft.AspNetCore.Mvc;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using MyProject.Services;

namespace MyProject.Controllers
{
    /// <summary>
    /// InfoController provides an endpoint for the Info Page to receive messages via SSE.
    /// </summary>
    [ApiController]
    [Route("api/info")]
    public class InfoController : ControllerBase
    {
        private readonly AMQConsumerSse _consumerSse;
        private readonly ConnectionStatisticsManager _statsManager;

        public InfoController(AMQConsumerSse consumerSse)
        {
            _consumerSse = consumerSse;
            _statsManager = ConnectionStatisticsManager.Instance;
        }

        /// <summary>
        /// SSE endpoint to stream messages for a specific infoId.
        /// The response is kept open and messages are sent in SSE format.
        /// Optional broadcastGroup and broadcastGroup2 parameters can be provided to filter broadcast messages.
        /// </summary>
        [HttpGet("stream")]
        public async Task Stream([FromQuery] string id, [FromQuery] string ?broadcastGroup, [FromQuery] string ?broadcastGroup2, CancellationToken cancellationToken)
        {
            var clientHost = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            string? connectionId = null;
            
            try
            {
                // 🚨 CHECKPOINT 1: Verificar límites de conexión total
                var currentStats = _statsManager.GetStatistics();
                const int MAX_TOTAL_CONNECTIONS = 500;
                
                if (currentStats.TotalActiveConnections >= MAX_TOTAL_CONNECTIONS)
                {
                    LoggerHelper.Warn($"[SSE-DENY] Total connection limit reached: {currentStats.TotalActiveConnections}/{MAX_TOTAL_CONNECTIONS}. Denying connection from {clientHost}");
                    _statsManager.RecordError(clientHost, string.Empty, "CONNECTION_LIMIT_EXCEEDED", $"Total connections: {currentStats.TotalActiveConnections}");
                    
                    Response.StatusCode = 503;
                    await Response.WriteAsync("Service temporarily unavailable - connection limit exceeded");
                    return;
                }

                // 🚨 CHECKPOINT 2: Verificar límites por IP
                var hostConnections = currentStats.HostStatistics.FirstOrDefault(h => h.Host == clientHost);
                const int MAX_CONNECTIONS_PER_IP = 20;
                
                if (hostConnections != null && hostConnections.ActiveConnections >= MAX_CONNECTIONS_PER_IP)
                {
                    LoggerHelper.Warn($"[SSE-DENY] IP connection limit reached for {clientHost}: {hostConnections.ActiveConnections}/{MAX_CONNECTIONS_PER_IP}");
                    _statsManager.RecordError(clientHost, string.Empty, "IP_CONNECTION_LIMIT_EXCEEDED", $"IP connections: {hostConnections.ActiveConnections}");
                    
                    Response.StatusCode = 429;
                    await Response.WriteAsync("Too many connections from your IP address");
                    return;
                }

                // Ensure broadcast groups are never null
                broadcastGroup = broadcastGroup ?? string.Empty;
                broadcastGroup2 = broadcastGroup2 ?? string.Empty;

                LoggerHelper.Info($"[SSE-ACCEPT] Stream requested from {clientHost} - infoId: {id}, broadcastGroup: {(string.IsNullOrEmpty(broadcastGroup) ? "none" : broadcastGroup)}, broadcastGroup2: {(string.IsNullOrEmpty(broadcastGroup2) ? "none" : broadcastGroup2)}. Active: {currentStats.TotalActiveConnections}/{MAX_TOTAL_CONNECTIONS}");
                
                if (string.IsNullOrEmpty(id))
                {
                    LoggerHelper.Warn($"[SSE] No infoId provided from {clientHost}, defaulting to 'default'.");
                    id = "default";
                }

                // Register connection with statistics manager
                connectionId = _statsManager.OpenConnection(clientHost, id, broadcastGroup, broadcastGroup2);

                // Set the response header to use SSE content type.
                Response.Headers["Content-Type"] = "text/event-stream";
                Response.Headers["Cache-Control"] = "no-cache";
                Response.Headers["Connection"] = "keep-alive";
                Response.Headers["Access-Control-Allow-Origin"] = "*";
                
                LoggerHelper.Debug($"[SSE] Response headers set for connection {connectionId}");

                // ✅ CRITICAL: Ensure response stream is properly managed
                try
                {
                    // Create a combined cancellation token with timeout
                    using var timeoutCts = new CancellationTokenSource(TimeSpan.FromMinutes(30)); // 30-minute SSE timeout
                    using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
                    
                    // Start the consumer loop to stream messages.
                    await _consumerSse.StartConsumerAsync(id, broadcastGroup, broadcastGroup2, Response, combinedCts.Token, connectionId);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    LoggerHelper.Info($"[SSE] Connection {connectionId} cancelled by client");
                    throw; // Re-throw client cancellation
                }
                catch (OperationCanceledException)
                {
                    LoggerHelper.Info($"[SSE] Connection {connectionId} timed out after 30 minutes");
                    _statsManager.CloseConnection(connectionId, "SSE_TIMEOUT_30MIN");
                    // Don't re-throw timeout cancellation, handle it gracefully
                }
                finally
                {
                    // Ensure response stream is properly closed
                    try
                    {
                        if (Response.Body.CanWrite)
                        {
                            await Response.Body.FlushAsync(cancellationToken);
                        }
                        LoggerHelper.Debug($"[SSE] Response stream flushed for connection {connectionId}");
                    }
                    catch (Exception flushEx)
                    {
                        LoggerHelper.Warn($"[SSE] Error flushing response stream for connection {connectionId}: {flushEx.Message}");
                    }
                }
                
                LoggerHelper.Info($"[SSE] Connection {connectionId} from {clientHost} completed normally");
            }
            catch (OperationCanceledException)
            {
                LoggerHelper.Info($"[SSE] Connection {connectionId} from {clientHost} was cancelled");
                if (!string.IsNullOrEmpty(connectionId))
                {
                    _statsManager.CloseConnection(connectionId, "CLIENT_CANCELLED");
                }
            }
            catch (Exception ex)
            {
                LoggerHelper.Error($"[SSE] Error in stream for connection {connectionId} from {clientHost}", ex);
                
                _statsManager.RecordError(clientHost, connectionId ?? string.Empty, "SSE_STREAM_ERROR", ex.Message, ex);
                
                if (!string.IsNullOrEmpty(connectionId))
                {
                    _statsManager.CloseConnection(connectionId, $"ERROR: {ex.Message}");
                }
                
                if (!Response.HasStarted)
                {
                    Response.StatusCode = 500;
                    await Response.WriteAsync("Internal server error in SSE stream");
                }
            }
            finally
            {
                // Ensure connection is closed in statistics
                if (!string.IsNullOrEmpty(connectionId))
                {
                    _statsManager.CloseConnection(connectionId, "STREAM_ENDED");
                }
                
                LoggerHelper.Info($"[SSE] Exiting Stream endpoint for connection {connectionId} from {clientHost}");
            }
        }
    }
}
