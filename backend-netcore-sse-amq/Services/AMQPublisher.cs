using Apache.NMS;
using Newtonsoft.Json;
using System;

namespace MyProject.Services
{
    /// <summary>
    /// AMQPublisher publishes messages to the ActiveMQ queue.
    /// It serializes the payload to JSON and sets the JMS property "id" for filtering.
    /// </summary>
    public class AMQPublisher
    {
        private readonly AMQConnectionManager _connectionManager;
        private readonly string queueName = "MyQueue";

        public AMQPublisher(AMQConnectionManager connectionManager)
        {
            _connectionManager = connectionManager;
        }

        /// <summary>
        /// Publishes a message with the given infoId and message text.
        /// Logs detailed information at each step.
        /// </summary>
        public void PublishMessage(string infoId, string messageText)
        {
            LoggerHelper.Debug($"PublishMessage called with infoId: {infoId} and messageText: {messageText}");

            if (string.IsNullOrEmpty(infoId))
            {
                LoggerHelper.Warn("infoId is null or empty, defaulting to 'default'.");
                infoId = "default";
            }

            try
            {
                var connection = _connectionManager.GetConnection();
                using (var session = connection.CreateSession(AcknowledgementMode.AutoAcknowledge))
                {
                    LoggerHelper.Debug("ActiveMQ session created for publishing.");
                    IDestination destination = session.GetQueue(queueName);
                    using (var producer = session.CreateProducer(destination))
                    {
                        LoggerHelper.Info($"Producer created for queue: {queueName}");
                        var payload = new { id = infoId, text = messageText };
                        string json = JsonConvert.SerializeObject(payload);
                        LoggerHelper.Debug($"Serialized payload: {json}");
                        ITextMessage message = session.CreateTextMessage(json);
                        message.Properties.SetString("id", infoId);
                        LoggerHelper.Debug($"Set JMS property 'id' to: {infoId}");
                        producer.Send(message);
                        LoggerHelper.Info("Message published successfully to ActiveMQ.");
                    }
                }
            }
            catch (Exception ex)
            {
                LoggerHelper.Error("Error occurred while publishing message.", ex);
            }
        }
    }
}
