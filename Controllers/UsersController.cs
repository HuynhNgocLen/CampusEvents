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
    public class UsersController : Controller
    {
        private readonly school_event_managementEntities db = new school_event_managementEntities();
        private string GetCurrentStudentId()
        {
            return JwtService.GetStudentId(Request);
        }

        private void LoadFavoriteData()
        {
            string studentId = GetCurrentStudentId();
            if (!string.IsNullOrEmpty(studentId))
            {
                ViewBag.SavedEventIds = db.SuKienYeuThiches
                                          .Where(f => f.IDSinhVien == studentId)
                                          .Select(f => f.MaEvent)
                                          .ToList();
            }
            else
            {
                ViewBag.SavedEventIds = new List<int>();
            }
        }

        protected override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            base.OnActionExecuting(filterContext);

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
        public ActionResult Index()
        {
            ViewBag.Title = "Khám phá Sự kiện";
            ViewBag.ActivePage = "home";
            ViewBag.ListVien = db.Viens.OrderBy(v => v.TenVien).ToList();
            ViewBag.DanhMucs = db.DanhMucs.ToList();

            var events = db.EVENTs
                .Include(e => e.DanhMuc)
                .Include(e => e.DiaDiem)
                .Include(e => e.Vien)
                .OrderByDescending(e => e.NgayBatDau)
                .ToList();

            return View(events);
        }

        // Users/Events
        public ActionResult Events(string[] vien, string[] danhmuc, string time, string[] status, string sort, string keyword = "", int page = 1)
        {
            ViewBag.Title = "Sự kiện";
            ViewBag.ActivePage = "events";
            ViewBag.ListVien = db.Viens.OrderBy(v => v.TenVien).ToList();
            ViewBag.DanhMucs = db.DanhMucs.ToList();

            int pageSize = 6;
            var today = DateTime.Today;

            // 1. CHỈ LẤY SỰ KIỆN KHÔNG BỊ ẨN
            var query = db.EVENTs
                .Include(e => e.DanhMuc)
                .Include(e => e.DiaDiem)
                .Include(e => e.Vien)
                .Where(e => e.IsHidden == false)
                .AsQueryable();

            // ---------------------------------------------------------
            // [MỚI] BỘ LỌC TÌM KIẾM TỪ KHÓA TỪ NAVBAR
            // ---------------------------------------------------------
            if (!string.IsNullOrEmpty(keyword))
            {
                string search = keyword.Trim().ToLower();
                query = query.Where(e => e.TenEvent.ToLower().Contains(search)
                                      || (e.MoTa != null && e.MoTa.ToLower().Contains(search)));

                // Lưu lại để hiển thị ngoài View
                ViewBag.Keyword = keyword;
            }

            // 2. Lọc theo Viện, Danh mục
            var selectedVien = vien?.ToList() ?? new List<string>();
            if (selectedVien.Count > 0 && !selectedVien.Contains("all"))
                query = query.Where(e => selectedVien.Contains(e.MaVien));

            var selectedDM = danhmuc?.ToList() ?? new List<string>();
            if (selectedDM.Count > 0 && !selectedDM.Contains("all"))
                query = query.Where(e => selectedDM.Contains(e.MaDanhMuc));

            // 3. Lọc theo Thời gian
            if (!string.IsNullOrEmpty(time) && time != "all")
            {
                if (time == "today")
                    query = query.Where(e => System.Data.Entity.DbFunctions.TruncateTime(e.NgayBatDau) == today);
                else if (time == "week")
                    query = query.Where(e => e.NgayBatDau >= today && e.NgayBatDau <= today.AddDays(7));
                else if (time == "month")
                    query = query.Where(e => e.NgayBatDau.Month == today.Month && e.NgayBatDau.Year == today.Year);
            }

            // 4. Lọc theo Trạng thái
            var selectedStatus = status?.ToList() ?? new List<string>();
            if (selectedStatus.Count > 0 && !selectedStatus.Contains("all"))
            {
                query = query.Where(e =>
                    (selectedStatus.Contains("free") && e.GiaVe == 0) ||
                    (selectedStatus.Contains("available") && (e.SoLuongToiDa - e.SoLuongDaDangKy) > 0) ||
                    (selectedStatus.Contains("almost") && (e.SoLuongToiDa - e.SoLuongDaDangKy) <= 20 && (e.SoLuongToiDa - e.SoLuongDaDangKy) > 0)
                );
            }

            // 5. XỬ LÝ SẮP XẾP
            if (sort == "newest")
            {
                query = query.Where(e => DbFunctions.TruncateTime(e.NgayBatDauDangKy) == today)
                             .OrderByDescending(e => e.MaEvent);
            }
            else if (sort == "upcoming")
            {
                DateTime tomorrow = today.AddDays(1);
                query = query.Where(e => e.NgayBatDau >= tomorrow)
                             .OrderBy(e => e.NgayBatDau);
            }
            else if (sort == "popular")
            {
                query = query.OrderByDescending(e => e.SoLuongDaDangKy);
            }
            else
            {
                query = query.OrderByDescending(e => e.NgayBatDau);
            }

            // 6. PHÂN TRANG
            var totalItems = query.Count();
            var data = query.Skip((page - 1) * pageSize).Take(pageSize).ToList();

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = (int)Math.Ceiling((double)totalItems / pageSize);
            ViewBag.CurrentSort = sort;

            LoadFavoriteData();
            return View(data);
        }

        // Users/EventDetail
        public ActionResult EventDetail(int? id)
        {
            if (id == null) return RedirectToAction("Events");

            string currentStudentId = GetCurrentStudentId();
            ViewBag.SinhVien = db.SinhViens.Include(s => s.Vien).FirstOrDefault(s => s.ID == currentStudentId);

            var tinhTrang = db.vw_SoChoConLai.FirstOrDefault(v => v.MaEvent == id);
            ViewBag.tinhTrang = tinhTrang;
            ViewBag.phanTram = (tinhTrang != null && tinhTrang.SoLuongToiDa > 0)
                ? tinhTrang.SoLuongDaDangKy * 100 / tinhTrang.SoLuongToiDa
                : 0;

            db.Configuration.ProxyCreationEnabled = false;

            var ev = db.EVENTs
                       .Include(e => e.DanhMuc)
                       .Include(e => e.Vien)
                       .Include(e => e.DiaDiem)
                       .FirstOrDefault(e => e.MaEvent == id);

            if (ev == null) return HttpNotFound();

            bool daDangKy = db.DangKySuKiens.Any(d =>
                d.MaEvent == id && d.IDSinhVien == currentStudentId && !d.TrangThai.Contains("Hủy"));
            ViewBag.DaDangKy = daDangKy;

            DateTime ngayHetHan = ev.NgayHetHanDangKy ?? DateTime.Now;
            ViewBag.SoNgayConLai = (ngayHetHan.Date - DateTime.Now.Date).Days;

            LoadFavoriteData();
            return View(ev);
        }

        // Users/Registrations
        public ActionResult Registrations()
        {
            ViewBag.Title = "Đăng ký của tôi";
            ViewBag.ActivePage = "registrations";

            string studentId = GetCurrentStudentId();
            int currentYear = DateTime.Now.Year;

            ViewBag.SinhVien = db.SinhViens.FirstOrDefault(s => s.ID == studentId);
            int tongHoanThanh = db.DangKySuKiens
                .Count(d => d.IDSinhVien == studentId
                         && d.NgayDangKy.Year == currentYear
                         && d.TrangThai.Trim() == "Đã hoàn thành");

            int tongHuy = db.DangKySuKiens
                .Count(d => d.IDSinhVien == studentId
                         && d.NgayDangKy.Year == currentYear
                         && d.TrangThai.ToLower() == "Hủy");
            int tongDangKy = db.DangKySuKiens
                .Count(d => d.IDSinhVien == studentId
                         && d.NgayDangKy.Year == currentYear
                         && d.TrangThai.Trim() == "Đã đăng ký");

            ViewData["TongHoanThanhNam"] = tongHoanThanh;
            ViewData["TongHuyNam"] = tongHuy;
            ViewData["TongDangKyNam"] = tongHoanThanh + tongDangKy;

            var today = DateTime.Today;

            ViewBag.DaDangKy = db.DangKySuKiens
                .Include(d => d.EVENT).Include(d => d.EVENT.DanhMuc).Include(d => d.EVENT.DiaDiem)
                .Where(d => d.IDSinhVien == studentId && d.TrangThai == "Đã đăng ký" && d.EVENT.NgayBatDau >= today)
                .OrderBy(d => d.EVENT.NgayBatDau).ToList();

            ViewBag.DaThamDu = db.DangKySuKiens
                .Include(d => d.EVENT).Include(d => d.EVENT.DanhMuc).Include(d => d.EVENT.DiaDiem)
                .Where(d => d.IDSinhVien == studentId && d.TrangThai.Trim() == "Đã hoàn thành")
                .OrderByDescending(d => d.EVENT.NgayBatDau).ToList();

            ViewBag.DaHuy = db.DangKySuKiens
                .Include(d => d.EVENT).Include(d => d.EVENT.DanhMuc).Include(d => d.EVENT.DiaDiem)
                .Where(d => d.IDSinhVien == studentId && (d.TrangThai == "Hủy" || d.TrangThai == "Quá hạn"))
                .OrderByDescending(d => d.NgayDangKy).ToList();

            ViewBag.DaLuu = db.SuKienYeuThiches
                .Where(f => f.IDSinhVien == studentId)
                .OrderByDescending(f => f.NgayLuu).AsEnumerable()
                .Select(f => new DangKySuKien
                {
                    MaEvent = f.MaEvent,
                    IDSinhVien = f.IDSinhVien,
                    EVENT = f.EVENT,
                    TrangThai = "Đã lưu"
                }).ToList();

            return View("~/Views/Users/Registrations/Registrations.cshtml");
        }

        [HttpPost]
        public JsonResult ToggleFavorite(int maEvent)
        {
            try
            {
                string studentId = GetCurrentStudentId();
                if (string.IsNullOrEmpty(studentId))
                {
                    return Json(new { success = false, message = "Vui lòng đăng nhập để lưu sự kiện." });
                }

                // Kiểm tra xem sự kiện đã được lưu chưa
                var favorite = db.SuKienYeuThiches.FirstOrDefault(f => f.MaEvent == maEvent && f.IDSinhVien == studentId);

                bool isFavorite = false;

                if (favorite != null)
                {
                    // Đã lưu -> Bỏ lưu
                    db.SuKienYeuThiches.Remove(favorite);
                    db.SaveChanges();
                    isFavorite = false;
                }
                else
                {
                    // Chưa lưu -> Thêm mới
                    db.SuKienYeuThiches.Add(new SuKienYeuThich
                    {
                        MaEvent = maEvent,
                        IDSinhVien = studentId,
                        NgayLuu = DateTime.Now,
                        TrangThai = "Luu"
                    });
                    db.SaveChanges();
                    isFavorite = true;
                }

                return Json(new { success = true, isFavorite = isFavorite });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi: " + ex.Message });
            }
        }

        // Xác nhận đăng ký sự kiện
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ConfirmRegister(int eventId)
        {
            string studentId = GetCurrentStudentId();

            var ev = db.EVENTs.FirstOrDefault(e => e.MaEvent == eventId);
            if (ev == null)
            {
                TempData["Error"] = "Sự kiện không tồn tại.";
                return RedirectToAction("EventDetail", new { id = eventId });
            }

            DateTime now = DateTime.Now;

            //KIỂM TRA CHƯA TỚI NGÀY ĐĂNG KÝ
            if (ev.NgayBatDauDangKy.HasValue && now < ev.NgayBatDauDangKy.Value)
            {
                TempData["Error"] = $"Sự kiện này sẽ mở đăng ký vào lúc {ev.NgayBatDauDangKy.Value.ToString("HH:mm dd/MM/yyyy")}.";
                return RedirectToAction("EventDetail", new { id = eventId });
            }

            // KIỂM TRA ĐÃ QUÁ HẠN ĐĂNG KÝ
            if (ev.NgayHetHanDangKy.HasValue && now.Date > ev.NgayHetHanDangKy.Value.Date)
            {
                TempData["Error"] = "Sự kiện này đã hết hạn đăng ký.";
                return RedirectToAction("EventDetail", new { id = eventId });
            }

            // KIỂM TRA ĐÃ ĐĂNG KÝ RỒI
            bool daDangKy = db.DangKySuKiens.Any(d =>
                d.MaEvent == eventId && d.IDSinhVien == studentId && !d.TrangThai.ToLower().Contains("Hủy"));

            if (daDangKy)
            {
                TempData["Error"] = "Bạn đã đăng ký sự kiện này rồi.";
                return RedirectToAction("EventDetail", new { id = eventId });
            }

            // KIỂM TRA ĐÃ ĐẦY CHỖ
            var tinhTrang = db.vw_SoChoConLai.FirstOrDefault(v => v.MaEvent == eventId);
            if (tinhTrang == null || tinhTrang.SoChoConLai <= 0)
            {
                TempData["Error"] = "Sự kiện này đã đầy (hết chỗ).";
                return RedirectToAction("EventDetail", new { id = eventId });
            }

            try
            {
                var existingReg = db.DangKySuKiens
                    .FirstOrDefault(d => d.MaEvent == eventId && d.IDSinhVien == studentId);

                if (existingReg != null)
                {
                    existingReg.TrangThai = "Đã đăng ký";
                    existingReg.NgayDangKy = DateTime.Now;
                }
                else
                {
                    db.DangKySuKiens.Add(new DangKySuKien
                    {
                        MaEvent = eventId,
                        IDSinhVien = studentId,
                        NgayDangKy = DateTime.Now,
                        TrangThai = "Đã đăng ký"
                    });
                }

                db.SaveChanges();
                TempData["ShowSuccessModal"] = true;
                return RedirectToAction("EventDetail", new { id = eventId });
            }
            catch (Exception ex)
            {
                var innerMsg = ex.InnerException?.InnerException?.Message ?? ex.Message;
                TempData["Error"] = "Có lỗi xảy ra: " + innerMsg;
                return RedirectToAction("EventDetail", new { id = eventId });
            }
        }

        // Hủy đăng ký
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult HuyDangKy(int maEvent)
        {
            string studentId = GetCurrentStudentId();
            var dangKy = db.DangKySuKiens.FirstOrDefault(d => d.MaEvent == maEvent && d.IDSinhVien == studentId);

            if (dangKy == null)
            {
                TempData["Error"] = "Không tìm thấy đăng ký.";
                return RedirectToAction("Registrations");
            }

            dangKy.TrangThai = "Hủy";
            db.SaveChanges();

            TempData["Success"] = "Đã hủy đăng ký thành công.";
            return RedirectToAction("Registrations");
        }

        // Users/Schedule
        public ActionResult Schedule(int offset = 0)
        {
            ViewBag.Title = "Lịch sự kiện của tôi";
            ViewBag.ActivePage = "schedule";

            string studentId = GetCurrentStudentId();
            if (string.IsNullOrEmpty(studentId))
            {
                return RedirectToAction("Login", "Auth");
            }

            // 1. Tính toán ngày dựa trên offset
            // offset = 0 (Tuần này), offset = 1 (Tuần sau), offset = -1 (Tuần trước)
            DateTime baseDate = DateTime.Today.AddDays(offset * 7);

            int diff = (7 + (baseDate.DayOfWeek - DayOfWeek.Monday)) % 7;
            DateTime startOfWeek = baseDate.AddDays(-1 * diff).Date;
            DateTime endOfWeek = startOfWeek.AddDays(7).AddTicks(-1);

            // 2. Truy vấn sự kiện
            var eventsInWeek = db.DangKySuKiens
                .Include(d => d.EVENT)
                .Include(d => d.EVENT.DiaDiem)
                .Include(d => d.EVENT.DanhMuc)
                .Where(d => d.IDSinhVien == studentId
                         && d.TrangThai != "Hủy"
                         && d.EVENT.NgayBatDau >= startOfWeek
                         && d.EVENT.NgayBatDau <= endOfWeek)
                .OrderBy(d => d.EVENT.NgayBatDau)
                .ToList();

            // 3. Truyền dữ liệu ra View
            ViewBag.StartOfWeek = startOfWeek;
            ViewBag.EndOfWeek = endOfWeek;
            ViewBag.WeekOffset = offset; // Truyền offset ra View để làm nút Bấm

            return View(eventsInWeek);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }

        //Thong tin ca nhan nguoi dung
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
                    return RedirectToAction("Index", "Home");
                }
                return RedirectToAction("Login", "Auth");
            }

            ViewBag.IsCurrentUser = (currentUserId == targetId);

            int currentMonth = DateTime.Now.Month;
            int currentYear = DateTime.Now.Year;

            int currentSemester = (currentMonth >= 1 && currentMonth <= 5) ? 1 : 2;
            int namHocBatDau = (currentMonth >= 1 && currentMonth <= 5) ? currentYear - 1 : currentYear;

            var lichSuThamGia = db.DangKySuKiens
                .Include(d => d.EVENT)
                .Where(d => d.IDSinhVien == targetId && d.TrangThai.Trim() == "Đã hoàn thành")
                .OrderByDescending(d => d.EVENT.NgayBatDau)
                .ToList();

            ViewBag.DaThamDu = lichSuThamGia;
            ViewBag.TongHoanThanh = lichSuThamGia.Count;

            var queryDRL = db.DangKySuKiens.Where(d => d.IDSinhVien == targetId && d.TrangThai.Trim() == "Đã hoàn thành");
            if (currentSemester == 1)
            {
                queryDRL = queryDRL.Where(d => d.EVENT.NgayBatDau.Year == currentYear && d.EVENT.NgayBatDau.Month >= 1 && d.EVENT.NgayBatDau.Month <= 5);
            }
            else
            {
                queryDRL = queryDRL.Where(d => d.EVENT.NgayBatDau.Year == currentYear && d.EVENT.NgayBatDau.Month >= 6 && d.EVENT.NgayBatDau.Month <= 12);
            }

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
        [HttpGet]
        public ActionResult SmartSearch(string keyword)
        {
            if (string.IsNullOrWhiteSpace(keyword))
            {
                return RedirectToAction("Index");
            }

            string searchKeyword = keyword.Trim();

            // Kiểm tra xem từ khóa có trùng với bất kỳ ID Sinh viên / CLB nào không
            var userExists = db.SinhViens.Any(s => s.ID.ToLower() == searchKeyword.ToLower());

            if (userExists)
            {
                // Chuyển hướng sang trang Profile của người đó
                return RedirectToAction("Profile", "Users", new { id = searchKeyword });
            }

            // Chuyển hướng sang trang Sự kiện để tìm tên sự kiện
            return RedirectToAction("Events", "Users", new { keyword = searchKeyword });
        }
    }
}

