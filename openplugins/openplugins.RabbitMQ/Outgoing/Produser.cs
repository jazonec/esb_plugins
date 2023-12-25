using RabbitMQ.Client;
using System.Threading.Channels;

namespace openplugins.RabbitMQ
{
    internal class Produser : RmqConnector
    {
        public Produser(RabbitMQSettings settings) : base(settings)
        {
        }

        internal IBasicProperties CreateBasicProperties()
        {
            return _channel.CreateBasicProperties();
        }

        internal void BasicPublish(string ExchangeName, string routingKey, IBasicProperties props, byte[] byteMessage)
        {
            _channel.BasicPublish(ExchangeName, routingKey, props, byteMessage);
        }
    }
}