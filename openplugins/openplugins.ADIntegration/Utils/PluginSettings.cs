using System.Collections.Generic;

namespace openplugins.ADIntegration
{
    internal class IngoingSettings
    {
        public LdapSettings Ldap { set; get; }
        public DebugSettings Debug { set; get; }
        public ReadUsers Users { set; get; }
        public ReadGroups Groups { set; get; }
    }
    internal class OutgoingSettings
    {
        public LdapSettings Ldap { set; get; }
        public DebugSettings Debug { get; set; }
        public string DefaultOU { get; set; }
        public Dictionary<string, AdTypes> Fields { set; get; }
        public string ClassIdRequest { get; set; }
        public string ClassIdResponse { get; set; }
    }
    internal class LdapSettings
    {
        public LdapSettings()
        {
            Port = 636;
        }

        public string Host { set; get; }
        public int Port { set; get; }
        public string Username { set; get; }
        public string Password { set; get; }
    }
    internal class DebugSettings
    {
        public DebugSettings()
        {
            DebugMode = false;
            StartDelay = 20;
        }

        public bool DebugMode { set; get; }
        public int StartDelay { set; get; }
    }
    internal class ReadUsers
    {
        public bool DebugMode { set; get; }
        public string[] Fields { set; get; }
        public string ClassId { set; get; }
        public string Cron { set; get; }
    }
    internal class ReadGroups
    {
        public bool DebugMode { set; get; }
        public string[] Fields { set; get; }
        public string ClassId { set; get; }
        public string Cron { set; get; }
        public string GroupFilter { get; set; }
    }
}