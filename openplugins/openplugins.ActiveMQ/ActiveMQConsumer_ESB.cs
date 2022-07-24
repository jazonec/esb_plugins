using ESB_ConnectionPoints.PluginsInterfaces;
using Newtonsoft.Json.Linq;
using System.Threading;

namespace openplugins.ActiveMQ
{
    internal delegate void MessageReceivedDelegate(string message);
    internal class ActiveMQConsumer_ESB : IStandartIngoingConnectionPoint
    {
        private readonly ILogger _logger;
        private readonly IMessageFactory _messageFactory;
        private IMessageHandler _messageHandler;
        private readonly bool _debugMode;
        private readonly string _queueName;
        private readonly string _host;
        private readonly string _login;
        private readonly string _password;

        public ActiveMQConsumer_ESB(JObject settings, IServiceLocator serviceLocator)
        {
            _logger = serviceLocator.GetLogger(GetType());
            _messageFactory = serviceLocator.GetMessageFactory();
            _debugMode = (bool)settings["debug"];

            _queueName = (string)settings["queue"];
            _host = (string)settings["host"];
            _login = (string)settings["login"];
            _password = (string)settings["password"];
        }
        public void Cleanup()
        {
        }

        public void Dispose()
        {
        }

        public void Initialize()
        {
        }

        public void Run(IMessageHandler messageHandler, CancellationToken ct)
        {
            if (_debugMode)
            {
                WriteLogString("30 секунд до старта!");
                ct.WaitHandle.WaitOne(30000);
            }

            WriteLogString("Приступаю к инициализации подписчика к очереди " + _queueName);
            using (TopicConsumer topicConsumer = new TopicConsumer(_queueName, _host, _login, _password, "ActiveMQConsumer_ESB", "ActiveMQConsumer_ESB"))
            {
                topicConsumer.OnMessageReceived += new MessageReceivedDelegate(SendMessagetoESB);
                WriteLogString("Подписчик инициализиолван");
                while (!ct.IsCancellationRequested)
                {
                    ct.WaitHandle.WaitOne(5000);
                }
            }
        }

        private void SendMessagetoESB(string activeMQmessage)
        {
            WriteLogString("Получено сообщение: " + activeMQmessage);
        }

        private void WriteLogString(string log)
        {
            if (_debugMode)
            {
                _logger.Debug(log);
            }
        }
    }
}