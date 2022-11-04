using Apache.NMS;
using ESB_ConnectionPoints.PluginsInterfaces;
using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace openplugins.ActiveMQ
{
    internal delegate void MessageReceivedDelegate(IMessage amqMessage);
    internal delegate void OnDebug(string message);
    internal delegate void OnError(string message, Exception exception);
    internal class ConsumerManager : IStandartIngoingConnectionPoint
    {
        private readonly ILogger _logger;
        private readonly IMessageFactory _messageFactory;
        private IMessageHandler _messageHandler;

        private readonly bool _debugMode;

        private readonly List<string> queueList;
        private Dictionary<string, QueueConsumer> consumers;
        private ConnectionPool connectionPool;

        public ConsumerManager(JObject settings, IServiceLocator serviceLocator)
        {
            _logger = serviceLocator.GetLogger(GetType());
            _messageFactory = serviceLocator.GetMessageFactory();
            _debugMode = (bool)settings["debug"];

            queueList = new List<string>();
            queueList.Add((string)settings["queue"]);

            string brokerUri = (string)settings["brockerUri"];
            string user = (string)settings["user"];
            string password = (string)settings["password"];

            connectionPool = new ConnectionPool(brokerUri, user, password);
            connectionPool.OnError += new OnError(ErrorLog);
            connectionPool.OnDebug += new OnDebug(DebugLog);

            connectionPool.CheckConnection();
        }
        public void Cleanup()
        {
        }

        public void Dispose()
        {
            foreach(var consumer in consumers.Values)
            {
                consumer?.Dispose();
            }
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

            _messageHandler = messageHandler;

            MessageReceivedDelegate messageDelegate = new MessageReceivedDelegate(SendMessagetoESB);
            OnError errorDelegate = new OnError(ErrorLog);
            OnDebug debugDelegate = new OnDebug(DebugLog);

            WriteLogString("Приступаю к инициализации подписчиков к очередям.");
            foreach (var queue in queueList)
            {
                QueueConsumer consumer = new QueueConsumer(queue, connectionPool);
                consumer.OnMessageReceived += messageDelegate;
                consumer.OnDebug += debugDelegate;
                consumer.OnError += errorDelegate;
                consumer.Run();
                consumers.Add(queue, consumer);
            }

            WriteLogString("Подписчики инициализированы");

            while (!ct.IsCancellationRequested)
            {
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
        }

        private void SendMessagetoESB(IMessage amqMessage)
        {
            byte[] messageBody;
            if(amqMessage is ITextMessage)
            {
                ITextMessage textMessage = amqMessage as ITextMessage;
                messageBody = Encoding.UTF8.GetBytes(textMessage.Text);
            }else if(amqMessage is IBytesMessage)
            {
                IBytesMessage bytesMessage = amqMessage as IBytesMessage;
                messageBody = bytesMessage.Content;
            }else if(amqMessage is IMapMessage)
            {
                IMapMessage mapMessage = amqMessage as IMapMessage;
                messageBody = Encoding.UTF8.GetBytes(GetMapAsString(mapMessage.Body));
            }
            else
            {
                throw new Exception("Uncknown amq-message type");
            }
            WriteLogString("Получено сообщение: " + Encoding.UTF8.GetString(messageBody));

            Message esbMessage = _messageFactory.CreateMessage("AMQ_message");
            try
            {
                esbMessage.Body = messageBody;
                esbMessage.SetPropertyWithValue("NMSType", amqMessage.NMSType.ToString());
                esbMessage.SetPropertyWithValue("NMSTimeToLive", amqMessage.NMSTimeToLive.ToString());
                esbMessage.SetPropertyWithValue("NMSMessageId", amqMessage.NMSMessageId.ToString());
                esbMessage.SetPropertyWithValue("NMSTimestamp", amqMessage.NMSTimestamp.ToString());
                esbMessage.SetPropertyWithValue("NMSRedelivered", amqMessage.NMSRedelivered.ToString());
                esbMessage.SetPropertyWithValue("NMSPriority", amqMessage.NMSPriority.ToString());
                esbMessage.SetPropertyWithValue("NMSDestination", amqMessage.NMSDestination.ToString());
                esbMessage.SetPropertyWithValue("NMSDeliveryMode", amqMessage.NMSDeliveryMode.ToString());
                esbMessage.SetPropertyWithValue("NMSCorrelationID", amqMessage.NMSCorrelationID);

                foreach (string amqMessagePropertyKey in amqMessage.Properties.Keys)
                {
                    esbMessage.SetPropertyWithValue("prop_" + amqMessagePropertyKey, amqMessage.Properties[amqMessagePropertyKey].ToString());
                }
            }
            catch (Exception ex)
            {
                _logger.Error("Ошибка преобразования сообщения: " + ex.Message, ex);
                throw ex;
            }

            try
            {
                int fiveTimes = 5;
                while (!_messageHandler.HandleMessage(esbMessage))
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

        private string GetMapAsString(IPrimitiveMap body)
        {
            ICollection keys = body.Keys;
            JObject keyValuePairs = new JObject();
            foreach (var key in keys)
            {
                keyValuePairs[key.ToString()] = body.GetString(key.ToString());
            }
            return keyValuePairs.ToString();
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