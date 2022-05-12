using ESB_ConnectionPoints.PluginsInterfaces;
using System;
using System.Text;

namespace openplugins.ReflectChangedMessage
{
    abstract class MessageHashChecker : IDisposable
    {
        internal string messageId;
        internal string type;
        internal string hash;

        public string Hash { get => hash; set => hash = value; }
        public void Dispose()
        {
        }
        public void SetNewMessage(Message message)
        {
            messageId = message.GetPropertyValue<string>("Id");
            type = message.Type;
            Hash = Encoding.UTF8.GetString(message.Body).GetHashCode().ToString();
        }
        public bool IsChanged()
        {
            return Hash != GetCurrentHash();
        }
        public void SaveHash()
        {
            SetCurrentHash();
        }
        public abstract string GetCurrentHash();
        public abstract void SetCurrentHash();
    }
}