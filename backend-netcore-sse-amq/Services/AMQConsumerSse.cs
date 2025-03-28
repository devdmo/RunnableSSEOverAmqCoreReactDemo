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
        public async Task StartConsumerAsync(string infoId, HttpResponse response, CancellationToken cancellationToken)
        {
            LoggerHelper.Debug($"Starting AMQConsumerSse for infoId: {infoId}");
            var connection = _connectionManager.GetConnection();
            using (var session = connection.CreateSession(AcknowledgementMode.Transactional))
            {
                // Personal messages consumer from queue with selector for this infoId
                IDestination personalDestination = session.GetQueue(queueName);
                string personalSelector = $"id = '{infoId}'";
                LoggerHelper.Debug($"Using JMS selector: {personalSelector}");
                using (var personalConsumer = session.CreateConsumer(personalDestination, personalSelector))
                // Broadcast consumer from topic (all subscribers receive these)
                using (var broadcastConsumer = session.CreateConsumer(session.GetTopic("MyBroadcastTopic")))
                {
                    LoggerHelper.Debug("Entering AMQConsumerSse.StartConsumerAsync loop.");
                    
                    // Create two tasks for personal and broadcast consumers.
                    var personalTask = Task.Run(async () =>
                    {
                        while (!cancellationToken.IsCancellationRequested)
                        {
                            LoggerHelper.Debug("Waiting for personal message...");
                            IMessage msg = personalConsumer.Receive(TimeSpan.FromSeconds(10));
                            if (msg == null)
                            {
                                continue;
                            }
                            await ProcessMessageAsync(msg, response, session, cancellationToken);
                        }
                    }, cancellationToken);
                    
                    var broadcastTask = Task.Run(async () =>
                    {
                        while (!cancellationToken.IsCancellationRequested)
                        {
                            LoggerHelper.Debug("Waiting for broadcast message...");
                            IMessage msg = broadcastConsumer.Receive(TimeSpan.FromSeconds(10));
                            if (msg == null)
                            {
                                continue;
                            }
                            await ProcessMessageAsync(msg, response, session, cancellationToken);
                        }
                    }, cancellationToken);
                    
                    await Task.WhenAll(personalTask, broadcastTask);
                }
            }
            LoggerHelper.Info("Exiting AMQConsumerSse.StartConsumerAsync loop.");
        }

        private async Task ProcessMessageAsync(IMessage msg, HttpResponse response, ISession session, CancellationToken cancellationToken)
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
