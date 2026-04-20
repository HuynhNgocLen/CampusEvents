using System;
using System.Web.Mvc;
using System.Web.Security;
using shcool_event_management.Models;

namespace shcool_event_management.Areas.Admin.Controllers
{
    // [Authorize(Roles = "Admin")]
    public class AdminSettingsController : Controller
    {
        private readonly school_event_managementEntities _db
            = new school_event_managementEntities();


        public ActionResult Index(string tab = "general")
        {
            ViewBag.ActiveMenu = "settings";
            ViewBag.ActiveTab = tab;

            // Lấy thông tin admin hiện tại
            var userId = Session["UserId"]?.ToString() ?? "SV001";
            var admin = _db.SinhViens.Find(userId);
            ViewBag.Admin = admin;

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult SaveGeneral(FormCollection form)
        {
            TempData["Success"] = "Đã lưu cài đặt chung thành công!";
            return RedirectToAction("Index", new { tab = "general" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ChangePassword(string currentPassword,
                                           string newPassword,
                                           string confirmPassword)
        {
            if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 6)
            {
                TempData["Error"] = "Mật khẩu mới phải có ít nhất 6 ký tự.";
                return RedirectToAction("Index", new { tab = "security" });
            }

            if (newPassword != confirmPassword)
            {
                TempData["Error"] = "Mật khẩu xác nhận không khớp.";
                return RedirectToAction("Index", new { tab = "security" });
            }

            var userId = Session["UserId"]?.ToString() ?? "SV001";
            var admin = _db.SinhViens.Find(userId);

            if (admin == null)
            {
                TempData["Error"] = "Không tìm thấy tài khoản.";
                return RedirectToAction("Index", new { tab = "security" });
            }

            var hashedCurrent = HashPassword(currentPassword);
            if (admin.MatKhau != hashedCurrent)
            {
                TempData["Error"] = "Mật khẩu hiện tại không đúng.";
                return RedirectToAction("Index", new { tab = "security" });
            }

            admin.MatKhau = HashPassword(newPassword);

            try
            {
                _db.SaveChanges();
                TempData["Success"] = "Đã đổi mật khẩu thành công!";
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Lỗi: " + ex.Message;
            }

            return RedirectToAction("Index", new { tab = "security" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult SaveNotifications(FormCollection form)
        {
            // Lưu các toggle vào Session hoặc database cài đặt
            Session["Notif_NewReg"] = form["notif_new_reg"] == "on";
            Session["Notif_Reminder"] = form["notif_reminder"] == "on";
            Session["Notif_WeekReport"] = form["notif_week_report"] == "on";
            Session["Notif_InApp"] = form["notif_inapp"] == "on";

            TempData["Success"] = "Đã lưu cài đặt thông báo!";
            return RedirectToAction("Index", new { tab = "notifications" });
        }

        [HttpPost]
        public JsonResult SaveTheme(string theme)
        {
            if (theme != "dark" && theme != "light")
                return Json(new { success = false, message = "Giá trị không hợp lệ." });

            Session["Theme"] = theme;
            return Json(new { success = true, theme });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Logout()
        {
            Session.Clear();
            Session.Abandon();
            FormsAuthentication.SignOut();
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        // ── Helper: Hash mật khẩu SHA-256 ────────────────────────
        private static string HashPassword(string password)
        {
            using (var sha = System.Security.Cryptography.SHA256.Create())
            {
                var bytes = System.Text.Encoding.UTF8.GetBytes(password);
                var hash = sha.ComputeHash(bytes);
                return BitConverter.ToString(hash).Replace("-", "").ToLower();
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _db.Dispose();
            base.Dispose(disposing);
        }
    }
}