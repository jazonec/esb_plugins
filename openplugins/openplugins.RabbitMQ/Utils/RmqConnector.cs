using RabbitMQ.Client;
using System;

namespace openplugins.RabbitMQ
{
    internal class RmqConnector : IDisposable
    {
        internal IConnection _connection;
        internal IModel _channel;

        public RmqConnector(RabbitMQSettings settings)
        {
            ConnectionFactory connectionFactory = new ConnectionFactory
            {
                HostName = settings.HostName,
                Port = settings.Port,
                VirtualHost = settings.VirtualHost,
                ClientProvidedName = "ESB"
            };
            if (settings.UserName != "")
            {
                connectionFactory.UserName = settings.UserName;
            }
            if (settings.Password != "")
            {
                connectionFactory.Password = settings.Password;
            }
            _connection = connectionFactory.CreateConnection();
            _channel = _connection.CreateModel();
        }

        public void Dispose()
        {
            _channel?.Close();
            _connection?.Close();
        }
    }
}