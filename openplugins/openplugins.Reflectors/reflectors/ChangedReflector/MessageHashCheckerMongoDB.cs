using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using System;

namespace openplugins.Reflectors
{
    internal class MessageHashCheckerMongoDB : MessageHashChecker
    {
        private readonly MongoClient _mongo;
        private readonly IMongoDatabase _mongoDB;
        private IMongoCollection<MessageHashMongo> _hashCollection;
        private readonly string _collectionPostfix;

        public MessageHashCheckerMongoDB(
            string connectionString,
            string dataBase = "HASH_TABLE",
            string collectionPostfix = "_message_hash")
        {
            connectionString = connectionString ?? "mongodb://localhost:27017";
            _mongo = new MongoClient(connectionString);
            _mongoDB = _mongo.GetDatabase(dataBase);
            _collectionPostfix = collectionPostfix;
        }
        public override string GetCurrentHash()
        {
            _hashCollection = _mongoDB.GetCollection<MessageHashMongo>(type + _collectionPostfix);
            try
            {
                MessageHashMongo ff = _hashCollection.FindSync(x => x.Id == messageId).Single();
                return ff.Hash;
            }
            catch
            {
                return null;
            }
        }
        public override void SetCurrentHash()
        {
            _hashCollection = _mongoDB.GetCollection<MessageHashMongo>(type + _collectionPostfix);
            var result = _hashCollection.UpdateOne(
                x => x.Id == messageId,
                Builders<MessageHashMongo>.Update.Set(u => u.Hash, Hash),
                new UpdateOptions() { IsUpsert = true });
        }
    }
    internal class MessageHashMongo
    {
        [BsonId]
        [BsonRepresentation(BsonType.String)]
        public string Id { get; set; }
        [BsonElement("Type")]
        [BsonRepresentation(BsonType.String)]
        public string Type { get; set; }
        [BsonElement("Hash")]
        [BsonRepresentation(BsonType.String)]
        public string Hash { get; set; }

        public MessageHashMongo(string id, string type, string hash)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            Type = type ?? throw new ArgumentNullException(nameof(type));
            Hash = hash ?? throw new ArgumentNullException(nameof(hash));
        }
    }
}