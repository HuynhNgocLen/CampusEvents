using school_event_management.Models;
using shcool_event_management.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Data.Entity;

namespace shcool_event_management.Controllers
{
    public class ScheduleController : Controller
    {
        private readonly school_event_managementEntities db = new school_event_managementEntities();

        private string GetCurrentStudentId()
        {
            return JwtService.GetStudentId(Request);
        }

        public ActionResult Schedule(int offset = 0)
        {
            ViewBag.Title = "Lịch sự kiện của tôi";
            ViewBag.ActivePage = "schedule";

            string studentId = GetCurrentStudentId();
            if (string.IsNullOrEmpty(studentId))
            {
                return RedirectToAction("Login", "Auth");
            }

            // 1. Tính toán ngày dựa trên offset
            // offset = 0 (Tuần này), offset = 1 (Tuần sau), offset = -1 (Tuần trước)
            DateTime baseDate = DateTime.Today.AddDays(offset * 7);

            int diff = (7 + (baseDate.DayOfWeek - DayOfWeek.Monday)) % 7;
            DateTime startOfWeek = baseDate.AddDays(-1 * diff).Date;
            DateTime endOfWeek = startOfWeek.AddDays(7).AddTicks(-1);

            // 2. Truy vấn sự kiện
            var eventsInWeek = db.DangKySuKiens
                .Include(d => d.EVENT)
                .Include(d => d.EVENT.DiaDiem)
                .Include(d => d.EVENT.DanhMuc)
                .Where(d => d.IDSinhVien == studentId
                         && d.TrangThai != "Hủy"
                         && d.EVENT.NgayBatDau >= startOfWeek
                         && d.EVENT.NgayBatDau <= endOfWeek)
                .OrderBy(d => d.EVENT.NgayBatDau)
                .ToList();

            // 3. Truyền dữ liệu ra View
            ViewBag.StartOfWeek = startOfWeek;
            ViewBag.EndOfWeek = endOfWeek;
            ViewBag.WeekOffset = offset;

            return View(eventsInWeek);
        }
    }
}