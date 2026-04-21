using System;
using System.Linq;
using System.Web.Mvc;
using ClosedXML.Excel;
using System.IO;
using shcool_event_management.Models;
using System.Data.Entity;

namespace shcool_event_management.Areas.Admin.Controllers
{
    // [Authorize(Roles = "Admin")]
    public class AdminStatsController : Controller
    {
        private readonly school_event_managementEntities _db
            = new school_event_managementEntities();


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

            int confirmed = _db.DangKySuKiens.Count(d => d.TrangThai == "Đã xác nhận");
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

        public ActionResult ExportReport(int year = 0, string semester = "")
        {
            if (year == 0) year = DateTime.Now.Year;

            int[] activeMonths = GetSemesterMonths(semester);
            string semLabel = semester == "hk1" ? "Học kỳ 1 (T8-T12)"
                            : semester == "hk2" ? "Học kỳ 2 (T1-T5)"
                            : semester == "hk3" ? "Học kỳ 3 (T6-T7)"
                            : "Cả năm";

            var events = _db.EVENTs
                .Include("DanhMuc")
                .Include("DiaDiem")
                .Where(e => e.NgayBatDau.Year == year && activeMonths.Contains(e.NgayBatDau.Month))
                .OrderByDescending(e => e.NgayBatDau)
                .ToList();

            using (var wb = new XLWorkbook())
            {
                var ws1 = wb.Worksheets.Add("Danh sách sự kiện");

                ws1.Cell(1, 1).Value = $"BÁO CÁO SỰ KIỆN {year} — {semLabel}";
                ws1.Cell(1, 1).Style.Font.Bold = true;
                ws1.Cell(1, 1).Style.Font.FontSize = 14;
                ws1.Range(1, 1, 1, 10).Merge();

                ws1.Cell(2, 1).Value = $"Kỳ: {semLabel} | Xuất lúc: {DateTime.Now:dd/MM/yyyy HH:mm}";
                ws1.Range(2, 1, 2, 10).Merge();

                var h1 = new[] { "STT","Tên sự kiện","Danh mục","Viện","Địa điểm",
                                 "Ngày tổ chức","Đăng ký","Tối đa","Tỷ lệ (%)","Trạng thái" };
                for (int i = 0; i < h1.Length; i++)
                {
                    var c = ws1.Cell(4, i + 1);
                    c.Value = h1[i];
                    c.Style.Font.Bold = true;
                    c.Style.Fill.BackgroundColor = XLColor.FromHtml("#137fec");
                    c.Style.Font.FontColor = XLColor.White;
                }

                for (int i = 0; i < events.Count; i++)
                {
                    var ev = events[i];
                    int row = 5 + i;
                    int pct = ev.SoLuongToiDa > 0
                              ? (int)Math.Round(ev.SoLuongDaDangKy * 100.0 / ev.SoLuongToiDa)
                              : 0;

                    ws1.Cell(row, 1).Value = i + 1;
                    ws1.Cell(row, 2).Value = ev.TenEvent;
                    ws1.Cell(row, 3).Value = ev.DanhMuc?.TenDanhMuc ?? "";
                    ws1.Cell(row, 4).Value = ev.MaVien;
                    ws1.Cell(row, 5).Value = ev.DiaDiem?.TenDiaDiem ?? "";
                    ws1.Cell(row, 6).Value = ev.NgayBatDau.ToString("dd/MM/yyyy");
                    ws1.Cell(row, 7).Value = ev.SoLuongDaDangKy;
                    ws1.Cell(row, 8).Value = ev.SoLuongToiDa;
                    ws1.Cell(row, 9).Value = pct;
                    ws1.Cell(row, 10).Value = ev.TrangThai;

                    if (i % 2 == 1)
                        ws1.Range(row, 1, row, 10).Style.Fill.BackgroundColor = XLColor.FromHtml("#f1f5f9");
                }
                ws1.Columns().AdjustToContents();

                var ws2 = wb.Worksheets.Add("Theo danh mục");
                ws2.Cell(1, 1).Value = "THỐNG KÊ THEO DANH MỤC";
                ws2.Cell(1, 1).Style.Font.Bold = true;
                ws2.Range(1, 1, 1, 5).Merge();

                var h2 = new[] { "Danh mục", "Số sự kiện", "Tổng đăng ký", "TB đăng ký/SK", "% tổng" };
                int totalAllReg = events.Sum(e => e.SoLuongDaDangKy);
                for (int i = 0; i < h2.Length; i++)
                {
                    var c = ws2.Cell(3, i + 1);
                    c.Value = h2[i];
                    c.Style.Font.Bold = true;
                    c.Style.Fill.BackgroundColor = XLColor.FromHtml("#137fec");
                    c.Style.Font.FontColor = XLColor.White;
                }

                var catGroups = events.Where(e => e.DanhMuc != null)
                    .GroupBy(e => e.DanhMuc.TenDanhMuc).ToList();

                for (int i = 0; i < catGroups.Count; i++)
                {
                    var g = catGroups[i];
                    int row = 4 + i;
                    int cnt = g.Sum(e => e.SoLuongDaDangKy);
                    ws2.Cell(row, 1).Value = g.Key;
                    ws2.Cell(row, 2).Value = g.Count();
                    ws2.Cell(row, 3).Value = cnt;
                    ws2.Cell(row, 4).Value = g.Count() > 0 ? Math.Round(cnt * 1.0 / g.Count(), 1) : 0;
                    ws2.Cell(row, 5).Value = totalAllReg > 0 ? Math.Round(cnt * 100.0 / totalAllReg, 1) : 0;
                }
                ws2.Columns().AdjustToContents();

                var stream = new MemoryStream();
                wb.SaveAs(stream);
                stream.Position = 0;

                return File(stream.ToArray(),
                            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                            $"BaoCao_SuKien_{year}_{(string.IsNullOrEmpty(semester) ? "CaNam" : semester.ToUpper())}_{DateTime.Now:yyyyMMdd}.xlsx");
            }
        }

        // ── Helper ───────────────────────────────────────────────
        private static int GetWeekNumber(DateTime date)
        {
            var cal = System.Globalization.CultureInfo.InvariantCulture.Calendar;
            return cal.GetWeekOfYear(date,
                System.Globalization.CalendarWeekRule.FirstFourDayWeek,
                DayOfWeek.Monday);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _db.Dispose();
            base.Dispose(disposing);
        }
    }
}