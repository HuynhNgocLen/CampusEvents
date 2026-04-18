using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using shcool_event_management.Models;

namespace shcool_event_management.Areas.Admin.Controllers
{
    // [Authorize]
    public class AdminEventsController : Controller
    {
        private readonly school_event_managementEntities _db
            = new school_event_managementEntities();

        private void PopulateDropdowns()
        {
            ViewBag.DanhMucList = _db.DanhMucs
                .Select(d => new SelectListItem
                {
                    Value = d.MaDanhMuc,
                    Text = d.TenDanhMuc
                })
                .ToList();

            ViewBag.DiaDiemList = _db.DiaDiems
                .Select(d => new SelectListItem
                {
                    Value = d.MaDiaDiem.ToString(),
                    Text = d.TenDiaDiem + (!string.IsNullOrEmpty(d.DiaChiChiTiet)
                                         ? " - " + d.DiaChiChiTiet
                                         : "")
                })
                .ToList();

            // Thêm danh sách Viện (hardcode vì bảng Vien ít)
            ViewBag.VienList = new List<SelectListItem>
            {
                new SelectListItem { Value = "CNS", Text = "Viện Công Nghệ Số" },
                new SelectListItem { Value = "KTCN", Text = "Viện Kỹ Thuật Công Nghệ" }
            };
        }

        public ActionResult Index()
        {
            ViewBag.ActiveMenu = "events";
            var events = _db.EVENTs
                            .Include("DanhMuc")
                            .Include("DiaDiem")
                            .OrderByDescending(e => e.NgayTao)
                            .ToList();
            return View(events);
        }

        public ActionResult Create()
        {
            ViewBag.ActiveMenu = "create";
            PopulateDropdowns();
            return View(new EVENT());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ValidateInput(false)]
        public ActionResult Create(EVENT model,
                                   HttpPostedFileBase coverImage,
                                   string btnAction)
        {
            ViewBag.ActiveMenu = "create";

            // Populate dropdowns khi validation fail
            PopulateDropdowns();

            // === 1. ModelState Validation ===
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            // === 2. Validation thủ công (rất quan trọng) ===
            if (string.IsNullOrWhiteSpace(model.TenEvent))
            {
                ModelState.AddModelError("TenEvent", "Tên sự kiện không được để trống.");
                return View(model);
            }

            if (string.IsNullOrWhiteSpace(model.MaDanhMuc))
            {
                ModelState.AddModelError("MaDanhMuc", "Vui lòng chọn danh mục sự kiện.");
                return View(model);
            }

            if (string.IsNullOrWhiteSpace(model.MaVien))
            {
                ModelState.AddModelError("MaVien", "Vui lòng chọn viện tổ chức.");
                return View(model);
            }

            if (!model.MaDiaDiem.HasValue)
            {
                ModelState.AddModelError("MaDiaDiem", "Vui lòng chọn địa điểm tổ chức.");
                return View(model);
            }

            if (model.NgayBatDau == default(DateTime) || model.NgayBatDau < DateTime.Now)
            {
                ModelState.AddModelError("NgayBatDau", "Ngày và giờ bắt đầu phải lớn hơn thời điểm hiện tại.");
                return View(model);
            }

            if (model.NgayHetHanDangKy.HasValue && model.NgayHetHanDangKy.Value < model.NgayBatDau.Date)
            {
                ModelState.AddModelError("NgayHetHanDangKy", "Hạn đăng ký phải trước hoặc bằng ngày bắt đầu sự kiện.");
                return View(model);
            }

            if (model.SoLuongToiDa < 10)
            {
                ModelState.AddModelError("SoLuongToiDa", "Số lượng tối đa phải ít nhất là 10 người.");
                return View(model);
            }

            // === 3. Xử lý ảnh bìa (BẮT BUỘC) ===
            if (coverImage == null || coverImage.ContentLength == 0)
            {
                ModelState.AddModelError("", "Vui lòng chọn ảnh bìa cho sự kiện.");
                return View(model);
            }

            const int maxBytes = 5 * 1024 * 1024; // 5MB
            var allowed = new[] { "image/jpeg", "image/png" };

            if (coverImage.ContentLength > maxBytes)
            {
                ModelState.AddModelError("", "Ảnh bìa phải nhỏ hơn 5 MB.");
                return View(model);
            }

            if (!allowed.Contains(coverImage.ContentType))
            {
                ModelState.AddModelError("", "Chỉ chấp nhận file JPG hoặc PNG.");
                return View(model);
            }

            try
            {
                var uploadDir = Server.MapPath("~/Uploads/Events/");
                Directory.CreateDirectory(uploadDir);

                var ext = Path.GetExtension(coverImage.FileName).ToLower();
                var fileName = $"ev_{DateTime.Now:yyyyMMddHHmmss}{ext}";

                coverImage.SaveAs(Path.Combine(uploadDir, fileName));
                model.AnhBia = "/Uploads/Events/" + fileName;
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Lỗi khi tải lên ảnh bìa: " + ex.Message);
                return View(model);
            }

            // === 4. Đặt giá trị mặc định ===
            model.TrangThai = btnAction == "draft" ? "Bản nháp" : "Sắp diễn ra";
            model.NgayTao = DateTime.Now;
            model.SoLuongDaDangKy = 0;
            model.IsHidden = false;
            model.NguoiDang = Session["UserId"]?.ToString() ?? "SV001";
            model.LuotXem = 0;

            // Xử lý trường hợp NgayKetThuc null
            if (model.NgayKetThuc == default(DateTime))
                model.NgayKetThuc = null;

            // === 5. Lưu vào database ===
            try
            {
                _db.EVENTs.Add(model);
                _db.SaveChanges();

                TempData["Success"] = btnAction == "draft"
                    ? "Đã lưu bản nháp thành công."
                    : "Sự kiện đã được đăng lên cộng đồng thành công!";

                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Có lỗi xảy ra khi lưu sự kiện: " + ex.Message);
                return View(model);
            }
        }

        public ActionResult RegistrationDetail(int id, int page = 1, int pageSize = 10)
        {
            ViewBag.ActiveMenu = "events";

            var ev = _db.EVENTs
                        .Include("DanhMuc")
                        .Include("DiaDiem")
                        .FirstOrDefault(e => e.MaEvent == id);

            if (ev == null) return HttpNotFound();

            var query = _db.DangKySuKiens
                           .Include("SinhVien")
                           .Where(d => d.MaEvent == id)
                           .OrderByDescending(d => d.NgayDangKy);

            int total = query.Count();
            var pageItems = query.Skip((page - 1) * pageSize)
                                 .Take(pageSize)
                                 .ToList();

            ViewBag.Event = ev;
            ViewBag.TotalReg = total;
            ViewBag.Page = page;
            ViewBag.PageSize = pageSize;
            ViewBag.TotalPage = (int)Math.Ceiling(total / (double)pageSize);

            return View(pageItems);
        }

        // ====================== DASHBOARD ======================
        public ActionResult Dashboard()
        {
            ViewBag.ActiveMenu = "dashboard";

            // Thống kê tổng quan
            var totalEvents = _db.EVENTs.Count();
            var upcomingEvents = _db.EVENTs.Count(e => e.TrangThai == "Sắp diễn ra" && e.NgayBatDau > DateTime.Now);
            var ongoingEvents = _db.EVENTs.Count(e => e.TrangThai == "Đang diễn ra");
            var totalRegistrations = _db.DangKySuKiens.Count();
            var totalStudents = _db.SinhViens.Count();

            // 5 sự kiện sắp diễn ra gần nhất
            var upcomingList = _db.EVENTs
                .Include("DanhMuc")
                .Where(e => e.NgayBatDau > DateTime.Now && e.IsHidden == false)
                .OrderBy(e => e.NgayBatDau)
                .Take(5)
                .ToList();

            // Thống kê theo danh mục
            var statsByCategory = _db.EVENTs
                .GroupBy(e => e.DanhMuc.TenDanhMuc)
                .Select(g => new
                {
                    Category = g.Key,
                    Count = g.Count(),
                    TotalReg = g.Sum(e => e.SoLuongDaDangKy)
                })
                .ToList();

            ViewBag.TotalEvents = totalEvents;
            ViewBag.UpcomingEvents = upcomingEvents;
            ViewBag.OngoingEvents = ongoingEvents;
            ViewBag.TotalRegistrations = totalRegistrations;
            ViewBag.TotalStudents = totalStudents;
            ViewBag.UpcomingList = upcomingList;
            ViewBag.StatsByCategory = statsByCategory;

            return View();
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing) _db.Dispose();
            base.Dispose(disposing);
        }
    }
}