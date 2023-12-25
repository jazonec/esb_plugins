using RabbitMQ.Client.Events;
using RabbitMQ.Client;
using System;

namespace openplugins.RabbitMQ
{
    internal class QueueConsumer : RmqConnector
    {
        private readonly string _queueName;
        private EventingBasicConsumer _consumer;

        public event MessageReceivedDelegate OnMessageReceived;
        public event OnDebug OnDebug;
        public event OnError OnError;

        public QueueConsumer(RabbitMQSettings settings, string queueName) : base(settings)
        {
            _queueName = queueName;
        }

        internal void Run()
        {
            QueueDeclareOk resp = _channel.QueueDeclarePassive(_queueName);
            _consumer = new EventingBasicConsumer(_channel);
            _consumer.Received += OnReceived;
            _channel.BasicConsume(_queueName, false, _consumer);
            OnDebug.Invoke(message: $"Запущено подключение к очереди {_queueName}. Количество сообщений в ожидании: {resp.MessageCount}");
        }

        private void OnReceived(object sender, BasicDeliverEventArgs e)
        {
            OnDebug?.Invoke("Прочитано сообщение: " + e.BasicProperties.MessageId);
            try
            {
                OnMessageReceived.Invoke(e, _queueName);
                _channel.BasicAck(e.DeliveryTag, false);
            }
            catch (Exception ex)
            {
                OnError.Invoke("Ошибка при отправке сообщения в шину", ex);
            }
        }
    }
}