using System;
using System.Linq;
using System.Web.Mvc;
using System.Data.Entity;
using shcool_event_management.Models;

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

            if (!HasStatsPermission())
            {
                filterContext.Result = RedirectToAction("Dashboard", "AdminDashboard", new { area = "Admin" });
            }
        }

        private bool HasStatsPermission()
        {
            if (Session["AdminQuyen"] == null) return false;
            int q = Convert.ToInt32(Session["AdminQuyen"]);
            return q == 0 || q == 1 || q == 2;
        }

        /// <summary>
        /// Quyền 0: có thể lọc theo maVien hoặc để trống (tất cả viện).
        /// Quyền 1, 2: chỉ dữ liệu của viện gắn với tài khoản (MaVien / MaQTV).
        /// </summary>
        private string ResolveEffectiveMaVien(string requestedMaVien, QuanTriVien admin, out int adminQuyen)
        {
            adminQuyen = admin?.Quyen ?? GetAdminQuyen();
            if (adminQuyen < 0 && admin != null)
                adminQuyen = admin.Quyen;

            if (adminQuyen == 0)
            {
                if (string.IsNullOrWhiteSpace(requestedMaVien))
                    return null;
                var trimmed = requestedMaVien.Trim();
                return _db.Viens.Any(v => v.MaVien == trimmed) ? trimmed : null;
            }

            var code = ResolveAdminVienCode(admin);
            if (!string.IsNullOrWhiteSpace(code))
                return code;

            return admin?.MaQTV;
        }

        private IQueryable<EVENT> BuildScopedEventsQuery(int adminQuyen, string effectiveMaVien)
        {
            var q = _db.EVENTs.AsQueryable();

            if (adminQuyen == 1 || adminQuyen == 2)
            {
                if (string.IsNullOrWhiteSpace(effectiveMaVien))
                    return q.Where(e => false);
                return q.Where(e => e.MaVien == effectiveMaVien);
            }

            if (!string.IsNullOrWhiteSpace(effectiveMaVien))
                q = q.Where(e => e.MaVien == effectiveMaVien);

            return q;
        }

        private static int[] GetSemesterMonths(string semester)
        {
            switch (semester)
            {
                case "hk1": return new[] { 8, 9, 10, 11, 12 };
                case "hk2": return new[] { 1, 2, 3, 4, 5 };
                case "hk3": return new[] { 6, 7 };
                default: return Enumerable.Range(1, 12).ToArray();
            }
        }

        public ActionResult AdminStats(int year = 0, string semester = "", string maVien = "")
        {
            ViewBag.ActiveMenu = "stats";

            var currentAdmin = GetCurrentAdmin();
            int adminQuyen = GetAdminQuyen();
            if (adminQuyen < 0 && currentAdmin != null)
            {
                adminQuyen = currentAdmin.Quyen;
                Session["AdminQuyen"] = adminQuyen;
            }

            string effectiveMaVien = ResolveEffectiveMaVien(maVien, currentAdmin, out adminQuyen);
            ViewBag.AdminQuyen = adminQuyen;
            ViewBag.SelectedMaVien = effectiveMaVien ?? "";
            ViewBag.StatsMaVien = effectiveMaVien ?? "";
            ViewBag.RequestedMaVien = adminQuyen == 0 ? (maVien ?? "") : (effectiveMaVien ?? "");

            if (!string.IsNullOrWhiteSpace(effectiveMaVien))
            {
                var vien = _db.Viens.FirstOrDefault(v => v.MaVien == effectiveMaVien);
                ViewBag.ScopeLabel = vien != null ? vien.TenVien : effectiveMaVien;
            }
            else if (adminQuyen == 0)
            {
                ViewBag.ScopeLabel = "Tất cả viện";
            }
            else
            {
                ViewBag.ScopeLabel = "Viện của bạn";
            }

            if (adminQuyen == 0)
            {
                ViewBag.Viens = _db.Viens.OrderBy(v => v.TenVien).ToList();
            }

            if (year == 0) year = DateTime.Now.Year;
            ViewBag.Year = year;
            ViewBag.Semester = semester;

            var eventsScope = BuildScopedEventsQuery(adminQuyen, effectiveMaVien);
            var scopedEventIds = eventsScope.Select(e => e.MaEvent);

            ViewBag.TotalEvents = eventsScope.Count();

            var registrationsScope = _db.DangKySuKiens.Where(d => scopedEventIds.Contains(d.MaEvent));
            ViewBag.TotalRegistrations = registrationsScope.Count();

            ViewBag.TotalStudents = registrationsScope
                .Select(d => d.IDSinhVien)
                .Distinct()
                .Count();

            ViewBag.TotalViews = eventsScope.Sum(e => (int?)e.LuotXem) ?? 0;

            int confirmed = registrationsScope.Count(d => d.TrangThai == "Đã xác nhận" || d.TrangThai == "Đã hoàn thành");
            int totalReg = registrationsScope.Count();
            ViewBag.ParticipationRate = totalReg > 0 ? Math.Round(confirmed * 100.0 / totalReg, 1) : 0;

            int[] activeMonths = GetSemesterMonths(semester);

            var monthly = registrationsScope
                .Where(d => d.NgayDangKy.Year == year && activeMonths.Contains(d.NgayDangKy.Month))
                .GroupBy(d => d.NgayDangKy.Month)
                .Select(g => new { Month = g.Key, Count = g.Count() })
                .ToList();

            ViewBag.MonthlyLabels = activeMonths.Select(m => "T" + m).ToArray();
            ViewBag.MonthlyCounts = activeMonths
                .Select(m => monthly.FirstOrDefault(x => x.Month == m)?.Count ?? 0)
                .ToArray();

            var byCategory = eventsScope
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

            var byStatus = eventsScope
                .GroupBy(e => e.TrangThai)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToList();
            ViewBag.ByStatus = byStatus;

            var twelveWeeksAgo = DateTime.Now.AddDays(-84);
            var weeklyRaw = registrationsScope
                .Where(d => d.NgayDangKy >= twelveWeeksAgo)
                .ToList()
                .GroupBy(d => GetWeekNumber(d.NgayDangKy))
                .OrderBy(g => g.Key)
                .Select(g => new { Week = g.Key, Count = g.Count() })
                .ToList();

            ViewBag.WeeklyLabels = weeklyRaw.Select(x => "Tuần " + x.Week).ToArray();
            ViewBag.WeeklyCounts = weeklyRaw.Select(x => x.Count).ToArray();

            ViewBag.ActiveStudents = registrationsScope
                .GroupBy(d => d.IDSinhVien)
                .Count(g => g.Count() >= 5);

            ViewBag.AvgDRL = eventsScope.Average(e => (double?)e.DRL) ?? 0;

            return View();
        }

        public JsonResult GetChartData(int year, string semester = "", string maVien = "")
        {
            var admin = GetCurrentAdmin();
            if (admin == null)
                return Json(new { labels = new string[0], data = new int[0] }, JsonRequestBehavior.AllowGet);

            string effectiveMaVien = ResolveEffectiveMaVien(maVien, admin, out _);
            int adminQuyen = admin.Quyen;
            var scopedEventIds = BuildScopedEventsQuery(adminQuyen, effectiveMaVien).Select(e => e.MaEvent);

            int[] activeMonths = GetSemesterMonths(semester);

            var monthly = _db.DangKySuKiens
                .Where(d => scopedEventIds.Contains(d.MaEvent))
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

        public JsonResult GetTopEvents(string type = "dangky", int year = 0, string maVien = "")
        {
            if (year == 0) year = DateTime.Now.Year;

            var admin = GetCurrentAdmin();
            if (admin == null)
                return Json(new object[0], JsonRequestBehavior.AllowGet);

            string effectiveMaVien = ResolveEffectiveMaVien(maVien, admin, out _);
            int adminQuyen = admin.Quyen;
            var eventsScope = BuildScopedEventsQuery(adminQuyen, effectiveMaVien);

            if (type == "yeuthich")
            {
                var favQuery = _db.SuKienYeuThiches.Where(f => f.EVENT.NgayBatDau.Year == year);
                if (adminQuyen == 1 || adminQuyen == 2)
                {
                    if (string.IsNullOrWhiteSpace(effectiveMaVien))
                        favQuery = favQuery.Where(f => false);
                    else
                        favQuery = favQuery.Where(f => f.EVENT.MaVien == effectiveMaVien);
                }
                else if (!string.IsNullOrWhiteSpace(effectiveMaVien))
                {
                    favQuery = favQuery.Where(f => f.EVENT.MaVien == effectiveMaVien);
                }

                var rows = favQuery
                    .GroupBy(f => new { f.MaEvent, f.EVENT.TenEvent, f.EVENT.TrangThai, TenDanhMuc = f.EVENT.DanhMuc.TenDanhMuc })
                    .Select(g => new {
                        TenEvent = g.Key.TenEvent,
                        TenDanhMuc = g.Key.TenDanhMuc,
                        TrangThai = g.Key.TrangThai,
                        SoYeuThich = g.Count()
                    })
                    .OrderByDescending(x => x.SoYeuThich)
                    .Take(10)
                    .ToList();
                return Json(rows, JsonRequestBehavior.AllowGet);
            }

            if (type == "luotxem")
            {
                var rows = eventsScope
                    .Where(e => e.NgayBatDau.Year == year)
                    .Include("DanhMuc")
                    .OrderByDescending(e => e.LuotXem)
                    .Take(10)
                    .Select(e => new {
                        TenEvent = e.TenEvent,
                        TenDanhMuc = e.DanhMuc.TenDanhMuc,
                        TrangThai = e.TrangThai,
                        LuotXem = e.LuotXem
                    })
                    .ToList();
                return Json(rows, JsonRequestBehavior.AllowGet);
            }

            var defRows = eventsScope
                .Where(e => e.NgayBatDau.Year == year)
                .Include("DanhMuc")
                .OrderByDescending(e => e.SoLuongDaDangKy)
                .Take(10)
                .Select(e => new {
                    TenEvent = e.TenEvent,
                    TenDanhMuc = e.DanhMuc.TenDanhMuc,
                    TrangThai = e.TrangThai,
                    SoLuongDaDangKy = e.SoLuongDaDangKy,
                    SoLuongToiDa = e.SoLuongToiDa
                })
                .ToList();
            return Json(defRows, JsonRequestBehavior.AllowGet);
        }

        private static int GetWeekNumber(DateTime date)
        {
            var cal = System.Globalization.CultureInfo.InvariantCulture.Calendar;
            return cal.GetWeekOfYear(date,
                System.Globalization.CalendarWeekRule.FirstFourDayWeek,
                DayOfWeek.Monday);
        }

    }
}
