using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using ClosedXML.Excel;                        // Install-Package ClosedXML
using shcool_event_management.Models;

namespace shcool_event_management.Areas.Admin.Controllers
{
    // [Authorize(Roles = "Admin")]
    public class AdminEventsController : Controller
    {
        private readonly school_event_managementEntities _db
            = new school_event_managementEntities();

        //  HELPERS

        private void PopulateDropdowns(string selectedCat = null,
                                       int? selectedDiaDiem = null,
                                       string selectedVien = null)
        {
            ViewBag.DanhMucList = _db.DanhMucs
                .Select(d => new SelectListItem
                {
                    Value = d.MaDanhMuc,
                    Text = d.TenDanhMuc,
                    Selected = d.MaDanhMuc == selectedCat
                })
                .ToList();

            ViewBag.DiaDiemList = _db.DiaDiems
                .Select(d => new SelectListItem
                {
                    Value = d.MaDiaDiem.ToString(),
                    Text = d.TenDiaDiem + (d.DiaChiChiTiet != null ? " - " + d.DiaChiChiTiet : ""),
                    Selected = d.MaDiaDiem == selectedDiaDiem
                })
                .ToList();

            ViewBag.VienList = new List<SelectListItem>
            {
                new SelectListItem { Value = "CNS",  Text = "Viện Công Nghệ Số",        Selected = selectedVien == "CNS"  },
                new SelectListItem { Value = "KTCN", Text = "Viện Kỹ Thuật Công Nghệ",  Selected = selectedVien == "KTCN" }
            };
        }

        //  TRANG SỰ KIỆN — Card view
        public ActionResult Index(string cat = null, string search = null)
        {
            ViewBag.ActiveMenu = "events";
            ViewBag.DanhMucs = _db.DanhMucs.ToList();

            var query = _db.EVENTs
                           .Include("DanhMuc")
                           .Include("DiaDiem")
                           .Where(e => e.IsHidden == false)
                           .AsQueryable();

            if (!string.IsNullOrWhiteSpace(cat))
                query = query.Where(e => e.MaDanhMuc == cat);

            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(e => e.TenEvent.Contains(search));

            ViewBag.CurrentCat = cat;
            ViewBag.CurrentSearch = search;

            var events = query.OrderByDescending(e => e.NgayBatDau).ToList();
            return View(events);
        }

        //  QUẢN LÝ BÀI ĐĂNG — Table view (CRUD)
        public ActionResult Manage(string search = null,
                                   string status = null,
                                   string cat = null,
                                   int page = 1,
                                   int pageSize = 10)
        {
            ViewBag.ActiveMenu = "posts";

            var query = _db.EVENTs
                           .Include("DanhMuc")
                           .Include("DiaDiem")
                           .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(e => e.TenEvent.Contains(search));

            if (!string.IsNullOrWhiteSpace(status))
                query = query.Where(e => e.TrangThai == status);

            if (!string.IsNullOrWhiteSpace(cat))
                query = query.Where(e => e.MaDanhMuc == cat);

            int total = query.Count();

            var items = query
                .OrderByDescending(e => e.NgayTao)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            ViewBag.Search = search;
            ViewBag.Status = status;
            ViewBag.Cat = cat;
            ViewBag.Page = page;
            ViewBag.PageSize = pageSize;
            ViewBag.TotalPage = (int)Math.Ceiling(total / (double)pageSize);
            ViewBag.Total = total;
            ViewBag.DanhMucs = _db.DanhMucs.ToList();

            return View(items);
        }

        //  TẠO SỰ KIỆN
        public ActionResult Create()
        {
            ViewBag.ActiveMenu = "posts";
            PopulateDropdowns();
            return View(new EVENT());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ValidateInput(false)]
        public ActionResult Create(EVENT model, HttpPostedFileBase coverImage, string btnAction)
        {
            ViewBag.ActiveMenu = "posts";
            PopulateDropdowns(model.MaDanhMuc, model.MaDiaDiem, model.MaVien);

            if (!ModelState.IsValid) return View(model);

            if (string.IsNullOrWhiteSpace(model.TenEvent))
            { ModelState.AddModelError("TenEvent", "Tên sự kiện không được để trống."); return View(model); }

            if (string.IsNullOrWhiteSpace(model.MaDanhMuc))
            { ModelState.AddModelError("MaDanhMuc", "Vui lòng chọn danh mục sự kiện."); return View(model); }

            if (string.IsNullOrWhiteSpace(model.MaVien))
            { ModelState.AddModelError("MaVien", "Vui lòng chọn viện tổ chức."); return View(model); }

            if (!model.MaDiaDiem.HasValue)
            { ModelState.AddModelError("MaDiaDiem", "Vui lòng chọn địa điểm tổ chức."); return View(model); }

            if (model.NgayBatDau == default(DateTime) || model.NgayBatDau < DateTime.Now)
            { ModelState.AddModelError("NgayBatDau", "Ngày bắt đầu phải lớn hơn thời điểm hiện tại."); return View(model); }

            if (model.NgayHetHanDangKy.HasValue && model.NgayHetHanDangKy.Value < model.NgayBatDau.Date)
            { ModelState.AddModelError("NgayHetHanDangKy", "Hạn đăng ký phải trước ngày bắt đầu."); return View(model); }

            if (model.SoLuongToiDa < 10)
            { ModelState.AddModelError("SoLuongToiDa", "Số lượng tối đa phải ít nhất 10 người."); return View(model); }

            if (coverImage == null || coverImage.ContentLength == 0)
            { ModelState.AddModelError("", "Vui lòng chọn ảnh bìa."); return View(model); }

            var allowedTypes = new[] { "image/jpeg", "image/png" };
            if (coverImage.ContentLength > 5 * 1024 * 1024)
            { ModelState.AddModelError("", "Ảnh bìa phải nhỏ hơn 5 MB."); return View(model); }

            if (!allowedTypes.Contains(coverImage.ContentType))
            { ModelState.AddModelError("", "Chỉ chấp nhận file JPG hoặc PNG."); return View(model); }

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
                ModelState.AddModelError("", "Lỗi tải ảnh: " + ex.Message);
                return View(model);
            }

            model.TrangThai = btnAction == "draft" ? "Bản nháp" : "Sắp diễn ra";
            model.NgayTao = DateTime.Now;
            model.SoLuongDaDangKy = 0;
            model.IsHidden = false;
            model.LuotXem = 0;
            model.NguoiDang = Session["UserId"]?.ToString() ?? "SV001";

            if (model.NgayKetThuc == default(DateTime))
                model.NgayKetThuc = null;

            try
            {
                _db.EVENTs.Add(model);
                _db.SaveChanges();
                TempData["Success"] = btnAction == "draft"
                    ? "Đã lưu bản nháp thành công."
                    : "Sự kiện đã được đăng thành công!";
                return RedirectToAction("Manage");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Lỗi lưu dữ liệu: " + ex.Message);
                return View(model);
            }
        }

        //  CHỈNH SỬA SỰ KIỆN
        public ActionResult Edit(int id)
        {
            ViewBag.ActiveMenu = "posts";
            var ev = _db.EVENTs.Find(id);
            if (ev == null) return HttpNotFound();

            PopulateDropdowns(ev.MaDanhMuc, ev.MaDiaDiem, ev.MaVien);
            return View(ev);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ValidateInput(false)]
        public ActionResult Edit(EVENT model, HttpPostedFileBase coverImage)
        {
            ViewBag.ActiveMenu = "posts";

            var existing = _db.EVENTs.Find(model.MaEvent);
            if (existing == null) return HttpNotFound();

            PopulateDropdowns(model.MaDanhMuc, model.MaDiaDiem, model.MaVien);

            if (!ModelState.IsValid) return View(model);

            if (string.IsNullOrWhiteSpace(model.TenEvent))
            { ModelState.AddModelError("TenEvent", "Tên sự kiện không được để trống."); return View(model); }

            if (string.IsNullOrWhiteSpace(model.MaDanhMuc))
            { ModelState.AddModelError("MaDanhMuc", "Vui lòng chọn danh mục."); return View(model); }

            if (!model.MaDiaDiem.HasValue)
            { ModelState.AddModelError("MaDiaDiem", "Vui lòng chọn địa điểm."); return View(model); }

            if (model.SoLuongToiDa < 10)
            { ModelState.AddModelError("SoLuongToiDa", "Số lượng tối đa phải ít nhất 10."); return View(model); }

            if (coverImage != null && coverImage.ContentLength > 0)
            {
                var allowedTypes = new[] { "image/jpeg", "image/png" };
                if (!allowedTypes.Contains(coverImage.ContentType))
                { ModelState.AddModelError("", "Chỉ chấp nhận JPG hoặc PNG."); return View(model); }

                try
                {
                    var uploadDir = Server.MapPath("~/Uploads/Events/");
                    Directory.CreateDirectory(uploadDir);
                    var ext = Path.GetExtension(coverImage.FileName).ToLower();
                    var fileName = $"ev_{DateTime.Now:yyyyMMddHHmmss}{ext}";
                    coverImage.SaveAs(Path.Combine(uploadDir, fileName));
                    existing.AnhBia = "/Uploads/Events/" + fileName;
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", "Lỗi tải ảnh: " + ex.Message);
                    return View(model);
                }
            }
            existing.TenEvent = model.TenEvent;
            existing.MaDanhMuc = model.MaDanhMuc;
            existing.MaVien = model.MaVien;
            existing.MaDiaDiem = model.MaDiaDiem;
            existing.NgayBatDau = model.NgayBatDau;
            existing.NgayKetThuc = model.NgayKetThuc == default(DateTime) ? (DateTime?)null : model.NgayKetThuc;
            existing.NgayHetHanDangKy = model.NgayHetHanDangKy;
            existing.SoLuongToiDa = model.SoLuongToiDa;
            existing.GiaVe = model.GiaVe;
            existing.DRL = model.DRL;
            existing.MoTa = model.MoTa;
            existing.ChiTiet = model.ChiTiet;
            existing.TrangThai = model.TrangThai;
            existing.LinkZalo = model.LinkZalo;
            existing.NgayCapNhat = DateTime.Now;

            try
            {
                _db.SaveChanges();
                TempData["Success"] = "Đã cập nhật sự kiện thành công!";
                return RedirectToAction("Manage");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Lỗi lưu dữ liệu: " + ex.Message);
                return View(model);
            }
        }

        //  XÓA SỰ KIỆN
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Delete(int id)
        {
            var ev = _db.EVENTs.Find(id);
            if (ev == null) return HttpNotFound();

            // Xóa đăng ký liên quan trước
            var registrations = _db.DangKySuKiens.Where(d => d.MaEvent == id).ToList();
            _db.DangKySuKiens.RemoveRange(registrations);

            _db.EVENTs.Remove(ev);

            try
            {
                _db.SaveChanges();
                TempData["Success"] = $"Đã xóa sự kiện \"{ev.TenEvent}\" thành công.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Lỗi khi xóa: " + ex.Message;
            }

            return RedirectToAction("Manage");
        }

        //  CHI TIẾT ĐĂNG KÝ — Danh sách sinh viên
        public ActionResult RegistrationDetail(int id,
                                               string search = null,
                                               string status = null,
                                               int page = 1,
                                               int pageSize = 10)
        {
            ViewBag.ActiveMenu = "posts";

            var ev = _db.EVENTs
                        .Include("DanhMuc")
                        .Include("DiaDiem")
                        .FirstOrDefault(e => e.MaEvent == id);

            if (ev == null) return HttpNotFound();

            var query = _db.DangKySuKiens
                           .Include("SinhVien")
                           .Where(d => d.MaEvent == id)
                           .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(d => d.SinhVien.Ten.Contains(search)
                                      || d.IDSinhVien.Contains(search));

            if (!string.IsNullOrWhiteSpace(status))
                query = query.Where(d => d.TrangThai == status);

            int total = query.Count();

            var pageItems = query
                .OrderByDescending(d => d.NgayDangKy)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            ViewBag.Event = ev;
            ViewBag.TotalReg = total;
            ViewBag.Confirmed = _db.DangKySuKiens.Count(d => d.MaEvent == id && d.TrangThai == "Đã xác nhận");
            ViewBag.Cancelled = _db.DangKySuKiens.Count(d => d.MaEvent == id && d.TrangThai == "Đã hủy");
            ViewBag.Page = page;
            ViewBag.PageSize = pageSize;
            ViewBag.TotalPage = (int)Math.Ceiling(total / (double)pageSize);
            ViewBag.Search = search;
            ViewBag.Status = status;

            return View(pageItems);
        }

        //  HỦY ĐĂNG KÝ CỦA SINH VIÊN
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CancelRegistration(int maEvent, string idSinhVien)
        {
            var reg = _db.DangKySuKiens
                         .FirstOrDefault(d => d.MaEvent == maEvent && d.IDSinhVien == idSinhVien);

            if (reg == null) return HttpNotFound();

            reg.TrangThai = "Đã hủy";

            // Cập nhật lại số lượng đăng ký của sự kiện
            var ev = _db.EVENTs.Find(maEvent);
            if (ev != null && ev.SoLuongDaDangKy > 0)
                ev.SoLuongDaDangKy--;

            _db.SaveChanges();

            TempData["Success"] = $"Đã hủy đăng ký cho sinh viên {idSinhVien}.";
            return RedirectToAction("RegistrationDetail", new { id = maEvent });
        }

        //  XUẤT EXCEL — Danh sách đăng ký
        public ActionResult ExportExcel(int id)
        {
            var ev = _db.EVENTs
                        .Include("DanhMuc")
                        .FirstOrDefault(e => e.MaEvent == id);

            if (ev == null) return HttpNotFound();

            var registrations = _db.DangKySuKiens
                .Include("SinhVien")
                .Include("SinhVien.MaNghanh1")
                .Where(d => d.MaEvent == id)
                .OrderBy(d => d.SinhVien.Ten)
                .ToList();

            using (var wb = new XLWorkbook())
            {
                var ws = wb.Worksheets.Add("Danh sách đăng ký");

                //  Tiêu đề sự kiện 
                ws.Cell(1, 1).Value = "DANH SÁCH ĐĂNG KÝ SỰ KIỆN";
                ws.Cell(1, 1).Style.Font.Bold = true;
                ws.Cell(1, 1).Style.Font.FontSize = 14;
                ws.Range(1, 1, 1, 9).Merge();

                ws.Cell(2, 1).Value = "Sự kiện: " + ev.TenEvent;
                ws.Cell(3, 1).Value = "Ngày tổ chức: " + ev.NgayBatDau.ToString("dd/MM/yyyy HH:mm");
                ws.Cell(4, 1).Value = "Tổng đăng ký: " + registrations.Count;
                ws.Range(2, 1, 2, 9).Merge();
                ws.Range(3, 1, 3, 9).Merge();
                ws.Range(4, 1, 4, 9).Merge();

                // ── Header bảng ───────────────────────────────────
                var headers = new[] { "STT", "Mã SV", "Họ và tên", "Lớp", "Ngành",
                                      "Email", "Số điện thoại", "Ngày đăng ký", "Trạng thái" };
                int headerRow = 6;
                for (int i = 0; i < headers.Length; i++)
                {
                    var cell = ws.Cell(headerRow, i + 1);
                    cell.Value = headers[i];
                    cell.Style.Font.Bold = true;
                    cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#137fec");
                    cell.Style.Font.FontColor = XLColor.White;
                    cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                }

                // ── Dữ liệu ───────────────────────────────────────
                for (int i = 0; i < registrations.Count; i++)
                {
                    var reg = registrations[i];
                    var sv = reg.SinhVien;
                    int row = headerRow + 1 + i;

                    ws.Cell(row, 1).Value = i + 1;
                    ws.Cell(row, 2).Value = sv?.ID ?? "";
                    ws.Cell(row, 3).Value = sv?.Ten ?? "";
                    ws.Cell(row, 4).Value = sv?.Lop ?? "";
                    ws.Cell(row, 5).Value = sv?.MaNghanh1?.TenNghanh ?? sv?.MaNghanh ?? "";
                    ws.Cell(row, 6).Value = sv?.Email ?? "";
                    ws.Cell(row, 7).Value = sv?.SoDienThoai ?? "";
                    ws.Cell(row, 8).Value = reg.NgayDangKy.ToString("dd/MM/yyyy HH:mm");
                    ws.Cell(row, 9).Value = reg.TrangThai ?? "";

                    // Tô màu xen kẽ
                    if (i % 2 == 1)
                        ws.Range(row, 1, row, 9).Style.Fill.BackgroundColor = XLColor.FromHtml("#f1f5f9");

                    // Tô màu cột Trạng thái
                    var statusCell = ws.Cell(row, 9);
                    if (reg.TrangThai == "Đã xác nhận")
                        statusCell.Style.Font.FontColor = XLColor.FromHtml("#16a34a");
                    else if (reg.TrangThai == "Đã hủy")
                        statusCell.Style.Font.FontColor = XLColor.FromHtml("#dc2626");
                }

                // ── Auto-fit cột + border ─────────────────────────
                ws.Columns().AdjustToContents();
                ws.Range(headerRow, 1, headerRow + registrations.Count, 9)
                  .Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                ws.Range(headerRow, 1, headerRow + registrations.Count, 9)
                  .Style.Border.InsideBorder = XLBorderStyleValues.Hair;

                // ── Stream về client ──────────────────────────────
                var stream = new MemoryStream();
                wb.SaveAs(stream);
                stream.Position = 0;

                var fileName = $"DangKy_{ev.TenEvent.Replace(" ", "_")}_{DateTime.Now:yyyyMMdd}.xlsx";
                return File(stream.ToArray(),
                            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                            fileName);
            }
        }

        // ════════════════════════════════════════════════════════
        //  TOGGLE ẨN / HIỆN SỰ KIỆN (AJAX)
        // ════════════════════════════════════════════════════════

        [HttpPost]
        public JsonResult ToggleHidden(int id)
        {
            var ev = _db.EVENTs.Find(id);
            if (ev == null)
                return Json(new { success = false, message = "Không tìm thấy sự kiện." });

            ev.IsHidden = !ev.IsHidden;
            ev.NgayCapNhat = DateTime.Now;
            _db.SaveChanges();

            return Json(new { success = true, isHidden = ev.IsHidden });
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _db.Dispose();
            base.Dispose(disposing);
        }
    }
}