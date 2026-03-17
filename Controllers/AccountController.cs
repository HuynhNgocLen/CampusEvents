using System.Web.Mvc;

namespace school_event_management.Controllers
{
    public class AccountController : Controller
    {
        // GET: /Account/Login
        public ActionResult Login()
        {
            ViewBag.Title = "Đăng nhập";
            return View();
        }

        // POST: /Account/Login
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Login(string username, string password)
        {
            // TODO: xác thực với database
            if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
            {
                Session["UserName"] = username;
                return RedirectToAction("Index", "Users");
            }
            ViewBag.Error = "Sai tên đăng nhập hoặc mật khẩu.";
            return View();
        }

        // POST: /Account/Register
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Register(string lastName, string firstName,
                                     string studentId, string email,
                                     string faculty, string password)
        {
            // TODO: lưu vào database
            TempData["Success"] = "Tài khoản đã được tạo! Vui lòng đăng nhập.";
            return RedirectToAction("Login");
        }

        // GET: /Account/Logout
        public ActionResult Logout()
        {
            Session.Clear();
            return RedirectToAction("Login");
        }
    }
}