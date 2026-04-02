using shcool_event_management.Models;
using school_event_management.Models; // ← thêm namespace này
using System;
using System.Web.Mvc;
using System.Linq;

namespace school_event_management.Controllers
{
    public class AccountController : Controller
    {
        // ← Thêm field db
        private readonly school_event_managementEntities db = new school_event_managementEntities();

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

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Login(string username, string password)
        {
            var sv = db.SinhViens.FirstOrDefault(s => (s.Email == username || s.ID == username) && s.MatKhau == password);

            if (sv != null)
            {
                Session["StudentId"] = sv.ID;
                Session["UserName"] = sv.Ten; 
                return RedirectToAction("Index", "Users");
            }

            TempData["Error"] = "Sai tên đăng nhập hoặc mật khẩu.";
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Register(string lastName, string firstName, string studentId,
            string email, string phoneNumber, string className,
            string faculty, string institute, string password)
        {
            using (var dbReg = new school_event_managementEntities())
            {
                var checkExist = dbReg.SinhViens.FirstOrDefault(s => s.ID == studentId);
                if (checkExist != null)
                {
                    TempData["Error"] = "Mã sinh viên này đã được đăng ký!";
                    return RedirectToAction("Login");
                }

                var newStudent = new SinhVien
                {
                    ID = studentId,
                    Ten = lastName + " " + firstName,
                    Email = email,
                    SoDienThoai = phoneNumber,
                    Lop = className,
                    MaNghanh = faculty, 
                    MaVien = institute,
                    MatKhau = password
                };

                try
                {
                    dbReg.SinhViens.Add(newStudent);
                    dbReg.SaveChanges();
                    TempData["Success"] = "Tài khoản đã được tạo! Vui lòng đăng nhập.";
                }
                catch (Exception ex)
                {
                    TempData["Error"] = "Có lỗi xảy ra khi lưu: " + ex.Message;
                }
            }

            return RedirectToAction("Login");
        }

        // GET: Account/ForgotPassword
        public ActionResult ForgotPassword()
        {
            ViewBag.Title = "Khôi phục mật khẩu";
            return View();
        }

        // POST: Account/ForgotPassword
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ForgotPassword(string identifier, string newPassword, string confirmPassword)
        {
            // 1. Kiểm tra mật khẩu nhập lại
            if (newPassword != confirmPassword)
            {
                TempData["Error"] = "Mật khẩu xác nhận không khớp!";
                return View();
            }

            using (var dbForgot = new school_event_managementEntities())
            {
                // 2. Tìm sinh viên theo ID (MSSV) hoặc Email
                var sv = dbForgot.SinhViens.FirstOrDefault(s => s.Email == identifier || s.ID == identifier);

                if (sv == null)
                {
                    // 3. Nếu không có tài khoản
                    TempData["Error"] = "Tài khoản này chưa có trong dữ liệu. Vui lòng đăng ký mới!";
                    return View();
                }

                try
                {
                    // 4. Nếu có, tiến hành đổi mật khẩu mới
                    sv.MatKhau = newPassword;
                    dbForgot.SaveChanges();

                    // Chuyển hướng về trang đăng nhập với thông báo thành công
                    TempData["Success"] = "Đổi mật khẩu thành công! Vui lòng đăng nhập lại.";
                    return RedirectToAction("Login");
                }
                catch (Exception ex)
                {
                    TempData["Error"] = "Có lỗi xảy ra: " + ex.Message;
                    return View();
                }
            }
        }

        public ActionResult Logout()
        {
            Session.Clear();
            return RedirectToAction("Login");
        }
    }
}