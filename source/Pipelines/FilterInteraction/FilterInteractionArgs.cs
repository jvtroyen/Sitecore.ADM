using MongoDB.Bson;
using MongoDB.Driver;
using Sitecore.Diagnostics;
using Sitecore.Pipelines;
using System;

namespace TheReference.DotNet.Sitecore.AnalyticsDatabaseManager.Pipelines.FilterInteraction
{
    public class FilterInteractionArgs : PipelineArgs
    {
        public readonly MongoDatabase Database;
        public readonly BsonDocument Interaction;
        public readonly BsonDocument Contact;

        public Guid ContactId
        {
            get
            {
                return this.Interaction["ContactId"].AsGuid;
            }
        }

        public Guid InteractionId
        {
            get
            {
                return this.Interaction["_id"].AsGuid;
            }
        }

        public bool RemoveInteraction { get; set; }

        public FilterInteractionArgs(BsonDocument interaction, BsonDocument contact, MongoDatabase database)
        {
            Assert.ArgumentNotNull((object)interaction, "interaction");
            this.Interaction = interaction;
            this.Contact = contact;
            this.RemoveInteraction = true;
            this.Database = database;
        }
    }
}
