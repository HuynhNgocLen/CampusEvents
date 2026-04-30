using System;
using System.IO;
using System.Linq;
using System.Web.Mvc;
using ClosedXML.Excel;
using System.Data.Entity;

namespace shcool_event_management.Areas.Admin.Controllers
{
    public partial class AdminStatsController
    {
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

                var h1 = new[] { "STT", "Tên sự kiện", "Danh mục", "Viện", "Địa điểm", "Ngày tổ chức", "Đăng ký", "Tối đa", "Tỷ lệ (%)", "Trạng thái" };
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
                    int pct = ev.SoLuongToiDa > 0 ? (int)Math.Round(ev.SoLuongDaDangKy * 100.0 / ev.SoLuongToiDa) : 0;

                    ws1.Cell(row, 1).Value = i + 1;
                    ws1.Cell(row, 2).Value = ev.TenEvent;
                    ws1.Cell(row, 3).Value = ev.DanhMuc?.TenDanhMuc ?? string.Empty;
                    ws1.Cell(row, 4).Value = ev.MaVien;
                    ws1.Cell(row, 5).Value = ev.DiaDiem?.TenDiaDiem ?? string.Empty;
                    ws1.Cell(row, 6).Value = ev.NgayBatDau.ToString("dd/MM/yyyy");
                    ws1.Cell(row, 7).Value = ev.SoLuongDaDangKy;
                    ws1.Cell(row, 8).Value = ev.SoLuongToiDa;
                    ws1.Cell(row, 9).Value = pct;
                    ws1.Cell(row, 10).Value = ev.TrangThai;

                    if (i % 2 == 1) ws1.Range(row, 1, row, 10).Style.Fill.BackgroundColor = XLColor.FromHtml("#f1f5f9");
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

                var catGroups = events.Where(e => e.DanhMuc != null).GroupBy(e => e.DanhMuc.TenDanhMuc).ToList();
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

                return File(
                    stream.ToArray(),
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    $"BaoCao_SuKien_{year}_{(string.IsNullOrEmpty(semester) ? "CaNam" : semester.ToUpper())}_{DateTime.Now:yyyyMMdd}.xlsx");
            }
        }
    }
}
