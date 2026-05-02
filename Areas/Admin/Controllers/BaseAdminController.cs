using System;
using System.Linq;
using System.Web.Mvc;
using shcool_event_management.Areas.Admin.Helpers;
using shcool_event_management.Models;

namespace shcool_event_management.Areas.Admin.Controllers
{
    [Authorize]
    public abstract class BaseAdminController : Controller
    {
        protected readonly school_event_managementEntities _db
            = new school_event_managementEntities();

        protected override void OnActionExecuted(ActionExecutedContext filterContext)
        {
            base.OnActionExecuted(filterContext);
            try
            {
                if (filterContext?.HttpContext?.Request == null)
                    return;

                var admin = GetCurrentAdmin();
                if (admin == null || (admin.Quyen != 1 && admin.Quyen != 2))
                    return;
                var method = filterContext.HttpContext.Request.HttpMethod;

                var rd = filterContext.RouteData;
                var controllerName = (rd?.Values["controller"] as string)
                    ?? filterContext.ActionDescriptor?.ControllerDescriptor?.ControllerName
                    ?? "";
                var actionName = (rd?.Values["action"] as string)
                    ?? filterContext.ActionDescriptor?.ActionName
                    ?? "";
                var path = filterContext.HttpContext.Request.RawUrl;
                if (!ShouldWriteAuditLog(method, controllerName, actionName, path))
                    return;

                QtvHanhDongLogHelper.Insert(
                    admin.TenDN,
                    admin.MaQTV,
                    method,
                    controllerName,
                    actionName,
                    path,
                    BuildAuditPrefix(admin) + " " + method + " " + controllerName + "/" + actionName);
            }
            catch
            {
                // Ghi log không được thì bỏ qua, không chặn thao tác
            }
        }

        protected override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            base.OnActionExecuting(filterContext);

            var tenDN = User?.Identity?.Name;
            if (string.IsNullOrWhiteSpace(tenDN))
            {
                return;
            }

            var isLocked = _db.Database.SqlQuery<int?>(
                "SELECT TOP 1 CAST(TrangThaiKhoa AS int) FROM QuanTriVien WHERE TenDN = @p0",
                tenDN).FirstOrDefault();

            if (isLocked.GetValueOrDefault() != 1)
            {
                return;
            }

            filterContext.Result = RedirectToAction("Locked", "AdminAccount", new { area = "Admin" });
        }

        protected int GetAdminQuyen()
        {
            return Session["AdminQuyen"] != null ? Convert.ToInt32(Session["AdminQuyen"]) : -1;
        }

        protected QuanTriVien GetCurrentAdmin()
        {
            var adminMaQTV = Session["AdminMaQTV"]?.ToString();
            if (!string.IsNullOrWhiteSpace(adminMaQTV))
            {
                var byMa = _db.QuanTriViens.FirstOrDefault(x => x.MaQTV == adminMaQTV);
                if (byMa != null) return byMa;
            }

            var tenDN = User?.Identity?.Name;
            if (string.IsNullOrWhiteSpace(tenDN))
                return null;

            var admin = _db.QuanTriViens.FirstOrDefault(x => x.TenDN == tenDN);
            if (admin != null)
            {
                Session["AdminMaQTV"] = admin.MaQTV;
                Session["AdminQuyen"] = admin.Quyen;
            }

            return admin;
        }

        protected string GetCurrentAdminMaQTV()
        {
            if (Session["AdminMaQTV"] != null)
            {
                return Session["AdminMaQTV"].ToString();
            }

            var tenDN = User?.Identity?.Name;
            if (string.IsNullOrEmpty(tenDN))
            {
                return null;
            }

            var admin = _db.QuanTriViens.FirstOrDefault(x => x.TenDN == tenDN);
            if (admin == null || string.IsNullOrEmpty(admin.MaQTV))
            {
                return null;
            }

            Session["AdminMaQTV"] = admin.MaQTV;
            Session["AdminQuyen"] = admin.Quyen;
            return admin.MaQTV;
        }

        protected string ResolveAdminVienCode(QuanTriVien admin)
        {
            if (admin == null) return null;

            // Resolve by actual QuanTriVien -> Vien relationship first.
            var maVien = _db.QuanTriViens
                .Where(q => q.TenDN == admin.TenDN)
                .Select(q => q.Vien.MaVien)
                .FirstOrDefault();

            if (!string.IsNullOrWhiteSpace(maVien))
            {
                return maVien;
            }

            if (!string.IsNullOrWhiteSpace(admin.Vien?.MaVien))
            {
                return admin.Vien.MaVien;
            }

            return admin.MaQTV;
        }

        protected static string BuildAuditPrefix(QuanTriVien admin)
        {
            if (admin == null)
                return "[unknown]";
            return "[" + (admin.TenDN ?? "unknown") + " - quyền " + admin.Quyen + "]";
        }

        private static bool ShouldWriteAuditLog(string method, string controllerName, string actionName, string path)
        {
            if (!string.IsNullOrWhiteSpace(method)
                && (string.Equals(method, "POST", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(method, "PUT", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(method, "DELETE", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(method, "PATCH", StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            if (!string.Equals(method, "GET", StringComparison.OrdinalIgnoreCase))
                return false;

            // GET chỉ ghi cho thao tác có tác động nghiệp vụ như xuất file.
            if (!string.IsNullOrWhiteSpace(actionName)
                && (actionName.IndexOf("Export", StringComparison.OrdinalIgnoreCase) >= 0
                    || actionName.IndexOf("Download", StringComparison.OrdinalIgnoreCase) >= 0))
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(path)
                && (path.IndexOf("export", StringComparison.OrdinalIgnoreCase) >= 0
                    || path.IndexOf("download", StringComparison.OrdinalIgnoreCase) >= 0))
            {
                return true;
            }

            return false;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _db.Dispose();
            base.Dispose(disposing);
        }
    }
}
