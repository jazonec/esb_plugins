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
                    case "members":
                        break;
                    case "useraccountcontrol":
                        _resultObject.Add("enabled", !Convert.ToBoolean((int)entity.Properties["userAccountControl"][0] & 0x0002));
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
                    case "enabled":
                        int flags = (int)entity.Properties["userAccountControl"].Value;
                        _resultObject.Add(key.ToLower(), !Convert.ToBoolean(flags & 0x0002));
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
                    case "members":
                        break;
                    case "useraccountcontrol":
                    case "enabled":
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
                    case "pwdLastSet":
                        string _lastSet;
                        try
                        {
                            _lastSet = DateTime.FromFileTime((long)sr.Properties[key][0]).ToString("s");
                        }
                        catch
                        {
                            _lastSet = string.Empty;
                        }
                        _resultObject.Add(key.ToLower(), _lastSet);
                        break;
                    case "msDS-UserPasswordExpiryTimeComputed":
                        string daysLeft;
                        try
                        {
                            daysLeft = DateTime.FromFileTime((long)sr.Properties[key][0]).ToString("s");
                        }
                        catch (Exception)
                        {
                            daysLeft = string.Empty;
                        }
                        _resultObject.Add("passwordexpires", daysLeft);
                        break;
                    default:
                        _resultObject.Add(key.ToLower(), sr.Properties[key].Count == 0 ? "" : sr.Properties[key][0].ToString());
                        break;
                }
            }
            return _resultObject;
        }
        internal static JObject SerializeGroup(DirectoryEntry entity, bool added)
        {
            string key;
            JObject _resultObject = new JObject
                    {
                        { "guid", GetAdObjectGuid(entity) },
                        { "sid", GetAdObjectSid(entity) }
            };
            key = "distinguishedName";
            _resultObject.Add(key.ToLower(), entity.Properties[key][0].ToString());
            key = "whenChanged";
            _resultObject.Add(key.ToLower(), entity.Properties[key].Count == 0 ? "" : ((DateTime)entity.Properties[key][0]).ToString("s"));
            key = "whenCreated";
            _resultObject.Add(key.ToLower(), entity.Properties[key].Count == 0 ? "" : ((DateTime)entity.Properties[key][0]).ToString("s"));
            key = "member";
            _resultObject.Add(key.ToLower(), entity.Properties[key].Count == 0 ? "" : entity.Properties[key][0].ToString());
            key = "enabled";
            _resultObject.Add(key.ToLower(), added);
            return _resultObject;
        }
        internal static JObject SerializeObject(SearchResult sr, string[] fields, List<SearchResult> members)
        {
            JObject _retVal = Serialize(sr, fields);
            _retVal.Add("members", SerializeList(members, "member", fields: new string[] { "name", "employeeNumber" }));
            return _retVal;
        }
        internal static JObject SerializeObject(SearchResult sr, string[] fields)
        {
            JObject _retVal = Serialize(sr, fields);
            return _retVal;
        }
    }
}