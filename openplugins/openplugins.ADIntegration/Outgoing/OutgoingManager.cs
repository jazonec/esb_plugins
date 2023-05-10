using ESB_ConnectionPoints.PluginsInterfaces;
using Newtonsoft.Json.Linq;
using System.DirectoryServices;
using System.Linq;
using System.Text;
using System.Threading;
using System;

namespace openplugins.ADIntegration
{
    internal class OutgoingManager : IStandartOutgoingConnectionPoint
    {
        private readonly bool _debugMode;
        private readonly OutgoingSettings settings;
        private readonly ILogger _logger;
        private readonly IMessageFactory _messageFactory;
        private readonly LdapConnection ldapConnection;

        public OutgoingManager(OutgoingSettings settings, IServiceLocator serviceLocator)
        {
            _logger = serviceLocator.GetLogger(GetType());
            _debugMode = settings.Debug.DebugMode;
            _messageFactory = serviceLocator.GetMessageFactory();

            ldapConnection = new LdapConnection(settings.Ldap);

            this.settings = settings;
        }
        public void Run(IMessageSource messageSource, IMessageReplyHandler replyHandler, CancellationToken ct)
        {
            if (_debugMode)
            {
                WriteLogString(string.Format("{0} секунд до старта!", settings.Debug.StartDelay));
                ct.WaitHandle.WaitOne(settings.Debug.StartDelay * 1000); // time to connect for debug
                WriteLogString("Работаем!");
            }
            while (!ct.IsCancellationRequested)
            {
                Message message = messageSource.PeekLockMessage(ct, 1000);
                if (message != null)
                {
                    JObject replayObject = new JObject();
                    try
                    {
                        replayObject = UpdateADObject(message);
                        messageSource.CompletePeekLock(message.Id);
                    }
                    catch (DirectoryServicesCOMException ex)
                    {
                        _logger.Error(ex.ExtendedErrorMessage, ex);
                        messageSource.CompletePeekLock(message.Id, MessageHandlingError.RejectedMessage, ex.ExtendedErrorMessage);
                        replayObject.Add("error", ex.ExtendedErrorMessage);
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(ex.Message, ex);
                        messageSource.CompletePeekLock(message.Id, MessageHandlingError.RejectedMessage, ex.Message);
                        replayObject.Add("error", ex.Message);
                    }
                    SendResponce(replayObject, message, replyHandler);
                }
                else
                {
                    ct.WaitHandle.WaitOne(5000);
                }
            }
        }
        private void SendResponce(JObject replayObject, Message message, IMessageReplyHandler replyHandler)
        {
            if (settings.ClassIdRequest == message.ClassId)
            {
                Message replayMessage = _messageFactory.CreateReplyMessage(message, message.Type + "_response");
                replayMessage.ClassId = settings.ClassIdResponse ?? message.ClassId;
                replayMessage.Properties = message.Properties;
                replayMessage.Body = Encoding.UTF8.GetBytes(replayObject.ToString());
                replyHandler.HandleReplyMessage(replayMessage);
            }
        }
        private JObject UpdateADObject(Message message)
        {
            DirectoryEntry entity;
            if (message.Properties.ContainsKey("guid"))
            {
                entity = FindEntityByGuid(message.GetPropertyValue<string>("guid"));
                FillProperties(entity, message, false);
            }
            else
            {
                entity = CreateNew(message);
                FillProperties(entity, message, true);
            }
            JObject serialized = Utils.SerializeEntity(entity, settings.Fields.Keys.ToArray());
            if (message.Properties.ContainsKey("password"))
            {
                try
                {
                    SetPassword(entity, message);
                    serialized.Add("PasswordSet", "true");
                    serialized.Add("Password", "Установлен стандартный пароль");
                }
                catch (Exception ex)
                {
                    _logger.Error("Не удалось установить пароль!", ex);
                    serialized.Add("PasswordSet", "false");
                    serialized.Add("Password", "Не удалось установить пароль!");
                }
            }
            ldapConnection.De.CommitChanges();
            entity.Close();
            return serialized;
        }
        private void SetPassword(DirectoryEntry entity, Message message)
        {
            string newPassword = message.GetPropertyValue<string>("password");
            entity.Invoke("SetPassword", new object[] { newPassword });
            entity.Properties["pwdLastSet"].Value = 0;
            entity.Properties["LockOutTime"].Value = 0;
            entity.CommitChanges();
        }
        private DirectoryEntry FindEntityByGuid(string uuid)
        {
            using (DirectorySearcher ds = new DirectorySearcher
            {
                SearchRoot = ldapConnection.De,
                Filter = string.Format(@"(&(ObjectCategory=user)(objectGuid={0}))", GetGuidSearchString(uuid))
            })
            {
                SearchResult result = ds.FindOne();
                return result.GetDirectoryEntry();
            }
        }
        private string GetGuidSearchString(string uuid)
        {
            Guid guid = new Guid(uuid);
            byte[] bytes = guid.ToByteArray();

            StringBuilder sb = new StringBuilder();
            foreach (byte b in bytes)
            {
                sb.Append(string.Format(@"\{0}", b.ToString("X")));
            }
            return sb.ToString();
        }

        private void FillProperties(DirectoryEntry entity, Message message, bool isNew)
        {
            string propList = "Изменяемые свойства: ";
            entity.UsePropertyCache = true;
            foreach (var property in message.Properties)
            {
                if (settings.Fields.ContainsKey(property.Key))
                {
                    if (settings.Fields[property.Key] == AdTypes.ad_string)
                    {
                        string newValue = message.GetPropertyValue<string>(property.Key);
                        if (newValue != (string)entity.Properties[property.Key].Value)
                        {
                            if (property.Key == "name" && isNew == false)
                            {
                                entity.Rename("CN=" + newValue);
                            }
                            else
                            {
                                entity.Properties[property.Key].Value = newValue;
                            }
                            propList = propList + property.Key + ", ";
                        }
                    }
                    else if (settings.Fields[property.Key] == AdTypes.ad_integer)
                    {
                        int newValue = message.GetPropertyValue<int>(property.Key);
                        if (newValue != (int)entity.Properties[property.Key].Value)
                        {
                            entity.Properties[property.Key].Value = newValue;
                            propList = propList + property.Key + ", ";
                        }
                    }
                    else
                    {
                        _logger.Info(string.Format("Свойство {0} не определено в маппинге", property.Key));
                    }
                }
            }
            WriteLogString(propList);
            entity.CommitChanges();
        }
        private DirectoryEntry CreateNew(Message message)
        {
            if (!message.Properties.ContainsKey("objectType"))
            {
                throw new ArgumentException("Отсутствует обязательное свойство для создания нового объекта", "objectType");
            }
            if (!message.Properties.ContainsKey("name"))
            {
                throw new ArgumentException("Отсутствует обязательное свойство для создания нового объекта", "name");
            }
            string objType = message.GetPropertyValue<string>("objectType");
            string objCN = string.Format("CN={0},OU={1}", message.GetPropertyValue<string>("name"), settings.DefaultOU);
            return ldapConnection.De.Children.Add(objCN, objType);
        }
        private void WriteLogString(string log)
        {
            if (_debugMode)
            {
                _logger.Debug(log);
            }
        }
        internal void WriteError(string v, Exception ex)
        {
            _logger?.Error(v, ex);
        }
        public void Cleanup()
        {
        }
        public void Dispose()
        {
        }
        public void Initialize()
        {
        }
    }

    internal enum AdTypes
    {
        ad_integer,
        ad_string,
        ad_boolean
    }
}