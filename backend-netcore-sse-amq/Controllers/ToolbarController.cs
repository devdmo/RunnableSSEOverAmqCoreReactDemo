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

        public ToolbarController(AMQPublisher publisher)
        {
            _publisher = publisher;
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
            LoggerHelper.Debug("Received SendMessage request from Toolbar.");

            if (message == null || string.IsNullOrWhiteSpace(message.text))
            {
                LoggerHelper.Warn("Message text is null or empty.");
                return BadRequest("Message text is required.");
            }

            // Handle null properties
            string messageId = message.id ?? "default";
            string messageText = message.text;
            string broadcastGroup = message.broadcastGroup ?? string.Empty;
            string broadcastGroup2 = message.broadcastGroup2 ?? string.Empty;

            // Publish message with first broadcast group
            _publisher.PublishMessage(messageId, messageText, broadcastGroup);
            
            // If there's a second broadcast group, publish the message there too
            if (!string.IsNullOrEmpty(broadcastGroup2) && messageId == "broadcast")
            {
                LoggerHelper.Info($"Publishing message to second broadcast group: {broadcastGroup2}");
                _publisher.PublishMessage(messageId, messageText, broadcastGroup2);
            }
            
            LoggerHelper.Info("Message processed and published from Toolbar.");
            return Ok("Message published successfully.");
        }
    }
}
