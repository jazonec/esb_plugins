using StackExchange.Redis;

namespace openplugins.Reflectors
{
    internal class MessageHashCheckerRedis : MessageHashChecker
    {
        private readonly IDatabase _redisDB;
        private readonly ConnectionMultiplexer _redis;

        public new void Dispose()
        {
            _redis?.Dispose();
        }
        public MessageHashCheckerRedis(string connectionString)
        {
            connectionString = connectionString ?? "localhost";
            _redis = ConnectionMultiplexer.Connect(connectionString);
            _redisDB = _redis.GetDatabase();
            return;
        }

        public override string GetCurrentHash() => _redisDB.StringGet(type + messageId);
        public override void SetCurrentHash() => _redisDB.StringSet(type + messageId, Hash);
    }
}