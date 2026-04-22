using school_event_management.Filters;
using school_event_management.Models;
using shcool_event_management.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Data.Entity;

namespace shcool_event_management.Controllers
{
    [RestrictGuest]
    public class RegistrationController : Controller
    {
        private readonly school_event_managementEntities db = new school_event_managementEntities();
        private string GetCurrentStudentId()
        {
            return JwtService.GetStudentId(Request);
        }

        public ActionResult Registrations()
        {
            ViewBag.Title = "Đăng ký của tôi";
            ViewBag.ActivePage = "registrations";

            string studentId = GetCurrentStudentId();   
            int currentYear = DateTime.Now.Year;

            var sinhVien = db.SinhViens.FirstOrDefault(s => s.ID == studentId);
            ViewBag.SinhVien = sinhVien;

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

            ViewBag.TongHoanThanhNam = tongHoanThanh;
            ViewBag.TongHuyNam = tongHuy;
            ViewBag.TongDangKyNam = tongHoanThanh + tongDangKy;

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

            return View("~/Views/Registration/Registrations.cshtml");
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

            // Cập nhật lại số lượng đăng ký của sự kiện
            var ev = db.EVENTs.Find(maEvent);
            if (ev != null && ev.SoLuongDaDangKy > 0)
                ev.SoLuongDaDangKy--;

            db.SaveChanges();

            TempData["Success"] = "Đã hủy đăng ký thành công.";
            return RedirectToAction("Registrations");
        }

    }
}