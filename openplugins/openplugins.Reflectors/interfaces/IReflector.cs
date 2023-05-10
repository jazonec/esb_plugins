using ESB_ConnectionPoints.PluginsInterfaces;
using System.Collections.Generic;
using System;

namespace openplugins.Reflectors
{
    internal interface IReflector : IDisposable
    {
        IMessageSource MessageSource { set; }
        IMessageReplyHandler ReplyHandler { set; }
        void ProceedMessage(Message message);
        IList<string> GetTypes();
        IList<string> GetClassIDs();
    }
}