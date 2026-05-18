using System;
using System.Linq;
using System.Web.Mvc;
using System.Data.Entity;

namespace shcool_event_management.Areas.Admin.Controllers
{
    [Authorize]
    public partial class AdminStatsController : BaseAdminController
    {
        protected override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            base.OnActionExecuting(filterContext);
            if (!Request.IsAuthenticated || Session["AdminQuyen"] == null)
            {
                filterContext.Result = RedirectToAction("Login", "AdminAccount", new
                {
                    area = "Admin",
                    returnUrl = Request.RawUrl
                });
                return;
            }

            if (!HasRootPermission())
            {
                filterContext.Result = RedirectToAction("Dashboard", "AdminDashboard", new { area = "Admin" });
            }
        }

        private bool HasRootPermission()
        {
            return Session["AdminQuyen"] != null && Convert.ToInt32(Session["AdminQuyen"]) == 0;
        }

        private static int[] GetSemesterMonths(string semester)
        {
            switch (semester)
            {
                case "hk1": return new[] { 8, 9, 10, 11, 12 };
                case "hk2": return new[] { 1, 2, 3, 4, 5 };
                case "hk3": return new[] { 6, 7 };
                default:    return Enumerable.Range(1, 12).ToArray();
            }
        }

        public ActionResult AdminStats(int year = 0, string semester = "")
        {
            ViewBag.ActiveMenu = "stats";

            if (year == 0) year = DateTime.Now.Year;
            ViewBag.Year = year;
            ViewBag.Semester = semester;

            ViewBag.TotalEvents = _db.EVENTs.Count();
            ViewBag.TotalRegistrations = _db.DangKySuKiens.Count();
            ViewBag.TotalStudents = _db.SinhViens.Count();
            ViewBag.TotalViews = _db.EVENTs.Sum(e => (int?)e.LuotXem) ?? 0;

            int confirmed = _db.DangKySuKiens.Count(d => d.TrangThai == "Đã xác nhận" || d.TrangThai == "Đã hoàn thành");
            int totalReg = _db.DangKySuKiens.Count();
            ViewBag.ParticipationRate = totalReg > 0 ? Math.Round(confirmed * 100.0 / totalReg, 1) : 0;

            int[] activeMonths = GetSemesterMonths(semester);

            var monthly = _db.DangKySuKiens
                .Where(d => d.NgayDangKy.Year == year && activeMonths.Contains(d.NgayDangKy.Month))
                .GroupBy(d => d.NgayDangKy.Month)
                .Select(g => new { Month = g.Key, Count = g.Count() })
                .ToList();

            ViewBag.MonthlyLabels = activeMonths.Select(m => "T" + m).ToArray();
            ViewBag.MonthlyCounts = activeMonths
                .Select(m => monthly.FirstOrDefault(x => x.Month == m)?.Count ?? 0)
                .ToArray();

            var byCategory = _db.EVENTs
                .Where(e => e.DanhMuc != null)
                .GroupBy(e => new { e.MaDanhMuc, e.DanhMuc.TenDanhMuc })
                .Select(g => new {
                    Ma = g.Key.MaDanhMuc,
                    Ten = g.Key.TenDanhMuc,
                    SoEvent = g.Count(),
                    TongDangKy = g.Sum(e => (int?)e.SoLuongDaDangKy) ?? 0
                })
                .ToList();
            ViewBag.ByCategory = byCategory;

            var byStatus = _db.EVENTs
                .GroupBy(e => e.TrangThai)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToList();
            ViewBag.ByStatus = byStatus;

            var twelveWeeksAgo = DateTime.Now.AddDays(-84);
            var weeklyRaw = _db.DangKySuKiens
                .Where(d => d.NgayDangKy >= twelveWeeksAgo)
                .ToList()
                .GroupBy(d => GetWeekNumber(d.NgayDangKy))
                .OrderBy(g => g.Key)
                .Select(g => new { Week = g.Key, Count = g.Count() })
                .ToList();

            ViewBag.WeeklyLabels = weeklyRaw.Select(x => "Tuần " + x.Week).ToArray();
            ViewBag.WeeklyCounts = weeklyRaw.Select(x => x.Count).ToArray();

            ViewBag.ActiveStudents = _db.DangKySuKiens
                .GroupBy(d => d.IDSinhVien)
                .Count(g => g.Count() >= 5);

            ViewBag.AvgDRL = _db.EVENTs.Average(e => (double?)e.DRL) ?? 0;

            return View();
        }

        public JsonResult GetChartData(int year, string semester = "")
        {
            int[] activeMonths = GetSemesterMonths(semester);

            var monthly = _db.DangKySuKiens
                .Where(d => d.NgayDangKy.Year == year && activeMonths.Contains(d.NgayDangKy.Month))
                .GroupBy(d => d.NgayDangKy.Month)
                .Select(g => new { Month = g.Key, Count = g.Count() })
                .ToList();

            var labels = activeMonths.Select(m => "T" + m).ToArray();
            var counts = activeMonths
                .Select(m => monthly.FirstOrDefault(x => x.Month == m)?.Count ?? 0)
                .ToArray();

            return Json(new { labels, data = counts }, JsonRequestBehavior.AllowGet);
        }

        public JsonResult GetTopEvents(string type = "dangky", int year = 0)
        {
            if (year == 0) year = DateTime.Now.Year;

            if (type == "yeuthich")
            {
                var rows = _db.SuKienYeuThiches
                    .Where(f => f.EVENT.NgayBatDau.Year == year)
                    .GroupBy(f => new { f.MaEvent, f.EVENT.TenEvent, f.EVENT.TrangThai, TenDanhMuc = f.EVENT.DanhMuc.TenDanhMuc })
                    .Select(g => new {
                        TenEvent    = g.Key.TenEvent,
                        TenDanhMuc  = g.Key.TenDanhMuc,
                        TrangThai   = g.Key.TrangThai,
                        SoYeuThich  = g.Count()
                    })
                    .OrderByDescending(x => x.SoYeuThich)
                    .Take(10)
                    .ToList();
                return Json(rows, JsonRequestBehavior.AllowGet);
            }

            if (type == "luotxem")
            {
                var rows = _db.EVENTs
                    .Where(e => e.NgayBatDau.Year == year)
                    .Include("DanhMuc")
                    .OrderByDescending(e => e.LuotXem)
                    .Take(10)
                    .Select(e => new {
                        TenEvent   = e.TenEvent,
                        TenDanhMuc = e.DanhMuc.TenDanhMuc,
                        TrangThai  = e.TrangThai,
                        LuotXem    = e.LuotXem
                    })
                    .ToList();
                return Json(rows, JsonRequestBehavior.AllowGet);
            }

            // Mặc định: dangky
            var defRows = _db.EVENTs
                .Where(e => e.NgayBatDau.Year == year)
                .Include("DanhMuc")
                .OrderByDescending(e => e.SoLuongDaDangKy)
                .Take(10)
                .Select(e => new {
                    TenEvent        = e.TenEvent,
                    TenDanhMuc      = e.DanhMuc.TenDanhMuc,
                    TrangThai       = e.TrangThai,
                    SoLuongDaDangKy = e.SoLuongDaDangKy,
                    SoLuongToiDa    = e.SoLuongToiDa
                })
                .ToList();
            return Json(defRows, JsonRequestBehavior.AllowGet);
        }

        // ── Helper ───────────────────────────────────────────────
        private static int GetWeekNumber(DateTime date)
        {
            var cal = System.Globalization.CultureInfo.InvariantCulture.Calendar;
            return cal.GetWeekOfYear(date,
                System.Globalization.CalendarWeekRule.FirstFourDayWeek,
                DayOfWeek.Monday);
        }

    }
}