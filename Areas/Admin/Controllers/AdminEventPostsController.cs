using System;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Data.Entity.Validation;
using shcool_event_management.Areas.Admin.Helpers;
using shcool_event_management.Models;

namespace shcool_event_management.Areas.Admin.Controllers
{
    [Authorize]
    public class AdminEventPostsController : BaseAdminController
    {
        private const string CreateViewPath = "~/Areas/Admin/Views/AdminEvents/Create.cshtml";

        public ActionResult Create()
        {
            ViewBag.ActiveMenu = "event-post-create";
            var currentAdmin = GetCurrentAdmin();
            ViewBag.AdminQuyen = currentAdmin?.Quyen ?? -1;
            var forcedMaVien = currentAdmin != null && currentAdmin.Quyen == 2 ? ResolveAdminVienCode(currentAdmin) : null;

            AdminSelectListHelper.PopulateDropdowns(ViewBag, _db, selectedVien: forcedMaVien, currentAdmin: currentAdmin, applyVienPermission: true);
            ViewBag.DiaDiemText = string.Empty;
            return View(CreateViewPath, new EVENT
            {
                MaVien = forcedMaVien,
                NgayBatDau = DateTime.Today
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ValidateInput(false)]
        public ActionResult Create(EVENT model, HttpPostedFileBase coverImage, string btnAction, string diaDiemText)
        {
            ViewBag.ActiveMenu = "event-post-create";
            var currentAdmin = GetCurrentAdmin();
            ViewBag.AdminQuyen = currentAdmin?.Quyen ?? -1;
            var forcedMaVien = currentAdmin != null && currentAdmin.Quyen == 2 ? ResolveAdminVienCode(currentAdmin) : null;

            if (currentAdmin != null && currentAdmin.Quyen == 2)
            {
                if (string.IsNullOrWhiteSpace(forcedMaVien)) ModelState.AddModelError("MaVien", "Không tìm thấy viện của quản trị viên hiện tại.");
                else model.MaVien = forcedMaVien;
            }

            AdminSelectListHelper.PopulateDropdowns(ViewBag, _db, model.MaDanhMuc, model.MaDiaDiem, model.MaVien, currentAdmin, true);
            ViewBag.DiaDiemText = diaDiemText;
            model.ChiTiet = model.MoTa;
            if (ModelState.ContainsKey("ChiTiet")) ModelState["ChiTiet"].Errors.Clear();
            if (!ModelState.IsValid) return View(CreateViewPath, model);

            if (string.IsNullOrWhiteSpace(model.TenEvent)) { ModelState.AddModelError("TenEvent", "Tên sự kiện không được để trống."); return View(CreateViewPath, model); }
            if (string.IsNullOrWhiteSpace(model.MaDanhMuc)) { ModelState.AddModelError("MaDanhMuc", "Vui lòng chọn danh mục sự kiện."); return View(CreateViewPath, model); }
            if (string.IsNullOrWhiteSpace(model.MaVien)) { ModelState.AddModelError("MaVien", "Vui lòng chọn viện tổ chức."); return View(CreateViewPath, model); }
            if (string.IsNullOrWhiteSpace(diaDiemText)) { ModelState.AddModelError("MaDiaDiem", "Vui lòng nhập địa điểm tổ chức."); return View(CreateViewPath, model); }
            if (model.NgayBatDau == default(DateTime) || model.NgayBatDau.Date < DateTime.Today.AddDays(-20)) { ModelState.AddModelError("NgayBatDau", "Ngày bắt đầu không được nhỏ hơn ngày hiện tại quá 20 ngày."); return View(CreateViewPath, model); }
            if (model.NgayHetHanDangKy.HasValue && model.NgayHetHanDangKy.Value < model.NgayBatDau.Date) { ModelState.AddModelError("NgayHetHanDangKy", "Hạn đăng ký phải trước ngày bắt đầu."); return View(CreateViewPath, model); }
            if (model.NgayKetThuc.HasValue && model.NgayKetThuc.Value.Date < model.NgayBatDau.Date) { ModelState.AddModelError("NgayKetThuc", "Ngày kết thúc phải bằng hoặc sau ngày bắt đầu."); return View(CreateViewPath, model); }
            if (model.SoLuongToiDa < 10) { ModelState.AddModelError("SoLuongToiDa", "Số lượng tối đa phải ít nhất 10 người."); return View(CreateViewPath, model); }
            if (coverImage == null || coverImage.ContentLength == 0) { ModelState.AddModelError("", "Vui lòng chọn ảnh bìa."); return View(CreateViewPath, model); }

            var allowedTypes = new[] { "image/jpeg", "image/png" };
            if (coverImage.ContentLength > 5 * 1024 * 1024) { ModelState.AddModelError("", "Ảnh bìa phải nhỏ hơn 5 MB."); return View(CreateViewPath, model); }
            if (!allowedTypes.Contains(coverImage.ContentType)) { ModelState.AddModelError("", "Chỉ chấp nhận file JPG hoặc PNG."); return View(CreateViewPath, model); }

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
                return View(CreateViewPath, model);
            }

            model.TrangThai = btnAction == "draft" ? "Bản nháp" : "Sắp diễn ra";
            model.ChiTiet = model.MoTa;
            model.NgayTao = DateTime.Now;
            model.SoLuongDaDangKy = 0;
            model.IsHidden = false;
            model.LuotXem = 0;
            model.NguoiDang = GetCurrentAdminMaQTV() ?? Session["UserId"]?.ToString() ?? "SV001";
            model.MaDiaDiem = AdminEventCommonHelper.ResolveOrCreateLocationId(_db, diaDiemText);

            if (!model.MaDiaDiem.HasValue) { ModelState.AddModelError("MaDiaDiem", "Không thể xử lý địa điểm tổ chức."); return View(CreateViewPath, model); }
            if (model.NgayKetThuc == default(DateTime)) model.NgayKetThuc = null;

            try
            {
                _db.EVENTs.Add(model);
                _db.SaveChanges();
                TempData["Success"] = btnAction == "draft" ? "Đã lưu bản nháp thành công." : "Sự kiện đã được đăng thành công!";
                return RedirectToAction("Manage", "AdminEvents", new { area = "Admin" });
            }
            catch (DbEntityValidationException ex)
            {
                ModelState.AddModelError("", AdminEventCommonHelper.BuildEntityValidationErrorMessage(ex));
                return View(CreateViewPath, model);
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Lỗi lưu dữ liệu: " + ex.Message);
                return View(CreateViewPath, model);
            }
        }
    }
}
