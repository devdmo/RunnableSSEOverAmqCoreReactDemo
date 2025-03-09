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
            public string id { get; set; }
            public string text { get; set; }
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

            _publisher.PublishMessage(message.id, message.text);
            LoggerHelper.Info("Message processed and published from Toolbar.");
            return Ok("Message published successfully.");
        }
    }
}
