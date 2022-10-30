using ESB_ConnectionPoints.PluginsInterfaces;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.DirectoryServices;
using System.Threading;

namespace openplugins.ADIntegration
{
    internal class ADObjectsCreator : IStandartOutgoingConnectionPoint
    {
        private const int MillisecondsTimeout = 30000;
        private readonly ILogger _logger;
        private readonly IMessageFactory _messageFactory;
        private readonly bool _debugMode;

        private readonly string _adUser;
        private readonly string _adPwd;
        private readonly string _adPath;
        private readonly DirectoryEntry _de;

        Dictionary<string,Type> _deTypes;

        public ADObjectsCreator(JObject settings, IServiceLocator serviceLocator)
        {
            _logger = serviceLocator.GetLogger(GetType());
            _debugMode = (bool)settings["debug"];
            _messageFactory = serviceLocator.GetMessageFactory();

            _adUser = (string)settings["user"];
            _adPwd = (string)settings["password"];
            _adPath = (string)settings["path"];
            _de = new DirectoryEntry("LDAP://" + _adPath, _adUser, _adPwd);

            _deTypes = new Dictionary<string,Type>();
            _deTypes.Add("departmentnumber", typeof(string));
            _deTypes.Add("employeeid", typeof(string));
            _deTypes.Add("employeenumber", typeof(string));
            _deTypes.Add("givenname", typeof(string));
            _deTypes.Add("kadr-id", typeof(int));
            _deTypes.Add("name", typeof(string));
            _deTypes.Add("samaccountname", typeof(string));

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

        public void Run(IMessageSource messageSource, IMessageReplyHandler replyHandler, CancellationToken ct)
        {
            if (_debugMode)
            {
                WriteLogString("30 секунд до старта!");
                ct.WaitHandle.WaitOne(MillisecondsTimeout); // time to connect for debug
            }
            while (!ct.IsCancellationRequested)
            {
                Message message = messageSource.PeekLockMessage(ct, 1000);
                if (message != null)
                {
                    if (!message.Properties.ContainsKey("objectType"))
                    {
                        _logger.Error("Отсутствует обязательное свойство objectType", new MissingFieldException("message", "objectType"));
                        messageSource.CompletePeekLock(message.Id, MessageHandlingError.InvalidMessageFormat, "Отсутствует обязательное свойство objectType");
                        continue;
                    }
                    if (!message.Properties.ContainsKey("CN"))
                    {
                        _logger.Error("Отсутствует обязательное свойство CN", new MissingFieldException("message", "CN"));
                        messageSource.CompletePeekLock(message.Id, MessageHandlingError.InvalidMessageFormat, "Отсутствует обязательное свойство CN");
                        continue;
                    }
                    if (!message.Properties.ContainsKey("OU"))
                    {
                        _logger.Error("Отсутствует обязательное свойство OU", new MissingFieldException("message", "OU"));
                        messageSource.CompletePeekLock(message.Id, MessageHandlingError.InvalidMessageFormat, "Отсутствует обязательное свойство OU");
                        continue;
                    }
                    try
                    {
                        CreateADObject(message);
                        messageSource.CompletePeekLock(message.Id);
                    }catch(Exception ex)
                    {
                        messageSource.CompletePeekLock(message.Id, MessageHandlingError.UnknowError, ex.Message);
                    }
                }
                else
                {
                    ct.WaitHandle.WaitOne(5000);
                }
            }
        }

        private void CreateADObject(Message message)
        {
            string objOU = message.GetPropertyValue<string>("OU");
            string objCN = string.Format("CN={0},OU={1}", message.GetPropertyValue<string>("CN"), objOU);
            string objType = message.GetPropertyValue<string>("objectType");
            DirectoryEntry newObject;

            try
            {
                newObject = _de.Children.Find(objCN);
            }
            catch
            {
                newObject = _de.Children.Add(objCN, objType);
            }
            foreach (var property in message.Properties)
            {
                if (_deTypes.ContainsKey(property.Key))
                {
                    if (_deTypes[property.Key] == typeof(string))
                    {
                        newObject.Properties[property.Key].Value = message.GetPropertyValue<string>(property.Key);
                    }else if(_deTypes[property.Key] == typeof(int))
                    {
                        newObject.Properties[property.Key].Value = message.GetPropertyValue<int>(property.Key);
                    }
                    else
                    {
                        _logger.Info(string.Format("Свойство {0} не определено в маппинге", property.Key));
                    }
                }
            }
            newObject.CommitChanges();
            _de.CommitChanges();
        }

        private void WriteLogString(string log)
        {
            if (_debugMode)
            {
                _logger.Debug(log);
            }
        }
    }
}