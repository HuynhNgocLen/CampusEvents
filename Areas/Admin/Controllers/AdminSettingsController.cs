using System;
using System.Web.Mvc;
using System.Web.Security;
using shcool_event_management.Areas.Admin.Helpers;
using school_event_management.Helpers;
using shcool_event_management.Models;

namespace shcool_event_management.Areas.Admin.Controllers
{
    [Authorize]
    public class AdminSettingsController : Controller
    {
        private const string LogRetentionSessionKey = "AdminLogRetentionDays";
        private readonly school_event_managementEntities _db
            = new school_event_managementEntities();


        public ActionResult Index(string tab = "general")
        {
            ViewBag.ActiveMenu = "settings";
            ViewBag.ActiveTab = tab;

            var tenDn = User.Identity?.Name;
            var admin = string.IsNullOrEmpty(tenDn) ? null : _db.QuanTriViens.Find(tenDn);
            ViewBag.Admin = admin;
            ViewBag.LogRetentionDays = QtvHanhDongLogHelper.GetLogRetentionDays();

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

            var tenDn = User.Identity?.Name;
            var admin = string.IsNullOrEmpty(tenDn) ? null : _db.QuanTriViens.Find(tenDn);

            if (admin == null)
            {
                TempData["Error"] = "Không tìm thấy tài khoản.";
                return RedirectToAction("Index", new { tab = "security" });
            }

            if (!PasswordHasher.Verify(currentPassword, admin.MatKhau))
            {
                TempData["Error"] = "Mật khẩu hiện tại không đúng.";
                return RedirectToAction("Index", new { tab = "security" });
            }

            admin.MatKhau = PasswordHasher.HashPassword(newPassword);

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
            Session["Notif_NewReg"] = form["notif_new_reg"] == "on";
            Session["Notif_Reminder"] = form["notif_reminder"] == "on";
            Session["Notif_WeekReport"] = form["notif_week_report"] == "on";

            TempData["Success"] = "Đã lưu cài đặt thông báo!";
            return RedirectToAction("Index", new { tab = "notifications" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult SaveLogSettings(int? log_retention_days)
        {
            var days = log_retention_days ?? 7;
            if (days != 0 && days != 1 && days != 3 && days != 7 && days != 14 && days != 30 && days != 90)
            {
                TempData["Error"] = "Thời gian lưu log không hợp lệ.";
                return RedirectToAction("Index", new { tab = "notifications" });
            }

            Session[LogRetentionSessionKey] = days;
            QtvHanhDongLogHelper.CleanupExpiredLogs();
            TempData["Success"] = days == 0
                ? "Đã bật chế độ không ghi log và xóa log hiện có."
                : "Đã lưu thời gian lưu log: " + days + " ngày.";
            return RedirectToAction("Index", new { tab = "notifications" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
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
            Session.Remove("AdminQuyen");
            Session.Clear();
            Session.Abandon();
            FormsAuthentication.SignOut();
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _db.Dispose();
            base.Dispose(disposing);
        }
    }
}