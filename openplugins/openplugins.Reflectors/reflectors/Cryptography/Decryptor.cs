using ESB_ConnectionPoints.PluginsInterfaces;
using System;
using System.Collections.Generic;
using System.Text;

namespace openplugins.Reflectors
{
    internal class Decryptor : IReflector
    {
        IMessageSource messageSource;
        IMessageReplyHandler replyHandler;

        private readonly DecryptorSettings settings;
        private readonly ReflectorManager manager;
        private readonly CryptoTools cryptoTools;

        public Decryptor(DecryptorSettings settings, ReflectorManager manager)
        {
            this.settings = settings;
            this.manager = manager;

            if (settings.decodeKey)
            {
                cryptoTools = new CryptoTools(settings.rsa);
            }
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
            Message reflectMessage = manager._messageFactory.CreateMessage(message.Type);
            reflectMessage.ClassId = message.ClassId;
            reflectMessage.Properties = message.Properties;
            byte[] _key;

            try
            {
                if (settings.decodeKey)
                {
                    var encodedKey = Convert.FromBase64String(message.GetPropertyValue<string>("encodedKey"));
                    _key = cryptoTools.Decrypt_RSA(encodedKey);
                    manager.WriteLogString("Расшифрованный ключ: " + Encoding.UTF8.GetString(_key));
                }
                else
                {
                    _key = Encoding.UTF8.GetBytes(message.GetPropertyValue<string>("key"));
                }
                reflectMessage.Body = Encoding.UTF8.GetBytes(CryptoTools.DecryptStringFromBytes_Aes(message.Body, _key));
                replyHandler.HandleReplyMessage(reflectMessage);
                messageSource.CompletePeekLock(message.Id);
            }
            catch (Exception ex)
            {
                manager.WriteErrorString("Произошла ошибка при декодировании сообщения!", ex);
                messageSource.CompletePeekLock(message.Id, MessageHandlingError.RejectedMessage, ex.Message);
            }
        }
    }
}
