using school_event_management.Models;
using shcool_event_management.Models;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Globalization;
using System.Linq;
using System.Web.Mvc;

namespace school_event_management.Controllers
{
    public class UsersController : Controller
    {
        private readonly SchoolEventManagementEntities db = new SchoolEventManagementEntities();

        //Lấy MSSV hiện tại 
        private string GetCurrentStudentId()
        {
            return Session["StudentId"] as string ?? "SV001";
        }

        //Đã lưu
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

        //Users/Index
        public ActionResult Index()
        {
            ViewBag.Title = "Khám phá Sự kiện";
            ViewBag.ActivePage = "home";
            ViewBag.UserName = "Sinh Viên";

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

        //Users/Events
        public ActionResult Events(string[] vien, string[] danhmuc, string time, string[] status)
        {
            ViewBag.Title = "Sự kiện";
            ViewBag.ActivePage = "events";
            ViewBag.UserName = "Sinh Viên";

            // Dữ liệu cho Filter Sidebar
            ViewBag.ListVien = db.Viens.OrderBy(v => v.TenVien).ToList();
            ViewBag.DanhMucs = db.DanhMucs.ToList();

            var query = db.EVENTs
                .Include(e => e.DanhMuc)
                .Include(e => e.DiaDiem)
                .Include(e => e.Vien)
                .AsQueryable();

            // Lọc theo Viện
            var selectedVien = vien?.ToList() ?? new List<string>();
            if (selectedVien.Count > 0 && !selectedVien.Contains("all"))
            {
                query = query.Where(e => selectedVien.Contains(e.MaVien));
            }

            // Lọc theo Danh mục
            var selectedDM = danhmuc?.ToList() ?? new List<string>();
            if (selectedDM.Count > 0 && !selectedDM.Contains("all"))
            {
                query = query.Where(e => selectedDM.Contains(e.MaDanhMuc));
            }

            // Lọc theo Thời gian
            var today = DateTime.Today;
            if (!string.IsNullOrEmpty(time) && time != "all")
            {
                if (time == "today")
                    query = query.Where(e => DbFunctions.TruncateTime(e.NgayBatDau) == today);
                else if (time == "week")
                    query = query.Where(e => e.NgayBatDau >= today && e.NgayBatDau <= today.AddDays(7));
                else if (time == "month")
                    query = query.Where(e => e.NgayBatDau.Month == today.Month && e.NgayBatDau.Year == today.Year);
            }

            // Lọc theo Trạng thái
            var selectedStatus = status?.ToList() ?? new List<string>();
            if (selectedStatus.Count > 0 && !selectedStatus.Contains("all"))
            {
                query = query.Where(e =>
                    (selectedStatus.Contains("free") && e.GiaVe == 0) ||
                    (selectedStatus.Contains("available") && (e.SoLuongToiDa - e.SoLuongDaDangKy) > 0) ||
                    (selectedStatus.Contains("almost") && (e.SoLuongToiDa - e.SoLuongDaDangKy) <= 20 && (e.SoLuongToiDa - e.SoLuongDaDangKy) > 0)
                );
            }

            var data = query.OrderByDescending(e => e.NgayBatDau).ToList();
            LoadFavoriteData();
            return View(data);
        }


        public ActionResult EventDetail(int? id)
        {
            if (id == null) return RedirectToAction("Events");
            string currentStudentId = GetCurrentStudentId();
            System.Diagnostics.Debug.WriteLine("ID: " + currentStudentId);
            ViewBag.SinhVien = db.SinhViens.Include(s => s.Vien).FirstOrDefault(s => s.ID == currentStudentId);

            //So Cho Con Lai
            var tinhTrang = db.vw_SoChoConLai.FirstOrDefault(v => v.MaEvent == id);
            ViewBag.tinhTrang = tinhTrang;

            //Phan Tram
            ViewBag.phanTram = tinhTrang.SoLuongDaDangKy * 100 / tinhTrang.SoLuongToiDa;

            db.Configuration.ProxyCreationEnabled = false;

            var ev = db.EVENTs
                       .Include(e => e.DanhMuc)
                       .Include(e => e.Vien)
                       .Include(e => e.DiaDiem)
                       .FirstOrDefault(e => e.MaEvent == id);

            if (ev == null) return HttpNotFound();

            bool daDangKy = db.DangKySuKiens.Any(d => d.MaEvent == id&& d.IDSinhVien == currentStudentId && !d.TrangThai.Contains("Hủy"));
            ViewBag.DaDangKy = daDangKy;

            //Time 
            DateTime ngayHetHan = ev.NgayHetHanDangKy ?? DateTime.Now;
            DateTime ngayHienTai = DateTime.Now.Date;
            TimeSpan difference = ngayHetHan - ngayHienTai;
            int soNgayConLai = difference.Days;
            ViewBag.SoNgayConLai = soNgayConLai;

            if (!string.IsNullOrEmpty(ev.LinkZalo))
            {
                ViewBag.QRCodeBase64 = QRCodeHelper.GenerateQRCodeFromLink(ev.LinkZalo);
            }

            LoadFavoriteData();
            return View(ev);
        }

        // Users/Registrations
        public ActionResult Registrations()
        {
            ViewBag.Title = "Đăng ký của tôi";
            ViewBag.ActivePage = "registrations";

            string studentId = GetCurrentStudentId();
            int currentYear = DateTime.Now.Year; // Lấy năm hiện tại (2026)

            var sv = db.SinhViens.FirstOrDefault(s => s.ID == studentId);
            ViewBag.SinhVien = sv;

            // --- LOGIC THỐNG KÊ THEO NĂM ---

            // 1. Tổng Đăng Ký Trong Năm (Tất cả trừ trạng thái Hủy)
            ViewData["TongDangKyNam"] = db.DangKySuKiens
                .Count(d => d.IDSinhVien == studentId
                       && d.NgayDangKy.Year == currentYear
                       && d.TrangThai != "hủy");

            // 2. Tổng Hoàn Thành Trong Năm
            ViewData["TongHoanThanhNam"] = db.DangKySuKiens
                .Count(d => d.IDSinhVien == studentId
                       && d.NgayDangKy.Year == currentYear
                       && d.TrangThai.Trim() == "Đã hoàn thành");

            // 3. Tổng Hủy Trong Năm
            ViewData["TongHuyNam"] = db.DangKySuKiens
                .Count(d => d.IDSinhVien == studentId
                       && d.NgayDangKy.Year == currentYear
                       && d.TrangThai.ToLower() == "hủy");

            // --- DỮ LIỆU CHO CÁC TAB ---
            var today = DateTime.Today;

            // Sự kiện sắp tới (Tab Đã đăng ký)
            ViewBag.DaDangKy = db.DangKySuKiens
                .Include(d => d.EVENT).Include(d => d.EVENT.DanhMuc).Include(d => d.EVENT.DiaDiem)
                .Where(d => d.IDSinhVien == studentId && d.TrangThai == "Đã đăng ký" && d.EVENT.NgayBatDau >= today)
                .OrderBy(d => d.EVENT.NgayBatDau).ToList();

            // Tab Đã tham dự
            ViewBag.DaThamDu = db.DangKySuKiens
                .Include(d => d.EVENT).Include(d => d.EVENT.DanhMuc).Include(d => d.EVENT.DiaDiem)
                .Where(d => d.IDSinhVien == studentId && d.TrangThai.Trim() == "Đã hoàn thành")
                .OrderByDescending(d => d.EVENT.NgayBatDau).ToList();

            // Tab Đã hủy
            ViewBag.DaHuy = db.DangKySuKiens
                .Include(d => d.EVENT).Include(d => d.EVENT.DanhMuc).Include(d => d.EVENT.DiaDiem)
                .Where(d => d.IDSinhVien == studentId && (d.TrangThai == "hủy" || d.TrangThai == "Quá hạn"))
                .OrderByDescending(d => d.NgayDangKy).ToList();

            // Tab Đã lưu
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

            // Truyền thêm dữ liệu cho Sidebar (nếu cần xử lý avatar ở Controller)
            ViewData["TenHienThi"] = sv?.Ten ?? "Sinh Viên";
            ViewData["MaSV"] = sv?.ID ?? "---";
            ViewData["Lop"] = sv?.Lop ?? "";
            ViewData["Avatar"] = !string.IsNullOrEmpty(sv?.Ten) ? sv.Ten.Substring(0, 1).ToUpper() : "SV";

            return View("~/Views/Users/Registrations/Registrations.cshtml");
        }

        // Thêm vào yêu thích
        [HttpPost]
        [ValidateAntiForgeryToken]
        public JsonResult AddFavorite(int maEvent)
        {
            try
            {
                string studentId = GetCurrentStudentId();
                // Kiểm tra tồn tại
                if (db.SuKienYeuThiches.Any(f => f.MaEvent == maEvent && f.IDSinhVien == studentId))
                    return Json(new { success = true });

                var favorite = new SuKienYeuThich
                {
                    MaEvent = maEvent,
                    IDSinhVien = studentId,
                    NgayLuu = DateTime.Now,
                    TrangThai = "Luu"
                };

                db.SuKienYeuThiches.Add(favorite);
                db.SaveChanges();
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi: " + ex.Message });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public JsonResult RemoveFavorite(int maEvent)
        {
            string studentId = GetCurrentStudentId();
            var favorite = db.SuKienYeuThiches.FirstOrDefault(f => f.MaEvent == maEvent && f.IDSinhVien == studentId);

            if (favorite != null)
            {
                db.SuKienYeuThiches.Remove(favorite);
                db.SaveChanges();
            }

            return Json(new { success = true });
        }

        //Xác nhận đăng ký
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ConfirmRegister(int eventId)
        {
            string studentId = GetCurrentStudentId();

            var daDangKy = db.DangKySuKiens.Any(d =>d.MaEvent == eventId&& d.IDSinhVien == studentId&& !d.TrangThai.ToLower().Contains("Hủy"));

            if (daDangKy)
            {
                TempData["Error"] = "Bạn đã đăng ký sự kiện này rồi.";
                return RedirectToAction("EventDetail", new { id = eventId });
            }

            var tinhTrang = db.vw_SoChoConLai.FirstOrDefault(v => v.MaEvent == eventId);
            if (tinhTrang == null || tinhTrang.SoChoConLai <= 0)
            {
                TempData["Error"] = "Sự kiện này đã hết chỗ mất rồi!";
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
                    var reg = new DangKySuKien
                    {
                        MaEvent = eventId,
                        IDSinhVien = studentId,
                        NgayDangKy = DateTime.Now,
                        TrangThai = "Đã đăng ký"
                    };
                    db.DangKySuKiens.Add(reg);
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

            dangKy.TrangThai = "hủy";
            db.SaveChanges();

            TempData["Success"] = "Đã hủy đăng ký thành công.";
            TempData["ShowSuccessModal"] = false;
            return RedirectToAction("Registrations");
        }

        // Schedule
        public ActionResult Schedule()
        {
            ViewBag.Title = "Lịch của tôi";
            ViewBag.ActivePage = "schedule";
            ViewBag.UserName = "Sinh Viên";
            return View();
        }

    }
}