using System.DirectoryServices;

namespace openplugins.ADIntegration
{
    internal class LdapConnection
    {
        private readonly string _username;
        private readonly string _password;
        private readonly string _host;
        private readonly int _port;

        public LdapConnection(LdapSettings ldap)
        {
            _username = ldap.Username;
            _password = ldap.Password;
            _host = ldap.Host;
            _port = ldap.Port;

            string path = string.Format("LDAP://{0}:{1}", _host, _port);
            De = new DirectoryEntry(path, _username, _password);
        }

        public DirectoryEntry De { get; }

        internal DirectoryEntry GetDirectoryEntry(string groupEntry)
        {
            string path = string.Format("LDAP://{0}:{1}/{2}", _host, _port, groupEntry);
            return new DirectoryEntry(path, _username, _password);
        }
    }
}