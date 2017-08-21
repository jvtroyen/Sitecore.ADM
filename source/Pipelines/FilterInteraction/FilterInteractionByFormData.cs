using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Builders;

namespace TheReference.DotNet.Sitecore.AnalyticsDatabaseManager.Pipelines.FilterInteraction
{
    public class FilterInteractionByFormData : FilterInteractionProcessor
    {
        public override bool IsRestricted(FilterInteractionArgs args)
        {
            IMongoQuery query = Query.EQ("InteractionId", (BsonValue)args.InteractionId);
            if (args.Database.GetCollection("FormData").Exists())
                return (ulong)args.Database.GetCollection("FormData").Find(query).SetLimit(1).Count() > 0UL;
            return false;
        }
    }
}