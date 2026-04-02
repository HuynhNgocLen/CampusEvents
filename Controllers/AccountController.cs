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
            var sv = db.SinhViens.FirstOrDefault(s => s.Email == username && s.MatKhau == password);
            if (sv != null)
            {
                Session["StudentId"] = sv.ID;
                Session["UserName"] = sv.Ten; 
                return RedirectToAction("Index", "Users");
            }

            // Đổi ViewBag thành TempData
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

        public ActionResult Logout()
        {
            Session.Clear();
            return RedirectToAction("Login");
        }
    }
}