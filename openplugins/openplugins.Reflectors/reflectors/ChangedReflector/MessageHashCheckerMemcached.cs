using Enyim.Caching;
using Enyim.Caching.Configuration;
using Enyim.Caching.Memcached;

namespace openplugins.Reflectors
{
    internal class MessageHashCheckerMemcached : MessageHashChecker
    {
        private readonly MemcachedClient _memcach;
        public new void Dispose()
        {
            _memcach?.Dispose();
        }
        public MessageHashCheckerMemcached(string connectionString)
        {
            connectionString = connectionString ?? "localhost:11211";
            MemcachedClientConfiguration memConfig;
            memConfig = new MemcachedClientConfiguration();
            memConfig.AddServer(connectionString);
            _memcach = new MemcachedClient(memConfig);
            return;
        }
        public override string GetCurrentHash() => _memcach.Get<string>(type + messageId);
        public override void SetCurrentHash() => _memcach.Store(StoreMode.Set, type + messageId, Hash);
    }
}