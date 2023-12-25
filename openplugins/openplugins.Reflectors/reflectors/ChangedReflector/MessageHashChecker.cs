using ESB_ConnectionPoints.PluginsInterfaces;
using System.Security.Cryptography;
using System;

namespace openplugins.Reflectors
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
            byte[] tmpHash = new MD5CryptoServiceProvider().ComputeHash(message.Body);
            Hash = Convert.ToBase64String(tmpHash);
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