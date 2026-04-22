using System.Web.Mvc;
using school_event_management.Models;

namespace school_event_management.Filters
{
    /// <summary>
    /// Chặn tài khoản khách (JWT userType=guest); chỉ dùng cho các action cần tài khoản sinh viên.
    /// </summary>
    public sealed class RestrictGuestAttribute : FilterAttribute, IAuthorizationFilter
    {
        public void OnAuthorization(AuthorizationContext filterContext)
        {
            if (JwtService.IsGuest(filterContext.HttpContext.Request))
            {
                filterContext.Controller.TempData["Info"] =
                    "Chế độ khách chỉ xem được trang chủ. Đăng nhập bằng tài khoản sinh viên để xem sự kiện, đăng ký và lịch cá nhân.";
                filterContext.Result = new RedirectToRouteResult(
                    new System.Web.Routing.RouteValueDictionary(new { area = "", controller = "Home", action = "Home" }));
            }
        }
    }
}
