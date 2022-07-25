using ESB_ConnectionPoints.PluginsInterfaces;
using Newtonsoft.Json.Linq;
using System;
using System.Threading;
using Apache.NMS;
using Apache.NMS.ActiveMQ;
using System.Text;

namespace openplugins.ActiveMQ
{
    internal class OutgoingActiveMQClient : IStandartOutgoingConnectionPoint
    {
        private readonly ILogger _logger;
        private readonly bool _debugMode;
        private readonly string _host;
        private readonly string _queue;
        private readonly string _login;
        private readonly string _password;

        IDestination destination;
        IConnection connection;
        ISession session;

        public OutgoingActiveMQClient(JObject settings, IServiceLocator serviceLocator)
        {
            _logger = serviceLocator.GetLogger(GetType());
            _debugMode = (bool)settings["debug"];
            _host = (string)settings["host"];
            _queue = (string)settings["queue"];
            _login = (string)settings["login"];
            _password = (string)settings["password"];
        }

        public void Cleanup()
        {
            connection?.Stop();
        }

        public void Dispose()
        {
            session?.Dispose();
            connection?.Dispose();
        }

        public void Initialize()
        {
            ConnectionFactory factory = new ConnectionFactory(_host);
            connection = factory.CreateConnection(_login, _password);
            connection.Start();
            session = connection.CreateSession();
            destination = session.GetQueue(_queue);
            WriteLogString(string.Format("Подключились к ActiveMQ: host'{0}' queue'{1}'", _host, _queue));
        }

        public void Run(IMessageSource messageSource, IMessageReplyHandler replyHandler, CancellationToken ct)
        {
            if (_debugMode)
            {
                WriteLogString("30 секунд до старта!");
                ct.WaitHandle.WaitOne(30000);
            }

            _logger.Info("Запущена отправка сообщений в ActiveMQ");
            IMessageProducer producer = session.CreateProducer(destination);

            while (!ct.IsCancellationRequested)
            {
                Message message = messageSource.PeekLockMessage(ct, 1000);
                if (message != null)
                {
                    ITextMessage amqMessage = session.CreateTextMessage();
                    amqMessage.Text = Encoding.UTF8.GetString(message.Body);
                    amqMessage.Properties["OriginalID"] = message.Id.ToString();
                    amqMessage.NMSType = message.Type;
                    producer.Send(amqMessage);

                    messageSource.CompletePeekLock(message.Id);
                }
                else
                {
                    ct.WaitHandle.WaitOne(5000);
                }
            }
            connection.Stop();
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