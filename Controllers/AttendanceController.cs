using System.Configuration;
using System.Linq;
using System.Web.Mvc;
using school_event_management.Helpers;
using school_event_management.Models;
using shcool_event_management.Models;

namespace school_event_management.Controllers
{
    /// <summary>
    /// Điểm danh sự kiện qua QR: sinh viên đã đăng nhập, mở link có chữ ký hợp lệ.
    /// </summary>
    public class AttendanceController : Controller
    {
        private readonly school_event_managementEntities _db = new school_event_managementEntities();

        private static string GetSigningKey()
        {
            var k = ConfigurationManager.AppSettings["AttendanceQrSigningKey"];
            return string.IsNullOrWhiteSpace(k) ? "CampusEvents_AttendanceQr_ChangeInWebConfig" : k.Trim();
        }

        [HttpGet]
        public ActionResult CheckIn(int? id, string t)
        {
            JwtService.TryRenewTokenCookie(Request, Response);
            ViewBag.Title = "Điểm danh sự kiện";

            if (!id.HasValue || string.IsNullOrWhiteSpace(t))
            {
                ViewBag.CheckInKind = "invalid";
                ViewBag.CheckInMessage = "Liên kết điểm danh không hợp lệ hoặc đã bị cắt bớt.";
                return View("CheckIn");
            }

            var maEvent = id.Value;
            if (!AttendanceCheckInTokenHelper.ValidateToken(maEvent, t, GetSigningKey()))
            {
                ViewBag.CheckInKind = "invalid";
                ViewBag.CheckInMessage = "Mã điểm danh không đúng hoặc đã hết hiệu lực. Mã được làm mới mỗi 1 phút — vui lòng quét QR / mở liên kết mới từ ban tổ chức.";
                return View("CheckIn");
            }

            var ev = _db.EVENTs.FirstOrDefault(e => e.MaEvent == maEvent);
            if (ev == null)
            {
                ViewBag.CheckInKind = "invalid";
                ViewBag.CheckInMessage = "Không tìm thấy sự kiện.";
                return View("CheckIn");
            }

            ViewBag.EventTitle = ev.TenEvent;

            var studentId = JwtService.GetStudentId(Request);
            if (string.IsNullOrEmpty(studentId))
            {
                var returnPath = Request.Url?.PathAndQuery ?? ("/Attendance/CheckIn?id=" + maEvent + "&t=" + t);
                return RedirectToAction("Login", "Account", new { returnUrl = returnPath });
            }

            if (JwtService.IsGuest(Request))
            {
                ViewBag.CheckInKind = "guest";
                ViewBag.CheckInMessage = "Chế độ khách không thể điểm danh. Đăng xuất khách và đăng nhập bằng tài khoản sinh viên.";
                return View("CheckIn");
            }

            var reg = _db.DangKySuKiens.FirstOrDefault(d => d.MaEvent == maEvent && d.IDSinhVien == studentId);
            if (reg == null)
            {
                ViewBag.CheckInKind = "notregistered";
                ViewBag.CheckInMessage = "Bạn chưa đăng ký sự kiện này.";
                return View("CheckIn");
            }

            var normalized = RegistrationStatusHelper.Normalize(reg.TrangThai);
            if (normalized == "Đã hủy")
            {
                ViewBag.CheckInKind = "cancelled";
                ViewBag.CheckInMessage = "Đăng ký của bạn đã bị hủy, không thể điểm danh.";
                return View("CheckIn");
            }

            if (normalized == "Đã hoàn thành")
            {
                ViewBag.CheckInKind = "already";
                ViewBag.CheckInMessage = "Bạn đã điểm danh / hoàn thành sự kiện này trước đó.";
                return View("CheckIn");
            }

            // "Đã đăng ký" (và chuẩn hóa từ legacy)
            reg.TrangThai = RegistrationStatusHelper.ToStoredRegistrationStatus("Đã hoàn thành");
            _db.SaveChanges();

            ViewBag.CheckInKind = "success";
            ViewBag.CheckInMessage = "Điểm danh thành công. Trạng thái tham dự đã được cập nhật thành Đã hoàn thành.";
            return View("CheckIn");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _db.Dispose();
            base.Dispose(disposing);
        }
    }
}
