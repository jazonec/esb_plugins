using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.DirectoryServices;
using System.Security.Principal;
using System;

namespace openplugins.ADIntegration
{
    internal class Utils
    {
        private static string GetAdObjectGuid(SearchResult sr)
        {
            byte[] uuid = (byte[])sr.Properties["objectGuid"][0];
            return new Guid(uuid).ToString();
        }
        private static string GetAdObjectSid(SearchResult sr)
        {
            byte[] sid = (byte[])sr.Properties["objectSid"][0];
            return new SecurityIdentifier(sid, 0).ToString();
        }
        private static string GetAdObjectGuid(DirectoryEntry sr)
        {
            byte[] uuid = (byte[])sr.Properties["objectGuid"][0];
            return new Guid(uuid).ToString();
        }
        private static string GetAdObjectSid(DirectoryEntry sr)
        {
            byte[] sid = (byte[])sr.Properties["objectSid"][0];
            return new SecurityIdentifier(sid, 0).ToString();
        }
        internal static JObject SerializeEntity(DirectoryEntry entity, string[] fields)
        {
            JObject _retVal = Serialize(entity, fields);
            return _retVal;
        }
        internal static JObject SerializeUser(SearchResult sr, string[] fields)
        {
            JObject _retVal = Serialize(sr, fields);
            _retVal.Add("members", GetUserGroups(sr.Properties["samAccountName"][0].ToString()));
            return _retVal;
        }
        internal static JObject SerializeGroup(SearchResult sr, string[] fields, List<SearchResult> members)
        {
            JObject _retVal = Serialize(sr, fields);
            _retVal.Add("members", SerializeList(members, "member", fields: new string[] { "name" }));
            return _retVal;
        }
        private static JObject SerializeList(List<SearchResult> list, string elementName, string[] fields)
        {
            JArray _lines = new JArray();
            JObject _arr = new JObject();
            foreach (SearchResult sr in list)
            {
                _lines.Add(Serialize(sr, fields));
            }
            _arr.Add(elementName, _lines);
            return _arr;
        }
        private static JObject Serialize(DirectoryEntry entity, string[] fields)
        {
            JObject _resultObject = new JObject
                    {
                        { "guid", GetAdObjectGuid(entity) },
                        { "sid", GetAdObjectSid(entity) }
                    };

            foreach (string key in fields)
            {
                switch (key)
                {
                    case "objectSid":
                    case "objectGuid":
                        break;
                    case "useraccountcontrol":
                        _resultObject.Add("enabled", !Convert.ToBoolean((int)entity.Properties["useraccountcontrol"][0] & 0x0002));
                        break;
                    case "whenChanged":
                    case "whenCreated":
                        // date fields
                        _resultObject.Add(key.ToLower(), entity.Properties[key].Count == 0 ? "" : ((DateTime)entity.Properties[key][0]).ToString("s"));
                        break;
                    case "lastLogon":
                        var lastLogonTime = DateTime.FromFileTime(entity.Properties[key].Count == 0 ? 0 : ((long)entity.Properties[key][0]));
                        _resultObject.Add(key.ToLower(), lastLogonTime.ToString("s"));
                        break;
                    default:
                        _resultObject.Add(key.ToLower(), entity.Properties[key].Count == 0 ? "" : entity.Properties[key][0].ToString());
                        break;
                }
            }
            return _resultObject;
        }
        private static JObject Serialize(SearchResult sr, string[] fields)
        {
            JObject _resultObject = new JObject
                    {
                        { "guid", GetAdObjectGuid(sr) },
                        { "sid", GetAdObjectSid(sr) }
                    };

            foreach (string key in fields)
            {
                switch (key)
                {
                    case "objectSid":
                    case "objectGuid":
                        break;
                    case "useraccountcontrol":
                        _resultObject.Add("enabled", !Convert.ToBoolean((int)sr.Properties["useraccountcontrol"][0] & 0x0002));
                        break;
                    case "whenChanged":
                    case "whenCreated":
                        // date fields
                        _resultObject.Add(key.ToLower(), sr.Properties[key].Count == 0 ? "" : ((DateTime)sr.Properties[key][0]).ToString("s"));
                        break;
                    case "lastLogon":
                        var lastLogonTime = DateTime.FromFileTime(sr.Properties[key].Count == 0 ? 0 : ((long)sr.Properties[key][0]));
                        _resultObject.Add(key.ToLower(), lastLogonTime.ToString("s"));
                        break;
                    default:
                        _resultObject.Add(key.ToLower(), sr.Properties[key].Count == 0 ? "" : sr.Properties[key][0].ToString());
                        break;
                }
            }
            return _resultObject;
        }
        private static JObject GetUserGroups(string samAccountName)
        {
            JObject _groups = new JObject();

            /*using (PrincipalContext context = new PrincipalContext(ContextType.Domain, _adPath, _adUser, _adPwd))
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
            }*/
            return _groups;
        }
    }
}