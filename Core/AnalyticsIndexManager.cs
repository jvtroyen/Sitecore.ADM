using MongoDB.Bson;
using MongoDB.Driver;
using Sitecore;
using Sitecore.Analytics.Data;
using Sitecore.Analytics.Processing.ProcessingPool;
using Sitecore.Configuration;
using Sitecore.Diagnostics;
using System;
using System.Collections;
using TheReference.DotNet.Sitecore.AnalyticsDatabaseManager.Core.Helpers;

namespace TheReference.DotNet.Sitecore.AnalyticsDatabaseManager.Core
{
    public class AnalyticsIndexManager
    {
        public void UpdateContactsInIndex()
        {
            MongoDatabase database = MongoSettingsHelper.GetDatabase("analytics");
            ProcessingPool processingPool = (ProcessingPool)Assert.ResultNotNull<object>(Factory.CreateObject("aggregationProcessing/processingPools/contact", true));
            string collectionName = "Contacts";
            MongoCursor mongoCursor = (MongoCursor)database.GetCollection(collectionName).FindAll().SetFields(new string[1]
            {
                "_id"
            }).SetFlags(QueryFlags.NoCursorTimeout);
            if (Context.Job != null)
            {
                Context.Job.Status.Total = mongoCursor.Count();
                Context.Job.Status.Messages.Add("Indexing contacts started. " + (object)Context.Job.Status.Total + " items to process ");
                Context.Job.Status.Processed = 0L;
            }
            foreach (BsonValue bsonValue in (IEnumerable)mongoCursor)
            {
                Guid asGuid = bsonValue[0].AsGuid;
                try
                {
                    ProcessingPoolItem workItem = new ProcessingPoolItem(asGuid.ToByteArray())
                    {
                        Properties = {
                          {
                            "Reason",
                            ProcessingReason.Updated.ToString()
                          }
                        }
                    };
                    processingPool.Add(workItem, (SchedulingOptions)null);
                    if (Context.Job != null)
                        ++Context.Job.Status.Processed;
                }
                catch (Exception ex)
                {
                    Log.Error("[Analytics Database Manager] Exception occured while index rebuild. ContactId " + (object)asGuid, ex, typeof(AnalyticsIndexManager));
                }
            }
        }

        public static long GetRecordsInPoolCount()
        {
            return MongoSettingsHelper.GetDatabase("tracking.contact").GetCollection("ProcessingPool").Count();
        }
    }
}