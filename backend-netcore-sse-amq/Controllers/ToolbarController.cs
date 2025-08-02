using Microsoft.AspNetCore.Mvc;
using MyProject.Services;

namespace MyProject.Controllers
{
    /// <summary>
    /// ToolbarController provides a REST endpoint to receive messages from the Toolbar UI.
    /// It uses AMQPublisher to send messages to ActiveMQ.
    /// </summary>
    [ApiController]
    [Route("api/toolbar")]
    public class ToolbarController : ControllerBase
    {
        private readonly AMQPublisher _publisher;
        private readonly ConnectionStatisticsManager _statsManager;

        public ToolbarController(AMQPublisher publisher)
        {
            _publisher = publisher;
            _statsManager = ConnectionStatisticsManager.Instance;
        }

        /// <summary>
        /// DTO for receiving toolbar messages.
        /// </summary>
        public class ToolbarMessageDto
        {
            public string? id { get; set; }
            public string? text { get; set; }
            public string? broadcastGroup { get; set; }
            public string? broadcastGroup2 { get; set; }
        }

        /// <summary>
        /// Receives a POST request to send a message.
        /// Logs all major steps.
        /// </summary>
        [HttpPost("send")]
        public IActionResult SendMessage([FromBody] ToolbarMessageDto message)
        {
            var clientHost = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            
            try
            {
                LoggerHelper.Debug($"[TOOLBAR] Received SendMessage request from {clientHost}.");

                if (message == null || string.IsNullOrWhiteSpace(message.text))
                {
                    LoggerHelper.Warn($"[TOOLBAR] Message text is null or empty from {clientHost}.");
                    _statsManager.RecordError(clientHost, string.Empty, "INVALID_MESSAGE", "Message text is required");
                    return BadRequest("Message text is required.");
                }

                // Handle null properties
                string messageId = message.id ?? "default";
                string messageText = message.text;
                string broadcastGroup = message.broadcastGroup ?? string.Empty;
                string broadcastGroup2 = message.broadcastGroup2 ?? string.Empty;

                LoggerHelper.Info($"[TOOLBAR] Publishing message from {clientHost} - ID: {messageId}, BG1: {broadcastGroup}, BG2: {broadcastGroup2}");

                // Publish message with first broadcast group
                _publisher.PublishMessage(messageId, messageText, broadcastGroup);
                
                // If there's a second broadcast group, publish the message there too
                if (!string.IsNullOrEmpty(broadcastGroup2) && messageId == "broadcast")
                {
                    LoggerHelper.Info($"[TOOLBAR] Publishing message to second broadcast group: {broadcastGroup2}");
                    _publisher.PublishMessage(messageId, messageText, broadcastGroup2);
                }
                
                LoggerHelper.Info($"[TOOLBAR] Message processed and published from {clientHost}.");
                return Ok("Message published successfully.");
            }
            catch (Exception ex)
            {
                LoggerHelper.Error($"[TOOLBAR] Error processing message from {clientHost}", ex);
                _statsManager.RecordError(clientHost, string.Empty, "TOOLBAR_PUBLISH_ERROR", ex.Message, ex);
                return StatusCode(500, new { error = "Failed to publish message", message = ex.Message });
            }
        }
    }
}
