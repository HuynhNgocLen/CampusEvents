using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using school_event_management.Helpers;
using shcool_event_management.Areas.Admin.Helpers;
using shcool_event_management.Models;

namespace shcool_event_management.Areas.Admin.Controllers
{
    [Authorize]
    public class AdminAdministratorsController : BaseAdminController
    {
        public ActionResult Index()
        {
            var currentAdmin = GetCurrentAdmin();
            if (currentAdmin == null || (currentAdmin.Quyen != 0 && currentAdmin.Quyen != 2))
            {
                TempData["Error"] = "Bạn không có quyền truy cập quản lí quản trị viên.";
                return RedirectToAction("Dashboard", "AdminDashboard");
            }

            ViewBag.ActiveMenu = "admin-managers";
            ViewBag.Viens = _db.Viens.OrderBy(v => v.TenVien).ToList();
            var admins = _db.QuanTriViens.AsQueryable();
            if (currentAdmin.Quyen == 2)
            {
                admins = admins.Where(a => a.MaQTV == currentAdmin.MaQTV);
            }
            ViewBag.Admins = admins.OrderBy(a => a.Quyen).ThenBy(a => a.TenDN).ToList();
            var roleOptions = GetCreatableRoles(currentAdmin.Quyen);
            ViewBag.RoleOptions = roleOptions;
            ViewBag.CurrentAdminRole = currentAdmin.Quyen;
            if (currentAdmin.Quyen == 2)
            {
                var ownVien = _db.Viens.FirstOrDefault(v => v.MaVien == currentAdmin.MaQTV);
                ViewBag.CurrentVienDisplay = ownVien != null
                    ? ownVien.TenVien + " (" + ownVien.MaVien + ")"
                    : currentAdmin.MaQTV;
            }
            ViewBag.LockedMap = _db.Database
                .SqlQuery<AdminLockRow>("SELECT TenDN, TrangThaiKhoa FROM QuanTriVien")
                .ToDictionary(x => x.TenDN, x => x.TrangThaiKhoa);

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(string tenDN, string matKhau, int? quyen, string maQtv)
        {
            var currentAdmin = GetCurrentAdmin();
            if (currentAdmin == null || (currentAdmin.Quyen != 0 && currentAdmin.Quyen != 2))
            {
                TempData["Error"] = "Bạn không có quyền thực hiện thao tác này.";
                return RedirectToAction("Dashboard", "AdminDashboard");
            }

            var normalizedTenDN = AdminAccountController.NormalizeTenDN(tenDN);
            if (string.IsNullOrWhiteSpace(normalizedTenDN) || string.IsNullOrWhiteSpace(matKhau) || !quyen.HasValue)
            {
                TempData["Error"] = "Vui lòng nhập đầy đủ tên đăng nhập, mật khẩu và quyền.";
                return RedirectToAction("Index");
            }

            var creatableRoles = GetCreatableRoles(currentAdmin.Quyen);
            if (!creatableRoles.Contains(quyen.Value))
            {
                TempData["Error"] = "Bạn không có quyền tạo tài khoản với quyền đã chọn.";
                return RedirectToAction("Index");
            }

            if (_db.QuanTriViens.Any(x => x.TenDN == normalizedTenDN))
            {
                TempData["Error"] = "Tên đăng nhập đã tồn tại.";
                return RedirectToAction("Index");
            }

            if (currentAdmin.Quyen == 2)
            {
                maQtv = currentAdmin.MaQTV;
            }

            if (string.IsNullOrWhiteSpace(maQtv) || !_db.Viens.Any(v => v.MaVien == maQtv))
            {
                TempData["Error"] = "Vui lòng chọn viện hợp lệ.";
                return RedirectToAction("Index");
            }

            // MaQTV is unique (FK Viện). Quyền 2 (QTV viện): mỗi viện một tài khoản — thông báo riêng.
            // Mã QTV = 1: tài khoản cấp cao, thêm bình thường nếu chưa trùng MaQTV.
            if (_db.QuanTriViens.Any(x => x.MaQTV == maQtv))
            {
                if (quyen.Value == 2)
                {
                    TempData["Error"] = "QTV viện đó đã tồn tại tài khoản.";
                }
                else
                {
                    TempData["Error"] = "Viện này đã có quản trị viên. Mỗi viện chỉ gắn một tài khoản (Mã QTV trùng mã viện, không thể tạo thêm).";
                }

                return RedirectToAction("Index");
            }

            var admin = new QuanTriVien
            {
                TenDN = normalizedTenDN,
                MatKhau = PasswordHasher.HashPassword(matKhau),
                Quyen = quyen.Value,
                MaQTV = maQtv
            };

            _db.QuanTriViens.Add(admin);
            _db.SaveChanges();

            try
            {
                QtvHanhDongLogHelper.Insert(
                    currentAdmin.TenDN,
                    currentAdmin.MaQTV,
                    "POST",
                    "AdminAdministrators",
                    "Create",
                    Request.RawUrl,
                    BuildAuditPrefix(currentAdmin) + " Thêm QTV mới: " + normalizedTenDN);
            }
            catch
            {
                // bảng log chưa có — bỏ qua
            }

            TempData["Success"] = "Thêm quản trị viên thành công.";
            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult UpdateRole(string tenDN, int? quyen)
        {
            var currentAdmin = GetCurrentAdmin();
            if (currentAdmin == null || currentAdmin.Quyen != 0)
            {
                TempData["Error"] = "Bạn không có quyền thực hiện thao tác này.";
                return RedirectToAction("Dashboard", "AdminDashboard");
            }

            if (string.IsNullOrWhiteSpace(tenDN) || !quyen.HasValue || quyen.Value < 0 || quyen.Value > 2)
            {
                TempData["Error"] = "Dữ liệu cập nhật quyền không hợp lệ.";
                return RedirectToAction("Index");
            }

            var admin = _db.QuanTriViens.Find(tenDN);
            if (admin == null)
            {
                TempData["Error"] = "Không tìm thấy quản trị viên.";
                return RedirectToAction("Index");
            }

            if (string.Equals(admin.TenDN, User.Identity?.Name, StringComparison.OrdinalIgnoreCase))
            {
                TempData["Error"] = "Bạn không thể tự thay đổi quyền của chính mình.";
                return RedirectToAction("Index");
            }

            if (admin.Quyen == 0)
            {
                TempData["Error"] = "Bạn không thể thay đổi quyền của tài khoản cùng cấp quyền 0.";
                return RedirectToAction("Index");
            }

            admin.Quyen = quyen.Value;
            _db.SaveChanges();

            TempData["Success"] = "Cập nhật quyền thành công.";
            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ToggleLock(string tenDN)
        {
            var currentAdmin = GetCurrentAdmin();
            if (currentAdmin == null || currentAdmin.Quyen != 0)
            {
                TempData["Error"] = "Bạn không có quyền thực hiện thao tác này.";
                return RedirectToAction("Dashboard", "AdminDashboard");
            }

            if (string.IsNullOrWhiteSpace(tenDN))
            {
                TempData["Error"] = "Thiếu tên đăng nhập quản trị viên.";
                return RedirectToAction("Index");
            }

            var admin = _db.QuanTriViens.Find(tenDN);
            if (admin == null)
            {
                TempData["Error"] = "Không tìm thấy quản trị viên.";
                return RedirectToAction("Index");
            }

            if (string.Equals(admin.TenDN, User.Identity?.Name, StringComparison.OrdinalIgnoreCase))
            {
                TempData["Error"] = "Không thể khóa chính tài khoản đang đăng nhập.";
                return RedirectToAction("Index");
            }

            var isLocked = IsAdminLocked(tenDN);
            _db.Database.ExecuteSqlCommand(
                "UPDATE QuanTriVien SET TrangThaiKhoa = @p0 WHERE TenDN = @p1",
                isLocked ? 0 : 1,
                tenDN);
            TempData["Success"] = isLocked ? "Đã mở khóa quản trị viên." : "Đã khóa quản trị viên.";
            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Delete(string tenDN)
        {
            var currentAdmin = GetCurrentAdmin();
            if (currentAdmin == null || currentAdmin.Quyen != 0)
            {
                TempData["Error"] = "Bạn không có quyền thực hiện thao tác này.";
                return RedirectToAction("Dashboard", "AdminDashboard");
            }

            if (string.IsNullOrWhiteSpace(tenDN))
            {
                TempData["Error"] = "Thiếu tên đăng nhập quản trị viên.";
                return RedirectToAction("Index");
            }

            if (string.Equals(tenDN, User.Identity?.Name, StringComparison.OrdinalIgnoreCase))
            {
                TempData["Error"] = "Không thể xóa chính tài khoản đang đăng nhập.";
                return RedirectToAction("Index");
            }

            var admin = _db.QuanTriViens.Find(tenDN);
            if (admin == null)
            {
                TempData["Error"] = "Không tìm thấy quản trị viên.";
                return RedirectToAction("Index");
            }

            _db.QuanTriViens.Remove(admin);
            _db.SaveChanges();

            TempData["Success"] = "Đã xóa quản trị viên.";
            return RedirectToAction("Index");
        }

        private bool IsAdminLocked(string tenDN)
        {
            var lockState = _db.Database.SqlQuery<int?>(
                "SELECT TOP 1 CAST(TrangThaiKhoa AS int) FROM QuanTriVien WHERE TenDN = @p0",
                tenDN).FirstOrDefault();
            return lockState.GetValueOrDefault() == 1;
        }

        private static List<int> GetCreatableRoles(int currentRole)
        {
            if (currentRole == 0) return new List<int> { 1, 2 };
            if (currentRole == 2) return new List<int> { 1 };
            return new List<int>();
        }

        private class AdminLockRow
        {
            public string TenDN { get; set; }
            public bool TrangThaiKhoa { get; set; }
        }
    }
}
