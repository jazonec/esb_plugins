using Apache.NMS;
using Apache.NMS.ActiveMQ;
using Apache.NMS.ActiveMQ.Commands;
using System;

namespace openplugins.ActiveMQ
{
    internal class QueueConsumer : IDisposable
    {
        private readonly string queue;
        private readonly string brokerUri;
        private readonly string userName;
        private readonly string password;
        private readonly string clientId;
        private bool isDisposed = false;

        private IMessageConsumer consumer;
        private ISession session;
        private IConnection connection;

        public QueueConsumer(string queue, string brokerUri, string userName, string password, string clientId)
        {
            this.queue = queue;
            this.brokerUri = brokerUri;
            this.userName = userName;
            this.password = password;
            this.clientId = clientId;
        }

        private void OnMessage(IMessage message)
        {
            OnDebug?.Invoke("Прочитано сообщение: " + message.NMSMessageId);
            try
            {
                OnMessageReceived?.Invoke(message);
            }catch(Exception ex)
            {
                OnError?.Invoke("ERR!", ex);
                throw;
            }
        }

        public event MessageReceivedDelegate OnMessageReceived;
        public event OnDebug OnDebug;
        public event OnError OnError;
        public void Dispose()
        {
            OnDebug?.Invoke("Уничтожаем консюмера");
            if (!isDisposed)
            {
                consumer?.Dispose();
                session?.Dispose();
                connection?.Dispose();
                isDisposed = true;
            }
        }

        internal void Run()
        {
            if (brokerUri == null)
            {
                throw new ArgumentNullException("brokerUri", "Broker URI not defined");
            }
            OnDebug?.Invoke("brokerUri: " + brokerUri);
            try
            {
                IConnectionFactory connectionFactory = new ConnectionFactory(brokerUri);
                connection = connectionFactory.CreateConnection(userName, password);
                connection.ClientId = clientId;
                session = connection.CreateSession();
                OnDebug?.Invoke("Сессия создана");
                consumer = session.CreateConsumer(session.GetQueue(queue));
                consumer.Listener += new MessageListener(OnMessage);
                OnDebug?.Invoke("Консюмер добавлен");
                connection.Start();
                OnDebug?.Invoke("Соединение запущено");
            }
            catch (Exception ex)
            {
                OnError?.Invoke("Can not start!", ex);
            }
        }
    }
}
