using MongoDB.Driver;
using System;
using System.Linq;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson;

namespace openplugins.ReflectChangedMessage
{
    internal class MessageHashCheckerMongoDB : MessageHashChecker
    {
        private readonly MongoClient _mongo;
        private readonly IMongoDatabase _mongoDB;
        IMongoCollection<MessageHashMongo> _hashCollection;
        private readonly string _collectionPostfix;

        public MessageHashCheckerMongoDB(
            string connectionString = "mongodb://localhost:27017",
            string dataBase = "HASH_TABLE",
            string collectionPostfix = "_message_hash")
        {
            _mongo = new MongoClient(connectionString);
            _mongoDB = _mongo.GetDatabase(dataBase);
            _collectionPostfix = collectionPostfix;
        }
        public override string GetCurrentHash()
        {
            _hashCollection = _mongoDB.GetCollection<MessageHashMongo>(type + _collectionPostfix);
            try
            {
                MessageHashMongo ff = _hashCollection.Find(x => x.Id == messageId).Single();
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
            _hashCollection.ReplaceOne(
                x => x.Id == messageId,
                new MessageHashMongo(messageId, type, Hash),
                new UpdateOptions() { IsUpsert = true});
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