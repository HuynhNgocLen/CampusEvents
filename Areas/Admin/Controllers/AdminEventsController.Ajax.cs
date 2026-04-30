using System;
using System.Linq;
using System.Web.Mvc;
using System.Data.Entity;
using school_event_management.Helpers;

namespace shcool_event_management.Areas.Admin.Controllers
{
    public partial class AdminEventsController
    {
        [HttpPost]
        public JsonResult ToggleHidden(int id)
        {
            var ev = _db.EVENTs.Find(id);
            if (ev == null) return Json(new { success = false, message = "Không tìm thấy sự kiện." });
            if (ev.NguoiDang != GetCurrentAdminMaQTV()) return Json(new { success = false, message = "Bạn không có quyền thao tác sự kiện này." });

            ev.IsHidden = !ev.IsHidden;
            ev.NgayCapNhat = DateTime.Now;
            _db.SaveChanges();
            return Json(new { success = true, isHidden = ev.IsHidden });
        }

        public JsonResult GetStudents(int id, string search = null)
        {
            var ev = _db.EVENTs.FirstOrDefault(x => x.MaEvent == id);
            if (ev == null) return Json(new { success = false, message = "Không tìm thấy sự kiện." }, JsonRequestBehavior.AllowGet);
            if (ev.NguoiDang != GetCurrentAdminMaQTV()) return Json(new { success = false, message = "Bạn không có quyền xem dữ liệu sự kiện này." }, JsonRequestBehavior.AllowGet);

            var query = _db.DangKySuKiens
                           .Include("SinhVien")
                           .Include("SinhVien.Vien")
                           .Where(d => d.MaEvent == id)
                           .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(d => d.SinhVien.Ten.Contains(search) || d.IDSinhVien.Contains(search));
            }

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

            return Json(new { success = true, data = list, total = list.Count }, JsonRequestBehavior.AllowGet);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public JsonResult UpdateStudentStatus(int maEvent, string idSinhVien, string trangThai)
        {
            if (string.IsNullOrWhiteSpace(idSinhVien) || !RegistrationStatuses.Contains(trangThai))
            {
                return Json(new { success = false, message = "Dữ liệu cập nhật không hợp lệ." });
            }

            var ownedEvent = _db.EVENTs.FirstOrDefault(x => x.MaEvent == maEvent);
            if (ownedEvent == null) return Json(new { success = false, message = "Không tìm thấy sự kiện." });
            if (ownedEvent.NguoiDang != GetCurrentAdminMaQTV()) return Json(new { success = false, message = "Bạn không có quyền thao tác sự kiện này." });

            var reg = _db.DangKySuKiens.FirstOrDefault(d => d.MaEvent == maEvent && d.IDSinhVien == idSinhVien);
            if (reg == null) return Json(new { success = false, message = "Không tìm thấy đăng ký của sinh viên." });
            if (string.Equals(reg.TrangThai, trangThai, StringComparison.OrdinalIgnoreCase))
            {
                return Json(new { success = true, status = reg.TrangThai, message = "Trạng thái đã được giữ nguyên." });
            }

            var oldStatus = reg.TrangThai ?? string.Empty;
            reg.TrangThai = RegistrationStatusHelper.ToStoredRegistrationStatus(trangThai);

            var ev = _db.EVENTs.Find(maEvent);
            if (ev != null)
            {
                if (oldStatus == "Đã hủy" && trangThai != "Đã hủy") ev.SoLuongDaDangKy++;
                else if (oldStatus != "Đã hủy" && trangThai == "Đã hủy" && ev.SoLuongDaDangKy > 0) ev.SoLuongDaDangKy--;
            }

            _db.SaveChanges();
            return Json(new { success = true, status = RegistrationStatusHelper.Normalize(reg.TrangThai) });
        }

        [HttpPost]
        public JsonResult CloseEvent(int id)
        {
            var ev = _db.EVENTs.Find(id);
            if (ev == null) return Json(new { success = false, message = "Không tìm thấy sự kiện." });
            if (ev.NguoiDang != GetCurrentAdminMaQTV()) return Json(new { success = false, message = "Bạn không có quyền thao tác sự kiện này." });

            ev.TrangThai = "Đã hủy";
            ev.IsHidden = true;
            ev.NgayCapNhat = DateTime.Now;
            _db.SaveChanges();

            return Json(new { success = true });
        }

        [HttpPost]
        public JsonResult ReopenEvent(int id)
        {
            var ev = _db.EVENTs.Find(id);
            if (ev == null) return Json(new { success = false, message = "Không tìm thấy sự kiện." });
            if (ev.NguoiDang != GetCurrentAdminMaQTV()) return Json(new { success = false, message = "Bạn không có quyền thao tác sự kiện này." });

            var now = DateTime.Now;
            var startReg = ev.NgayBatDauDangKy;
            var endReg = ev.NgayHetHanDangKy;

            if (startReg.HasValue && startReg.Value > now) ev.TrangThai = "Sắp diễn ra";
            else if (startReg.HasValue && endReg.HasValue && now >= startReg.Value && now <= endReg.Value) ev.TrangThai = "Đang diễn ra";
            else ev.TrangThai = "Sắp diễn ra";

            ev.IsHidden = false;
            ev.NgayCapNhat = DateTime.Now;
            _db.SaveChanges();

            return Json(new { success = true, status = ev.TrangThai });
        }
    }
}
