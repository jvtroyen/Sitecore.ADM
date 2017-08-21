namespace TheReference.DotNet.Sitecore.AnalyticsDatabaseManager.Pipelines.FilterInteraction
{
    public abstract class FilterInteractionProcessor
    {
        public virtual void Process(FilterInteractionArgs args)
        {
            if (!this.IsRestricted(args))
                return;
            args.RemoveInteraction = false;
            args.AbortPipeline();
        }

        public abstract bool IsRestricted(FilterInteractionArgs args);
    }
}