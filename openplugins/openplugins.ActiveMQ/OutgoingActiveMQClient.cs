using ESB_ConnectionPoints.PluginsInterfaces;
using Newtonsoft.Json.Linq;
using System;
using System.Threading;
using Apache.NMS;
using Apache.NMS.ActiveMQ;
using System.Text;
using System.Collections.Generic;

namespace openplugins.ActiveMQ
{
    internal class OutgoingActiveMQClient : IStandartOutgoingConnectionPoint
    {
        private readonly ILogger _logger;
        private readonly bool _debugMode;
        private readonly string _host;
        private readonly string _login;
        private readonly string _password;
        private readonly string _queue;

        //private readonly Dictionary<string, string> _typeToQueue;

        Dictionary<string, IDestination> destinations;
        IConnection connection;
        ISession session;

        public OutgoingActiveMQClient(JObject settings, IServiceLocator serviceLocator)
        {
            //_typeToQueue = new Dictionary<string, string>();
            destinations = new Dictionary<string, IDestination>();

            _logger = serviceLocator.GetLogger(GetType());
            _debugMode = (bool)settings["debug"];
            _host = (string)settings["host"];
            _login = (string)settings["login"];
            _password = (string)settings["password"];
            _queue = (string)settings["queue"];

            /*JArray queues = (JArray)settings["mapping"];
            foreach (JObject queue in queues)
            {
                _typeToQueue.Add((string)queue["type"], (string)queue["queue"]);
            }*/

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
            /*foreach (KeyValuePair<string, string> typeToQueue in _typeToQueue)
            {
                destinations.Add(typeToQueue.Key, session.GetQueue(typeToQueue.Value));
            }*/
            WriteLogString(string.Format("Подключились к ActiveMQ: host'{0}'", _host));
        }

        public void Run(IMessageSource messageSource, IMessageReplyHandler replyHandler, CancellationToken ct)
        {
            if (_debugMode)
            {
                WriteLogString("30 секунд до старта!");
                ct.WaitHandle.WaitOne(30000);
            }

            _logger.Info("Запущена отправка сообщений в ActiveMQ в очередь " + _queue);
            IMessageProducer producer = session.CreateProducer();
            IQueue queue = session.GetQueue(_queue);

            while (!ct.IsCancellationRequested)
            {
                Message message = messageSource.PeekLockMessage(ct, 1000);
                if (message != null)
                {
                    /*if (!_typeToQueue.ContainsKey(message.Type))
                    {
                        var errorString = string.Format("Для типа {0} отсутствует настройка", message.Type);
                        _logger.Error(errorString);
                        messageSource.CompletePeekLock(message.Id, MessageHandlingError.RejectedMessage, errorString);
                        continue;
                    }*/

                    try
                    {
                        IBytesMessage amqMessage = session.CreateBytesMessage();
                        amqMessage.WriteBytes(message.Body);
                        amqMessage.Properties["OriginalID"] = message.Id.ToString();
                        amqMessage.NMSType = message.Type;
                        FillProperties(message, amqMessage);
                        producer.Send(queue, amqMessage);
                        WriteLogString(string.Format("Сообщение {0} отправлено в очередь {1}", message.Id, queue));

                        messageSource.CompletePeekLock(message.Id);
                    }catch (Exception ex){
                        _logger.Error("Ошибка отправки сообщения в ActiveMQ", ex);
                        messageSource.CompletePeekLock(message.Id, MessageHandlingError.RejectedMessage, ex.Message);
                    }
                }
                else
                {
                    ct.WaitHandle.WaitOne(5000);
                }
            }
            connection.Stop();
        }

        private void FillProperties(Message message, IBytesMessage amqMessage)
        {
            foreach (var property in message.Properties)
            {
                amqMessage.Properties[property.Key] = property.Value.ToString();
            }
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