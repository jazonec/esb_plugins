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
                    SendResponse(replayObject, message, replyHandler);
                }
                else
                {
                    ct.WaitHandle.WaitOne(5000);
                }
            }
        }
        private void SendResponse(JObject replayObject, Message message, IMessageReplyHandler replyHandler)
        {
            if (message.GetPropertyValue("sync", false))
            {
                Message replayMessage = _messageFactory.CreateReplyMessage(message, message.Type + "_response");
                replayMessage.ClassId = message.ClassId;
                replayMessage.Properties = message.Properties;
                replayMessage.Body = Encoding.UTF8.GetBytes(replayObject.ToString());
                replyHandler.HandleReplyMessage(replayMessage);
            }
        }
        private JObject UpdateADObject(Message message)
        {
            DirectoryEntry entity;
            JObject serialized;
            entity = GetEntity(message);
            switch (entity.SchemaClassName)
            {
                case "user":
                    FillProperties(entity, message);
                    serialized = Utils.SerializeEntity(entity, settings.Fields.Keys.ToArray());
                    SetPassword(entity, message, serialized);
                    break;
                case "group":
                    //FillProperties(entity, message);
                    bool added = FillGroupMembers(entity, message);
                    serialized = Utils.SerializeGroup(entity, added);
                    break;
                default: throw new NotSupportedException(string.Format("Обработка класса {0} не поддерживается!", entity.SchemaClassName));
            }

            ldapConnection.De.CommitChanges();
            entity.Close();
            return serialized;
        }

        private bool FillGroupMembers(DirectoryEntry entity, Message message)
        {
            if (!message.Properties.ContainsKey("member"))
            {
                throw new ArgumentNullException("member", "Отсутствует обязательное свойство member"); // нет свойства, содержащего id изменяемой сущности
            };
            DirectoryEntry member = FindEntityByGuid(message.GetPropertyValue<string>("member"));
            bool mode = message.GetPropertyValue<bool>("enabled"); // true = add, false = delete
            if (mode)
            {
                return AddUserToGroup(group: entity, member: member);
            }
            else
            {
                return !RemoveUserFromGroup(group: entity, member: member);
            }
        }

        private bool RemoveUserFromGroup(DirectoryEntry group, DirectoryEntry member)
        {
            string userDN = member.Properties["distinguishedName"][0].ToString();
            string gpDN = group.Properties["distinguishedName"][0].ToString();
            if (member.Properties["memberOf"].Contains(gpDN))
            {
                group.Properties["member"].Remove(userDN);
                group.CommitChanges();
            }
            return true;
        }

        private bool AddUserToGroup(DirectoryEntry group, DirectoryEntry member)
        {
            string userDN = member.Properties["distinguishedName"][0].ToString();
            string gpDN = group.Properties["distinguishedName"][0].ToString();
            if (!member.Properties["memberOf"].Contains(gpDN))
            {
                group.Properties["member"].Add(userDN);
                group.CommitChanges();
            }
            return true;
        }

        private DirectoryEntry GetEntity(Message message)
        {
            DirectoryEntry entity;
            if (message.Properties.ContainsKey("guid"))
            {
                entity = FindEntityByGuid(message.GetPropertyValue<string>("guid"));
            }
            else
            {
                entity = CreateNew(message);
            }
            return entity;
        }

        private void SetPassword(DirectoryEntry entity, Message message, JObject serialized)
        {
            if (message.Properties.ContainsKey("password"))
            {
                try
                {
                    string newPassword = message.GetPropertyValue<string>("password");
                    entity.Invoke("SetPassword", new object[] { newPassword });
                    entity.Properties["pwdLastSet"].Value = 0;
                    entity.Properties["LockOutTime"].Value = 0;
                    entity.CommitChanges();
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
        }
        private DirectoryEntry FindEntityByGuid(string uuid)
        {
            using (DirectorySearcher ds = new DirectorySearcher
            {
                SearchRoot = ldapConnection.De,
                //Filter = string.Format(@"(&(ObjectCategory=user)(objectGuid={0}))", GetGuidSearchString(uuid))
                Filter = string.Format(@"(&(objectGuid={0}))", GetGuidSearchString(uuid))
            })
            {
                SearchResult result = ds.FindOne();
                if (result == null)
                {
                    string exceptionText = string.Format("Отсутствует объект с guid = '{0}'!", uuid);
                    throw new NullReferenceException(exceptionText);
                }

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

        private void FillProperties(DirectoryEntry entity, Message message)
        {
            // ToDo: нужен рефакторинг, написано "по-одинэсовски"
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
                            if (property.Key == "name" && message.Properties.ContainsKey("guid"))
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
                    else if (settings.Fields[property.Key] == AdTypes.ad_boolean)
                    {
                        bool newValue = message.GetPropertyValue<bool>(property.Key);
                        if (property.Key == "enabled")
                        {
                            if (newValue) { EnableUser(entity); }
                            else { DisableUser(entity); }
                        }
                        else
                        {
                            if (newValue != (bool)entity.Properties[property.Key].Value)
                            {
                                entity.Properties[property.Key].Value = newValue;
                                propList = propList + property.Key + ", ";
                            }
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

        private void DisableUser(DirectoryEntry entity)
        {
            int val = (int)entity.Properties["userAccountControl"].Value;
            entity.Properties["userAccountControl"].Value = val | 0x2;
        }

        private void EnableUser(DirectoryEntry entity)
        {
            int val = (int)entity.Properties["userAccountControl"].Value;
            entity.Properties["userAccountControl"].Value = val & ~0x2;
        }

        private DirectoryEntry CreateNew(Message message)
        {
            string _entryOU = GetOUByString(settings.DefaultOU);
            if (!message.Properties.ContainsKey("objectType"))
            {
                throw new ArgumentException("Отсутствует обязательное свойство для создания нового объекта", "objectType");
            }
            if (!message.Properties.ContainsKey("name"))
            {
                throw new ArgumentException("Отсутствует обязательное свойство для создания нового объекта", "name");
            }
            if (!message.Properties.ContainsKey("samaccountname"))
            {
                throw new ArgumentException("Отсутствует обязательное свойство для создания нового объекта", "samaccountname");
            }
            if (message.Properties.ContainsKey("ou"))
            {
                _entryOU = GetOUByString(message.GetPropertyValue<string>("ou"));
            }
            string objType = message.GetPropertyValue<string>("objectType");
            string objCN = string.Format("CN={0},{1}", message.GetPropertyValue<string>("name"), _entryOU);
            DirectoryEntry _newobject = ldapConnection.De.Children.Add(objCN, objType);
            _newobject.Properties["samaccountname"].Value = message.GetPropertyValue<string>("samaccountname");
            _newobject.CommitChanges();
            return _newobject;
        }

        private string GetOUByString(string entryOU)
        {
            string[] words = entryOU.Split(';');
            string result = "";
            foreach (string word in words)
            {
                if (string.IsNullOrEmpty(word)) { continue; }
                if (!string.IsNullOrEmpty(result)) { result += ","; }
                result += string.Format("OU={0}", word);
            }
            return result;
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