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

        // POST: /Account/Login
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Login(string userId, string password) // Đổi tên tham số cho khớp với Form
        {
            if (!string.IsNullOrEmpty(userId) && !string.IsNullOrEmpty(password))
            {
                // Sau này mày viết code check DB ở đây
                Session["StudentId"] = userId;
                return RedirectToAction("Index", "Users");
            }
            ViewBag.Error = "Sai tên đăng nhập hoặc mật khẩu.";
            return View();
        }

        // POST: /Account/Register
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Register(string FullName, string StudentId, string Password)
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