using Enyim.Caching;
using Enyim.Caching.Configuration;
using Enyim.Caching.Memcached;
using ESB_ConnectionPoints.PluginsInterfaces;
using Newtonsoft.Json.Linq;
using System;
using System.Text;
using System.Threading;

namespace openplugins.ReflectChangedMessage
{
    internal class MessageReflector : IStandartOutgoingConnectionPoint
    {
        private readonly ILogger _logger;
        private IMessageFactory _messageFactory;
        private readonly bool _debugMode;

        public void Cleanup()
        {
        }

        public void Dispose()
        {
        }

        public void Initialize()
        {
        }

        public MessageReflector(JObject settings, IServiceLocator serviceLocator)
        {
            _logger = serviceLocator.GetLogger(GetType());
            _debugMode = (bool)settings["debug"];
            _messageFactory = serviceLocator.GetMessageFactory();
        }

        public void Run(IMessageSource messageSource, IMessageReplyHandler replyHandler, CancellationToken ct)
        {
            _logger.Info(string.Format("Приступил к работе {0}", DateTime.Now.ToString()));
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
                    using (MessageHashChecker hashChecker = new MessageHashChecker(message))
                    {
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
    internal class MessageHashChecker : IDisposable
    {
        MemcachedClient _client;
        private string messageId;
        private string type;

        public string Hash { get; }

        public void Dispose()
        {
            _client?.Dispose();
        }
        public MessageHashChecker(Message message)
        {
            messageId = message.GetPropertyValue<string>("Id");
            type = message.Type;
            Hash = Encoding.UTF8.GetString(message.Body).GetHashCode().ToString();

            MemcachedClientConfiguration memConfig;
            memConfig = new MemcachedClientConfiguration();
            memConfig.AddServer("127.0.0.1:11211");
            _client = new MemcachedClient(memConfig);
        }

        public bool IsChanged()
        {
            string currentHash = _client.Get<string>(type + messageId);
            return currentHash != Hash;
        }
        public void SaveHash()
        {
            bool res = _client.Store(StoreMode.Set, type + messageId, Hash);
            if (!res)
            {
                throw new Exception("Не сохранили хэш");
            }
        }
    }
}