using Enyim.Caching;
using Enyim.Caching.Configuration;
using Enyim.Caching.Memcached;
using ESB_ConnectionPoints.PluginsInterfaces;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using Newtonsoft.Json.Linq;
using StackExchange.Redis;
using System;
using System.Text;
using System.Threading;
using System.Linq;

namespace openplugins.ReflectChangedMessage
{
    internal class MessageReflector : IStandartOutgoingConnectionPoint
    {
        private readonly ILogger _logger;
        private readonly IMessageFactory _messageFactory;
        private readonly bool _debugMode;
        private MessageHashChecker hashChecker;

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
        }

        public void Run(IMessageSource messageSource, IMessageReplyHandler replyHandler, CancellationToken ct)
        {
            /*if (_debugMode)
            {
                WriteLogString("One minute pause...");
                ct.WaitHandle.WaitOne(60000);
            }*/

            hashChecker = new MessageHashChecker();
            _logger.Info(string.Format("Приступил к работе {0}", DateTime.Now.ToString()));
            _logger.Info(string.Format("Режим хранения HASH: {0}", hashChecker.GetMode().ToString()));
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
    internal class MessageHashChecker : IDisposable
    {
        private readonly MemcachedClient _memcach;

        private readonly IDatabase _redisDB;
        private readonly ConnectionMultiplexer _redis;

        private readonly MongoClient _mongo;
        private readonly IMongoDatabase _mongoDB;
        IMongoCollection<MessageHashMongo> _hashCollection;

        private readonly HashCheckerMode _mode;
        private string messageId;
        private string type;
        private string hash;

        public string Hash { get => hash; set => hash = value; }
        public HashCheckerMode GetMode()
        {
            return _mode;
        }

        public void Dispose()
        {
            _memcach?.Dispose();
            _redis?.Dispose();
        }
        public MessageHashChecker()
        {
            try
            {
                _redis = ConnectionMultiplexer.Connect("localhost");
                _redisDB = _redis.GetDatabase();
                _mode = HashCheckerMode.Redis;
                return;
            }
            catch (Exception)
            {
                // этого тоже нет
            }

            try
            {
                _mongo = new MongoClient("mongodb://localhost:27017");
                _mongoDB = _mongo.GetDatabase("HASH_TABLE");
                _mode = HashCheckerMode.MongoDB;
                return;
            }
            catch
            {
                // и mongo нет
            }

            try
            {
                MemcachedClientConfiguration memConfig;
                memConfig = new MemcachedClientConfiguration();
                memConfig.AddServer("localhost:11211");
                _memcach = new MemcachedClient(memConfig);
                _mode = HashCheckerMode.MemCached;
                return;
            }
            catch (Exception)
            {
                // этого нет...
            }

            throw new NotImplementedException("Варианты закончились...");
        }

        public void SetNewMessage(Message message)
        {
            messageId = message.GetPropertyValue<string>("Id");
            type = message.Type;
            Hash = Encoding.UTF8.GetString(message.Body).GetHashCode().ToString();
        }

        public bool IsChanged()
        {
            string currentHash = GetCurrentHashAsync();
            return currentHash != Hash;
        }
        public void SaveHash()
        {
            SetCurrentHash();
        }
        private string GetCurrentHashAsync()
        {
            switch (_mode)
            {
                case HashCheckerMode.Redis:
                    return _redisDB.StringGet(type + messageId);
                case HashCheckerMode.MemCached:
                    return _memcach.Get<string>(type + messageId);
                case HashCheckerMode.MongoDB:
                    _hashCollection = _mongoDB.GetCollection<MessageHashMongo>(type + "_message_hash");
                    try
                    {
                        MessageHashMongo ff = _hashCollection.Find(x => x.Id == messageId).Single();
                        return ff.Hash;
                    }
                    catch
                    {
                        return null;
                    }
            }
            return null;
        }
        private void SetCurrentHash()
        {
            switch (_mode)
            {
                case HashCheckerMode.Redis:
                    _redisDB.StringSet(type + messageId, Hash);
                    break;
                case HashCheckerMode.MemCached:
                    _memcach.Store(StoreMode.Set, type + messageId, Hash);
                    break;
                case HashCheckerMode.MongoDB:
                    _hashCollection = _mongoDB.GetCollection<MessageHashMongo>(type + "_message_hash");
                    _hashCollection.ReplaceOne(
                        x => x.Id == messageId,
                        new MessageHashMongo(messageId, type, Hash),
                        new UpdateOptions() { IsUpsert = true});
                    break;
            }
        }
    }
    internal enum HashCheckerMode
    {
        MemCached,
        Redis,
        MongoDB
    }
    internal class MessageHashMongo
    {
        [BsonId]
        [BsonRepresentation(BsonType.String)]
        public string Id { get; set; }
        [BsonElement("Type")]
        [BsonRepresentation(BsonType.String)]
        public string Type { get; set; }
        [BsonElement("Hash")]
        [BsonRepresentation(BsonType.String)]
        public string Hash { get; set; }

        public MessageHashMongo(string id, string type, string hash)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            Type = type ?? throw new ArgumentNullException(nameof(type));
            Hash = hash ?? throw new ArgumentNullException(nameof(hash));
        }
    }
}