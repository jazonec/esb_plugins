using ESB_ConnectionPoints.PluginsInterfaces;
using Newtonsoft.Json.Linq;
using System;
using System.Threading;

namespace openplugins.ReflectChangedMessage
{
    internal class MessageReflector : IStandartOutgoingConnectionPoint
    {
        private readonly ILogger _logger;
        private readonly IMessageFactory _messageFactory;
        private readonly bool _debugMode;
        
        private readonly MessageHashChecker hashChecker;
        private readonly HashCheckerMode _mode;

        public void Cleanup()
        {
        }

        public void Dispose()
        {
            hashChecker.Dispose();
        }

        public void Initialize()
        {
        }

        public MessageReflector(JObject settings, IServiceLocator serviceLocator)
        {
            _logger = serviceLocator.GetLogger(GetType());
            _debugMode = (bool)settings["debug"];
            _messageFactory = serviceLocator.GetMessageFactory();
            string connectionString = null;
            if (settings.ContainsKey("connectionString"))
            {
                connectionString = (string)settings["connectionString"];
            }
            switch ((string)settings["mode"])
            {
                case "redis":
                    _mode = HashCheckerMode.Redis;
                    if (connectionString != null)
                    {
                        hashChecker = new MessageHashCheckerRedis(connectionString);
                    }
                    else
                    {
                        hashChecker = new MessageHashCheckerRedis();
                    }
                    break;
                case "mongo":
                    _mode = HashCheckerMode.MongoDB;
                    if (connectionString != null)
                    {
                        hashChecker = new MessageHashCheckerMongoDB(connectionString);
                    }
                    else
                    {
                        hashChecker = new MessageHashCheckerMongoDB();
                    }
                    break;
                default:
                    _mode = HashCheckerMode.MemCached;
                    if (connectionString != null)
                    {
                        hashChecker = new MessageHashCheckerMemcached(connectionString);
                    }
                    else
                    {
                        hashChecker = new MessageHashCheckerMemcached();
                    }
                    break;
            }
        }

        public void Run(IMessageSource messageSource, IMessageReplyHandler replyHandler, CancellationToken ct)
        {
            _logger.Info(string.Format("Приступил к работе {0}", DateTime.Now.ToString()));
            _logger.Info(string.Format("Режим хранения HASH: {0}", _mode.ToString()));
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    Message message = null;
                    message = messageSource.PeekLockMessage(ct, 1000);
                    if (message == null)
                    {
                        ct.WaitHandle.WaitOne(30000);
                        continue;
                    }
                    hashChecker.SetNewMessage(message);
                    if (hashChecker.IsChanged())
                    {
                        WriteLogString(string.Format("Reflect changed object, id={0}, hash={1}", message.GetPropertyValue<string>("Id"), hashChecker.Hash));
                        SendChangedMessage(message, replyHandler, ct);
                        hashChecker.SaveHash();
                    }
                    else
                    {
                        WriteLogString(string.Format("Object not change, id={0}, hash={1}", message.GetPropertyValue<string>("Id"), hashChecker.Hash));
                    }
                    messageSource.CompletePeekLock(message.Id);
                }
                catch (Exception ex)
                {
                    _logger.Error("Ошибка в потоке!", ex);
                }
            }
        }
        internal void SendChangedMessage(Message message, IMessageReplyHandler replyHandler, CancellationToken ct)
        {
            Message _responseMessage = _messageFactory.CreateMessage(message.Type + "_changed");
            _responseMessage.Body = message.Body;
            _responseMessage.ClassId = message.ClassId;
            _responseMessage.SetPropertyWithValue("originalId", message.Id.ToString());
            while (!replyHandler.HandleReplyMessage(_responseMessage))
            {
                ct.WaitHandle.WaitOne(500);
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
    internal enum HashCheckerMode
    {
        MemCached,
        Redis,
        MongoDB
    }
}