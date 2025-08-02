using Microsoft.AspNetCore.Mvc;
using System.Threading;
using System.Threading.Tasks;
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
                // Ensure broadcast groups are never null
                broadcastGroup = broadcastGroup ?? string.Empty;
                broadcastGroup2 = broadcastGroup2 ?? string.Empty;

                LoggerHelper.Info($"[SSE] Stream requested from {clientHost} - infoId: {id}, broadcastGroup: {(string.IsNullOrEmpty(broadcastGroup) ? "none" : broadcastGroup)}, broadcastGroup2: {(string.IsNullOrEmpty(broadcastGroup2) ? "none" : broadcastGroup2)}");
                
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

                // Start the consumer loop to stream messages.
                await _consumerSse.StartConsumerAsync(id, broadcastGroup, broadcastGroup2, Response, cancellationToken, connectionId);
                
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
