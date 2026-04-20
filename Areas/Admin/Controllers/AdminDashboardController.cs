using System;
using System.Linq;
using System.Web.Mvc;
using shcool_event_management.Models;

namespace shcool_event_management.Areas.Admin.Controllers
{
    // [Authorize(Roles = "Admin")]
    public class AdminDashboardController : Controller
    {
        private readonly school_event_managementEntities _db
            = new school_event_managementEntities();

        public ActionResult Dashboard()
        {
            ViewBag.ActiveMenu = "dashboard";

            // Thống kê tổng quan 
            ViewBag.TotalEvents = _db.EVENTs.Count();
            ViewBag.UpcomingEvents = _db.EVENTs.Count(e => e.TrangThai == "Sắp diễn ra"
                                                            && e.NgayBatDau > DateTime.Now);
            ViewBag.OngoingEvents = _db.EVENTs.Count(e => e.TrangThai == "Đang diễn ra");
            ViewBag.TotalRegistrations = _db.DangKySuKiens.Count();
            ViewBag.TotalStudents = _db.SinhViens.Count();
            ViewBag.CancelledEvents = _db.EVENTs.Count(e => e.TrangThai == "Đã hủy");

            // Sự kiện sắp diễn ra gần nhất
            ViewBag.UpcomingList = _db.EVENTs
                .Include("DanhMuc")
                .Include("DiaDiem")
                .Where(e => e.NgayBatDau > DateTime.Now && e.IsHidden == false)
                .OrderBy(e => e.NgayBatDau)
                .Take(5)
                .ToList();

            // Thống kê đăng ký theo tháng (năm hiện tại)
            int currentYear = DateTime.Now.Year;
            var monthlyStats = _db.DangKySuKiens
                .Where(d => d.NgayDangKy.Year == currentYear)
                .GroupBy(d => d.NgayDangKy.Month)
                .Select(g => new { Month = g.Key, Count = g.Count() })
                .OrderBy(x => x.Month)
                .ToList();

            // Đảm bảo đủ 12 tháng (tháng không có data = 0)
            var monthlyData = Enumerable.Range(1, 12)
                .Select(m => new {
                    Month = m,
                    Count = monthlyStats.FirstOrDefault(x => x.Month == m)?.Count ?? 0
                }).ToList();

            ViewBag.MonthlyLabels = monthlyData.Select(x => "T" + x.Month).ToArray();
            ViewBag.MonthlyCounts = monthlyData.Select(x => x.Count).ToArray();

            // Thống kê theo danh mục
            var statsByCategory = _db.EVENTs
                .Where(e => e.DanhMuc != null)
                .GroupBy(e => new { e.MaDanhMuc, e.DanhMuc.TenDanhMuc })
                .Select(g => new {
                    Category = g.Key.TenDanhMuc,
                    MaDanhMuc = g.Key.MaDanhMuc,
                    EventCount = g.Count(),
                    TotalReg = g.Sum(e => (int?)e.SoLuongDaDangKy) ?? 0
                })
                .ToList();

            ViewBag.StatsByCategory = statsByCategory;

            return View();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _db.Dispose();
            base.Dispose(disposing);
        }
    }
}