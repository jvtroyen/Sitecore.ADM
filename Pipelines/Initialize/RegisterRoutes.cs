using Sitecore.Diagnostics;
using Sitecore.Pipelines;
using System.Web.Http;

namespace TheReference.DotNet.Sitecore.AnalyticsDatabaseManager.Pipelines.Initialize
{
    public class RegisterRoutes
    {
        public virtual void Process(PipelineArgs args)
        {
            Assert.ArgumentNotNull(args, "args");
            HttpRouteCollectionExtensions.MapHttpRoute(GlobalConfiguration.Configuration.Routes, "ADM_Operations", "sitecore/ADM/Operations/{action}", 
                new  {
                controller = "ADMOperations"
            });
        }
    }
}