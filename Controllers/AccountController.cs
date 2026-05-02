using Newtonsoft.Json;
using school_event_management.Helpers;
using school_event_management.Models;
using shcool_event_management.Models;
using System;
using System.Configuration;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;

namespace school_event_management.Controllers
{
    public class AccountController : Controller
    {
        private readonly school_event_managementEntities db = new school_event_managementEntities();
        private static readonly string GoogleClientId = ConfigurationManager.AppSettings["GoogleClientId"];
        private static readonly string GoogleClientSecret = ConfigurationManager.AppSettings["GoogleClientSecret"];
        private const string AllowedDomain = "student.tdmu.edu.vn";

        public ActionResult TestEmail()
        {
            try
            {
                school_event_management.Services.EmailService.SendEmail(
                    "huynhngclen222333@gmail.com", "Test", "Test email");
                return Content("OK - Gửi thành công!");
            }
            catch (Exception ex)
            {
                string smtpEmail = ConfigurationManager.AppSettings["SmtpEmail"];
                string smtpPass = ConfigurationManager.AppSettings["SmtpPassword"];
                return Content($"FAIL<br/>SmtpEmail: {smtpEmail}<br/>PassLength: {smtpPass?.Length}<br/>Error: {ex.Message}");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ResendOTP()
        {
            var studentData = Session["TempStudent"] as SinhVien;
            var resendCount = Session["ResendCount"] as int? ?? 0;

            if (studentData == null)
                return Json(new { success = false, message = "Phiên đăng ký đã hết hạn." });

            if (resendCount >= 3)
            {
                // Xóa session, bắt đăng ký lại
                Session.Remove("TempStudent");
                Session.Remove("RegisterOTP");
                Session.Remove("OTPExpiry");
                Session.Remove("ResendCount");
                return Json(new { success = false, redirect = Url.Action("Login", "Account"), message = "Đã gửi lại quá 3 lần. Vui lòng đăng ký lại." });
            }

            string otp = new Random().Next(10000000, 99999999).ToString();
            Session["RegisterOTP"] = otp;
            Session["OTPExpiry"] = DateTime.Now.AddMinutes(5);
            Session["ResendCount"] = resendCount + 1;

            try
            {
                string body = $@"
        <div style='font-family: Arial, sans-serif; max-width: 500px; margin: 0 auto;
                     border: 1px solid #e0e0e0; border-radius: 12px; overflow: hidden;'>
            <div style='background: #2D3FE2; padding: 24px; text-align: center;'>
                <h2 style='color: #fff; margin: 0;'>🎓 CampusEvents</h2>
            </div>
            <div style='padding: 30px;'>
                <h3 style='color: #1a1a1a; margin-top: 0;'>Mã OTP mới của bạn</h3>
                <p>Xin chào <b>{studentData.Ten}</b>,</p>
                <div style='background: #f4f7fe; border-radius: 10px; padding: 20px; text-align: center; margin: 20px 0;'>
                    <span style='font-size: 36px; font-weight: bold; color: #2D3FE2; letter-spacing: 10px;'>{otp}</span>
                </div>
                <p style='color: #888; font-size: 13px;'>⏱ Mã có hiệu lực trong <b>5 phút</b>.</p>
            </div>
        </div>";

                school_event_management.Services.EmailService.SendEmail(studentData.Email, "🔑 Mã OTP mới - CampusEvents", body);
                return Json(new { success = true, remaining = 3 - (resendCount + 1) });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi gửi mail: " + ex.Message });
            }
        }

        [HttpGet]
        public ActionResult Login(bool tokenExpired = false)
        {
            if (JwtService.GetStudentId(Request) != null)
                return RedirectToAction("Home", "Home");
            if (tokenExpired)
                TempData["Error"] = "Phiên làm việc đã thay đổi hoặc hết hạn. Vui lòng thử lại.";
            return View();
        }

        [ChildActionOnly]
        public ActionResult GetForm(string type)
        {
            if (type == "Register")
            {
                ViewBag.MaNghanhs = db.MaNghanhs.OrderBy(n => n.TenNghanh).ToList();
                ViewBag.Viens = db.Viens.OrderBy(v => v.TenVien).ToList();
                return PartialView("_RegisterForm");
            }

            return PartialView("_LoginForm");
        }

        public ActionResult GetFormAjax(string type)
        {
            if (type == "Register")
            {
                ViewBag.MaNghanhs = db.MaNghanhs.OrderBy(n => n.TenNghanh).ToList();
                ViewBag.Viens = db.Viens.OrderBy(v => v.TenVien).ToList();
                return PartialView("_RegisterForm");
            }

            return PartialView("_LoginForm");
        }

        [HttpGet]
        public JsonResult GetMajorsByInstitute(string maVien)
        {
            if (string.IsNullOrWhiteSpace(maVien))
                return Json(Enumerable.Empty<object>(), JsonRequestBehavior.AllowGet);

            var majors = db.Database.SqlQuery<MajorOption>(
                @"SELECT MaNghanh AS MaNghanh, TenNghanh AS TenNghanh
                  FROM MaNghanh
                  WHERE THUOCVIEN = @p0
                  ORDER BY TenNghanh",
                maVien)
                .ToList()
                .Select(m => new
                {
                    maNghanh = m.MaNghanh,
                    tenNghanh = m.TenNghanh
                })
                .ToList();

            return Json(majors, JsonRequestBehavior.AllowGet);
        }

        /// <summary>Đăng nhập khách: chỉ được xem trang chủ (JWT có claim userType=guest).</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult GuestLogin()
        {
            SetJwtCookie(JwtService.GuestUserId, "Khách tham quan", isGuest: true);
            return RedirectToAction("Home", "Home");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Login(string username, string password)
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                TempData["Error"] = "Vui lòng nhập đầy đủ thông tin.";
                return View();
            }
            var sv = db.SinhViens.FirstOrDefault(s => s.Email == username || s.ID == username);
            if (sv == null)
            {
                TempData["Error"] = "Tài khoản không tồn tại.";
                return View();
            }
            if (!PasswordHasher.Verify(password, sv.MatKhau))
            {
                TempData["Error"] = "Sai tên đăng nhập hoặc mật khẩu.";
                return View();
            }

            if (PasswordHasher.NeedsUpgrade(sv.MatKhau))
            {
                sv.MatKhau = PasswordHasher.HashPassword(password);
                db.SaveChanges();
            }

            SetJwtCookie(sv.ID, sv.Ten);
            return RedirectToAction("Home", "Home");
        }

        public ActionResult GoogleLogin()
        {
            string redirectUri = Url.Action("GoogleCallback", "Account", null, Request.Url.Scheme);
            string state = Guid.NewGuid().ToString("N");
            Session["oauth_state"] = state;
            string url = "https://accounts.google.com/o/oauth2/v2/auth"
                + $"?client_id={Uri.EscapeDataString(GoogleClientId)}"
                + $"&redirect_uri={Uri.EscapeDataString(redirectUri)}"
                + "&response_type=code&scope=openid%20email%20profile"
                + $"&state={state}&hd=student.tdmu.edu.vn";
            return Redirect(url);
        }

        public async Task<ActionResult> GoogleCallback(string code, string state, string error)
        {
            if (!string.IsNullOrEmpty(error)) { TempData["Error"] = "Đăng nhập Google bị hủy."; return RedirectToAction("Login"); }
            if (state != Session["oauth_state"]?.ToString()) { TempData["Error"] = "Yêu cầu không hợp lệ."; return RedirectToAction("Login"); }
            Session.Remove("oauth_state");

            string redirectUri = Url.Action("GoogleCallback", "Account", null, Request.Url.Scheme);
            string tokenJson;
            using (var http = new HttpClient())
            {
                var body = new StringContent(
                    $"code={Uri.EscapeDataString(code)}&client_id={Uri.EscapeDataString(GoogleClientId)}&client_secret={Uri.EscapeDataString(GoogleClientSecret)}&redirect_uri={Uri.EscapeDataString(redirectUri)}&grant_type=authorization_code",
                    Encoding.UTF8, "application/x-www-form-urlencoded");
                tokenJson = await (await http.PostAsync("https://oauth2.googleapis.com/token", body)).Content.ReadAsStringAsync();
            }
            var tokenData = Newtonsoft.Json.Linq.JObject.Parse(tokenJson);
            string accessToken = tokenData["access_token"]?.ToString();
            if (string.IsNullOrEmpty(accessToken)) { TempData["Error"] = "Không lấy được token từ Google."; return RedirectToAction("Login"); }

            string userJson;
            using (var http = new HttpClient())
            {
                http.DefaultRequestHeaders.Add("Authorization", "Bearer " + accessToken);
                userJson = await http.GetStringAsync("https://www.googleapis.com/oauth2/v2/userinfo");
            }
            var userInfo = Newtonsoft.Json.Linq.JObject.Parse(userJson);
            string email = userInfo["email"]?.ToString();
            string googleName = userInfo["name"]?.ToString();
            if (string.IsNullOrEmpty(email) || !email.EndsWith("@" + AllowedDomain))
            { TempData["Error"] = $"Chỉ chấp nhận email @{AllowedDomain}."; return RedirectToAction("Login"); }

            string mssv = email.Split('@')[0];
            var sv = db.SinhViens.FirstOrDefault(s => s.ID == mssv);
            if (sv == null)
            {
                sv = new SinhVien
                {
                    ID = mssv,
                    Ten = googleName ?? mssv,
                    Email = email,
                    MatKhau = PasswordHasher.HashPassword(Guid.NewGuid().ToString("N"))
                };
                try { db.SinhViens.Add(sv); db.SaveChanges(); }
                catch (Exception ex) { TempData["Error"] = "Lỗi tạo tài khoản: " + ex.Message; return RedirectToAction("Login"); }
            }
            SetJwtCookie(sv.ID, sv.Ten);
            return RedirectToAction("Home", "Home");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Register(string lastName, string firstName, string studentId,
            string email, string phoneNumber, string className, string faculty, string institute, string password)
        {
            if (string.IsNullOrWhiteSpace(studentId) || string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(email))
            { TempData["Error"] = "Vui lòng nhập đầy đủ thông tin bắt buộc."; return RedirectToAction("Login"); }
            if (db.SinhViens.Any(s => s.ID == studentId))
            { TempData["Error"] = "Mã sinh viên này đã được đăng ký!"; return RedirectToAction("Login"); }

            string otp = new Random().Next(10000000, 99999999).ToString();
            var newStudent = new SinhVien
            {
                ID = studentId,
                Ten = (lastName + " " + firstName).Trim(),
                Email = email,
                SoDienThoai = phoneNumber,
                Lop = className,
                MaNghanh = faculty,
                MaVien = institute,
                MatKhau = PasswordHasher.HashPassword(password)
            };
            Session["TempStudent"] = newStudent;
            Session["RegisterOTP"] = otp;
            Session["OTPExpiry"] = DateTime.Now.AddMinutes(10);

            try
            {
                string body = $@"<div style='font-family:Arial,sans-serif;max-width:500px;margin:0 auto;border:1px solid #e0e0e0;border-radius:12px;overflow:hidden'>
                    <div style='background:#2D3FE2;padding:24px;text-align:center'><h2 style='color:#fff;margin:0'>🎓 CampusEvents</h2></div>
                    <div style='padding:30px'>
                        <h3>Xác nhận đăng ký tài khoản</h3>
                        <p>Xin chào <b>{newStudent.Ten}</b>, mã OTP của bạn là:</p>
                        <div style='background:#f4f7fe;border-radius:10px;padding:20px;text-align:center;margin:20px 0'>
                            <span style='font-size:36px;font-weight:bold;color:#2D3FE2;letter-spacing:10px'>{otp}</span>
                        </div>
                        <p style='color:#888;font-size:13px'>⏱ Mã có hiệu lực trong <b>10 phút</b>.</p>
                    </div>
                    <div style='background:#f9f9f9;padding:16px;text-align:center;border-top:1px solid #eee'><p style='color:#aaa;font-size:12px;margin:0'>© CampusEvents</p></div>
                </div>";
                school_event_management.Services.EmailService.SendEmail(email, "🔑 Mã OTP xác thực CampusEvents", body);
                TempData["Info"] = $"Mã OTP đã gửi đến <b>{email}</b>.";
                return RedirectToAction("VerifyOTP");
            }
            catch (Exception ex) { TempData["Error"] = "Lỗi gửi mail: " + ex.Message; return RedirectToAction("Login"); }
        }

        [HttpGet]
        public ActionResult VerifyOTP()
        {
            if (Session["RegisterOTP"] == null) return RedirectToAction("Login");
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult VerifyOTP(string otpInput)
        {
            var sessionOtp = Session["RegisterOTP"] as string;
            var studentData = Session["TempStudent"] as SinhVien;
            var expiry = Session["OTPExpiry"] as DateTime?;

            if (expiry.HasValue && DateTime.Now > expiry.Value)
            {
                Session.Remove("RegisterOTP"); Session.Remove("TempStudent"); Session.Remove("OTPExpiry");
                ViewBag.Error = "Mã OTP đã hết hạn! Vui lòng đăng ký lại.";
                return View();
            }
            if (studentData != null && sessionOtp == otpInput)
            {
                try
                {
                    db.SinhViens.Add(studentData); db.SaveChanges();
                    Session.Remove("RegisterOTP"); Session.Remove("TempStudent"); Session.Remove("OTPExpiry");
                    TempData["Success"] = "Đăng ký thành công! Mời bạn đăng nhập.";
                    return RedirectToAction("Login");
                }
                catch (Exception ex) { ViewBag.Error = "Lỗi hệ thống: " + ex.Message; }
            }
            else { ViewBag.Error = "Mã xác thực không chính xác!"; }
            return View();
        }

        [HttpGet]
        public ActionResult ForgotPassword() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ForgotPassword(string identifier)
        {
            if (string.IsNullOrWhiteSpace(identifier)) { TempData["Error"] = "Vui lòng nhập MSSV hoặc Email."; return View(); }
            var sv = db.SinhViens.FirstOrDefault(s => s.Email == identifier || s.ID == identifier);
            if (sv == null) { TempData["Success"] = "Nếu tài khoản tồn tại, email khôi phục đã được gửi."; return View(); }

            string resetToken = Guid.NewGuid().ToString("N");
            Session["ResetToken_" + resetToken] = sv.ID;
            Session["ResetToken_Expiry_" + resetToken] = DateTime.Now.AddMinutes(30);
            string resetLink = Url.Action("ResetPassword", "Account", new { token = resetToken }, Request.Url.Scheme);

            try
            {
                string body = $@"<div style='font-family:Arial,sans-serif;max-width:500px;margin:0 auto;border:1px solid #e0e0e0;border-radius:12px;overflow:hidden'>
                    <div style='background:#2D3FE2;padding:24px;text-align:center'><h2 style='color:#fff;margin:0'>🎓 CampusEvents</h2></div>
                    <div style='padding:30px'>
                        <h3>Yêu cầu đặt lại mật khẩu</h3>
                        <p>Xin chào <b>{sv.Ten}</b>, nhấn nút bên dưới để đặt lại mật khẩu:</p>
                        <div style='text-align:center;margin:30px 0'>
                            <a href='{resetLink}' style='background:#2D3FE2;color:#fff;padding:14px 32px;border-radius:8px;text-decoration:none;font-weight:bold'>🔐 Đặt lại mật khẩu</a>
                        </div>
                        <p style='color:#888;font-size:13px'>⏱ Liên kết có hiệu lực trong <b>30 phút</b>.</p>
                        <p style='color:#aaa;font-size:12px'>Hoặc copy link: <span style='color:#2D3FE2'>{resetLink}</span></p>
                    </div>
                    <div style='background:#f9f9f9;padding:16px;text-align:center;border-top:1px solid #eee'><p style='color:#aaa;font-size:12px;margin:0'>© CampusEvents</p></div>
                </div>";
                school_event_management.Services.EmailService.SendEmail(sv.Email, "🔐 Đặt lại mật khẩu CampusEvents", body);
            }
            catch (Exception ex) { TempData["Error"] = "Lỗi gửi mail: " + ex.Message; return View(); }

            TempData["Success"] = $"Email đã gửi đến <b>{MaskEmail(sv.Email)}</b>. Kiểm tra hộp thư (hoặc thư rác).";
            return View();
        }

        [HttpGet]
        public ActionResult ResetPassword(string token)
        {
            if (string.IsNullOrEmpty(token)) return RedirectToAction("Login");
            var studentId = Session["ResetToken_" + token] as string;
            var expiry = Session["ResetToken_Expiry_" + token] as DateTime?;
            if (studentId == null || !expiry.HasValue || DateTime.Now > expiry.Value)
            { TempData["Error"] = "Liên kết không hợp lệ hoặc đã hết hạn."; return RedirectToAction("ForgotPassword"); }
            ViewBag.Token = token;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ResetPassword(string token, string newPassword, string confirmPassword)
        {
            if (string.IsNullOrEmpty(token)) return RedirectToAction("Login");
            var studentId = Session["ResetToken_" + token] as string;
            var expiry = Session["ResetToken_Expiry_" + token] as DateTime?;
            if (studentId == null || !expiry.HasValue || DateTime.Now > expiry.Value)
            { TempData["Error"] = "Liên kết đã hết hạn."; return RedirectToAction("ForgotPassword"); }
            if (newPassword != confirmPassword) { ViewBag.Error = "Mật khẩu xác nhận không khớp!"; ViewBag.Token = token; return View(); }
            if (newPassword.Length < 8) { ViewBag.Error = "Mật khẩu phải có ít nhất 8 ký tự."; ViewBag.Token = token; return View(); }

            var sv = db.SinhViens.FirstOrDefault(s => s.ID == studentId);
            if (sv == null) { TempData["Error"] = "Tài khoản không tồn tại."; return RedirectToAction("Login"); }
            try
            {
                sv.MatKhau = PasswordHasher.HashPassword(newPassword);
                db.SaveChanges();
                Session.Remove("ResetToken_" + token); Session.Remove("ResetToken_Expiry_" + token);
                TempData["Success"] = "Đổi mật khẩu thành công! Vui lòng đăng nhập lại.";
                return RedirectToAction("Login");
            }
            catch (Exception ex) { ViewBag.Error = "Có lỗi xảy ra: " + ex.Message; ViewBag.Token = token; return View(); }
        }

        public ActionResult Logout()
        {
            Response.Cookies.Add(new HttpCookie("jwt") { Expires = DateTime.Now.AddDays(-1), HttpOnly = true });
            return RedirectToAction("Login");
        }

        private static string MaskEmail(string email)
        {
            if (string.IsNullOrEmpty(email)) return email;
            var parts = email.Split('@');
            if (parts.Length != 2) return email;
            var local = parts[0];
            var masked = local.Length <= 2 ? local + "***" : local.Substring(0, 2) + new string('*', local.Length - 2);
            return masked + "@" + parts[1];
        }

        private void SetJwtCookie(string studentId, string fullName, bool isGuest = false)
        {
            string token = JwtService.GenerateToken(studentId, fullName, isGuest);
            Response.Cookies.Add(new HttpCookie("jwt", token)
            {
                HttpOnly = true,
                Expires = DateTime.Now.AddMinutes(JwtService.TokenLifetimeMinutes)
            });
        }

        protected override void Dispose(bool disposing) { if (disposing) db.Dispose(); base.Dispose(disposing); }

        private class MajorOption
        {
            public string MaNghanh { get; set; }
            public string TenNghanh { get; set; }
        }
    }
}