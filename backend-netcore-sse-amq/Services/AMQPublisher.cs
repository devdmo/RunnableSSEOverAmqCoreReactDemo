using Apache.NMS;
using Newtonsoft.Json;
using System;

namespace MyProject.Services
{
    /// <summary>
    /// AMQPublisher publishes messages to the ActiveMQ queue or topic.
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
        public void PublishMessage(string infoId, string messageText, string broadcastGroup = null)
        {
            // Make sure broadcastGroup isn't null to avoid null reference exceptions
            broadcastGroup = broadcastGroup ?? string.Empty;

            LoggerHelper.Debug($"PublishMessage called with infoId: {infoId}, messageText: {messageText}, broadcastGroup: {(string.IsNullOrEmpty(broadcastGroup) ? "none" : broadcastGroup)}");

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

                    // Determine destination: if broadcast, use topic; otherwise use queue.
                    IDestination destination;
                    if (infoId == "broadcast")
                    {
                        LoggerHelper.Info($"Publishing broadcast message with group: {(string.IsNullOrEmpty(broadcastGroup) ? "none" : broadcastGroup)}");
                        destination = session.GetTopic("MyBroadcastTopic");
                    }
                    else
                    {
                        destination = session.GetQueue(queueName);
                    }

                    using (var producer = session.CreateProducer(destination))
                    {
                        LoggerHelper.Info($"Producer created for destination: {(infoId == "broadcast" ? "MyBroadcastTopic" : queueName)}");
                        var payload = new { id = infoId, text = messageText };
                        string json = JsonConvert.SerializeObject(payload);
                        LoggerHelper.Debug($"Serialized payload: {json}");
                        ITextMessage message = session.CreateTextMessage(json);
                        message.Properties.SetString("id", infoId);
                        LoggerHelper.Debug($"Set JMS property 'id' to: {infoId}");
                        
                        // Set broadcast group as a property for topic selector if present
                        if (infoId == "broadcast" && !string.IsNullOrEmpty(broadcastGroup))
                        {
                            message.Properties.SetString("broadcastGroup", broadcastGroup);
                            LoggerHelper.Debug($"Set JMS property 'broadcastGroup' to: {broadcastGroup}");
                        }
                        
                        // Set expiration time for the message.
                        TimeSpan expiration = TimeSpan.FromSeconds(30);
                        producer.Send(message, MsgDeliveryMode.Persistent, MsgPriority.Normal, expiration);
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
