using MongoDB.Bson;

namespace TheReference.DotNet.Sitecore.AnalyticsDatabaseManager.Pipelines.FilterContact
{
    public class FilterContactInList : FilterContactProcessor
    {
        public override bool IsRestricted(BsonDocument contact)
        {
            if (contact.Contains("Tags"))
            {
                BsonDocument asBsonDocument = contact["Tags"].AsBsonDocument;
                if (asBsonDocument.Contains("Entries") && asBsonDocument["Entries"].AsBsonDocument.Contains("ContactLists"))
                    return true;
            }
            return false;
        }
    }
}
