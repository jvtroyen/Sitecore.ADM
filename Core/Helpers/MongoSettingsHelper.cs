using MongoDB.Driver;
using Sitecore.Diagnostics;
using Sitecore.Pipelines;
using System;
using System.Configuration;
using TheReference.DotNet.Sitecore.AnalyticsDatabaseManager.Configuration;

namespace TheReference.DotNet.Sitecore.AnalyticsDatabaseManager.Core.Helpers
{
    public static class MongoSettingsHelper
    {
        public static MongoDatabase GetDatabase(string databaseName)
        {
            Assert.IsNotNull((object)ConfigurationManager.ConnectionStrings[databaseName], string.Format("Connection string with name {0} does not exist", (object)databaseName));
            MongoUrl url = new MongoUrl(ConfigurationManager.ConnectionStrings[databaseName].ConnectionString);
            MongoClientSettings settings = MongoClientSettings.FromUrl(url);
            if (Settings.UseUpdateMongoDriverSettingsPipeline)
            {
                try
                {
                    Type type = Type.GetType("Sitecore.Analytics.Pipelines.UpdateMongoDriverSettings.UpdateMongoDriverSettingsArgs,Sitecore.Analytics.MongoDB");
                    if (type == (Type)null)
                        Log.Warn("[Analytics Database Manager] The 'Sitecore.Analytics.Pipelines.UpdateMongoDriverSettings.UpdateMongoDriverSettingsArgs,Sitecore.Analytics.MongoDB' type was not found in the assemblies. Please refer ADM.UseUpdateMongoDriverSettingsPipeline setting for details", (object)typeof(MongoSettingsHelper));
                    else
                        CorePipeline.Run("updateMongoDriverSettings", type.GetConstructor(new Type[2]
                        {
              typeof (string),
              typeof (MongoClientSettings)
                        }).Invoke(new object[2]
                        {
              (object) url.DatabaseName,
              (object) settings
                        }) as PipelineArgs, false);
                }
                catch (Exception ex)
                {
                    Log.Error("[Analytics Database Manager] Exception was thrown while executing the UpdateMongoDriverSettings pipeline. Default mongo driver settings will be used.", ex, typeof(MongoSettingsHelper));
                }
            }
            return new MongoClient(settings).GetServer().GetDatabase(url.DatabaseName);
        }
    }
}