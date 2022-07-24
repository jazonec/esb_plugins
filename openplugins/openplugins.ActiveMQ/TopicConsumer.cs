using Apache.NMS;
using Apache.NMS.ActiveMQ;
using Apache.NMS.ActiveMQ.Commands;
using System;

namespace openplugins.ActiveMQ
{
    internal class TopicConsumer : IDisposable
    {
        private readonly string queueName = null;
        private readonly IConnectionFactory connectionFactory;
        private readonly IConnection connection;
        private readonly ISession session;
        private readonly IMessageConsumer consumer;
        private bool isDisposed = false;

        public TopicConsumer(string queue, string brokerUri, string userName, string password, string clientId)
        {
            queueName = queue;
            connectionFactory = new ConnectionFactory(brokerUri);
            connection = connectionFactory.CreateConnection(userName, password);
            connection.ClientId = clientId;
            connection.Start();
            session = connection.CreateSession();
            consumer = session.CreateConsumer(session.GetQueue(queue));
            consumer.Listener += new MessageListener(OnMessage);
        }

        private void OnMessage(IMessage message)
        {
            ITextMessage textMessage = message as ITextMessage;
            OnMessageReceived?.Invoke(textMessage.Text);
        }

        public event MessageReceivedDelegate OnMessageReceived;
        public void Dispose()
        {
            if (!isDisposed)
            {
                consumer.Dispose();
                session.Dispose();
                connection.Dispose();
                isDisposed = true;
            }
        }
    }
}
