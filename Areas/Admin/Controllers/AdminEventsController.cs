using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Data.Entity.Validation;
using ClosedXML.Excel;                        // Install-Package ClosedXML
using school_event_management.Helpers;
using shcool_event_management.Models;

namespace shcool_event_management.Areas.Admin.Controllers
{
    // [Authorize(Roles = "Admin")]
    public class AdminEventsController : Controller
    {
        private readonly school_event_managementEntities _db
            = new school_event_managementEntities();

        private string BuildEntityValidationErrorMessage(DbEntityValidationException ex)
        {
            var errors = ex.EntityValidationErrors
                .SelectMany(e => e.ValidationErrors)
                .Select(v => string.Format("{0}: {1}", v.PropertyName, v.ErrorMessage))
                .Distinct()
                .ToList();

            return errors.Any()
                ? "Dữ liệu chưa hợp lệ - " + string.Join(" | ", errors)
                : "Dữ liệu chưa hợp lệ.";
        }

        private int? ResolveOrCreateLocationId(string locationName)
        {
            var normalized = (locationName ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(normalized))
                return null;

            var existing = _db.DiaDiems.FirstOrDefault(d => d.TenDiaDiem == normalized);
            if (existing != null)
                return existing.MaDiaDiem;

            // Fallback khong phan biet hoa thuong de tan dung dia diem co san.
            existing = _db.DiaDiems
                .ToList()
                .FirstOrDefault(d => string.Equals((d.TenDiaDiem ?? string.Empty).Trim(), normalized, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
                return existing.MaDiaDiem;

            var newLocation = new DiaDiem
            {
                TenDiaDiem = normalized,
                DiaChiChiTiet = normalized
            };

            _db.DiaDiems.Add(newLocation);
            _db.SaveChanges();
            return newLocation.MaDiaDiem;
        }

        private static readonly string[] RegistrationStatuses =
        {
            "Đã đăng ký", "Đã hoàn thành", "Đã hủy"
        };

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

        //  QUẢN LÍ SỰ KIỆN — Card / List view
        public ActionResult Manage(string search = null,
                                   string status = null,
                                   string cat = null,
                                   int? semester = null,
                                   int? year = null,
                                   int page = 1,
                                   int pageSize = 12)
        {
            ViewBag.ActiveMenu = "manage";
            int selectedYear = year ?? DateTime.Now.Year;

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

            query = query.Where(e => e.NgayBatDau.Year == selectedYear);

            // Lọc theo học kỳ: K1 = tháng 9-12, K2 = tháng 1-4, K3 = tháng 5-8
            if (semester.HasValue)
            {
                int[] months;
                switch (semester.Value)
                {
                    case 1: months = new[] { 9, 10, 11, 12 }; break;
                    case 2: months = new[] { 1, 2, 3, 4 }; break;
                    case 3: months = new[] { 5, 6, 7, 8 }; break;
                    default: months = null; break;
                }
                if (months != null)
                    query = query.Where(e => months.Contains(e.NgayBatDau.Month));
            }

            int total = query.Count();

            var items = query
                .OrderByDescending(e => e.NgayTao)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            ViewBag.Search = search;
            ViewBag.Status = status;
            ViewBag.Cat = cat;
            ViewBag.Semester = semester;
            ViewBag.Year = selectedYear;
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
            ViewBag.ActiveMenu = "manage";
            PopulateDropdowns();
            ViewBag.DiaDiemText = string.Empty;
            return View(new EVENT());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ValidateInput(false)]
        public ActionResult Create(EVENT model, HttpPostedFileBase coverImage, string btnAction, string diaDiemText)
        {
            ViewBag.ActiveMenu = "manage";
            PopulateDropdowns(model.MaDanhMuc, model.MaDiaDiem, model.MaVien);
            ViewBag.DiaDiemText = diaDiemText;
            model.ChiTiet = model.MoTa;
            if (ModelState.ContainsKey("ChiTiet"))
            {
                ModelState["ChiTiet"].Errors.Clear();
            }

            if (!ModelState.IsValid) return View(model);

            if (string.IsNullOrWhiteSpace(model.TenEvent))
            { ModelState.AddModelError("TenEvent", "Tên sự kiện không được để trống."); return View(model); }

            if (string.IsNullOrWhiteSpace(model.MaDanhMuc))
            { ModelState.AddModelError("MaDanhMuc", "Vui lòng chọn danh mục sự kiện."); return View(model); }

            if (string.IsNullOrWhiteSpace(model.MaVien))
            { ModelState.AddModelError("MaVien", "Vui lòng chọn viện tổ chức."); return View(model); }

            if (string.IsNullOrWhiteSpace(diaDiemText))
            { ModelState.AddModelError("MaDiaDiem", "Vui lòng nhập địa điểm tổ chức."); return View(model); }

            if (model.NgayBatDau == default(DateTime) || model.NgayBatDau.Date < DateTime.Today)
            { ModelState.AddModelError("NgayBatDau", "Ngày bắt đầu phải từ hôm nay trở đi."); return View(model); }

            if (model.NgayHetHanDangKy.HasValue && model.NgayHetHanDangKy.Value < model.NgayBatDau.Date)
            { ModelState.AddModelError("NgayHetHanDangKy", "Hạn đăng ký phải trước ngày bắt đầu."); return View(model); }

            if (model.NgayKetThuc.HasValue && model.NgayKetThuc.Value.Date < model.NgayBatDau.Date)
            { ModelState.AddModelError("NgayKetThuc", "Ngày kết thúc phải bằng hoặc sau ngày bắt đầu."); return View(model); }

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
            model.ChiTiet = model.MoTa;
            model.NgayTao = DateTime.Now;
            model.SoLuongDaDangKy = 0;
            model.IsHidden = false;
            model.LuotXem = 0;
            model.NguoiDang = Session["UserId"]?.ToString() ?? "SV001";
            model.MaDiaDiem = ResolveOrCreateLocationId(diaDiemText);

            if (!model.MaDiaDiem.HasValue)
            { ModelState.AddModelError("MaDiaDiem", "Không thể xử lý địa điểm tổ chức."); return View(model); }

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
            catch (DbEntityValidationException ex)
            {
                ModelState.AddModelError("", BuildEntityValidationErrorMessage(ex));
                return View(model);
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
            ViewBag.ActiveMenu = "manage";
            _db.Configuration.ProxyCreationEnabled = false;
            var ev = _db.EVENTs.Find(id);
            _db.Configuration.ProxyCreationEnabled = true;
            if (ev == null) return HttpNotFound();

            PopulateDropdowns(ev.MaDanhMuc, ev.MaDiaDiem, ev.MaVien);
            ViewBag.DiaDiemText = ev.MaDiaDiem.HasValue
                ? (_db.DiaDiems.Where(d => d.MaDiaDiem == ev.MaDiaDiem.Value).Select(d => d.TenDiaDiem).FirstOrDefault() ?? string.Empty)
                : string.Empty;
            return View(ev);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ValidateInput(false)]
        public ActionResult Edit(EVENT model, HttpPostedFileBase coverImage, string diaDiemText)
        {
            ViewBag.ActiveMenu = "manage";

            var existing = _db.EVENTs.Find(model.MaEvent);
            if (existing == null) return HttpNotFound();

            PopulateDropdowns(model.MaDanhMuc, model.MaDiaDiem, model.MaVien);
            ViewBag.DiaDiemText = diaDiemText;
            model.ChiTiet = model.MoTa;
            if (ModelState.ContainsKey("ChiTiet"))
            {
                ModelState["ChiTiet"].Errors.Clear();
            }

            if (!ModelState.IsValid) return View(model);

            if (string.IsNullOrWhiteSpace(model.TenEvent))
            { ModelState.AddModelError("TenEvent", "Tên sự kiện không được để trống."); return View(model); }

            if (string.IsNullOrWhiteSpace(model.MaDanhMuc))
            { ModelState.AddModelError("MaDanhMuc", "Vui lòng chọn danh mục."); return View(model); }

            if (string.IsNullOrWhiteSpace(diaDiemText))
            { ModelState.AddModelError("MaDiaDiem", "Vui lòng nhập địa điểm tổ chức."); return View(model); }

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
            existing.MaDiaDiem = ResolveOrCreateLocationId(diaDiemText);
            if (!existing.MaDiaDiem.HasValue)
            { ModelState.AddModelError("MaDiaDiem", "Không thể xử lý địa điểm tổ chức."); return View(model); }
            existing.NgayBatDau = model.NgayBatDau;
            existing.NgayKetThuc = model.NgayKetThuc == default(DateTime) ? (DateTime?)null : model.NgayKetThuc;
            existing.NgayHetHanDangKy = model.NgayHetHanDangKy;
            existing.SoLuongToiDa = model.SoLuongToiDa;
            existing.GiaVe = model.GiaVe;
            existing.DRL = model.DRL;
            existing.MoTa = model.MoTa;
            existing.ChiTiet = model.MoTa;
            existing.TrangThai = model.TrangThai;
            existing.LinkZalo = model.LinkZalo;
            existing.NgayCapNhat = DateTime.Now;

            try
            {
                _db.SaveChanges();
                TempData["Success"] = "Đã cập nhật sự kiện thành công!";
                return RedirectToAction("Manage");
            }
            catch (DbEntityValidationException ex)
            {
                ModelState.AddModelError("", BuildEntityValidationErrorMessage(ex));
                return View(model);
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
            ViewBag.ActiveMenu = "manage";

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
            {
                if (status == "Đã đăng ký")
                {
                    query = query.Where(d => d.TrangThai == "Đã đăng ký" || d.TrangThai == "Đã xác nhận" || d.TrangThai == "Chờ xác nhận");
                }
                else if (status == "Đã hoàn thành")
                {
                    query = query.Where(d => d.TrangThai == "Đã hoàn thành" || d.TrangThai == "Đã xác nhận");
                }
                else
                {
                    query = query.Where(d => d.TrangThai == status);
                }
            }

            int total = query.Count();

            var pageItems = query
                .OrderByDescending(d => d.NgayDangKy)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            ViewBag.Event = ev;
            ViewBag.TotalReg = total;
            ViewBag.Registered = _db.DangKySuKiens
                .Where(d => d.MaEvent == id)
                .ToList()
                .Count(d => RegistrationStatusHelper.Normalize(d.TrangThai) == "Đã đăng ký");
            ViewBag.Completed = _db.DangKySuKiens
                .Where(d => d.MaEvent == id)
                .ToList()
                .Count(d => RegistrationStatusHelper.Normalize(d.TrangThai) == "Đã hoàn thành");
            ViewBag.Cancelled = _db.DangKySuKiens
                .Where(d => d.MaEvent == id)
                .ToList()
                .Count(d => RegistrationStatusHelper.Normalize(d.TrangThai) == "Đã hủy");
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
        public ActionResult ExportExcel(int id, string search = null, string status = null)
        {
            var ev = _db.EVENTs
                        .Include("DanhMuc")
                        .FirstOrDefault(e => e.MaEvent == id);

            if (ev == null) return HttpNotFound();

            var registrations = _db.DangKySuKiens
                .Include("SinhVien")
                .Include("SinhVien.MaNghanh1")
                .Where(d => d.MaEvent == id)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                registrations = registrations.Where(d => d.SinhVien.Ten.Contains(search)
                    || d.IDSinhVien.Contains(search));
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                if (status == "Đã đăng ký")
                {
                    registrations = registrations.Where(d => d.TrangThai == "Đã đăng ký" || d.TrangThai == "Đã xác nhận" || d.TrangThai == "Chờ xác nhận");
                }
                else if (status == "Đã hoàn thành")
                {
                    registrations = registrations.Where(d => d.TrangThai == "Đã hoàn thành" || d.TrangThai == "Đã xác nhận");
                }
                else
                {
                    registrations = registrations.Where(d => d.TrangThai == status);
                }
            }

            var exportList = registrations
                .ToList()
                .Where(d => RegistrationStatusHelper.MatchStatusWithLegacy(d.TrangThai, status))
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
                ws.Cell(4, 1).Value = "Tổng đăng ký: " + exportList.Count;
                ws.Range(2, 1, 2, 9).Merge();
                ws.Range(3, 1, 3, 9).Merge();
                ws.Range(4, 1, 4, 9).Merge();
                if (!string.IsNullOrWhiteSpace(search) || !string.IsNullOrWhiteSpace(status))
                {
                    ws.Cell(5, 1).Value = "Bộ lọc: "
                        + (!string.IsNullOrWhiteSpace(search) ? $"Từ khóa \"{search}\"" : "Không")
                        + " | Trạng thái: "
                        + (!string.IsNullOrWhiteSpace(status) ? status : "Tất cả");
                    ws.Range(5, 1, 5, 9).Merge();
                }

                // ── Header bảng ───────────────────────────────────
                var headers = new[] { "STT", "Mã SV", "Họ và tên", "Lớp", "Ngành",
                                      "Email", "Số điện thoại", "Ngày đăng ký", "Trạng thái" };
                int headerRow = (!string.IsNullOrWhiteSpace(search) || !string.IsNullOrWhiteSpace(status)) ? 7 : 6;
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
                for (int i = 0; i < exportList.Count; i++)
                {
                    var reg = exportList[i];
                    var sv = reg.SinhVien;
                    int row = headerRow + 1 + i;
                    var normalizedStatus = RegistrationStatusHelper.Normalize(reg.TrangThai);

                    ws.Cell(row, 1).Value = i + 1;
                    ws.Cell(row, 2).Value = sv?.ID ?? "";
                    ws.Cell(row, 3).Value = sv?.Ten ?? "";
                    ws.Cell(row, 4).Value = sv?.Lop ?? "";
                    ws.Cell(row, 5).Value = sv?.MaNghanh1?.TenNghanh ?? sv?.MaNghanh ?? "";
                    ws.Cell(row, 6).Value = sv?.Email ?? "";
                    ws.Cell(row, 7).Value = sv?.SoDienThoai ?? "";
                    ws.Cell(row, 8).Value = reg.NgayDangKy.ToString("dd/MM/yyyy HH:mm");
                    ws.Cell(row, 9).Value = normalizedStatus;

                    // Tô màu xen kẽ
                    if (i % 2 == 1)
                        ws.Range(row, 1, row, 9).Style.Fill.BackgroundColor = XLColor.FromHtml("#f1f5f9");

                    // Tô màu cột Trạng thái
                    var statusCell = ws.Cell(row, 9);
                    if (normalizedStatus == "Đã hoàn thành")
                        statusCell.Style.Font.FontColor = XLColor.FromHtml("#0f766e");
                    else if (normalizedStatus == "Đã hủy")
                        statusCell.Style.Font.FontColor = XLColor.FromHtml("#dc2626");
                    else
                        statusCell.Style.Font.FontColor = XLColor.FromHtml("#137fec");
                }

                // ── Auto-fit cột + border ─────────────────────────
                ws.Columns().AdjustToContents();
                ws.Range(headerRow, 1, headerRow + exportList.Count, 9)
                  .Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                ws.Range(headerRow, 1, headerRow + exportList.Count, 9)
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

        //  LẤY DANH SÁCH SINH VIÊN ĐĂNG KÝ (AJAX JSON)
        public JsonResult GetStudents(int id, string search = null)
        {
            var query = _db.DangKySuKiens
                           .Include("SinhVien")
                           .Include("SinhVien.Vien")
                           .Where(d => d.MaEvent == id)
                           .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(d => d.SinhVien.Ten.Contains(search)
                                       || d.IDSinhVien.Contains(search));

            var list = query
                .OrderBy(d => d.SinhVien.Ten)
                .Select(d => new
                {
                    idSinhVien = d.IDSinhVien,
                    mssv = d.IDSinhVien,
                    hoTen = d.SinhVien.Ten,
                    lop = d.SinhVien.Lop,
                    vien = d.SinhVien.Vien != null ? d.SinhVien.Vien.TenVien : d.SinhVien.MaVien,
                    trangThai = d.TrangThai
                })
                .ToList()
                .Select(d => new
                {
                    d.idSinhVien,
                    d.mssv,
                    d.hoTen,
                    d.lop,
                    d.vien,
                    trangThai = RegistrationStatusHelper.Normalize(d.trangThai)
                })
                .ToList();

            return Json(new { success = true, data = list, total = list.Count },
                        JsonRequestBehavior.AllowGet);
        }

        //  CẬP NHẬT TRẠNG THÁI ĐĂNG KÝ CỦA SINH VIÊN (AJAX)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public JsonResult UpdateStudentStatus(int maEvent, string idSinhVien, string trangThai)
        {
            var allowedStatuses = RegistrationStatuses;
            if (string.IsNullOrWhiteSpace(idSinhVien) || !allowedStatuses.Contains(trangThai))
            {
                return Json(new { success = false, message = "Dữ liệu cập nhật không hợp lệ." });
            }

            var reg = _db.DangKySuKiens
                .FirstOrDefault(d => d.MaEvent == maEvent && d.IDSinhVien == idSinhVien);
            if (reg == null)
            {
                return Json(new { success = false, message = "Không tìm thấy đăng ký của sinh viên." });
            }

            if (string.Equals(reg.TrangThai, trangThai, StringComparison.OrdinalIgnoreCase))
            {
                return Json(new { success = true, status = reg.TrangThai, message = "Trạng thái đã được giữ nguyên." });
            }

            var oldStatus = reg.TrangThai ?? string.Empty;
            var dbStatus = RegistrationStatusHelper.ToStoredRegistrationStatus(trangThai);
            reg.TrangThai = dbStatus;

            var ev = _db.EVENTs.Find(maEvent);
            if (ev != null)
            {
                // Đồng bộ lại số lượng đăng ký theo trạng thái đã hủy/không hủy.
                if (oldStatus == "Đã hủy" && trangThai != "Đã hủy")
                {
                    ev.SoLuongDaDangKy++;
                }
                else if (oldStatus != "Đã hủy" && trangThai == "Đã hủy" && ev.SoLuongDaDangKy > 0)
                {
                    ev.SoLuongDaDangKy--;
                }
            }

            _db.SaveChanges();

            return Json(new { success = true, status = RegistrationStatusHelper.Normalize(reg.TrangThai) });
        }

        //  ĐÓNG SỰ KIỆN (AJAX)
        [HttpPost]
        public JsonResult CloseEvent(int id)
        {
            var ev = _db.EVENTs.Find(id);
            if (ev == null)
                return Json(new { success = false, message = "Không tìm thấy sự kiện." });

            ev.TrangThai = "Đã hủy";
            ev.IsHidden = true;
            ev.NgayCapNhat = DateTime.Now;
            _db.SaveChanges();

            return Json(new { success = true });
        }

        //  MỞ LẠI SỰ KIỆN (AJAX)
        [HttpPost]
        public JsonResult ReopenEvent(int id)
        {
            var ev = _db.EVENTs.Find(id);
            if (ev == null)
                return Json(new { success = false, message = "Không tìm thấy sự kiện." });

            var now = DateTime.Now;
            var startReg = ev.NgayBatDauDangKy;
            var endReg = ev.NgayHetHanDangKy;

            if (startReg.HasValue && startReg.Value > now)
            {
                ev.TrangThai = "Sắp diễn ra";
            }
            else if (startReg.HasValue && endReg.HasValue
                     && now >= startReg.Value
                     && now <= endReg.Value)
            {
                ev.TrangThai = "Đang diễn ra";
            }
            else
            {
                ev.TrangThai = "Sắp diễn ra";
            }

            ev.IsHidden = false;
            ev.NgayCapNhat = DateTime.Now;
            _db.SaveChanges();

            return Json(new { success = true, status = ev.TrangThai });
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _db.Dispose();
            base.Dispose(disposing);
        }
    }
}