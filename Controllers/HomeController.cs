using school_event_management.Filters;
using school_event_management.Models;
using shcool_event_management.Models;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;

namespace school_event_management.Controllers
{
    [JwtAuthorize]
    public class HomeController : Controller
    {
        private readonly school_event_managementEntities db = new school_event_managementEntities();
        private string GetCurrentStudentId()
        {
            return JwtService.GetStudentId(Request);
        }


        protected override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            base.OnActionExecuting(filterContext);

            ViewBag.IsGuest = JwtService.IsGuest(Request);

            if (JwtService.IsGuest(Request))
            {
                ViewBag.UserName = "Khách tham quan";
                ViewData["TenHienThi"] = "Khách tham quan";
                ViewData["MaSV"] = "";
                ViewData["Lop"] = "";
                ViewData["Avatar"] = "KH";
                return;
            }

            string studentId = GetCurrentStudentId();
            if (string.IsNullOrEmpty(studentId)) return;

            var sv = db.SinhViens.FirstOrDefault(s => s.ID == studentId);
            if (sv == null) return;

            ViewBag.UserName = sv.Ten;
            ViewData["TenHienThi"] = sv.Ten;
            ViewData["MaSV"] = sv.ID;
            ViewData["Lop"] = sv.Lop;

            var words = sv.Ten.Trim().Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            string initials = "SV";
            if (words.Length > 0)
            {
                initials = words[words.Length - 1].Substring(0, 1).ToUpper();
                if (words.Length >= 2)
                    initials = words[words.Length - 2].Substring(0, 1).ToUpper() + initials;
            }
            ViewData["Avatar"] = initials;
        }

        // Users/Index
        public ActionResult Home()
        {
            ViewBag.Title = "Trang chủ";
            ViewBag.ActivePage = "home";
            ViewBag.ListVien = db.Viens.OrderBy(v => v.TenVien).ToList();
            ViewBag.DanhMucs = db.DanhMucs.ToList();
            ViewBag.StudentCount = db.SinhViens.Count();
            ViewBag.ClubCount = db.Viens.Count();

            var threeDaysAgo = DateTime.Now.AddDays(-3);

            var events = db.EVENTs
                .Include(e => e.DanhMuc)
                .Include(e => e.DiaDiem)
                .Include(e => e.Vien)
                .Where(e => e.IsHidden == false)
                .Where(e =>
                    e.TrangThai == "Sắp diễn ra"
                    || e.TrangThai == "Đang diễn ra"
                    || (e.TrangThai == "Đã kết thúc"
                        && (e.NgayKetThuc.HasValue
                            ? e.NgayKetThuc.Value >= threeDaysAgo
                            : e.NgayBatDau >= threeDaysAgo))
                )
                .OrderByDescending(e => e.NgayBatDau)
                .ToList();

            return View(events);
        }

        [RestrictGuest]
        [HttpGet]
        public ActionResult Notifications()
        {
            string studentId = GetCurrentStudentId();
            if (string.IsNullOrEmpty(studentId))
            {
                return RedirectToAction("Login", "Account");
            }

            var now = DateTime.Now;
            var inThreeDays = now.AddDays(3);

            var upcomingRegistrations = db.DangKySuKiens
                .Include(d => d.EVENT)
                .Where(d => d.IDSinhVien == studentId
                            && d.EVENT != null
                            && d.EVENT.IsHidden == false
                            && d.EVENT.NgayBatDau >= now)
                .OrderBy(d => d.EVENT.NgayBatDau)
                .Take(8)
                .ToList();

            var completedRecently = db.DangKySuKiens
                .Include(d => d.EVENT)
                .Where(d => d.IDSinhVien == studentId
                            && d.EVENT != null
                            && d.EVENT.IsHidden == false
                            && d.TrangThai == "Đã hoàn thành")
                .OrderByDescending(d => d.EVENT.NgayBatDau)
                .Take(5)
                .ToList();

            ViewBag.Notifications = upcomingRegistrations.Select(d => new
            {
                Title = d.EVENT.TenEvent,
                Message = d.EVENT.NgayBatDau <= inThreeDays
                    ? "Sắp diễn ra trong 3 ngày tới."
                    : "Bạn đã đăng ký sự kiện này.",
                TimeLabel = d.EVENT.NgayBatDau.ToString("dd/MM/yyyy HH:mm"),
                IsUrgent = d.EVENT.NgayBatDau <= inThreeDays
            }).ToList();

            ViewBag.CompletedNotifications = completedRecently.Select(d => new
            {
                Title = d.EVENT.TenEvent,
                Message = "Bạn đã hoàn thành sự kiện và được ghi nhận.",
                TimeLabel = d.EVENT.NgayBatDau.ToString("dd/MM/yyyy"),
                IsUrgent = false
            }).ToList();

            ViewBag.Title = "Thông báo";
            ViewBag.ActivePage = "notifications";
            return View();
        }

        [RestrictGuest]
        [HttpGet]
        public ActionResult Settings()
        {
            ViewBag.ReceiveInApp = Session["ReceiveInApp"] as bool? ?? true;
            ViewBag.ReceiveEmail = Session["ReceiveEmail"] as bool? ?? true;
            ViewBag.ReceiveReminder = Session["ReceiveReminder"] as bool? ?? true;
            ViewBag.Title = "Cài đặt";
            ViewBag.ActivePage = "settings";
            return View();
        }

        [RestrictGuest]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult UpdateSettings(bool? receiveInApp, bool? receiveEmail, bool? receiveReminder)
        {
            Session["ReceiveInApp"] = receiveInApp.HasValue;
            Session["ReceiveEmail"] = receiveEmail.HasValue;
            Session["ReceiveReminder"] = receiveReminder.HasValue;

            TempData["Success"] = "Đã cập nhật cài đặt thông báo.";
            return RedirectToAction("Settings");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }

        //Thong tin ca nhan nguoi dung
        [RestrictGuest]
        [HttpGet]
        public ActionResult Profile(string id = "")
        {
            string currentUserId = GetCurrentStudentId();
            string targetId = string.IsNullOrEmpty(id) ? currentUserId : id;
            var sv = db.SinhViens.FirstOrDefault(s => s.ID == targetId);
                
            if (sv == null)
            {
                if (!string.IsNullOrEmpty(id))
                {
                    TempData["Error"] = "Không tìm thấy hồ sơ này!";
                    return RedirectToAction("Home", "Home");
                }
                return RedirectToAction("Login", "Account");
            }

            ViewBag.IsCurrentUser = (currentUserId == targetId);

            int currentMonth = DateTime.Now.Month;
            int currentYear = DateTime.Now.Year;

            int currentSemester = (currentMonth >= 1 && currentMonth <= 5) ? 1 : 2;
            int namHocBatDau = (currentMonth >= 1 && currentMonth <= 5) ? currentYear - 1 : currentYear;

            var daHoanThanh = db.DangKySuKiens.Where(d => d.IDSinhVien == targetId && d.TrangThai == "Đã hoàn thành");

            // HK1 năm học (nam–nam+1): tháng 9–12 năm nam + tháng 1–5 năm nam+1 (không chỉ tháng 1–5 của năm hiện tại).
            IQueryable<DangKySuKien> trongHocKyHienTai(IQueryable<DangKySuKien> q)
            {
                if (currentSemester == 1)
                {
                    int cy = currentYear;
                    return q.Where(d =>
                        (d.EVENT.NgayBatDau.Year == cy && d.EVENT.NgayBatDau.Month >= 1 && d.EVENT.NgayBatDau.Month <= 5)
                        || (d.EVENT.NgayBatDau.Year == cy - 1 && d.EVENT.NgayBatDau.Month >= 9 && d.EVENT.NgayBatDau.Month <= 12));
                }

                return q.Where(d => d.EVENT.NgayBatDau.Year == currentYear
                    && d.EVENT.NgayBatDau.Month >= 6 && d.EVENT.NgayBatDau.Month <= 12);
            }

            var lichSuThamGia = trongHocKyHienTai(daHoanThanh)
                .Include(d => d.EVENT)
                .OrderByDescending(d => d.EVENT.NgayBatDau)
                .ToList();

            ViewBag.DaThamDu = lichSuThamGia;
            ViewBag.TongHoanThanh = lichSuThamGia.Count;

            var queryDRL = trongHocKyHienTai(daHoanThanh);

            ViewBag.DRL = queryDRL.Select(d => (int?)d.EVENT.DRL).Sum() ?? 0;
            ViewBag.HocKy = currentSemester;
            ViewBag.Nam = namHocBatDau;

            // 5. LẤY DANH SÁCH SỰ KIỆN ĐÃ TỔ CHỨC (Của targetId)
            ViewBag.CurrentYear = currentYear;
            ViewBag.SuKienTrongNam = db.EVENTs
                .Where(e => e.NguoiDang == targetId
                         && e.NgayBatDau.Year == currentYear
                         && e.IsHidden == false)
                .OrderByDescending(e => e.NgayBatDau)
                .ToList();

            ViewBag.ThanhTich = new List<string> {
        "Sinh viên 5 tốt cấp trường 2025",
        "Top 10 cuộc thi Hackathon Khoa CNTT",
        "Tích cực tham gia Mùa hè xanh"
    };

            ViewBag.Title = ViewBag.IsCurrentUser ? "Hồ sơ cá nhân" : $"Hồ sơ: {sv.Ten}";
            ViewBag.ActivePage = "profile";

            return View(sv);
        }

        // --- XỬ LÝ CẬP NHẬT THÔNG TIN ---
        [RestrictGuest]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult UpdateProfile(SinhVien model)
        {
            string studentId = GetCurrentStudentId();
            var sv = db.SinhViens.FirstOrDefault(s => s.ID == studentId);

            if (sv != null)
            {
                // Chỉ cho phép cập nhật những thông tin này (Không cho đổi MSSV hoặc Tên)
                sv.NgayThangNamSinh = model.NgayThangNamSinh;
                sv.SoDienThoai = model.SoDienThoai;
                sv.Email = model.Email;
                // sv.Lop = model.Lop; // Tùy bạn có cho sinh viên tự đổi lớp không

                db.SaveChanges();
                TempData["Success"] = "Cập nhật thông tin thành công!";
            }
            else
            {
                TempData["Error"] = "Không tìm thấy thông tin sinh viên.";
            }

            return RedirectToAction("Profile");
        }

        //tim kiem thong minh tren navbar
        [RestrictGuest]
        [HttpGet]
        public ActionResult SmartSearch(string keyword)
        {
            if (string.IsNullOrWhiteSpace(keyword))
            {
                return RedirectToAction("Home");
            }

            string searchKeyword = keyword.Trim();

            // Kiểm tra xem từ khóa có trùng với bất kỳ ID Sinh viên / CLB nào không
            var userExists = db.SinhViens.Any(s => s.ID.ToLower() == searchKeyword.ToLower());

            if (userExists)
            {
                // Chuyển hướng sang trang Profile của người đó
                return RedirectToAction("Profile", "Home", new { id = searchKeyword });
            }

            // Chuyển hướng sang trang Sự kiện để tìm tên sự kiện
            return RedirectToAction("Events", "Events", new { keyword = searchKeyword });
        }
    }
}

