using MongoDB.Bson;

namespace TheReference.DotNet.Sitecore.AnalyticsDatabaseManager.Pipelines.FilterContact
{
    public class FilterContactByIdentifier : FilterContactProcessor
    {
        public override bool IsRestricted(BsonDocument contact)
        {
            BsonElement bsonElement;
            return contact.TryGetElement("Identifiers", out bsonElement) && bsonElement.Value.AsBsonDocument.TryGetElement("Identifier", out bsonElement);
        }
    }
}
