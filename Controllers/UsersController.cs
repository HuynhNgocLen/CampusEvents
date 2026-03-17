using System;
using System.Collections.Generic;
using System.Web.Mvc;

namespace school_event_management.Controllers
{
    public class UsersController : Controller
    {
        // GET: /Users/Index  (Trang chủ)
        public ActionResult Index()
        {
            ViewBag.Title = "Khám phá Sự kiện";
            ViewBag.ActivePage = "home";
            ViewBag.UserName = "Sinh Viên"; // TODO: lấy từ Session/Auth
            return View();
        }

        // GET: /Users/Events  (Danh sách sự kiện)
        public ActionResult Events()
        {
            ViewBag.Title = "Sự kiện";
            ViewBag.ActivePage = "events";
            ViewBag.UserName = "Sinh Viên";
            return View();
        }

        // GET: /Users/EventDetail/{id}
        public ActionResult EventDetail(int? id)
        {
            ViewBag.Title = "Chi tiết Sự kiện";
            ViewBag.ActivePage = "events";
            ViewBag.UserName = "Sinh Viên";
            return View();
        }

        // GET: /Users/Registrations  (Đăng ký của tôi)
        public ActionResult Registrations()
        {
            ViewBag.Title = "Đăng ký của tôi";
            ViewBag.ActivePage = "registrations";
            ViewBag.UserName = "Sinh Viên";
            return View();
        }

        // POST: /Users/Register  (Xử lý đăng ký sự kiện)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Register(int eventId)
        {
            // TODO: lưu vào database
            TempData["Success"] = "Đăng ký thành công!";
            return RedirectToAction("Registrations");
        }

        // GET: /Users/Schedule  (Lịch của tôi)
        public ActionResult Schedule()
        {
            ViewBag.Title = "Lịch của tôi";
            ViewBag.ActivePage = "schedule";
            ViewBag.UserName = "Sinh Viên";
            return View();
        }
    }
}