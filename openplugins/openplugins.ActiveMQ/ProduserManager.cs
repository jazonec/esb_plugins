using ESB_ConnectionPoints.PluginsInterfaces;
using Newtonsoft.Json.Linq;
using System;
using System.Threading;
using Apache.NMS;
using Apache.NMS.ActiveMQ;
using System.Text;
using System.Collections.Generic;
using Apache.NMS.ActiveMQ.Commands;
using Message = ESB_ConnectionPoints.PluginsInterfaces.Message;

namespace openplugins.ActiveMQ
{
    internal class ProduserManager : IStandartOutgoingConnectionPoint, IEsbAmqManager
    {
        private readonly ILogger _logger;
        private readonly bool _debugMode;

        private readonly string brokerUri;
        private readonly string user;
        private readonly string password;

        private readonly string queueName;

        private bool hasError = false;
        private string errorMessage = "";

        private ConnectionPool connectionPool;
        private IConnection connection;
        private ISession session;

        public ProduserManager(JObject settings, IServiceLocator serviceLocator)
        {
            _logger = serviceLocator.GetLogger(GetType());

            _debugMode = (bool)settings["debug"];

            brokerUri = (string)settings["brokerUri"];
            user = (string)settings["user"];
            password = (string)settings["password"];

            queueName = (string)settings["queue"];
        }

        public bool HasError { get => hasError; }
        public string ErrorMessage { get => errorMessage; }

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
            connectionPool = new ConnectionPool(this, brokerUri, user, password);
        }

        public void Run(IMessageSource messageSource, IMessageReplyHandler replyHandler, CancellationToken ct)
        {
            if (_debugMode)
            {
                WriteLogString("30 секунд до старта!");
                ct.WaitHandle.WaitOne(30000);
            }

            IMessageProducer producer = GetProducer(queueName);
            if (producer != null)
            {
                hasError = false;
                errorMessage = "";
            }

            while (!ct.IsCancellationRequested)
            {
                if (hasError)
                {
                    _logger.Error(errorMessage);
                    break;
                }
                ESB_ConnectionPoints.PluginsInterfaces.Message message = messageSource.PeekLockMessage(ct, 1000);
                if (message != null)
                {
                    try
                    {
                        IBytesMessage amqMessage = producer.CreateBytesMessage();
                        amqMessage.WriteBytes(message.Body);
                        amqMessage.Properties["ESB_Id"] = message.Id.ToString();
                        amqMessage.Properties["ESB_ClassId"] = message.ClassId;
                        amqMessage.Properties["ESB_Source"] = message.Source;
                        amqMessage.Properties["ESB_CorrelationId"] = message.CorrelationId.ToString();
                        amqMessage.Properties["ESB_CreationTime"] = message.CreationTime.ToString("o");
                        amqMessage.NMSType = message.Type;
                        FillProperties(message, amqMessage);
                        producer.Send(amqMessage);
                        WriteLogString(string.Format("Сообщение {0} отправлено в очередь {1}", message.Id, queueName));

                        messageSource.CompletePeekLock(message.Id);
                    }catch (Exception ex){
                        hasError = true;
                        errorMessage = ex.Message;
                        _logger.Error("Ошибка отправки сообщения в ActiveMQ", ex);
                        messageSource.AbandonPeekLock(message.Id);
                    }
                }
                else
                {
                    ct.WaitHandle.WaitOne(5000);
                }
            }
            connectionPool.ClearConnection("producer");
        }

        private IMessageProducer GetProducer(string queueName)
        {
            try
            {
                connection = connectionPool.GetConnection("producer");
                if (!connection.IsStarted)
                    connection.Start();
                WriteLogString("Соединение для продюссера запущено");
                session = connection.CreateSession();
                IQueue queue = session.GetQueue(queueName);
                WriteLogString("Сессия для продюссера создана");
                IMessageProducer producer = session.CreateProducer(queue);
                _logger.Info("Запущена отправка сообщений в ActiveMQ в очередь " + queueName);
                return producer;
            }
            catch (Exception ex)
            {
                hasError = true;
                errorMessage = ex.Message;
                connectionPool.ClearConnection("producer");
                return null;
            }
        }

        public void SetError(string error)
        {
            hasError = true;
            errorMessage = error;
        }

        private void FillProperties(Message message, IBytesMessage amqMessage)
        {
            foreach (var property in message.Properties)
            {
                amqMessage.Properties[property.Key] = property.Value.ToString();
            }
        }

        public void WriteLogString(string log)
        {
            if (_debugMode)
            {
                _logger.Debug(log);
            }
        }
    }
}