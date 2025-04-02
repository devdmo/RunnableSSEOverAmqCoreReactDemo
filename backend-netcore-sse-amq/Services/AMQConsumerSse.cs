using Apache.NMS;
using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace MyProject.Services
{
    /// <summary>
    /// AMQConsumerSse creates a consumer that listens to ActiveMQ messages filtered by "id"
    /// and writes them to the HTTP response in SSE format.
    /// </summary>
    public class AMQConsumerSse
    {
        private readonly AMQConnectionManager _connectionManager;
        private readonly string queueName = "MyQueue";

        public AMQConsumerSse(AMQConnectionManager connectionManager)
        {
            _connectionManager = connectionManager;
        }

        /// <summary>
        /// Starts a consumer for messages matching the provided infoId.
        /// Each received message is sent to the client as an SSE event.
        /// </summary>
        public async Task StartConsumerAsync(string infoId, string broadcastGroup, HttpResponse response, CancellationToken cancellationToken)
        {
            // Make sure broadcastGroup isn't null to avoid null reference exceptions
            broadcastGroup = broadcastGroup ?? string.Empty;

            LoggerHelper.Debug($"Starting AMQConsumerSse for infoId: {infoId}, broadcastGroup: {(string.IsNullOrEmpty(broadcastGroup) ? "none" : broadcastGroup)}");
            var connection = _connectionManager.GetConnection();
            using (var personalSession = connection.CreateSession(AcknowledgementMode.Transactional))
            using (var broadcastSession = connection.CreateSession(AcknowledgementMode.Transactional))
            {
                // Personal messages consumer from queue with selector for this infoId
                IDestination personalDestination = personalSession.GetQueue(queueName);
                string personalSelector = $"id = '{infoId}'";
                LoggerHelper.Debug($"Using JMS selector: {personalSelector}");
                using (var personalConsumer = personalSession.CreateConsumer(personalDestination, personalSelector))
                {
                    // Broadcast consumer from topic with selector for this broadcast group if specified
                    IMessageConsumer broadcastConsumer;
                    if (!string.IsNullOrEmpty(broadcastGroup))
                    {
                        string broadcastSelector = $"broadcastGroup = '{broadcastGroup}'";
                        LoggerHelper.Debug($"Using broadcast JMS selector: {broadcastSelector}");
                        broadcastConsumer = broadcastSession.CreateConsumer(broadcastSession.GetTopic("MyBroadcastTopic"), broadcastSelector);
                    }
                    else
                    {
                        LoggerHelper.Debug("No broadcast group specified, receiving all broadcast messages");
                        broadcastConsumer = broadcastSession.CreateConsumer(broadcastSession.GetTopic("MyBroadcastTopic"));
                    }

                    using (broadcastConsumer)
                    {
                        LoggerHelper.Debug("Entering AMQConsumerSse.StartConsumerAsync loop.");
                        
                        var personalTask = Task.Run(async () =>
                        {
                            while (!cancellationToken.IsCancellationRequested)
                            {
                                LoggerHelper.Debug("Waiting for personal message...");
                                IMessage msg = personalConsumer.Receive(TimeSpan.FromSeconds(10));
                                if (msg == null) continue;
                                await ProcessMessageAsync(msg, response, personalSession, cancellationToken);
                            }
                        }, cancellationToken);
                        
                        var broadcastTask = Task.Run(async () =>
                        {
                            while (!cancellationToken.IsCancellationRequested)
                            {
                                LoggerHelper.Debug("Waiting for broadcast message...");
                                IMessage msg = broadcastConsumer.Receive(TimeSpan.FromSeconds(10));
                                if (msg == null) continue;
                                await ProcessMessageAsync(msg, response, broadcastSession, cancellationToken);
                            }
                        }, cancellationToken);
                        
                        await Task.WhenAll(personalTask, broadcastTask);
                    }
                }
            }
            LoggerHelper.Info("Exiting AMQConsumerSse.StartConsumerAsync loop.");
        }

        private async Task ProcessMessageAsync(IMessage msg, HttpResponse response, Apache.NMS.ISession session, CancellationToken cancellationToken)
        {
            if (msg is ITextMessage textMsg)
            {
                string body = textMsg.Text;
                LoggerHelper.Info($"Message received: {body}");
                string sseMessage = $"data: {body}\n\n";
                byte[] data = Encoding.UTF8.GetBytes(sseMessage);
                await response.Body.WriteAsync(data, 0, data.Length, cancellationToken);
                await response.Body.FlushAsync(cancellationToken);
                LoggerHelper.Debug("SSE message written to response stream.");
                session.Commit();
                LoggerHelper.Debug("Session committed after processing message.");
            }
            else
            {
                session.Commit();
                LoggerHelper.Warn("Non-text message received; session committed without processing.");
            }
        }
    }
}
