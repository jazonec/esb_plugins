using Apache.NMS;
using Apache.NMS.ActiveMQ;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace openplugins.ActiveMQ
{
    internal class ConnectionPool
    {
        private string brokerUri;
        private string user;
        private string password;

        private static Dictionary<string, IConnection> connections = new Dictionary<string, IConnection>();

        public event OnDebug OnDebug;
        public event OnError OnError;

        public ConnectionPool(string brokerUri, string user, string password)
        {
            if (string.IsNullOrEmpty(brokerUri))
            {
                throw new ArgumentException("brokeruri null");
            }

            if (string.IsNullOrEmpty(user))
            {
                throw new ArgumentException("user null");
            }

            if (string.IsNullOrEmpty(password))
            {
                throw new ArgumentException("password null");
            }

            this.brokerUri = brokerUri;
            this.user = user;
            this.password = password;
        }

        internal IConnection GetConnection(string name)
        {
            lock (connections)
            {
                var kv = connections.FirstOrDefault(x => x.Key == name);
                if (kv.Value == null)
                    return Connection(name);
                return kv.Value;
            }
        }

        private IConnection Connection(string name)
        {
            lock (connections)
            {
                ConnectionFactory factory = new ConnectionFactory(brokerUri);
                IConnection connection = factory.CreateConnection(user, password);
                connection.ConnectionInterruptedListener += Connection_ConnectionInterruptedListener;
                connection.ExceptionListener += Connection_ExceptionListener;
                connection.ClientId = "DatareonESB_" + name;
                connections.Add(name, connection);
                OnDebug?.Invoke("Создано соединение для " + name);
                return connection;
            }
        }

        private void Reconnection()
        {
            lock (connections)
            {
                foreach (var item in connections)
                {
                    ConnectionFactory factory = new ConnectionFactory(brokerUri);
                    IConnection connection = factory.CreateConnection(user, password);

                    connection.ConnectionInterruptedListener += Connection_ConnectionInterruptedListener;
                    connection.ExceptionListener += Connection_ExceptionListener;
                    connections[item.Key] = connection;
                }
            }
        }

        private void Connection_ExceptionListener(Exception exception)
        {
            OnError("Соединение вернуло ошибку!", exception);
            Reconnection();
        }

        private void Connection_ConnectionInterruptedListener()
        {
            OnDebug("Соединение прервалось, пробую перепоключиться");
            Reconnection();
        }

        internal void CheckConnection()
        {
            var factory = new ConnectionFactory(brokerUri, "DatareonESB_check");
            using (IConnection conn = factory.CreateConnection(user, password))
            {
                conn.Start();
                OnDebug("Соединение проверено");
            }
        }

        internal void ClearConnection(string name)
        {
            lock (connections)
            {
                connections.Remove(name);
            }
        }
    }
}