using System;
using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;
using shcool_event_management.Areas.Admin.ViewModels;
using shcool_event_management.Infrastructure.Constants;
using shcool_event_management.Models;

namespace shcool_event_management.Areas.Admin.Controllers
{
    [Authorize]
    public class AdminDashboardController : BaseAdminController
    {
        public ActionResult Dashboard(int? year = null, int? semester = null)
        {
            ViewBag.ActiveMenu = "dashboard";
            int selectedYear = year ?? DateTime.Now.Year;
            var currentAdmin = GetCurrentAdmin();
            string currentAdminMaQTV = GetCurrentAdminMaQTV();
            int adminQuyen = GetAdminQuyen();
            if (adminQuyen < 0)
            {
                var tenDN = User?.Identity?.Name;
                if (!string.IsNullOrEmpty(tenDN))
                {
                    var adminByUserName = _db.QuanTriViens.FirstOrDefault(x => x.TenDN == tenDN);
                    if (adminByUserName != null)
                    {
                        adminQuyen = adminByUserName.Quyen;
                        Session["AdminQuyen"] = adminQuyen;
                    }
                }
            }
            var adminVienCode = ResolveAdminVienCode(currentAdmin);

            var eventsQuery = _db.EVENTs.AsQueryable();
            if (adminQuyen == 0)
            {
                // QTV quyền 0 xem toàn bộ sự kiện
            }
            else
            {
                // QTV quyền 2 (và các quyền khác) chỉ xem sự kiện của chính họ
                if (string.IsNullOrWhiteSpace(currentAdminMaQTV))
                {
                    eventsQuery = eventsQuery.Where(e => false);
                }
                else
                {
                    eventsQuery = eventsQuery.Where(e => e.NguoiDang == currentAdminMaQTV);
                }
            }

            eventsQuery = eventsQuery.Where(e => e.NgayBatDau.Year == selectedYear);

            var months = Helpers.AdminEventCommonHelper.ResolveSemesterMonths(semester);
            if (months != null) eventsQuery = eventsQuery.Where(e => months.Contains(e.NgayBatDau.Month));

            var visibleEventIds = eventsQuery.Select(e => e.MaEvent);
            var registrationsQuery = _db.DangKySuKiens.Where(d => visibleEventIds.Contains(d.MaEvent));

            // Thống kê tổng quan 
            ViewBag.TotalEvents = eventsQuery.Count();
            ViewBag.UpcomingEvents = eventsQuery.Count(e => e.TrangThai == EventTrangThai.SapDienRa
                                                            && e.NgayBatDau > DateTime.Now);
            ViewBag.OngoingEvents = eventsQuery.Count(e => e.TrangThai == EventTrangThai.DangDienRa);
            ViewBag.CompletedEvents = eventsQuery.Count(e => e.TrangThai == EventTrangThai.DaKetThuc);
            ViewBag.TotalRegistrations = registrationsQuery.Count();
            // "Sinh viên toàn hệ thống" theo bộ lọc hiện tại: mỗi sinh viên trong từng sự kiện được tính là 1 lượt.
            ViewBag.TotalStudents = registrationsQuery
                .Select(d => new { d.IDSinhVien, d.MaEvent })
                .Distinct()
                .Count();
            ViewBag.CancelledEvents = eventsQuery.Count(e => e.TrangThai == EventTrangThai.DaHuy);

            int inFacultyCount = 0;
            int outFacultyCount = 0;
            if (!string.IsNullOrWhiteSpace(adminVienCode))
            {
                inFacultyCount = registrationsQuery.Count(d => d.SinhVien != null && d.SinhVien.MaVien == adminVienCode);
                outFacultyCount = registrationsQuery.Count(d => d.SinhVien != null && d.SinhVien.MaVien != adminVienCode);
            }
            else
            {
                outFacultyCount = registrationsQuery.Count(d => d.SinhVien != null);
            }
            ViewBag.StudentsInAdminFaculty = inFacultyCount;
            ViewBag.StudentsOutAdminFaculty = outFacultyCount;
            ViewBag.AdminVienCode = adminVienCode;

            // Bản nháp của quản trị viên hiện tại (lọc theo năm/học kỳ dựa trên ngày tạo)
            var draftEventsQuery = _db.EVENTs.AsQueryable();
            if (string.IsNullOrWhiteSpace(currentAdminMaQTV))
            {
                draftEventsQuery = draftEventsQuery.Where(e => false);
            }
            else
            {
                draftEventsQuery = draftEventsQuery.Where(e => e.NguoiDang == currentAdminMaQTV);
            }

            var draftStatuses = new[] { "Bản nháp", "Bản Nháp", "Nháp", "Nhap" };
            draftEventsQuery = draftEventsQuery
                .Where(e => draftStatuses.Contains(e.TrangThai) && e.NgayTao.Year == selectedYear);

            if (months != null) draftEventsQuery = draftEventsQuery.Where(e => months.Contains(e.NgayTao.Month));

            ViewBag.UpcomingList = draftEventsQuery
                .Include("DanhMuc")
                .Include("DiaDiem")
                .OrderByDescending(e => e.NgayTao)
                .Take(5)
                .ToList();

            // Thống kê đăng ký theo tháng của năm đang lọc
            var monthlyStats = registrationsQuery
                .Where(d => d.NgayDangKy.Year == selectedYear)
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
            ViewBag.Year = selectedYear;
            ViewBag.Semester = semester;
            ViewBag.AdminQuyen = adminQuyen;

            var availableYears = _db.EVENTs
                .Select(e => e.NgayBatDau.Year)
                .Distinct()
                .OrderByDescending(y => y)
                .ToList();
            if (!availableYears.Contains(selectedYear))
            {
                availableYears.Insert(0, selectedYear);
            }
            ViewBag.AvailableYears = availableYears;

            // Thống kê theo danh mục
            var statsByCategory = eventsQuery
                .Where(e => e.DanhMuc != null)
                .GroupBy(e => new { e.MaDanhMuc, e.DanhMuc.TenDanhMuc })
                .Select(g => new CategoryStat
                {
                    Category = g.Key.TenDanhMuc,
                    Count = g.Count(),
                    TotalReg = g.Sum(e => (int?)e.SoLuongDaDangKy) ?? 0
                })
                .ToList();

            ViewBag.StatsByCategory = statsByCategory;
            ViewBag.CategoryStats = statsByCategory;

            return View();
        }

    }
}