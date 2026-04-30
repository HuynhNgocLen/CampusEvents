using System;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Web.Mvc;
using System.Web.Security;
using school_event_management.Helpers;
using shcool_event_management.Models;

namespace shcool_event_management.Areas.Admin.Controllers
{
    /// <summary>Đăng nhập quản trị (Forms Auth, timeout cấu hình trong Web.config).</summary>
    [AllowAnonymous]
    public class AdminAccountController : Controller
    {
        private readonly school_event_managementEntities _db = new school_event_managementEntities();

        [HttpGet]
        public ActionResult Login(string returnUrl)
        {
            if (User.Identity.IsAuthenticated)
                return RedirectToLocal(returnUrl);

            ViewBag.ReturnUrl = returnUrl;
            return View();
        }

        [Authorize]
        [HttpGet]
        public ActionResult Locked()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Login(string tenDN, string matKhau, string returnUrl)
        {
            ViewBag.ReturnUrl = returnUrl;

            if (string.IsNullOrWhiteSpace(tenDN) || string.IsNullOrWhiteSpace(matKhau))
            {
                ModelState.AddModelError("", "Vui lòng nhập đầy đủ tên đăng nhập và mật khẩu.");
                return View();
            }

            var key = NormalizeTenDN(tenDN);
            if (string.IsNullOrEmpty(key))
            {
                ModelState.AddModelError("", "Tên đăng nhập không hợp lệ.");
                return View();
            }

            var admin = _db.QuanTriViens.Find(key);
            if (admin == null)
            {
                ModelState.AddModelError("", "Sai tên đăng nhập hoặc mật khẩu.");
                return View();
            }

            var isLocked = _db.Database.SqlQuery<int?>(
                "SELECT TOP 1 CAST(TrangThaiKhoa AS int) FROM QuanTriVien WHERE TenDN = @p0",
                admin.TenDN).FirstOrDefault();
            if (isLocked.GetValueOrDefault() == 1)
            {
                ModelState.AddModelError("", "Tài khoản của bạn đã bị khóa.");
                return View();
            }

            if (!PasswordHasher.Verify(matKhau, admin.MatKhau))
            {
                ModelState.AddModelError("", "Sai tên đăng nhập hoặc mật khẩu.");
                return View();
            }

            if (PasswordHasher.NeedsUpgrade(admin.MatKhau))
            {
                admin.MatKhau = PasswordHasher.HashPassword(matKhau);
                _db.SaveChanges();
            }

            Session["AdminQuyen"] = admin.Quyen;
            Session["AdminMaQTV"] = admin.MaQTV;
            FormsAuthentication.SetAuthCookie(admin.TenDN, false);

            return RedirectToLocal(returnUrl);
        }

        [Authorize]
        [HttpGet]
        public ActionResult Logout()
        {
            Session.Remove("AdminQuyen");
            Session.Remove("AdminMaQTV");
            Session.Clear();
            Session.Abandon();
            FormsAuthentication.SignOut();
            return RedirectToAction("Login", "AdminAccount", new { area = "Admin" });
        }

        private ActionResult RedirectToLocal(string returnUrl)
        {
            if (Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);
            return RedirectToAction("Dashboard", "AdminDashboard");
        }

        /// <summary>Chuẩn hóa: bỏ khoảng trắng, chữ thường, bỏ dấu tiếng Việt.</summary>
        internal static string NormalizeTenDN(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return null;
            var noSpace = new string(input.Where(c => !char.IsWhiteSpace(c)).ToArray());
            if (noSpace.Length == 0) return null;
            var noMarks = RemoveDiacritics(noSpace);
            return noMarks.Trim().ToLowerInvariant();
        }

        private static string RemoveDiacritics(string text)
        {
            var normalized = text.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder(normalized.Length);
            foreach (var c in normalized)
            {
                var cat = CharUnicodeInfo.GetUnicodeCategory(c);
                if (cat != UnicodeCategory.NonSpacingMark)
                    sb.Append(c);
            }
            return sb.ToString().Normalize(NormalizationForm.FormC);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _db.Dispose();
            base.Dispose(disposing);
        }
    }
}
