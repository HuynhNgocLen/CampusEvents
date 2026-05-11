using System;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Data.Entity;
using System.Data.Entity.Validation;
using shcool_event_management.Areas.Admin.Helpers;
using shcool_event_management.Models;
using school_event_management.Helpers;

namespace shcool_event_management.Areas.Admin.Controllers
{
    [Authorize]
    public partial class AdminEventsController : BaseAdminController
    {
        private static readonly string[] DraftStatuses = { "Bản nháp", "Bản Nháp", "Nháp", "Nhap" };
        private static readonly string[] RegistrationStatuses = { "Đã đăng ký", "Đã hoàn thành", "Đã hủy" };

        private IQueryable<EVENT> ScopeToCurrentAdmin(IQueryable<EVENT> query)
        {
            var currentAdminMaQTV = GetCurrentAdminMaQTV();
            if (string.IsNullOrEmpty(currentAdminMaQTV))
            {
                return query.Where(e => false);
            }

            return query.Where(e => e.NguoiDang == currentAdminMaQTV);
        }

        private void PopulateDropdowns(string selectedCat = null,
                                       int? selectedDiaDiem = null,
                                       string selectedVien = null,
                                       QuanTriVien currentAdmin = null,
                                       bool applyVienPermission = false)
        {
            AdminSelectListHelper.PopulateDropdowns(ViewBag, _db, selectedCat, selectedDiaDiem, selectedVien, currentAdmin, applyVienPermission);
        }

        public ActionResult Index(string cat = null, string search = null, int? semester = null, int? year = null)
        {
            ViewBag.ActiveMenu = "events";
            ViewBag.DanhMucs = _db.DanhMucs.ToList();
            int selectedYear = year ?? DateTime.Now.Year;

            var query = ScopeToCurrentAdmin(_db.EVENTs
                           .Include("DanhMuc")
                           .Include("DiaDiem")
                           .Where(e => e.IsHidden == false)
                           .AsQueryable());

            query = query.Where(e => DraftStatuses.Contains(e.TrangThai) && e.NgayTao.Year == selectedYear);

            var months = AdminEventCommonHelper.ResolveSemesterMonths(semester);
            if (months != null)
            {
                query = query.Where(e => months.Contains(e.NgayTao.Month));
            }

            if (!string.IsNullOrWhiteSpace(cat))
            {
                query = query.Where(e => e.MaDanhMuc == cat);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(e => e.TenEvent.Contains(search));
            }

            ViewBag.CurrentCat = cat;
            ViewBag.CurrentSearch = search;
            ViewBag.Semester = semester;
            ViewBag.Year = selectedYear;

            var availableYears = ScopeToCurrentAdmin(_db.EVENTs.AsQueryable())
                .Where(e => DraftStatuses.Contains(e.TrangThai))
                .Select(e => e.NgayTao.Year)
                .Distinct()
                .OrderByDescending(y => y)
                .ToList();
            if (!availableYears.Contains(selectedYear))
            {
                availableYears.Insert(0, selectedYear);
            }
            ViewBag.AvailableYears = availableYears;

            var events = query.OrderByDescending(e => e.NgayBatDau).ToList();
            return View(events);
        }

        public ActionResult Manage(string search = null,
                                   string status = null,
                                   string cat = null,
                                   string vien = null,
                                   int? semester = null,
                                   int? year = null,
                                   int page = 1,
                                   int pageSize = 12)
        {
            ViewBag.ActiveMenu = "manage";
            int selectedYear = year ?? DateTime.Now.Year;
            var currentAdmin = GetCurrentAdmin();
            var adminQuyen = currentAdmin?.Quyen ?? -1;
            var forcedVien = adminQuyen != 0 ? ResolveAdminVienCode(currentAdmin) : null;

            var query = _db.EVENTs
                           .Include("DanhMuc")
                           .Include("DiaDiem")
                           .AsQueryable();

            if (adminQuyen != 0)
            {
                var currentAdminMaQTV = GetCurrentAdminMaQTV();
                query = string.IsNullOrWhiteSpace(currentAdminMaQTV)
                    ? query.Where(e => false)
                    : query.Where(e => e.NguoiDang == currentAdminMaQTV);

                if (!string.IsNullOrWhiteSpace(forcedVien))
                {
                    query = query.Where(e => e.MaVien == forcedVien);
                    vien = forcedVien;
                }
            }
            else if (!string.IsNullOrWhiteSpace(vien))
            {
                query = query.Where(e => e.MaVien == vien);
            }

            if (!string.IsNullOrWhiteSpace(search)) query = query.Where(e => e.TenEvent.Contains(search));
            if (!string.IsNullOrWhiteSpace(status)) query = query.Where(e => e.TrangThai == status);
            if (!string.IsNullOrWhiteSpace(cat)) query = query.Where(e => e.MaDanhMuc == cat);

            query = query.Where(e => e.NgayBatDau.Year == selectedYear);
            var months = AdminEventCommonHelper.ResolveSemesterMonths(semester);
            if (months != null) query = query.Where(e => months.Contains(e.NgayBatDau.Month));

            int total = query.Count();
            var items = query.OrderByDescending(e => e.NgayTao).Skip((page - 1) * pageSize).Take(pageSize).ToList();

            ViewBag.Search = search;
            ViewBag.Status = status;
            ViewBag.Cat = cat;
            ViewBag.Vien = vien;
            ViewBag.IsVienFilterLocked = adminQuyen != 0;
            ViewBag.AdminQuyen = adminQuyen;
            ViewBag.Semester = semester;
            ViewBag.Year = selectedYear;
            ViewBag.Page = page;
            ViewBag.PageSize = pageSize;
            ViewBag.TotalPage = (int)Math.Ceiling(total / (double)pageSize);
            ViewBag.Total = total;
            ViewBag.DanhMucs = _db.DanhMucs.ToList();
            ViewBag.Viens = _db.Viens.OrderBy(v => v.TenVien).ToList();
            return View(items);
        }

        public ActionResult Edit(int id)
        {
            ViewBag.ActiveMenu = "manage";
            _db.Configuration.ProxyCreationEnabled = false;
            var ev = _db.EVENTs.Find(id);
            _db.Configuration.ProxyCreationEnabled = true;
            if (ev == null) return HttpNotFound();
            if (ev.NguoiDang != GetCurrentAdminMaQTV()) return new HttpUnauthorizedResult();

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
            if (existing.NguoiDang != GetCurrentAdminMaQTV()) return new HttpUnauthorizedResult();

            PopulateDropdowns(model.MaDanhMuc, model.MaDiaDiem, model.MaVien);
            ViewBag.DiaDiemText = diaDiemText;
            if (ModelState.ContainsKey("ChiTiet")) ModelState["ChiTiet"].Errors.Clear();
            if (!ModelState.IsValid) return View(model);

            if (string.IsNullOrWhiteSpace(model.TenEvent)) { ModelState.AddModelError("TenEvent", "Tên sự kiện không được để trống."); return View(model); }
            if (string.IsNullOrWhiteSpace(model.MaDanhMuc)) { ModelState.AddModelError("MaDanhMuc", "Vui lòng chọn danh mục."); return View(model); }
            if (string.IsNullOrWhiteSpace(diaDiemText)) { ModelState.AddModelError("MaDiaDiem", "Vui lòng nhập địa điểm tổ chức."); return View(model); }
            if (model.SoLuongToiDa < 10) { ModelState.AddModelError("SoLuongToiDa", "Số lượng tối đa phải ít nhất 10."); return View(model); }

            if (coverImage != null && coverImage.ContentLength > 0)
            {
                var allowedTypes = new[] { "image/jpeg", "image/png" };
                if (!allowedTypes.Contains(coverImage.ContentType))
                {
                    ModelState.AddModelError("", "Chỉ chấp nhận JPG hoặc PNG.");
                    return View(model);
                }
                try
                {
                    var uploadDir = Server.MapPath("~/Uploads/Events/");
                    System.IO.Directory.CreateDirectory(uploadDir);
                    var ext = System.IO.Path.GetExtension(coverImage.FileName).ToLower();
                    var fileName = $"ev_{DateTime.Now:yyyyMMddHHmmss}{ext}";
                    coverImage.SaveAs(System.IO.Path.Combine(uploadDir, fileName));
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
            existing.MaDiaDiem = AdminEventCommonHelper.ResolveOrCreateLocationId(_db, diaDiemText);
            if (!existing.MaDiaDiem.HasValue) { ModelState.AddModelError("MaDiaDiem", "Không thể xử lý địa điểm tổ chức."); return View(model); }

            existing.NgayBatDau = model.NgayBatDau;
            existing.NgayKetThuc = model.NgayKetThuc == default(DateTime) ? (DateTime?)null : model.NgayKetThuc;
            existing.NgayHetHanDangKy = model.NgayHetHanDangKy;
            existing.SoLuongToiDa = model.SoLuongToiDa;
            existing.GiaVe = model.GiaVe;
            existing.DRL = model.DRL;
            existing.MoTa = model.MoTa;
            existing.ChiTiet = string.IsNullOrWhiteSpace(model.ChiTiet) ? null : model.ChiTiet.Trim();
            existing.TrangThai = model.TrangThai;
            existing.LinkZalo = WebUrlHelper.NormalizeExternalHref(model.LinkZalo);
            existing.NgayCapNhat = DateTime.Now;

            try
            {
                _db.SaveChanges();
                TempData["Success"] = "Đã cập nhật sự kiện thành công!";
                return RedirectToAction("Manage");
            }
            catch (DbEntityValidationException ex)
            {
                ModelState.AddModelError("", AdminEventCommonHelper.BuildEntityValidationErrorMessage(ex));
                return View(model);
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Lỗi lưu dữ liệu: " + ex.Message);
                return View(model);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Delete(int id)
        {
            var ev = _db.EVENTs.Find(id);
            if (ev == null) return HttpNotFound();
            if (ev.NguoiDang != GetCurrentAdminMaQTV()) return new HttpUnauthorizedResult();

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
    }
}
