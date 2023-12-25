using ESB_ConnectionPoints.PluginsInterfaces;
using RabbitMQ.Client.Events;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace openplugins.RabbitMQ
{
    internal delegate void MessageReceivedDelegate(BasicDeliverEventArgs rmqMessage, string queueName);
    internal delegate void OnDebug(string message);
    internal delegate void OnError(string message, Exception exception);
    internal class ConsumerManager : IStandartIngoingConnectionPoint, IEsbRmqManager
    {
        private readonly ILogger _logger;
        private readonly DebugSettings _debugMode;
        private readonly RabbitMQSettings _rabbitMQSettings;
        private readonly List<string> _queuesList = new List<string>();

        private bool hasError = false;
        private string errorMessage = "";
        private IMessageHandler _messageHandler;

        private Dictionary<string, QueueConsumer> _consumers;

        public bool HasError => hasError;
        public string ErrorMessage => errorMessage;

        public ConsumerManager(IngoingSettings settings, IServiceLocator serviceLocator)
        {
            _logger = serviceLocator.GetLogger(GetType());
            _debugMode = settings.Debug;
            _rabbitMQSettings = settings.RmqServer;
            _queuesList = settings.Queues;
            _consumers = new Dictionary<string, QueueConsumer>();
        }

        public void Cleanup()
        {
        }

        public void Dispose()
        {
            foreach (var consumer in _consumers.Values)
            {
                consumer?.Dispose();
            }
        }

        public void Initialize()
        {
        }

        public void Run(IMessageHandler messageHandler, CancellationToken ct)
        {
            if (_debugMode.DebugMode)
            {
                WriteLogString(string.Format("{0} секунд до старта!", _debugMode.StartDelay));
                ct.WaitHandle.WaitOne(_debugMode.StartDelay * 1000);
            }

            hasError = false;
            errorMessage = null;

            _messageHandler = messageHandler;

            MessageReceivedDelegate messageDelegate = new MessageReceivedDelegate(SendMessagetoESB);
            OnError errorDelegate = new OnError(ErrorLog);
            OnDebug debugDelegate = new OnDebug(DebugLog);

            WriteLogString("Приступаю к инициализации подписчиков к очередям.");
            foreach (string queueName in _queuesList)
            {
                QueueConsumer consumer = new QueueConsumer(_rabbitMQSettings, queueName);
                consumer.OnError += errorDelegate;
                consumer.OnDebug += debugDelegate;
                consumer.OnMessageReceived += messageDelegate;
                consumer.Run();
                _consumers.Add(queueName, consumer);
            }

            while (!ct.IsCancellationRequested)
            {
                if (hasError)
                {
                    _logger.Error(errorMessage);
                    break;
                }
                ct.WaitHandle.WaitOne(5000);
            }
        }

        private void DebugLog(string logMessage)
        {
            WriteLogString(logMessage);
        }

        private void ErrorLog(string errorMessage, Exception exception)
        {
            _logger.Error(errorMessage, exception);
            SetError(errorMessage);
        }

        private void SendMessagetoESB(BasicDeliverEventArgs rmqMessage, string queueName)
        {
            Message message = new Message();
            message.SetPropertyWithValue("RMQ_QueueName", queueName);
            FillBasicProperties(message, rmqMessage);
            FillHeaders(message, rmqMessage);
            HandleMessage(message);
        }

        private void HandleMessage(Message message)
        {
            try
            {
                int fiveTimes = 5;
                while (!_messageHandler.HandleMessage(message))
                {
                    Thread.Sleep(1000);
                    fiveTimes--;
                    if (fiveTimes == 0)
                    {
                        throw new Exception("Не удалось отправить сообщение в шину");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error("Не удалось отправить сообщение в шину: " + ex.Message, ex);
                throw ex;
            }
        }

        private void FillHeaders(Message message, BasicDeliverEventArgs rmqMessage)
        {
            if (rmqMessage.BasicProperties.Headers != null)
            {
                foreach (KeyValuePair<string, object> item in rmqMessage.BasicProperties.Headers)
                {
                    message.SetPropertyWithValue("RMQHeader_" + item.Key, Encoding.UTF8.GetString((byte[])item.Value));
                }
            }
        }

        private void FillBasicProperties(Message message, BasicDeliverEventArgs rmqMessage)
        {
            message.Body = rmqMessage.Body.ToArray();
            message.Id = Guid.NewGuid();
            message.SetPropertyWithValue("RMQ_RoutingKey", rmqMessage.RoutingKey ?? "");
            message.SetPropertyWithValue("RMQ_Exchange", rmqMessage.Exchange);
            message.SetPropertyWithValue("RMQ_MessageId", rmqMessage.BasicProperties.MessageId ?? "");
            message.SetPropertyWithValue("RMQ_ContentType", rmqMessage.BasicProperties.ContentType ?? "");
            message.SetPropertyWithValue("RMQ_ContentEncoding", rmqMessage.BasicProperties.ContentEncoding ?? "");
        }

        public void SetError(string error)
        {
            hasError = true;
            errorMessage = error;
        }

        public void WriteLogString(string log)
        {
            if (_debugMode.DebugMode)
            {
                _logger.Debug(log);
            }
        }
    }
}