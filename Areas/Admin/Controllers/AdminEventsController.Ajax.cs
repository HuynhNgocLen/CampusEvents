using System;
using System.Linq;
using System.Web.Mvc;
using System.Data.Entity;
using school_event_management.Helpers;
using shcool_event_management.Infrastructure.Constants;

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
            var currentAdmin = GetCurrentAdmin();
            if (currentAdmin == null)
            {
                return Json(new { success = false, message = "Phiên đăng nhập đã hết hạn." }, JsonRequestBehavior.AllowGet);
            }

            var ev = _db.EVENTs.FirstOrDefault(x => x.MaEvent == id);
            if (ev == null) return Json(new { success = false, message = "Không tìm thấy sự kiện." }, JsonRequestBehavior.AllowGet);
            var canAccessEvent = currentAdmin.Quyen == 0
                || string.Equals(ev.NguoiDang, currentAdmin.MaQTV, StringComparison.OrdinalIgnoreCase);
            if (!canAccessEvent) return Json(new { success = false, message = "Bạn không có quyền xem dữ liệu sự kiện này." }, JsonRequestBehavior.AllowGet);

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

            ev.TrangThai = EventTrangThai.DaHuy;
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

            if (startReg.HasValue && startReg.Value > now) ev.TrangThai = EventTrangThai.SapDienRa;
            else if (startReg.HasValue && endReg.HasValue && now >= startReg.Value && now <= endReg.Value) ev.TrangThai = EventTrangThai.DangDienRa;
            else ev.TrangThai = EventTrangThai.SapDienRa;

            ev.IsHidden = false;
            ev.NgayCapNhat = DateTime.Now;
            _db.SaveChanges();

            return Json(new { success = true, status = ev.TrangThai });
        }

        /// <summary>Liên kết điểm danh + ảnh QR (modal trang Quản lý sự kiện).</summary>
        public JsonResult GetAttendanceQr(int id)
        {
            var currentAdmin = GetCurrentAdmin();
            if (currentAdmin == null)
            {
                return Json(new { success = false, message = "Phiên đăng nhập đã hết hạn." }, JsonRequestBehavior.AllowGet);
            }

            var ev = _db.EVENTs.FirstOrDefault(x => x.MaEvent == id);
            if (ev == null)
            {
                return Json(new { success = false, message = "Không tìm thấy sự kiện." }, JsonRequestBehavior.AllowGet);
            }

            var canAccess = currentAdmin.Quyen == 0
                || string.Equals(ev.NguoiDang, currentAdmin.MaQTV, StringComparison.OrdinalIgnoreCase);
            if (!canAccess)
            {
                return Json(new { success = false, message = "Bạn không có quyền xem dữ liệu sự kiện này." }, JsonRequestBehavior.AllowGet);
            }

            var qrSecret = System.Configuration.ConfigurationManager.AppSettings["AttendanceQrSigningKey"];
            qrSecret = string.IsNullOrWhiteSpace(qrSecret) ? "CampusEvents_AttendanceQr_ChangeInWebConfig" : qrSecret.Trim();
            var token = AttendanceCheckInTokenHelper.CreateToken(id, qrSecret);
            var rel = Url.Action("CheckIn", "Attendance", new { area = "", id = id, t = token });
            string abs = null;
            if (Request?.Url != null && rel != null)
            {
                abs = new System.Uri(Request.Url, rel).AbsoluteUri;
            }

            string qrSrc = null;
            if (!string.IsNullOrEmpty(abs))
            {
                try
                {
                    qrSrc = shcool_event_management.Helpers.QRCodeHelper.GenerateQRCodeFromLink(abs, 12);
                }
                catch
                {
                    // bỏ qua nếu thư viện QR lỗi
                }
            }

            var expiresAtUtc = AttendanceCheckInTokenHelper.GetCurrentTokenExpiresAtUtc();
            var downloadFileName = AttendanceQrDownloadFileNameHelper.Build(id, ev.TenEvent);

            return Json(new
            {
                success = true,
                checkInUrl = abs,
                qrSrc,
                eventTitle = ev.TenEvent,
                downloadFileName,
                expiresAtUtc = expiresAtUtc.ToString("o", System.Globalization.CultureInfo.InvariantCulture),
                windowSeconds = AttendanceCheckInTokenHelper.TokenWindowSeconds
            }, JsonRequestBehavior.AllowGet);
        }
    }
}
