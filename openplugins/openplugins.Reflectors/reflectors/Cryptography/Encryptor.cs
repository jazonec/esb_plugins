using ESB_ConnectionPoints.PluginsInterfaces;
using MongoDB.Bson;
using System;
using System.Collections.Generic;
using System.Text;

namespace openplugins.Reflectors
{
    internal class Encryptor : IReflector
    {
        IMessageSource messageSource;
        IMessageReplyHandler replyHandler;

        private readonly EncryptorSettings settings;
        private readonly ReflectorManager manager;
        private readonly CryptoTools cryptoTools;

        public Encryptor(EncryptorSettings settings, ReflectorManager manager)
        {
            this.manager = manager;
            this.settings = settings;

            if (settings.encodeKey)
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
            byte[] key;

            try
            {
                if (settings.createRandomKey)
                {
                    manager.WriteLogString("Создаю рандомный ключ");
                    key = CryptoTools.RandomString(settings.keyLenght);
                    reflectMessage.SetPropertyWithValue("key", Encoding.UTF8.GetString(key));
                }
                else
                {
                    string strKey = message.GetPropertyValue<string>("key");
                    key = Encoding.UTF8.GetBytes(strKey);
                }

                manager.WriteLogString("Кодирую тело сообщения");
                reflectMessage.Body = CryptoTools.EncryptStringToBytes_Aes(Encoding.UTF8.GetString(message.Body), key);

                if (settings.encodeKey)
                {
                    manager.WriteLogString("Кодирую ключ");
                    string encodedKey = Convert.ToBase64String(cryptoTools.Encrypt_RSA(key));
                    reflectMessage.SetPropertyWithValue("encodedKey", encodedKey);
                }
                replyHandler.HandleReplyMessage(reflectMessage);
                messageSource.CompletePeekLock(message.Id);
            }catch (Exception ex)
            {
                manager.WriteErrorString("Произошла ошибка при кодировании сообщения!", ex);
                messageSource.CompletePeekLock(message.Id, MessageHandlingError.RejectedMessage, ex.Message);
            }
        }
    }
}
