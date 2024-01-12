using ESB_ConnectionPoints.PluginsInterfaces;
using System.Collections.Generic;

namespace openplugins.Reflectors
{
    internal class ChangedReflector : IReflector
    {
        IMessageSource messageSource;
        IMessageReplyHandler replyHandler;

        private readonly ReflectorManager manager;
        private readonly ChangedSettings settings;
        private readonly MessageHashChecker hashChecker;

        public ChangedReflector(ChangedSettings settings, ReflectorManager manager)
        {
            this.manager = manager;
            this.settings = settings;

            switch (settings.mode)
            {
                case "redis":
                    hashChecker = new MessageHashCheckerRedis(settings.connectionString);
                    break;
                case "mongo":
                    hashChecker = new MessageHashCheckerMongoDB(settings.connectionString);
                    break;
                default:
                    hashChecker = new MessageHashCheckerMemcached(settings.connectionString);
                    break;
            }
            manager.WriteLogString(string.Format("Подключен hashChecker, тип {0}, строка подключения {1}", settings.mode, settings.connectionString));
        }

        public IMessageSource MessageSource { set => messageSource = value; }
        public IMessageReplyHandler ReplyHandler { set => replyHandler = value; }
        public void Dispose()
        {
            hashChecker.Dispose();
        }

        public IList<string> GetClassIDs()
        {
            return settings.classID;
        }

        public IList<string> GetTypes()
        {
            return settings.type;
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