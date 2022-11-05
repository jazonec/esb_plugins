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

        private Dictionary<string, IConnection> connections = new Dictionary<string, IConnection>();

        private readonly IEsbAmqManager esbManager;

        public ConnectionPool(IEsbAmqManager esbManager, string brokerUri, string user, string password)
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
            this.esbManager = esbManager;
        }

        internal IConnection GetConnection(string name="esb")
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
                esbManager.WriteLogString("Создано соединение для " + name);
                return connection;
            }
        }

        private void Connection_ExceptionListener(Exception exception)
        {
            esbManager.SetError("Соединение вернуло ошибку! " + exception.Message);
        }

        private void Connection_ConnectionInterruptedListener()
        {
            esbManager.SetError("Соединение прервалось!");
        }

        internal void ClearConnection(string name="esb")
        {
            lock (connections)
            {
                var kv = connections.FirstOrDefault(x => x.Key == name);
                if (kv.Value != null)
                {
                    kv.Value.Dispose();
                }
                connections.Remove(name);
            }
        }
    }
}