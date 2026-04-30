using System;
using System.Linq;
using System.Web.Mvc;
using shcool_event_management.Models;

namespace shcool_event_management.Areas.Admin.Controllers
{
    [Authorize]
    public abstract class BaseAdminController : Controller
    {
        protected readonly school_event_managementEntities _db
            = new school_event_managementEntities();

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

        protected override void Dispose(bool disposing)
        {
            if (disposing) _db.Dispose();
            base.Dispose(disposing);
        }
    }
}
