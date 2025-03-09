using Apache.NMS;
using Apache.NMS.ActiveMQ;
using System;

namespace MyProject.Services
{
    /// <summary>
    /// AMQConnectionManager handles the creation and retrieval of a shared ActiveMQ connection.
    /// This ensures a single connection is used across the application.
    /// </summary>
    public class AMQConnectionManager
    {
        private readonly string brokerUri = "tcp://localhost:61616";
        private readonly string userName = "admin";
        private readonly string password = "admin";
        private IConnection _connection;
        private readonly object _lock = new object();

        /// <summary>
        /// Returns a shared ActiveMQ connection, creating one if necessary.
        /// </summary>
        public IConnection GetConnection()
        {
            LoggerHelper.Debug("Entering GetConnection method.");
            if (_connection == null)
            {
                lock (_lock)
                {
                    if (_connection == null)
                    {
                        try
                        {
                            LoggerHelper.Info("Creating new ActiveMQ connection...");
                            var factory = new ConnectionFactory(brokerUri);
                            _connection = factory.CreateConnection(userName, password);
                            _connection.Start();
                            LoggerHelper.Info("ActiveMQ connection successfully established.");
                        }
                        catch (Exception ex)
                        {
                            LoggerHelper.Fatal("Failed to create ActiveMQ connection.", ex);
                            throw;
                        }
                    }
                }
            }
            LoggerHelper.Debug("Exiting GetConnection method.");
            return _connection;
        }
    }
}
