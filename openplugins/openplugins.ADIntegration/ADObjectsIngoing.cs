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
        private readonly bool _debugUsers;
        private readonly bool _debugGroups;

        private readonly int _delayMinutes;

        private readonly DirectoryEntry _de;
        private readonly string _adUser;
        private readonly string _adPwd;
        private readonly string _adPath;

        private readonly string[] _adUserFields;
        private readonly bool _usersWithGroups;

        private readonly string[] _adGroupFields;
        private readonly bool _groupWithMembers;

        public ADObjectsIngoing(JObject settings, IServiceLocator serviceLocator)
        {
            _logger = serviceLocator.GetLogger(GetType());
            _messageFactory = serviceLocator.GetMessageFactory();

            _debugMode = (bool)settings["debug"];
            _debugUsers = (bool)settings["debugusers"];
            _debugGroups = (bool)settings["debuggroups"];

            _adUser = (string)settings["user"];
            _adPwd = (string)settings["password"];
            _adPath = (string)settings["path"];
            _de = new DirectoryEntry("LDAP://" + _adPath, _adUser, _adPwd);

            string userFields = "objectSid,objectGuid,useraccountcontrol," + (string)settings["userfields"];
            _adUserFields = userFields.Split(',');
            _usersWithGroups = (bool)settings["addgroupstouser"];

            string groupFields = (string)settings["groupfields"];
            _adGroupFields = groupFields.Split(',');
            _groupWithMembers = (bool)settings["addmemberstogroups"];

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

                if (!_debugMode || (_debugMode && _debugUsers))
                {
                    SendADUsersToESB(messageHandler, ct);
                }
                if (!_debugMode || (_debugMode && _debugGroups))
                {
                    SendADGroupsToESB(messageHandler, ct);
                }

                WriteLogString("Done!");
            }
        }

        private void SendADGroupsToESB(IMessageHandler messageHandler, CancellationToken ct)
        {
            using (DirectorySearcher ds = new DirectorySearcher(_de, "(&(objectCategory=group))"))
            {
                ds.PageSize = 400;

                SearchResultCollection results = ds.FindAll();

                foreach (SearchResult sr in results)
                {
                    if (ct.IsCancellationRequested)
                    {
                        return; // выходим если шина пытается остановить адаптер
                    }

                    JObject _mesObj = new JObject
                    {
                        { "guid", GetAdObjectGuid(sr) },
                        { "sid", GetAdObjectSid(sr) },
                        { "whenchanged", ((DateTime)sr.Properties["whenChanged"][0]).ToString("s") }
                    };

                    foreach (string key in _adGroupFields)
                    {
                        if (key == "objectSid" || key == "objectGuid" || key == "whenChanged")
                        {
                            continue;
                        }
                        _mesObj.Add(key, sr.Properties[key].Count == 0 ? "" : sr.Properties[key][0].ToString());
                    }

                    if (_groupWithMembers)
                    {
                        _mesObj.Add("members", GetGroupsMembers(sr.GetDirectoryEntry()));
                    }

                    SendToESB(_mesObj, "ADGroup", messageHandler, ct);
                }
            }
        }

        private JObject GetGroupsMembers(DirectoryEntry objGroupEntry)
        {
            JArray _member = new JArray();
            JObject _members = new JObject();
            foreach (object objMember in objGroupEntry.Properties["member"])
            {
                _member.Add(objMember.ToString());
            }
            _members.Add("member", _member);
            return _members;
        }

        private void SendADUsersToESB(IMessageHandler messageHandler, CancellationToken ct)
        {
            using (DirectorySearcher ds = new DirectorySearcher(_de, "(&(objectCategory=User)(objectClass=person))", _adUserFields))
            {
                ds.PageSize = 400;
                SearchResultCollection results = ds.FindAll();

                foreach (SearchResult sr in results)
                {
                    if (ct.IsCancellationRequested)
                    {
                        return; // выходим если шина пытается остановить адаптер
                    }

                    if (sr.Properties["samAccountName"].Count == 0 ||
                        sr.Properties["objectSid"].Count == 0 ||
                        sr.Properties["objectGuid"].Count == 0
                       )
                    {
                        WriteLogString("Отсутствуют обязательные данные (один из samAccountName, SID, Guid)! " + sr.Path);
                        continue; // неполноценный объект в домене? Пусть админы посмотрят
                    }

                    JObject _mesObj = new JObject
                    {
                        { "sid", GetAdObjectSid(sr) },
                        { "guid", GetAdObjectGuid(sr) },
                        { "enabled", !Convert.ToBoolean((int)sr.Properties["useraccountcontrol"][0] & 0x0002) },
                        { "whenchanged", ((DateTime)sr.Properties["whenChanged"][0]).ToString("s") }
                    };

                    foreach (string key in _adUserFields)
                    {
                        if (key == "objectSid" || key == "objectGuid" || key == "userAccountСontrol" || key == "whenChanged")
                        {
                            continue;
                        }
                        _mesObj.Add(key, sr.Properties[key].Count == 0 ? "" : sr.Properties[key][0].ToString());
                    }

                    if (_usersWithGroups)
                    {
                        JObject _groups = GetUserGroups(sr.Properties["samAccountName"][0].ToString(), ct);
                        _mesObj.Add("groups", _groups);
                    }

                    SendToESB(_mesObj, "ADUser", messageHandler, ct);

                }
            }
        }

        private string GetAdObjectGuid(SearchResult sr)
        {
            byte[] uuid = (byte[])sr.Properties["objectGuid"][0];
            return new Guid(uuid).ToString();
        }

        private string GetAdObjectSid(SearchResult sr)
        {
            byte[] sid = (byte[])sr.Properties["objectSid"][0];
            return new SecurityIdentifier(sid, 0).ToString();
        }

        private JObject GetUserGroups(string samAccountName, CancellationToken ct)
        {
            JObject _groups = new JObject();

            using (PrincipalContext context = new PrincipalContext(ContextType.Domain, _adPath, _adUser, _adPwd))
            {
                UserPrincipal user = UserPrincipal.FindByIdentity(context, IdentityType.SamAccountName, samAccountName);

                PrincipalSearchResult<Principal> groups = user.GetAuthorizationGroups();
                JArray _groupLine = new JArray();
                foreach (Principal grp in groups)
                {
                    if (!ct.IsCancellationRequested)
                    {
                        JObject _group = new JObject
                        {
                            { "guid", grp.Guid },
                            { "sid", grp.Sid.ToString() },
                            { "name", grp.Name },
                            { "description", grp.Description },
                            { "samaccountname", grp.SamAccountName },
                            { "distinguishedname", grp.DistinguishedName }
                        };
                        _groupLine.Add(_group);
                    }
                    else
                        return new JObject();
                }
                _groups.Add("group", _groupLine);
            }
            return _groups;
        }

        private void SendToESB(JObject objectToSend, string objectType, IMessageHandler messageHandler, CancellationToken ct)
        {
            Message mes = _messageFactory.CreateMessage(objectType);
            mes.Body = Encoding.UTF8.GetBytes(objectToSend.ToString());
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
        private void WriteLogString(string log)
        {
            if (_debugMode)
            {
                _logger.Debug(log);
            }
        }
    }
}