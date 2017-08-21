using Sitecore;
using Sitecore.Globalization;
using System.Net;
using System.Net.Http;
using System.Web.Http.Controllers;
using System.Web.Http.Filters;

namespace TheReference.DotNet.Sitecore.AnalyticsDatabaseManager.Core.Security
{
    public class AuthorizedUserFilter : AuthorizationFilterAttribute
    {
        public override void OnAuthorization(HttpActionContext actionContext)
        {
            if (Context.User.IsAdministrator || Context.User.IsInRole("sitecore\\ADM Admin"))
                return;
            string message = Translate.Text("Unauthorized Access");
            actionContext.Response = HttpRequestMessageExtensions.CreateErrorResponse(actionContext.ControllerContext.Request, HttpStatusCode.Unauthorized, message);
            base.OnAuthorization(actionContext);
        }
    }
}