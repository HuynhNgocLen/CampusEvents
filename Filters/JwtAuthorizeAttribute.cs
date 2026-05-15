using school_event_management.Models;
using System.Web;
using System.Web.Mvc;

namespace school_event_management.Filters
{
    /// <summary>
    /// Yêu cầu cookie JWT hợp lệ (sinh viên hoặc khách có token).
    /// </summary>
    public sealed class JwtAuthorizeAttribute : AuthorizeAttribute
    {
        protected override bool AuthorizeCore(HttpContextBase httpContext)
        {
            var cookie = httpContext.Request.Cookies["jwt"];
            if (cookie == null) return false;
            return JwtService.ValidateToken(cookie.Value) != null;
        }

        protected override void HandleUnauthorizedRequest(AuthorizationContext filterContext)
            => filterContext.Result = new RedirectResult("/Account/Login?tokenExpired=true");
    }
}
