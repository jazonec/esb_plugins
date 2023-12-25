using ESB_ConnectionPoints.PluginsInterfaces;
using RabbitMQ.Client;
using System.Collections.Generic;
using System.Threading;
using System;

namespace openplugins.RabbitMQ
{
    internal class ProduserManager : IStandartOutgoingConnectionPoint, IEsbRmqManager
    {
        private readonly ILogger _logger;
        private readonly DebugSettings _debugMode;
        private readonly RabbitMQSettings _rabbitMQSettings;
        private Produser _produser;

        private bool hasError = false;
        private string errorMessage = "";

        public bool HasError => hasError;
        public string ErrorMessage => errorMessage;

        public ProduserManager(OutgoingSettings settings, IServiceLocator serviceLocator)
        {
            _logger = serviceLocator.GetLogger(GetType());
            _debugMode = settings.Debug;
            _rabbitMQSettings = settings.RmqServer;
        }

        public void Cleanup()
        {
        }

        public void Dispose()
        {
            _produser?.Dispose();
        }

        public void Initialize()
        {
        }

        public void Run(IMessageSource messageSource, IMessageReplyHandler replyHandler, CancellationToken ct)
        {
            if (_debugMode.DebugMode)
            {
                WriteLogString(String.Format("{0} секунд до старта!", _debugMode.StartDelay));
                ct.WaitHandle.WaitOne(_debugMode.StartDelay * 1000);
            }

            hasError = false;
            errorMessage = null;

            WriteLogString("Приступаю к инициализации продюсера.");
            _produser = new Produser(_rabbitMQSettings);
            WriteLogString("Работаем!");

            while (!ct.IsCancellationRequested)
            {
                if (hasError)
                {
                    _logger.Error(errorMessage);
                    break;
                }
                Message message = messageSource.PeekLockMessage(ct, 1000);
                if (message != null)
                {
                    try
                    {
                        byte[] messageBodyBytes = message.Body;
                        IBasicProperties props = GetBasicProperties(message);
                        string routingKey = GetRoutingKey(message);

                        _produser.BasicPublish(
                            ExchangeName: _rabbitMQSettings.ExchangeName,
                            routingKey: routingKey,
                            props: props,
                            byteMessage: messageBodyBytes);

                        WriteLogString($"Сообщение {message.Id} отправлено в точку {_rabbitMQSettings.ExchangeName} с ключом маршрутизации {routingKey}");

                        messageSource.CompletePeekLock(message.Id);
                    }
                    catch (Exception ex)
                    {
                        hasError = true;
                        errorMessage = ex.Message;
                        _logger.Error("Ошибка отправки сообщения в RabbitMQ", ex);
                        messageSource.AbandonPeekLock(message.Id);
                        _produser.Dispose();
                    }
                }
                else
                {
                    ct.WaitHandle.WaitOne(5000);
                }
            }

        }

        private string GetRoutingKey(Message message)
        {
            if (message.HasProperty("routingKey"))
            {
                return message.GetPropertyValue<string>("routingKey");
            }
            else
            {
                return $"ClassId.{message.ClassId}.Type.{message.Type}";
            }
        }

        private IBasicProperties GetBasicProperties(Message message)
        {
            IBasicProperties props = _produser.CreateBasicProperties();

            props.ContentType = message.HasProperty("ContentType") ? message.GetPropertyValue<string>("ContentType") : null;
            props.ContentEncoding = message.HasProperty("ContentEncoding") ? message.GetPropertyValue<string>("ContentEncoding") : null;
            props.DeliveryMode = (byte)(message.HasProperty("DeliveryMode") ? message.GetPropertyValue<int>("DeliveryMode") : 2);
            props.Headers = new Dictionary<string, object>
                        {
                            { "ESB_Id", message.Id.ToString() },
                            { "ESB_ClassId", message.ClassId },
                            { "ESB_Type", message.Type },
                            { "ESB_Source", message.Source },
                            { "ESB_CorrelationId", message.CorrelationId.ToString() },
                            { "ESB_CreationTime", message.CreationTime.ToString("o") }
                        };
            FillProperties(message, props);

            return props;
        }

        private void FillProperties(Message message, IBasicProperties props)
        {
            foreach (var property in message.Properties)
            {
                props.Headers.Add($"ESBProperty_{property.Key}", property.Value.ToString());
            }
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