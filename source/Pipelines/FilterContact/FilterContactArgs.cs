using MongoDB.Bson;
using Sitecore.Diagnostics;
using Sitecore.Pipelines;
using System;

namespace TheReference.DotNet.Sitecore.AnalyticsDatabaseManager.Pipelines.FilterContact
{
    public class FilterContactArgs : PipelineArgs
    {
        public Guid ContactId
        {
            get
            {
                return this.Contact["_id"].AsGuid;
            }
        }

        public BsonDocument Contact { get; private set; }

        public bool RemoveContact { get; set; }

        public FilterContactArgs(BsonDocument contact)
        {
            Assert.ArgumentNotNull((object)contact, "contact");
            this.Contact = contact;
            this.RemoveContact = true;
        }
    }
}
