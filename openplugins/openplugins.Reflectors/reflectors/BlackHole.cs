using ESB_ConnectionPoints.PluginsInterfaces;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Linq;

namespace openplugins.Reflectors
{
    internal class BlackHole : IReflector
    {
        IMessageSource messageSource;

        private readonly IList<string> types = new List<string>();
        private readonly IList<string> classIDs = new List<string>();

        public BlackHole(JObject settings, IMessageFactory messageFactory)
        {
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
        }
        public IMessageSource MessageSource { set => messageSource = value; }
        public IMessageReplyHandler ReplyHandler { set => _ = value; }
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
            messageSource.CompletePeekLock(message.Id);
        }
        public void Dispose()
        {
        }
    }
}