using ESB_ConnectionPoints.PluginsInterfaces;
using System;
using System.Collections.Generic;

namespace openplugins.Reflectors
{
    internal class MultiplyReflector : IReflector
    {
        IMessageSource messageSource;
        IMessageReplyHandler replyHandler;

        private readonly ReflectorManager manager;
        private readonly MultiplySettings settings;

        public MultiplyReflector(MultiplySettings settings, ReflectorManager manager)
        {
            this.manager = manager;
            this.settings = settings;
        }

        public IMessageSource MessageSource { set => messageSource = value; }
        public IMessageReplyHandler ReplyHandler { set => replyHandler = value; }

        public void Dispose()
        {
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
            manager.WriteLogString(String.Format("Умножаем сообщение {0}, количество {1}", message.Id, settings.reflectAmount));
            for (int i=0; i<settings.reflectAmount; i++)
            {
                Message reflectMessage = manager._messageFactory.CreateMessage(message.Type + "_reflect_" + (i + 1).ToString());
                reflectMessage.Body = message.Body;
                reflectMessage.Properties = message.Properties;
                replyHandler.HandleReplyMessage(reflectMessage);
            }
            messageSource.CompletePeekLock(message.Id);
        }
    }
}