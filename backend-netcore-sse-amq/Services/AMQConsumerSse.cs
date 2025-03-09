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
                IDestination destination = session.GetQueue(queueName);
                string selector = $"id = '{infoId}'";
                LoggerHelper.Debug($"Using JMS selector: {selector}");
                using (var consumer = session.CreateConsumer(destination, selector))
                {
                    LoggerHelper.Info("Consumer created and listening for messages...");
                    while (!cancellationToken.IsCancellationRequested)
                    {
                        IMessage msg = consumer.Receive(TimeSpan.FromSeconds(1));
                        if (msg == null)
                        {
                            LoggerHelper.Debug("No message received in this cycle; waiting...");
                            await Task.Delay(500, cancellationToken);
                            continue;
                        }
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
            LoggerHelper.Info("Exiting AMQConsumerSse.StartConsumerAsync loop.");
        }
    }
}
