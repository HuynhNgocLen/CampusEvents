using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Optimization;
using System.Web.Routing;
using school_event_management.Models;

namespace school_event_management
{
    public class MvcApplication : System.Web.HttpApplication
    {
        protected void Application_Start()
        {
            AreaRegistration.RegisterAllAreas();
            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
            RouteConfig.RegisterRoutes(RouteTable.Routes);
            BundleConfig.RegisterBundles(BundleTable.Bundles);
        }

        protected void Application_Error()
        {
            var exception = Server.GetLastError();
            if (exception is HttpAntiForgeryException)
            {
                Server.ClearError();
                Response.Redirect("~/Account/Login?tokenExpired=true");
            }
        }

        protected void Application_AcquireRequestState(object sender, EventArgs e)
        {
            var context = HttpContext.Current;
            if (context == null) return;

            var wrappedContext = new HttpContextWrapper(context);
            JwtService.TryRenewTokenCookie(wrappedContext.Request, wrappedContext.Response);
        }
    }
}
