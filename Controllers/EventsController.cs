using school_event_management.Models;
using shcool_event_management.Models;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace shcool_event_management.Controllers
{
    public class EventsController : Controller
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

        public ActionResult Events(string[] vien, string[] danhmuc, string time, string[] status, string sort, string keyword = "", int page = 1)
        {
            ViewBag.Title = "Sự kiện";
            ViewBag.ActivePage = "events";
            ViewBag.ListVien = db.Viens.OrderBy(v => v.TenVien).ToList();
            ViewBag.DanhMucs = db.DanhMucs.ToList();

            int pageSize = 6;
            var today = DateTime.Today;

            var query = db.EVENTs
                .Include(e => e.DanhMuc)
                .Include(e => e.DiaDiem)
                .Include(e => e.Vien)
                .Where(e => e.IsHidden == false)
                .AsQueryable();

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
                {
                    query = query.Where(e => DbFunctions.TruncateTime(e.NgayBatDau) == today);
                }
                else if (time == "week")
                {
                    DateTime nextWeek = today.AddDays(7);
                    query = query.Where(e => e.NgayBatDau >= today && e.NgayBatDau <= nextWeek);
                }
                else if (time == "month")
                {
                    query = query.Where(e => e.NgayBatDau.Month == today.Month && e.NgayBatDau.Year == today.Year);
                }
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

        //EventDetail
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

        // Toggle sự kiện yêu thích
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
    }
}