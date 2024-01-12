using ESB_ConnectionPoints.PluginsInterfaces;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace openplugins.Reflectors
{
    internal class UnbatchReflector : IReflector
    {
        IMessageSource messageSource;
        IMessageReplyHandler replyHandler;

        private readonly ReflectorManager manager;
        private readonly UnbatchSettings settings;

        public UnbatchReflector(UnbatchSettings settings, ReflectorManager manager)
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
            MatchCollection matches = Regex.Matches(Encoding.UTF8.GetString(message.Body), settings.pattern, RegexOptions.Singleline);

            manager.WriteLogString(string.Format("Для сообщения {0} Найдено {1} совпадений", message.Id, matches.Count));
            foreach (Match match in matches)
            {
                Group group = match.Groups[0];
                Message reflectMessage = manager._messageFactory.CreateMessage(settings.responseType);
                reflectMessage.ClassId = settings.responseClassId;
                reflectMessage.Body = Encoding.UTF8.GetBytes(group.Value);
                reflectMessage.Properties = message.Properties;
                replyHandler.HandleReplyMessage(reflectMessage);
            }
            messageSource.CompletePeekLock(message.Id);
        }
    }
}