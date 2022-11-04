using Apache.NMS;
using Apache.NMS.ActiveMQ;
using Apache.NMS.ActiveMQ.Commands;
using System;

namespace openplugins.ActiveMQ
{
    internal class QueueConsumer : IDisposable
    {
        private readonly string queue;
        private ConnectionPool connectionPool;
        private bool isDisposed = false;

        private IMessageConsumer consumer;
        private ISession session;
        private IConnection connection;

        public event MessageReceivedDelegate OnMessageReceived;
        public event OnDebug OnDebug;
        public event OnError OnError;

        public QueueConsumer(string queue, ConnectionPool connectionPool)
        {
            this.queue = queue;
            this.connectionPool = connectionPool;
            connection = connectionPool.GetConnection(queue);
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

        public void Dispose()
        {
            OnDebug?.Invoke("Уничтожаем консюмера");
            if (!isDisposed)
            {
                consumer?.Dispose();
                session?.Dispose();
                connection?.Dispose();
                connectionPool.ClearConnection(queue);
                isDisposed = true;
            }
        }

        internal void Run()
        {
            connection.Start();
            session = connection.CreateSession();
            OnDebug?.Invoke("Сессия создана");
            consumer = session.CreateConsumer(session.GetQueue(queue));
            consumer.Listener += new MessageListener(OnMessage);
            OnDebug?.Invoke("Консюмер добавлен");
            connection.Start();
            OnDebug?.Invoke("Соединение запущено");
        }
    }
}
