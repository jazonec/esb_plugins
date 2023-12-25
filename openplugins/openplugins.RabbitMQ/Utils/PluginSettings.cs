using System.Collections.Generic;

namespace openplugins.RabbitMQ
{
    internal class IngoingSettings
    {
        public RabbitMQSettings RmqServer { set; get; }
        public DebugSettings Debug { set; get; }
        public List<string> Queues { set; get; }
    }
    internal class OutgoingSettings
    {
        public RabbitMQSettings RmqServer { set; get; }
        public DebugSettings Debug { get; set; }
    }
    internal class RabbitMQSettings
    {
        public RabbitMQSettings()
        {
            Port = 5672;
            HostName = "localhost";
            VirtualHost = "/";
        }

        public string HostName { set; get; }
        public int Port { set; get; }
        public string UserName { set; get; }
        public string Password { set; get; }
        public string VirtualHost { set; get; }
        public string ExchangeName { set; get; }
    }
    internal class DebugSettings
    {
        public DebugSettings()
        {
            DebugMode = false;
            StartDelay = 20;
        }

        public bool DebugMode { set; get; }
        public int StartDelay { set; get; }
    }
}