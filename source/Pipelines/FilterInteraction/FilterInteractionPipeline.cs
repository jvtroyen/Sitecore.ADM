using Sitecore.Diagnostics;
using Sitecore.Pipelines;
using System;

namespace TheReference.DotNet.Sitecore.AnalyticsDatabaseManager.Pipelines.FilterInteraction
{
    public class FilterInteractionPipeline
    {
        public static void Run(FilterInteractionArgs args)
        {
            Assert.ArgumentNotNull((object)args, "args");
            try
            {
                CorePipeline.Run("filterInteraction", (PipelineArgs)args, "ADM");
            }
            catch (Exception ex)
            {
                Log.Error("Exception occured while executing the filterInteraction pipeline for the " + (object)args.InteractionId + " interaction. Interaction was not removed", ex, typeof(FilterInteractionPipeline));
                args.RemoveInteraction = false;
            }
        }
    }
}