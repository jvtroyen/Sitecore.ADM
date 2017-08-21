using MongoDB.Bson;

namespace TheReference.DotNet.Sitecore.AnalyticsDatabaseManager.Pipelines.FilterContact
{
    public abstract class FilterContactProcessor
    {
        public virtual void Process(FilterContactArgs args)
        {
            if (!this.IsRestricted(args.Contact))
                return;
            args.RemoveContact = false;
            args.AbortPipeline();
        }

        public abstract bool IsRestricted(BsonDocument contact);
    }
}
