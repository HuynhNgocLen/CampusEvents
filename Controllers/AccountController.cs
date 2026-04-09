using Newtonsoft.Json;
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

        // GET: Account/Login
        [HttpGet]
        public ActionResult Login()
        {
            if (JwtService.GetStudentId(Request) != null)
                return RedirectToAction("Index", "Users");
            return View();
        }

        [ChildActionOnly]
        public ActionResult GetForm(string type)
        {
            return PartialView(type == "Register" ? "_RegisterForm" : "_LoginForm");
        }

        public ActionResult GetFormAjax(string type)
        {
            if (type == "Register") return PartialView("_RegisterForm");
            return PartialView("_LoginForm");
        }

        // POST: Account/Login
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Login(string username, string password)
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                TempData["Error"] = "Vui lòng nhập đầy đủ thông tin.";
                return View();
            }

            var sv = db.SinhViens.FirstOrDefault(s =>
                (s.Email == username || s.ID == username) && s.MatKhau == password);

            if (sv == null)
            {
                TempData["Error"] = "Sai tên đăng nhập hoặc mật khẩu.";
                return View();
            }

            SetJwtCookie(sv.ID, sv.Ten);
            return RedirectToAction("Index", "Users");
        }

        // GET: Account/GoogleLogin — khởi động OAuth
        public ActionResult GoogleLogin()
        {
            string redirectUri = Url.Action("GoogleCallback", "Account", null, Request.Url.Scheme);

            // State ngẫu nhiên chống CSRF
            string state = Guid.NewGuid().ToString("N");
            Session["oauth_state"] = state;

            string url = "https://accounts.google.com/o/oauth2/v2/auth"
                + $"?client_id={Uri.EscapeDataString(GoogleClientId)}"
                + $"&redirect_uri={Uri.EscapeDataString(redirectUri)}"
                + "&response_type=code"
                + "&scope=openid%20email%20profile"
                + $"&state={state}"
                + "&hd=student.tdmu.edu.vn";

            return Redirect(url);
        }

        // GET: Account/GoogleCallback — Google gọi về đây
        public async Task<ActionResult> GoogleCallback(string code, string state, string error)
        {
            // 1. Lỗi từ Google (user bấm Cancel)
            if (!string.IsNullOrEmpty(error))
            {
                TempData["Error"] = "Đăng nhập Google bị hủy.";
                return RedirectToAction("Login");
            }

            // 2. Kiểm tra state chống CSRF
            if (state != Session["oauth_state"]?.ToString())
            {
                TempData["Error"] = "Yêu cầu không hợp lệ.";
                return RedirectToAction("Login");
            }
            Session.Remove("oauth_state");

            // 3. Đổi code lấy access token
            string redirectUri = Url.Action("GoogleCallback", "Account", null, Request.Url.Scheme);
            string tokenJson;
            using (var http = new HttpClient())
            {
                var body = new StringContent(
                    $"code={Uri.EscapeDataString(code)}"
                    + $"&client_id={Uri.EscapeDataString(GoogleClientId)}"
                    + $"&client_secret={Uri.EscapeDataString(GoogleClientSecret)}"
                    + $"&redirect_uri={Uri.EscapeDataString(redirectUri)}"
                    + "&grant_type=authorization_code",
                    Encoding.UTF8, "application/x-www-form-urlencoded");

                var resp = await http.PostAsync("https://oauth2.googleapis.com/token", body);
                tokenJson = await resp.Content.ReadAsStringAsync();
            }

            var tokenData = Newtonsoft.Json.Linq.JObject.Parse(tokenJson);
            string accessToken = tokenData["access_token"]?.ToString();

            if (string.IsNullOrEmpty(accessToken))
            {
                TempData["Error"] = "Không lấy được token từ Google.";
                return RedirectToAction("Login");
            }

            // Lấy thông tin user
            string userJson;
            using (var http = new HttpClient())
            {
                http.DefaultRequestHeaders.Add("Authorization", "Bearer " + accessToken);
                userJson = await http.GetStringAsync("https://www.googleapis.com/oauth2/v2/userinfo");
            }

            var userInfo = Newtonsoft.Json.Linq.JObject.Parse(userJson);
            string email = userInfo["email"]?.ToString();
            string googleName = userInfo["name"]?.ToString();

            // Kiểm tra đúng domain trường
            if (string.IsNullOrEmpty(email) || !email.EndsWith("@" + AllowedDomain))
            {
                TempData["Error"] = $"Chỉ chấp nhận email @{AllowedDomain} của trường.";
                return RedirectToAction("Login");
            }

            // Tách MSSV từ phần trước @
            string mssv = email.Split('@')[0];

            // Tìm hoặc tạo tài khoản
            var sv = db.SinhViens.FirstOrDefault(s => s.ID == mssv);
            if (sv == null)
            {
                sv = new SinhVien
                {
                    ID = mssv,
                    Ten = googleName ?? mssv,
                    Email = email,
                    MatKhau = Guid.NewGuid().ToString()
                };

                try
                {
                    db.SinhViens.Add(sv);
                    db.SaveChanges();
                }
                catch (Exception ex)
                {
                    TempData["Error"] = "Lỗi tạo tài khoản: " + ex.Message;
                    return RedirectToAction("Login");
                }
            }

            // Set JWT và vào app
            SetJwtCookie(sv.ID, sv.Ten);
            return RedirectToAction("Index", "Users");
        }

        // POST: Account/Register
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Register(string lastName, string firstName, string studentId,
            string email, string phoneNumber, string className,
            string faculty, string institute, string password)
        {
            if (string.IsNullOrWhiteSpace(studentId) || string.IsNullOrWhiteSpace(password)
                || string.IsNullOrWhiteSpace(email))
            {
                TempData["Error"] = "Vui lòng nhập đầy đủ thông tin bắt buộc.";
                return RedirectToAction("Login");
            }

            if (password.Length < 8)
            {
                TempData["Error"] = "Mật khẩu phải có ít nhất 8 ký tự.";
                return RedirectToAction("Login");
            }

            if (db.SinhViens.Any(s => s.ID == studentId))
            {
                TempData["Error"] = "Mã sinh viên này đã được đăng ký!";
                return RedirectToAction("Login");
            }

            var newStudent = new SinhVien
            {
                ID = studentId,
                Ten = (lastName + " " + firstName).Trim(),
                Email = email,
                SoDienThoai = phoneNumber,
                Lop = className,
                MaNghanh = faculty,
                MaVien = institute,
                MatKhau = password
            };

            try
            {
                db.SinhViens.Add(newStudent);
                db.SaveChanges();
                TempData["Success"] = "Tài khoản đã được tạo! Vui lòng đăng nhập.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Có lỗi xảy ra khi lưu: " + ex.Message;
            }

            return RedirectToAction("Login");
        }

        // GET/POST: Account/ForgotPassword
        [HttpGet]
        public ActionResult ForgotPassword() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ForgotPassword(string identifier, string newPassword, string confirmPassword)
        {
            if (newPassword != confirmPassword)
            {
                TempData["Error"] = "Mật khẩu xác nhận không khớp!";
                return View();
            }
            if (newPassword.Length < 8)
            {
                TempData["Error"] = "Mật khẩu phải có ít nhất 8 ký tự.";
                return View();
            }

            var sv = db.SinhViens.FirstOrDefault(s => s.Email == identifier || s.ID == identifier);
            if (sv == null)
            {
                TempData["Error"] = "Tài khoản này chưa có trong dữ liệu. Vui lòng đăng ký mới!";
                return View();
            }

            try
            {
                sv.MatKhau = newPassword;
                db.SaveChanges();
                TempData["Success"] = "Đổi mật khẩu thành công! Vui lòng đăng nhập lại.";
                return RedirectToAction("Login");
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Có lỗi xảy ra: " + ex.Message;
                return View();
            }
        }

        // Logout
        public ActionResult Logout()
        {
            Response.Cookies.Add(new HttpCookie("jwt")
            {
                Expires = DateTime.Now.AddDays(-1),
                HttpOnly = true
            });
            return RedirectToAction("Login");
        }

        // Helper
        private void SetJwtCookie(string studentId, string fullName)
        {
            string token = JwtService.GenerateToken(studentId, fullName);
            Response.Cookies.Add(new HttpCookie("jwt", token)
            {
                HttpOnly = true,
                Expires = DateTime.Now.AddMinutes(30)
            });
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }

    // JwtAuthorize Attribute
    public class JwtAuthorizeAttribute : AuthorizeAttribute
    {
        protected override bool AuthorizeCore(HttpContextBase httpContext)
        {
            var cookie = httpContext.Request.Cookies["jwt"];
            if (cookie == null) return false;
            return JwtService.ValidateToken(cookie.Value) != null;
        }

        protected override void HandleUnauthorizedRequest(AuthorizationContext filterContext)
        {
            filterContext.Result = new RedirectResult("/Account/Login");
        }
    }
}