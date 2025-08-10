using Apache.NMS;
using System;
using System.Collections.Generic;
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
        private readonly ConnectionStatisticsManager _statsManager;
        private readonly string queueName = "MyQueue";

        public AMQConsumerSse(AMQConnectionManager connectionManager)
        {
            _connectionManager = connectionManager;
            _statsManager = ConnectionStatisticsManager.Instance;
        }

        /// <summary>
        /// Starts a consumer for messages matching the provided infoId.
        /// Each received message is sent to the client as an SSE event.
        /// Can listen to up to two broadcast groups simultaneously.
        /// </summary>
        public async Task StartConsumerAsync(string infoId, string broadcastGroup, string broadcastGroup2, HttpResponse response, CancellationToken cancellationToken, string connectionId)
        {
            // Make sure broadcastGroups aren't null to avoid null reference exceptions
            broadcastGroup = broadcastGroup ?? string.Empty;
            broadcastGroup2 = broadcastGroup2 ?? string.Empty;

            LoggerHelper.Debug($"[AMQ-{connectionId}] Starting AMQConsumerSse for infoId: {infoId}, broadcastGroup: {(string.IsNullOrEmpty(broadcastGroup) ? "none" : broadcastGroup)}, broadcastGroup2: {(string.IsNullOrEmpty(broadcastGroup2) ? "none" : broadcastGroup2)}");
            var connection = _connectionManager.GetConnection();
            
            Apache.NMS.ISession? personalSession = null;
            Apache.NMS.ISession? broadcastSession = null;
            Apache.NMS.ISession? broadcastSession2 = null;
            IMessageConsumer? personalConsumer = null;
            IMessageConsumer? broadcastConsumer = null;
            IMessageConsumer? broadcastConsumer2 = null;
            
            try
            {
                personalSession = connection.CreateSession(AcknowledgementMode.Transactional);
                broadcastSession = connection.CreateSession(AcknowledgementMode.Transactional);
                broadcastSession2 = connection.CreateSession(AcknowledgementMode.Transactional);

                // Personal messages consumer from queue with selector for this infoId
                IDestination personalDestination = personalSession.GetQueue(queueName);
                string personalSelector = $"id = '{infoId}'";
                LoggerHelper.Debug($"Using JMS selector: {personalSelector}");
                personalConsumer = personalSession.CreateConsumer(personalDestination, personalSelector);

                // First broadcast consumer from topic with selector for first broadcast group if specified
                if (!string.IsNullOrEmpty(broadcastGroup))
                {
                    string broadcastSelector = $"broadcastGroup = '{broadcastGroup}'";
                    LoggerHelper.Debug($"Using broadcast JMS selector: {broadcastSelector}");
                    broadcastConsumer = broadcastSession.CreateConsumer(broadcastSession.GetTopic("MyBroadcastTopic"), broadcastSelector);
                }
                else
                {
                    LoggerHelper.Debug("No first broadcast group specified, receiving all broadcast messages");
                    broadcastConsumer = broadcastSession.CreateConsumer(broadcastSession.GetTopic("MyBroadcastTopic"));
                }

                // Second broadcast consumer from topic with selector for second broadcast group if specified
                if (!string.IsNullOrEmpty(broadcastGroup2))
                {
                    string broadcastSelector2 = $"broadcastGroup = '{broadcastGroup2}'";
                    LoggerHelper.Debug($"Using second broadcast JMS selector: {broadcastSelector2}");
                    broadcastConsumer2 = broadcastSession2.CreateConsumer(broadcastSession2.GetTopic("MyBroadcastTopic"), broadcastSelector2);
                }

                LoggerHelper.Debug("Entering AMQConsumerSse.StartConsumerAsync loop.");
                
                // ✅ CRITICAL: Add heartbeat task to detect dead connections
                var heartbeatTask = Task.Run(async () =>
                {
                    try
                    {
                        while (!cancellationToken.IsCancellationRequested)
                        {
                            await Task.Delay(TimeSpan.FromMinutes(5), cancellationToken); // Send heartbeat every 5 minutes
                            
                            // Send SSE heartbeat
                            string heartbeat = ": heartbeat\n\n";
                            byte[] heartbeatData = Encoding.UTF8.GetBytes(heartbeat);
                            await response.Body.WriteAsync(heartbeatData, 0, heartbeatData.Length, cancellationToken);
                            await response.Body.FlushAsync(cancellationToken);
                            
                            LoggerHelper.Debug($"[AMQ-{connectionId}] Heartbeat sent");
                            _statsManager.RecordMessage(connectionId); // Update last activity
                        }
                    }
                    catch (Exception ex)
                    {
                        LoggerHelper.Error($"[AMQ-{connectionId}] Error in heartbeat task", ex);
                        _statsManager.RecordError("localhost", connectionId, "HEARTBEAT_ERROR", ex.Message, ex);
                        throw;
                    }
                }, cancellationToken);
                
                var personalTask = Task.Run(async () =>
                {
                    try
                    {
                        while (!cancellationToken.IsCancellationRequested)
                        {
                            LoggerHelper.Debug($"[AMQ-{connectionId}] Waiting for personal message... Using personal selector: {personalSelector}");                                
                            IMessage msg = personalConsumer.Receive(TimeSpan.FromSeconds(10));
                            if (msg == null) continue;
                            LoggerHelper.Info($"[AMQ-{connectionId}] Personal message received: {msg}");
                            await ProcessMessageAsync(msg, response, personalSession, cancellationToken, connectionId);
                        }
                    }
                    catch (Exception ex)
                    {
                        LoggerHelper.Error($"[AMQ-{connectionId}] Error in personal consumer task", ex);
                        _statsManager.RecordError("localhost", connectionId, "PERSONAL_CONSUMER_ERROR", ex.Message, ex);
                        throw;
                    }
                }, cancellationToken);
                
                var broadcastTask = Task.Run(async () =>
                {
                    try
                    {
                        while (!cancellationToken.IsCancellationRequested)
                        {
                            LoggerHelper.Debug($"[AMQ-{connectionId}] Waiting for first broadcast message...");
                            IMessage msg = broadcastConsumer.Receive(TimeSpan.FromSeconds(10));
                            if (msg == null) continue;
                            LoggerHelper.Info($"[AMQ-{connectionId}] First broadcast message received: {msg}");
                            await ProcessMessageAsync(msg, response, broadcastSession, cancellationToken, connectionId);
                        }
                    }
                    catch (Exception ex)
                    {
                        LoggerHelper.Error($"[AMQ-{connectionId}] Error in first broadcast consumer task", ex);
                        _statsManager.RecordError("localhost", connectionId, "BROADCAST1_CONSUMER_ERROR", ex.Message, ex);
                        throw;
                    }
                }, cancellationToken);
                
                Task? broadcastTask2 = null;
                if (broadcastConsumer2 != null)
                {
                    broadcastTask2 = Task.Run(async () =>
                    {
                        try
                        {
                            while (!cancellationToken.IsCancellationRequested)
                            {
                                LoggerHelper.Debug($"[AMQ-{connectionId}] Waiting for second broadcast message...");
                                IMessage msg = broadcastConsumer2.Receive(TimeSpan.FromSeconds(10));
                                if (msg == null) continue;
                                LoggerHelper.Info($"[AMQ-{connectionId}] Second broadcast message received: {msg}");
                                await ProcessMessageAsync(msg, response, broadcastSession2, cancellationToken, connectionId);
                            }
                        }
                        catch (Exception ex)
                        {
                            LoggerHelper.Error($"[AMQ-{connectionId}] Error in second broadcast consumer task", ex);
                            _statsManager.RecordError("localhost", connectionId, "BROADCAST2_CONSUMER_ERROR", ex.Message, ex);
                            throw;
                        }
                    }, cancellationToken);
                }
                
                // Wait for all active tasks
                var tasks = new List<Task> { personalTask, broadcastTask, heartbeatTask };
                if (broadcastTask2 != null)
                {
                    tasks.Add(broadcastTask2);
                }
                
                await Task.WhenAll(tasks);
            }
            catch (Exception ex)
            {
                LoggerHelper.Error($"[AMQ-{connectionId}] Error in AMQConsumerSse.StartConsumerAsync", ex);
                _statsManager.RecordError("localhost", connectionId, "AMQ_CONSUMER_ERROR", ex.Message, ex);
                throw;
            }
            finally
            {
                // ✅ CRITICAL: Ensure proper disposal of all resources in correct order
                LoggerHelper.Info($"[AMQ-{connectionId}] Disposing AMQ resources...");
                
                try
                {
                    personalConsumer?.Close();
                    personalConsumer?.Dispose();
                }
                catch (Exception ex)
                {
                    LoggerHelper.Error($"[AMQ-{connectionId}] Error disposing personal consumer", ex);
                }
                
                try
                {
                    broadcastConsumer?.Close();
                    broadcastConsumer?.Dispose();
                }
                catch (Exception ex)
                {
                    LoggerHelper.Error($"[AMQ-{connectionId}] Error disposing broadcast consumer", ex);
                }
                
                try
                {
                    broadcastConsumer2?.Close();
                    broadcastConsumer2?.Dispose();
                }
                catch (Exception ex)
                {
                    LoggerHelper.Error($"[AMQ-{connectionId}] Error disposing broadcast consumer 2", ex);
                }
                
                try
                {
                    personalSession?.Close();
                    personalSession?.Dispose();
                }
                catch (Exception ex)
                {
                    LoggerHelper.Error($"[AMQ-{connectionId}] Error disposing personal session", ex);
                }
                
                try
                {
                    broadcastSession?.Close();
                    broadcastSession?.Dispose();
                }
                catch (Exception ex)
                {
                    LoggerHelper.Error($"[AMQ-{connectionId}] Error disposing broadcast session", ex);
                }
                
                try
                {
                    broadcastSession2?.Close();
                    broadcastSession2?.Dispose();
                }
                catch (Exception ex)
                {
                    LoggerHelper.Error($"[AMQ-{connectionId}] Error disposing broadcast session 2", ex);
                }
                
                LoggerHelper.Info($"[AMQ-{connectionId}] Exiting AMQConsumerSse.StartConsumerAsync loop.");
            }
        }

        private async Task ProcessMessageAsync(IMessage msg, HttpResponse response, Apache.NMS.ISession session, CancellationToken cancellationToken, string connectionId)
        {
            try
            {
                if (msg is ITextMessage textMsg)
                {
                    string body = textMsg.Text;
                    LoggerHelper.Info($"[AMQ-{connectionId}] Message received: {body}");
                    string sseMessage = $"data: {body}\n\n";
                    byte[] data = Encoding.UTF8.GetBytes(sseMessage);
                    await response.Body.WriteAsync(data, 0, data.Length, cancellationToken);
                    await response.Body.FlushAsync(cancellationToken);
                    LoggerHelper.Debug($"[AMQ-{connectionId}] SSE message written to response stream.");
                    session.Commit();
                    LoggerHelper.Debug($"[AMQ-{connectionId}] Session committed after processing message.");
                    
                    // Record message in statistics
                    _statsManager.RecordMessage(connectionId);
                }
                else
                {
                    session.Commit();
                    LoggerHelper.Warn($"[AMQ-{connectionId}] Non-text message received; session committed without processing.");
                }
            }
            catch (Exception ex)
            {
                LoggerHelper.Error($"[AMQ-{connectionId}] Error processing message", ex);
                _statsManager.RecordError("localhost", connectionId, "MESSAGE_PROCESSING_ERROR", ex.Message, ex);
                
                try
                {
                    session.Rollback();
                    LoggerHelper.Debug($"[AMQ-{connectionId}] Session rolled back due to error.");
                }
                catch (Exception rollbackEx)
                {
                    LoggerHelper.Error($"[AMQ-{connectionId}] Error during session rollback", rollbackEx);
                    _statsManager.RecordError("localhost", connectionId, "SESSION_ROLLBACK_ERROR", rollbackEx.Message, rollbackEx);
                }
                
                throw;
            }
        }
    }
}
