using ESB_ConnectionPoints.PluginsInterfaces;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Xml.Linq;

namespace openplugins.Reflectors
{
    internal class ChangedReflector : IReflector
    {
        IMessageSource messageSource;
        IMessageReplyHandler replyHandler;

        private readonly ReflectorManager manager;
        private readonly IList<string> types = new List<string>();
        private readonly IList<string> classIDs = new List<string>();
        private readonly MessageHashChecker hashChecker;

        public ChangedReflector(JObject settings, ReflectorManager manager)
        {
            this.manager = manager;
            JArray typeArr = (JArray)settings["type"];
            if (typeArr != null)
            {
                foreach (string type in typeArr.Select(v => (string)v))
                {
                    types.Add(type);
                }
            }
            JArray classArr = (JArray)settings["classId"];
            if (classArr != null)
            {
                foreach (string classId in classArr.Select(v => (string)v))
                {
                    classIDs.Add(classId);
                }
            }
            string connectionString = null;
            if (settings.ContainsKey("connectionString"))
            {
                connectionString = (string)settings["connectionString"];
            }
            string checkerType = (string)settings["mode"];
            switch (checkerType)
            {
                case "redis":
                    hashChecker = new MessageHashCheckerRedis(connectionString);
                    break;
                case "mongo":
                    hashChecker = new MessageHashCheckerMongoDB(connectionString);
                    break;
                default:
                    hashChecker = new MessageHashCheckerMemcached(connectionString);
                    break;
            }
            manager.WriteLogString(string.Format("Подключен hashChecker, тип {0}, строка подключения {1}", checkerType, connectionString));
        }

        public IMessageSource MessageSource { set => messageSource = value; }
        public IMessageReplyHandler ReplyHandler { set => replyHandler = value; }
        public void Dispose()
        {
            hashChecker.Dispose();
        }

        public IList<string> GetClassIDs()
        {
            return classIDs;
        }

        public IList<string> GetTypes()
        {
            return types;
        }

        public void ProceedMessage(Message message)
        {
            System.Guid messageId = message.Id;
            manager.WriteLogString(string.Format("Начинаю проверку сообщения {0}", messageId));
            hashChecker.SetNewMessage(message);
            if (hashChecker.IsChanged())
            {
                manager.WriteLogString(string.Format("Отражаю измененный объект, id={0}, hash={1}", message.GetPropertyValue<string>("Id"), hashChecker.Hash));
                SendChangedMessage(message);
                hashChecker.SaveHash();
            }
            else
            {
                manager.WriteLogString(string.Format("Объект на изменился, id={0}, hash={1}", message.GetPropertyValue<string>("Id"), hashChecker.Hash));
            }
            messageSource.CompletePeekLock(message.Id);

        }
        internal void SendChangedMessage(Message message)
        {
            Message _responseMessage = manager._messageFactory.CreateMessage(message.Type + "_changed");
            _responseMessage.Body = message.Body;
            _responseMessage.ClassId = message.ClassId;
            _responseMessage.SetPropertyWithValue("originalId", message.Id.ToString());
            _responseMessage.SetPropertyWithValue("originalSource", message.Source);
            _responseMessage.SetPropertyWithValue("originalType", message.Type);
            replyHandler.HandleReplyMessage(_responseMessage);
        }
    }
}