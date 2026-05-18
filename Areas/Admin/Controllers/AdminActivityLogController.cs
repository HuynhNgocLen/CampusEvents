using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Web.Mvc;
using shcool_event_management.Areas.Admin.Helpers;
using shcool_event_management.Areas.Admin.Models;
using shcool_event_management.Models;

namespace shcool_event_management.Areas.Admin.Controllers
{
    /// <summary>Nhật ký đăng nhập + hành động theo phân quyền 0/2.</summary>
    [Authorize]
    public class AdminActivityLogController : BaseAdminController
    {
        private const int PageSize = 40;

        public ActionResult Index(int page = 1, int? role = null, string tenDn = null, int? days = null)
        {
            var admin = GetCurrentAdmin();
            if (admin == null || (admin.Quyen != 0 && admin.Quyen != 2))
            {
                TempData["Error"] = "Chỉ tài khoản quyền 0 hoặc 2 mới xem được nhật ký.";
                return RedirectToAction("Dashboard", "AdminDashboard");
            }

            ViewBag.ActiveMenu = "activity-log";
            ViewBag.Title = "Nhật ký & đăng nhập";

            page = Math.Max(1, page);
            var skip = (page - 1) * PageSize;

            try
            {
                var normalizedTenDn = string.IsNullOrWhiteSpace(tenDn) ? null : tenDn.Trim().ToLowerInvariant();
                var roleFilter = role.HasValue && role.Value >= 0 && role.Value <= 2 ? role : null;
                var dayFilter = NormalizeDayFilter(days);
                var fromTime = dayFilter.HasValue ? DateTime.Now.AddDays(-dayFilter.Value) : (DateTime?)null;
                var accountScope = BuildAllowedAccounts(admin);
                var selectedTenDn = accountScope.Any(x => string.Equals(x, normalizedTenDn, StringComparison.OrdinalIgnoreCase))
                    ? normalizedTenDn
                    : null;

                var scopeMaQtv = admin.Quyen == 2 ? ResolveAdminVienCode(admin) : null;
                var actCount = GetActivityCount(scopeMaQtv, roleFilter, selectedTenDn, fromTime);
                var loginCount = GetLoginCount(scopeMaQtv, roleFilter, selectedTenDn, fromTime);
                var activityRows = QueryActivity(skip, PageSize, scopeMaQtv, roleFilter, selectedTenDn, fromTime);
                var loginRows = QueryLogin(skip, PageSize, scopeMaQtv, roleFilter, selectedTenDn, fromTime);

                var totalPages = Math.Max(
                    actCount > 0 ? (int)Math.Ceiling(actCount / (double)PageSize) : 1,
                    loginCount > 0 ? (int)Math.Ceiling(loginCount / (double)PageSize) : 1);
                totalPages = Math.Max(1, totalPages);

                ViewBag.CurrentPage = page;
                ViewBag.TotalPages = totalPages;
                ViewBag.ActivityCount = actCount;
                ViewBag.LoginCount = loginCount;
                ViewBag.ActivityRows = activityRows;
                ViewBag.LoginRows = loginRows;
                ViewBag.LogTableMissing = false;
                ViewBag.LoginTableMissing = false;
                ViewBag.RoleFilter = roleFilter;
                ViewBag.TenDnFilter = selectedTenDn;
                ViewBag.DayFilter = dayFilter;
                ViewBag.FilterableAccounts = accountScope;
                ViewBag.CanFilterRole0 = admin.Quyen == 0;

                return View();
            }
            catch (Exception)
            {
                ViewBag.LogTableMissing = true;
                ViewBag.LoginTableMissing = true;
                ViewBag.ActivityRows = new List<QtvHanhDongLogRow>();
                ViewBag.LoginRows = new List<AdminDangNhapLogRow>();
                ViewBag.CurrentPage = 1;
                ViewBag.TotalPages = 1;
                return View();
            }
        }

        public ActionResult Audit(int page = 1)
        {
            return RedirectToAction("Index", new { page });
        }

        private int GetActivityCount(string scopeMaQtv, int? roleFilter, string tenDnFilter, DateTime? fromTime)
        {
            const string sql = @"
SELECT COUNT(*)
FROM dbo.QTVHanhDongLog
WHERE (@roleFilter IS NULL OR MoTa LIKE '%' + @roleToken + '%')
  AND (@scopeMaQtv IS NULL OR MaQTV = @scopeMaQtv)
  AND (@tenDnFilter IS NULL OR TenDN = @tenDnFilter)
  AND (@fromTime IS NULL OR ThoiGian >= @fromTime)";
            return GetCountWithFilters(sql, scopeMaQtv, roleFilter, tenDnFilter, fromTime);
        }

        private int GetLoginCount(string scopeMaQtv, int? roleFilter, string tenDnFilter, DateTime? fromTime)
        {
            const string sql = @"
SELECT COUNT(*)
FROM dbo.QTVAdminDangNhapLog
WHERE (@roleFilter IS NULL OR Quyen = @roleFilter)
  AND (@scopeMaQtv IS NULL OR MaQTV = @scopeMaQtv)
  AND (@tenDnFilter IS NULL OR TenDN = @tenDnFilter)
  AND (@fromTime IS NULL OR ThoiGian >= @fromTime)";
            return GetCountWithFilters(sql, scopeMaQtv, roleFilter, tenDnFilter, fromTime);
        }

        private List<QtvHanhDongLogRow> QueryActivity(int skip, int take, string scopeMaQtv, int? roleFilter, string tenDnFilter, DateTime? fromTime)
        {
            const string sql = @"
SELECT Id, ThoiGian, TenDN, MaQTV, PhuongThuc, ControllerName, ActionName, DuongDan, MoTa
FROM dbo.QTVHanhDongLog
WHERE (@roleFilter IS NULL OR MoTa LIKE '%' + @roleToken + '%')
  AND (@scopeMaQtv IS NULL OR MaQTV = @scopeMaQtv)
  AND (@tenDnFilter IS NULL OR TenDN = @tenDnFilter)
  AND (@fromTime IS NULL OR ThoiGian >= @fromTime)
ORDER BY ThoiGian DESC, Id DESC
OFFSET @skip ROWS FETCH NEXT @take ROWS ONLY";
            return ReadActivityRows(sql, skip, take, scopeMaQtv, roleFilter, tenDnFilter, fromTime);
        }

        private List<QtvHanhDongLogRow> ReadActivityRows(string sql, int skip, int take, string scopeMaQtv, int? roleFilter, string tenDnFilter, DateTime? fromTime)
        {
            var cs = QtvHanhDongLogHelper.TryGetSqlConnectionString();
            if (string.IsNullOrEmpty(cs))
                throw new InvalidOperationException("Thiếu kết nối SQL.");

            var list = new List<QtvHanhDongLogRow>();
            using (var conn = new SqlConnection(cs))
            using (var cmd = new SqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@skip", skip);
                cmd.Parameters.AddWithValue("@take", take);
                cmd.Parameters.AddWithValue("@roleFilter", roleFilter.HasValue ? (object)roleFilter.Value : DBNull.Value);
                cmd.Parameters.AddWithValue("@roleToken", roleFilter.HasValue ? ("quyền " + roleFilter.Value) : (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@scopeMaQtv", (object)scopeMaQtv ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@tenDnFilter", (object)tenDnFilter ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@fromTime", fromTime.HasValue ? (object)fromTime.Value : DBNull.Value);
                conn.Open();
                using (var r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        var account = r.IsDBNull(2) ? null : r.GetString(2);
                        list.Add(new QtvHanhDongLogRow
                        {
                            Id = r.GetInt64(0),
                            ThoiGian = r.GetDateTime(1),
                            TenDN = account,
                            MaQTV = r.IsDBNull(3) ? null : r.GetString(3),
                            PhuongThuc = r.IsDBNull(4) ? null : r.GetString(4),
                            ControllerName = r.IsDBNull(5) ? null : r.GetString(5),
                            ActionName = r.IsDBNull(6) ? null : r.GetString(6),
                            DuongDan = r.IsDBNull(7) ? null : r.GetString(7),
                            MoTa = r.IsDBNull(8) ? null : r.GetString(8)
                        });
                    }
                }
            }

            return list;
        }

        private List<AdminDangNhapLogRow> QueryLogin(int skip, int take, string scopeMaQtv, int? roleFilter, string tenDnFilter, DateTime? fromTime)
        {
            var cs = QtvHanhDongLogHelper.TryGetSqlConnectionString();
            if (string.IsNullOrEmpty(cs))
                throw new InvalidOperationException("Thiếu kết nối SQL.");

            const string sql = @"
SELECT Id, ThoiGian, TenDN, MaQTV, Quyen, DiaChiIP, ThietBi
FROM dbo.QTVAdminDangNhapLog
WHERE (@roleFilter IS NULL OR Quyen = @roleFilter)
  AND (@scopeMaQtv IS NULL OR MaQTV = @scopeMaQtv)
  AND (@tenDnFilter IS NULL OR TenDN = @tenDnFilter)
  AND (@fromTime IS NULL OR ThoiGian >= @fromTime)
ORDER BY ThoiGian DESC, Id DESC
OFFSET @skip ROWS FETCH NEXT @take ROWS ONLY";

            var list = new List<AdminDangNhapLogRow>();
            using (var conn = new SqlConnection(cs))
            using (var cmd = new SqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@skip", skip);
                cmd.Parameters.AddWithValue("@take", take);
                cmd.Parameters.AddWithValue("@roleFilter", roleFilter.HasValue ? (object)roleFilter.Value : DBNull.Value);
                cmd.Parameters.AddWithValue("@scopeMaQtv", (object)scopeMaQtv ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@tenDnFilter", (object)tenDnFilter ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@fromTime", fromTime.HasValue ? (object)fromTime.Value : DBNull.Value);
                conn.Open();
                using (var r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        var account = r.IsDBNull(2) ? null : r.GetString(2);
                        list.Add(new AdminDangNhapLogRow
                        {
                            Id = r.GetInt64(0),
                            ThoiGian = r.GetDateTime(1),
                            TenDN = account,
                            MaQTV = r.IsDBNull(3) ? null : r.GetString(3),
                            Quyen = r.IsDBNull(4) ? (int?)null : r.GetInt32(4),
                            DiaChiIP = r.IsDBNull(5) ? null : r.GetString(5),
                            ThietBi = r.IsDBNull(6) ? null : r.GetString(6)
                        });
                    }
                }
            }

            return list;
        }

        private int GetCountWithFilters(string sql, string scopeMaQtv, int? roleFilter, string tenDnFilter, DateTime? fromTime)
        {
            var cs = QtvHanhDongLogHelper.TryGetSqlConnectionString();
            if (string.IsNullOrEmpty(cs))
                throw new InvalidOperationException("Thiếu kết nối SQL.");

            using (var conn = new SqlConnection(cs))
            using (var cmd = new SqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@roleFilter", roleFilter.HasValue ? (object)roleFilter.Value : DBNull.Value);
                cmd.Parameters.AddWithValue("@roleToken", roleFilter.HasValue ? ("quyền " + roleFilter.Value) : (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@scopeMaQtv", (object)scopeMaQtv ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@tenDnFilter", (object)tenDnFilter ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@fromTime", fromTime.HasValue ? (object)fromTime.Value : DBNull.Value);
                conn.Open();
                return Convert.ToInt32(cmd.ExecuteScalar());
            }
        }

        private static int? NormalizeDayFilter(int? days)
        {
            if (!days.HasValue) return null;
            if (days.Value == 1 || days.Value == 7 || days.Value == 30) return days.Value;
            return null;
        }

        private List<string> BuildAllowedAccounts(QuanTriVien currentAdmin)
        {
            if (currentAdmin == null)
            {
                return new List<string>();
            }
            if (currentAdmin.Quyen == 0)
            {
                return _db.QuanTriViens.Select(x => x.TenDN).ToList();
            }
            var maVien = ResolveAdminVienCode(currentAdmin);
            return _db.QuanTriViens
                .Where(x => x.MaQTV == maVien)
                .Select(x => x.TenDN)
                .ToList();
        }

    }
}
