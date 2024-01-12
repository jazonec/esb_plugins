using System.Collections.Generic;

namespace openplugins.Reflectors
{
    internal class PluginSettings
    {
        public DebugSettings debug = new DebugSettings();
        public PluginReflectors reflectors = new PluginReflectors();
    }
    internal class PluginReflectors
    {
        public EncryptorSettings encryptor;
        public UnbatchSettings unbatch;
        public MultiplySettings multiply;
        public BlackHoleSettings blackHole;
        public BatchReflectorSettings batch;
        public ChangedSettings changed;
    }
    internal class ChangedSettings : ReflectorSettings
    {
        public string connectionString { set; get; }
        public string mode { set; get; }
    }
    internal class BatchReflectorSettings : ReflectorSettings
    {
    }
    internal class BlackHoleSettings : ReflectorSettings
    {
    }
    internal class MultiplySettings : ReflectorSettings
    {
        public int reflectAmount { set; get; }
    }
    internal class UnbatchSettings : ReflectorSettings
    {
        public string pattern { set; get; }
        public string responseType { set; get; }
        public string responseClassId { set; get; }
    }
    internal class EncryptorSettings : ReflectorSettings
    {
        public bool createRandomKey { set; get; }
        public int keyLenght { set; get; }
        public bool encodeKey { set; get; }
        public RsaSettings rsa { set; get; }

        public EncryptorSettings()
        {
            createRandomKey = false;
            encodeKey = false;
        }
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
    internal class ReflectorSettings
    {
        public IList<string> type;
        public IList<string> classID;

        public ReflectorSettings()
        {
            type = new List<string>();
            classID = new List<string>();
        }
    }
    internal class RsaSettings
    {
        public string certificate;

        public RsaSettings()
        {
            certificate = null;
        }
    }
}
