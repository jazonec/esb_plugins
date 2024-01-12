using ESB_ConnectionPoints.PluginsInterfaces;
using System.Collections.Generic;

namespace openplugins.Reflectors
{
    internal class BlackHole : IReflector
    {
        IMessageSource messageSource;

        private readonly ReflectorManager manager;
        private readonly BlackHoleSettings settings;

        public BlackHole(BlackHoleSettings settings, ReflectorManager manager)
        {
            this.manager = manager;
            this.settings = settings;
        }
        public IMessageSource MessageSource { set => messageSource = value; }
        public IMessageReplyHandler ReplyHandler { set => _ = value; }
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
            manager.WriteLogString(string.Format("Отправляем сообщение {0} в корзину.", message.Id));
            messageSource.CompletePeekLock(message.Id);
        }
        public void Dispose()
        {
        }
    }
}