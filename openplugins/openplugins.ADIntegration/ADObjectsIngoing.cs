using ESB_ConnectionPoints.PluginsInterfaces;
using Newtonsoft.Json.Linq;
using System;
using System.DirectoryServices;
using System.DirectoryServices.AccountManagement;
using System.Security.Principal;
using System.Text;
using System.Threading;

namespace openplugins.ADIntegration
{
    internal class ADObjectsIngoing : IStandartIngoingConnectionPoint

    {
        private const int _oneMinuteTimeout = 60000;
        private readonly ILogger _logger;
        private readonly IMessageFactory _messageFactory;
        private readonly bool _debugMode;

        private readonly int _delayMinutes;

        private readonly string _adUser;
        private readonly string _adPwd;
        private readonly string _adPath;

        private readonly string[] _adFields;

        public ADObjectsIngoing(JObject settings, IServiceLocator serviceLocator)
        {
            _logger = serviceLocator.GetLogger(GetType());
            _messageFactory = serviceLocator.GetMessageFactory();
            _debugMode = (bool)settings["debug"];
            _adUser = (string)settings["user"];
            _adPwd = (string)settings["password"];
            _adPath = (string)settings["path"];
            //string fields = "objectSid,objectGuid," + (string)settings["fields"];
            string fields = (string)settings["fields"];
            _adFields = fields.Split(',');
            _delayMinutes = (int)settings["delay"] != 0 ? (int)settings["delay"] : 3600;
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

        public void Run(IMessageHandler messageHandler, CancellationToken ct)
        {
            if (_debugMode)
            {
                WriteLogString("Минута до старта!");
                ct.WaitHandle.WaitOne(_oneMinuteTimeout); // time to connect for debug
            }

            DateTime _oldTime = DateTime.Now;
            while (!ct.IsCancellationRequested)
            {
                if (_oldTime > DateTime.Now)
                {
                    ct.WaitHandle.WaitOne(_oneMinuteTimeout);
                    continue;
                }
                // Сработал тик, обновим метку времени
                WriteLogString(string.Format("Tick {0}", _oldTime.ToString()));
                _oldTime = DateTime.Now.AddMinutes(_delayMinutes);
                WriteLogString(string.Format("Next tick {0}", _oldTime.ToString()));

                SendADUsersToESB(messageHandler, ct);
            }
        }

        private void SendADUsersToESB(IMessageHandler messageHandler, CancellationToken ct)
        {
            DirectoryEntry de = new DirectoryEntry("LDAP://" + _adPath, _adUser, _adPwd);
            //DirectorySearcher ds = new DirectorySearcher(de, "(&(objectCategory=User)(objectClass=person))", _adFields);
            DirectorySearcher ds = new DirectorySearcher(de, "(&(objectCategory=User)(objectClass=person))");
            ds.PageSize = 400;

            SearchResultCollection results = ds.FindAll();

            foreach (SearchResult sr in results)
            {
                if (sr.Properties["samAccountName"].Count == 0) continue;

                JObject _mesObj = new JObject();

                using (PrincipalContext context = new PrincipalContext(ContextType.Domain, _adPath, _adUser, _adPwd))
                {
                    UserPrincipal user = UserPrincipal.FindByIdentity(context, IdentityType.SamAccountName, sr.Properties["samAccountName"][0].ToString());
                    _mesObj.Add("sid", user.Sid.ToString());
                    _mesObj.Add("guid", user.Guid.ToString());
                    _mesObj.Add("enabled", user.Enabled);
                    //
                }

                /*
                string sidStr;
                string uuidStr;

                if (sr.Properties["objectSid"].Count == 0)
                {
                    WriteLogString("Отсутствует SID! " + sr.Path);
                    continue;
                }
                else
                {
                    byte[] sid = (byte[])sr.Properties["objectSid"][0];
                    sidStr = new SecurityIdentifier(sid, 0).ToString();
                }

                if (sr.Properties["objectGuid"].Count == 0)
                {
                    WriteLogString("Отсутствует Guid! " + sr.Path);
                    continue;
                }
                else
                {
                    byte[] uuid = (byte[])sr.Properties["objectGuid"][0];
                    uuidStr = new Guid(uuid).ToString();
                }
                */

                foreach (string key in _adFields)
                {
                    /*if (key == "objectSid")
                    {
                        _mesObj.Add("sid", sidStr);
                        continue;
                    }
                    if (key == "objectGuid") {
                        _mesObj.Add("guid", uuidStr);
                        continue;
                    }*/
                    _mesObj.Add(key, sr.Properties[key].Count == 0 ? "" : sr.Properties[key][0].ToString());
                }

                Message mes = _messageFactory.CreateMessage("ADRecord");
                mes.Body = Encoding.UTF8.GetBytes(_mesObj.ToString());
                while (true)
                {
                    bool result = messageHandler.HandleMessage(mes);
                    if (result)
                    {
                        break;
                    }
                    ct.WaitHandle.WaitOne(1000);
                }
            }
            ds.Dispose();
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