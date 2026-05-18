using System;
using System.IO;
using System.Linq;
using System.Web.Mvc;
using System.Data.Entity;
using ClosedXML.Excel;
using school_event_management.Helpers;
using shcool_event_management.Areas.Admin.Helpers;

namespace shcool_event_management.Areas.Admin.Controllers
{
    public partial class AdminEventsController
    {
        public ActionResult RegistrationDetail(int id,
                                               string search = null,
                                               string status = null,
                                               int page = 1,
                                               int pageSize = 10)
        {
            ViewBag.ActiveMenu = "manage";
            var currentAdmin = GetCurrentAdmin();
            if (currentAdmin == null) return new HttpUnauthorizedResult();

            var ev = _db.EVENTs
                        .Include("DanhMuc")
                        .Include("DiaDiem")
                        .FirstOrDefault(e => e.MaEvent == id);

            if (ev == null) return HttpNotFound();
            var canAccessEvent = currentAdmin.Quyen == 0
                || string.Equals(ev.NguoiDang, currentAdmin.MaQTV, StringComparison.OrdinalIgnoreCase);
            if (!canAccessEvent) return new HttpUnauthorizedResult();

            var query = _db.DangKySuKiens
                           .Include("SinhVien")
                           .Where(d => d.MaEvent == id)
                           .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(d => d.SinhVien.Ten.Contains(search) || d.IDSinhVien.Contains(search));
            }

            query = AdminEventCommonHelper.ApplyRegistrationStatusFilter(query, status);
            int total = query.Count();

            var pageItems = query
                .OrderByDescending(d => d.NgayDangKy)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var allRegs = _db.DangKySuKiens.Where(d => d.MaEvent == id).ToList();
            ViewBag.Event = ev;
            ViewBag.TotalReg = total;
            ViewBag.Registered = allRegs.Count(d => RegistrationStatusHelper.Normalize(d.TrangThai) == "Đã đăng ký");
            ViewBag.Completed = allRegs.Count(d => RegistrationStatusHelper.Normalize(d.TrangThai) == "Đã hoàn thành");
            ViewBag.Cancelled = allRegs.Count(d => RegistrationStatusHelper.Normalize(d.TrangThai) == "Đã hủy");
            ViewBag.Page = page;
            ViewBag.PageSize = pageSize;
            ViewBag.TotalPage = (int)Math.Ceiling(total / (double)pageSize);
            ViewBag.Search = search;
            ViewBag.Status = status;

            return View(pageItems);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CancelRegistration(int maEvent, string idSinhVien)
        {
            var reg = _db.DangKySuKiens.FirstOrDefault(d => d.MaEvent == maEvent && d.IDSinhVien == idSinhVien);
            if (reg == null) return HttpNotFound();

            var ev = _db.EVENTs.FirstOrDefault(x => x.MaEvent == maEvent);
            if (ev == null) return HttpNotFound();
            if (ev.NguoiDang != GetCurrentAdminMaQTV()) return new HttpUnauthorizedResult();

            reg.TrangThai = "Đã hủy";
            if (ev.SoLuongDaDangKy > 0) ev.SoLuongDaDangKy--;
            _db.SaveChanges();

            TempData["Success"] = $"Đã hủy đăng ký cho sinh viên {idSinhVien}.";
            return RedirectToAction("RegistrationDetail", new { id = maEvent });
        }

        public ActionResult ExportExcel(int id, string search = null, string status = null)
        {
            var currentAdmin = GetCurrentAdmin();
            if (currentAdmin == null) return new HttpUnauthorizedResult();

            var ev = _db.EVENTs.Include("DanhMuc").FirstOrDefault(e => e.MaEvent == id);
            if (ev == null) return HttpNotFound();
            var canAccessEvent = currentAdmin.Quyen == 0
                || string.Equals(ev.NguoiDang, currentAdmin.MaQTV, StringComparison.OrdinalIgnoreCase);
            if (!canAccessEvent) return new HttpUnauthorizedResult();

            var registrations = _db.DangKySuKiens
                .Include("SinhVien")
                .Include("SinhVien.MaNghanh1")
                .Where(d => d.MaEvent == id)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                registrations = registrations.Where(d => d.SinhVien.Ten.Contains(search) || d.IDSinhVien.Contains(search));
            }
            registrations = AdminEventCommonHelper.ApplyRegistrationStatusFilter(registrations, status);

            var exportList = registrations
                .ToList()
                .Where(d => RegistrationStatusHelper.MatchStatusWithLegacy(d.TrangThai, status))
                .OrderBy(d => d.SinhVien.Ten)
                .ToList();

            using (var wb = new XLWorkbook())
            {
                var ws = wb.Worksheets.Add("Danh sách đăng ký");
                ws.Cell(1, 1).Value = "DANH SÁCH ĐĂNG KÝ SỰ KIỆN";
                ws.Cell(1, 1).Style.Font.Bold = true;
                ws.Cell(1, 1).Style.Font.FontSize = 14;
                ws.Range(1, 1, 1, 9).Merge();

                ws.Cell(2, 1).Value = "Sự kiện: " + ev.TenEvent;
                ws.Cell(3, 1).Value = "Ngày tổ chức: " + ev.NgayBatDau.ToString("dd/MM/yyyy HH:mm");
                ws.Cell(4, 1).Value = "Tổng đăng ký: " + exportList.Count;
                ws.Range(2, 1, 2, 9).Merge();
                ws.Range(3, 1, 3, 9).Merge();
                ws.Range(4, 1, 4, 9).Merge();
                if (!string.IsNullOrWhiteSpace(search) || !string.IsNullOrWhiteSpace(status))
                {
                    ws.Cell(5, 1).Value = "Bộ lọc: "
                        + (!string.IsNullOrWhiteSpace(search) ? $"Từ khóa \"{search}\"" : "Không")
                        + " | Trạng thái: "
                        + (!string.IsNullOrWhiteSpace(status) ? status : "Tất cả");
                    ws.Range(5, 1, 5, 9).Merge();
                }

                var headers = new[] { "STT", "Mã SV", "Họ và tên", "Lớp", "Ngành", "Email", "Số điện thoại", "Ngày đăng ký", "Trạng thái" };
                int headerRow = (!string.IsNullOrWhiteSpace(search) || !string.IsNullOrWhiteSpace(status)) ? 7 : 6;
                for (int i = 0; i < headers.Length; i++)
                {
                    var cell = ws.Cell(headerRow, i + 1);
                    cell.Value = headers[i];
                    cell.Style.Font.Bold = true;
                    cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#137fec");
                    cell.Style.Font.FontColor = XLColor.White;
                    cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                }

                for (int i = 0; i < exportList.Count; i++)
                {
                    var reg = exportList[i];
                    var sv = reg.SinhVien;
                    int row = headerRow + 1 + i;
                    var normalizedStatus = RegistrationStatusHelper.Normalize(reg.TrangThai);

                    ws.Cell(row, 1).Value = i + 1;
                    ws.Cell(row, 2).Value = sv?.ID ?? string.Empty;
                    ws.Cell(row, 3).Value = sv?.Ten ?? string.Empty;
                    ws.Cell(row, 4).Value = sv?.Lop ?? string.Empty;
                    ws.Cell(row, 5).Value = sv?.MaNghanh1?.TenNghanh ?? sv?.MaNghanh ?? string.Empty;
                    ws.Cell(row, 6).Value = sv?.Email ?? string.Empty;
                    ws.Cell(row, 7).Value = sv?.SoDienThoai ?? string.Empty;
                    ws.Cell(row, 8).Value = reg.NgayDangKy.ToString("dd/MM/yyyy HH:mm");
                    ws.Cell(row, 9).Value = normalizedStatus;

                    if (i % 2 == 1) ws.Range(row, 1, row, 9).Style.Fill.BackgroundColor = XLColor.FromHtml("#f1f5f9");

                    var statusCell = ws.Cell(row, 9);
                    if (normalizedStatus == "Đã hoàn thành") statusCell.Style.Font.FontColor = XLColor.FromHtml("#0f766e");
                    else if (normalizedStatus == "Đã hủy") statusCell.Style.Font.FontColor = XLColor.FromHtml("#dc2626");
                    else statusCell.Style.Font.FontColor = XLColor.FromHtml("#137fec");
                }

                ws.Columns().AdjustToContents();
                ws.Range(headerRow, 1, headerRow + exportList.Count, 9).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                ws.Range(headerRow, 1, headerRow + exportList.Count, 9).Style.Border.InsideBorder = XLBorderStyleValues.Hair;

                var stream = new MemoryStream();
                wb.SaveAs(stream);
                stream.Position = 0;
                var fileName = $"DangKy_{ev.TenEvent.Replace(" ", "_")}_{DateTime.Now:yyyyMMdd}.xlsx";

                if (currentAdmin != null)
                {
                    try
                    {
                        QtvHanhDongLogHelper.Insert(
                            currentAdmin.TenDN,
                            currentAdmin.MaQTV,
                            "GET",
                            "AdminEvents",
                            "ExportExcel",
                            Request?.RawUrl,
                            BuildAuditPrefix(currentAdmin) + " Xuất Excel danh sách sinh viên sự kiện: " + ev.TenEvent);
                    }
                    catch
                    {
                        // Không chặn export nếu ghi log lỗi.
                    }
                }

                return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
            }
        }
    }
}
