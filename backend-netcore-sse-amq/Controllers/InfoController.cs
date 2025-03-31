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

        public InfoController(AMQConsumerSse consumerSse)
        {
            _consumerSse = consumerSse;
        }

        /// <summary>
        /// SSE endpoint to stream messages for a specific infoId.
        /// The response is kept open and messages are sent in SSE format.
        /// Optional broadcastGroup parameter can be provided to filter broadcast messages.
        /// </summary>
        [HttpGet("stream")]
        public async Task Stream([FromQuery] string id, [FromQuery] string broadcastGroup, CancellationToken cancellationToken)
        {
            LoggerHelper.Info($"SSE stream requested for infoId: {id}, broadcastGroup: {broadcastGroup ?? "none"}");
            if (string.IsNullOrEmpty(id))
            {
                LoggerHelper.Warn("No infoId provided in query, defaulting to 'default'.");
                id = "default";
            }

            // Set the response header to use SSE content type.
            Response.Headers.Add("Content-Type", "text/event-stream");
            LoggerHelper.Debug("Response header set to text/event-stream.");

            // Start the consumer loop to stream messages.
            await _consumerSse.StartConsumerAsync(id, broadcastGroup, Response, cancellationToken);
            LoggerHelper.Info("Exiting Stream endpoint.");
        }
    }
}
