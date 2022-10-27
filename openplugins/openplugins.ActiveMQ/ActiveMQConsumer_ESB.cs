using Apache.NMS;
using ESB_ConnectionPoints.PluginsInterfaces;
using Newtonsoft.Json.Linq;
using System;
using System.Text;
using System.Threading;

namespace openplugins.ActiveMQ
{
    internal delegate void MessageReceivedDelegate(IMessage amqMessage);
    internal delegate void OnDebug(string message);
    internal delegate void OnError(string message, Exception exception);
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

            _messageHandler = messageHandler;
            WriteLogString("Приступаю к инициализации подписчика к очереди " + _queueName);
            using (QueueConsumer queueConsumer = new QueueConsumer(_queueName, _host, _login, _password, "ActiveMQConsumer_ESB"))
            {
                queueConsumer.OnMessageReceived += new MessageReceivedDelegate(SendMessagetoESB);
                queueConsumer.OnDebug += new OnDebug(DebugLog);
                queueConsumer.OnError += new OnError(ErrorLog);
                queueConsumer.Run();
                WriteLogString("Подписчик инициализирован");
                while (!ct.IsCancellationRequested)
                {
                    ct.WaitHandle.WaitOne(5000);
                }
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

        private void WriteLogString(string log)
        {
            if (_debugMode)
            {
                _logger.Debug(log);
            }
        }
    }
}