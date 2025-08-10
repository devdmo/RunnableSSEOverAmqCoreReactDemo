using Apache.NMS;
using Apache.NMS.AMQP; // Updated to use AMQP
using System;

namespace MyProject.Services
{
    /// <summary>
    /// AMQConnectionManager handles the creation and retrieval of a shared AMQP connection.
    /// This ensures a single connection is used across the application.
    /// </summary>
    public class AMQConnectionManager : IDisposable
    {
        private readonly string brokerUri = "amqp://localhost:5672/info-broker"; // Updated for info-broker
        private readonly string userName = "admin"; // Ensure this matches the broker's username
        private readonly string password = "admin"; // Ensure this matches the broker's password
        private IConnection? _connection;
        private readonly object _lock = new object();

        /// <summary>
        /// Returns a shared AMQP connection, creating one if necessary.
        /// </summary>
        public IConnection GetConnection()
        {
            LoggerHelper.Debug("Entering GetConnection method.");
            
            // 🚨 CHECKPOINT 3: Verificar conexión AMQ antes de usar
            if (_connection != null && !_connection.IsStarted)
            {
                LoggerHelper.Warn("AMQ connection exists but is not started. Recreating connection.");
                try
                {
                    _connection.Dispose();
                }
                catch (Exception ex)
                {
                    LoggerHelper.Error("Error disposing old AMQ connection", ex);
                }
                _connection = null;
            }
            
            if (_connection == null)
            {
                lock (_lock)
                {
                    if (_connection == null)
                    {
                        try
                        {
                            LoggerHelper.Info("Creating new AMQP connection...");
                            var factory = new NmsConnectionFactory(brokerUri);
                            
                            // 🚨 CHECKPOINT 4: Timeout para evitar bloqueos indefinidos
                            // factory.RequestTimeout = 30000; // 30 seconds in milliseconds (if supported)
                            
                            _connection = factory.CreateConnection(userName, password);
                            _connection.Start();
                            LoggerHelper.Info("AMQP connection successfully established.");
                        }
                        catch (Exception ex)
                        {
                            LoggerHelper.Fatal("Failed to create AMQP connection.", ex);
                            // 🚨 CHECKPOINT 5: Registrar error en estadísticas
                            ConnectionStatisticsManager.Instance.RecordError("localhost", string.Empty, "AMQ_CONNECTION_FAILED", ex.Message, ex);
                            throw;
                        }
                    }
                }
            }
            
            LoggerHelper.Debug("Exiting GetConnection method.");
            return _connection;
        }

        /// <summary>
        /// Properly disposes of the AMQ connection to prevent connection leaks.
        /// </summary>
        public void Dispose()
        {
            LoggerHelper.Debug("Disposing AMQConnectionManager...");
            
            lock (_lock)
            {
                if (_connection != null)
                {
                    try
                    {
                        LoggerHelper.Info("Closing AMQ connection...");
                        if (_connection.IsStarted)
                        {
                            _connection.Stop();
                        }
                        _connection.Close();
                        _connection.Dispose();
                        LoggerHelper.Info("AMQ connection disposed successfully.");
                    }
                    catch (Exception ex)
                    {
                        LoggerHelper.Error("Error disposing AMQ connection", ex);
                        ConnectionStatisticsManager.Instance.RecordError("localhost", string.Empty, "AMQ_DISPOSE_ERROR", ex.Message, ex);
                    }
                    finally
                    {
                        _connection = null;
                    }
                }
            }
        }
    }
}
