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

        private readonly IMessageFactory messageFactory;
        private readonly IList<string> types = new List<string>();
        private readonly IList<string> classIDs = new List<string>();
        private readonly string pattern;
        private readonly string responseType;
        private readonly string responseClassId;

        public UnbatchReflector(JObject settings, IMessageFactory messageFactory)
        {
            this.messageFactory = messageFactory;
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
                    types.Add(classId);
                }
            }

            pattern = (string)settings["pattern"];
            responseType = (string)settings["responseType"];
            responseClassId = (string)settings["responseClassId"];
        }

        public IMessageSource MessageSource { set => messageSource = value; }
        public IMessageReplyHandler ReplyHandler { set => replyHandler = value; }

        public void Dispose()
        {
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
            MatchCollection matches = Regex.Matches(Encoding.UTF8.GetString(message.Body), pattern, RegexOptions.Singleline);

            foreach (Match match in matches)
            {
                Group group = match.Groups[0];
                Message reflectMessage = messageFactory.CreateMessage(responseType);
                reflectMessage.ClassId = responseClassId;
                reflectMessage.Body = Encoding.UTF8.GetBytes(group.Value);
                reflectMessage.Properties = message.Properties;
                replyHandler.HandleReplyMessage(reflectMessage);
            }
            messageSource.CompletePeekLock(message.Id);
        }
    }
}