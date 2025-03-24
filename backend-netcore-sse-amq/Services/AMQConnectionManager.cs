using Apache.NMS;
using Apache.NMS.AMQP; // Updated to use AMQP
using System;

namespace MyProject.Services
{
    /// <summary>
    /// AMQConnectionManager handles the creation and retrieval of a shared AMQP connection.
    /// This ensures a single connection is used across the application.
    /// </summary>
    public class AMQConnectionManager
    {
        private readonly string brokerUri = "amqp://localhost:5672/info-broker"; // Updated for info-broker
        private readonly string userName = "admin"; // Ensure this matches the broker's username
        private readonly string password = "admin"; // Ensure this matches the broker's password
        private IConnection _connection;
        private readonly object _lock = new object();

        /// <summary>
        /// Returns a shared AMQP connection, creating one if necessary.
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
                            LoggerHelper.Info("Creating new AMQP connection...");
                            var factory = new NmsConnectionFactory(brokerUri); // Updated factory for AMQP
                            _connection = factory.CreateConnection(userName, password);
                            _connection.Start();
                            LoggerHelper.Info("AMQP connection successfully established.");
                        }
                        catch (Exception ex)
                        {
                            LoggerHelper.Fatal("Failed to create AMQP connection.", ex);
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
