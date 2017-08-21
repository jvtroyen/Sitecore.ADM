using Sitecore.Diagnostics;
using Sitecore.Pipelines;
using System;

namespace TheReference.DotNet.Sitecore.AnalyticsDatabaseManager.Pipelines.FilterContact
{
    public class FilterContactPipeline
    {
        public static void Run(FilterContactArgs args)
        {
            Assert.ArgumentNotNull((object)args, "args");
            try
            {
                CorePipeline.Run("filterContact", (PipelineArgs)args, "ADM");
            }
            catch (Exception ex)
            {
                Log.Error("Exception occured while executing the filterContact pipeline for the " + (object)args.ContactId + "contact. Contact was not removed", ex, typeof(FilterContactPipeline));
                args.RemoveContact = false;
            }
        }
    }
}
