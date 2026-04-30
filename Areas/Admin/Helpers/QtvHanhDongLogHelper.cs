using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Text.RegularExpressions;
using System.Web;

namespace shcool_event_management.Areas.Admin.Helpers
{
    /// <summary>Ghi nhật ký hành động vào bảng QTVHanhDongLog (script Scripts/QTVHanhDongLog.sql).</summary>
    public static class QtvHanhDongLogHelper
    {
        private const int DefaultRetentionDays = 7;
        private const string RetentionSettingKey = "AdminLogRetentionDays";
        private static readonly HashSet<int> AllowedRetentionDays = new HashSet<int> { 0, 1, 3, 7, 14, 30, 90 };

        /// <summary>Chuỗi kết nối SQL thuần (lấy từ EF connection string).</summary>
        public static string TryGetSqlConnectionString()
        {
            return GetProviderConnectionString();
        }

        public static bool IsLoggingEnabled()
        {
            return GetLogRetentionDays() > 0;
        }

        public static int GetLogRetentionDays()
        {
            var sessionValue = HttpContext.Current?.Session?[RetentionSettingKey];
            if (sessionValue != null)
            {
                var parsedSession = ParseRetentionDays(sessionValue.ToString());
                if (parsedSession.HasValue)
                    return parsedSession.Value;
            }

            var appSetting = ConfigurationManager.AppSettings[RetentionSettingKey];
            var parsedSetting = ParseRetentionDays(appSetting);
            return parsedSetting ?? DefaultRetentionDays;
        }

        public static void Insert(
            string tenDN,
            string maQtv,
            string phuongThuc,
            string controllerName,
            string actionName,
            string duongDan,
            string moTa)
        {
            var cs = TryGetSqlConnectionString();
            if (string.IsNullOrEmpty(cs))
                return;
            if (!IsLoggingEnabled())
            {
                CleanupExpiredLogs(cs, 0);
                return;
            }

            const string sql = @"
INSERT INTO dbo.QTVHanhDongLog (TenDN, MaQTV, PhuongThuc, ControllerName, ActionName, DuongDan, MoTa)
VALUES (@tenDN, @maQTV, @phuongThuc, @controllerName, @actionName, @duongDan, @moTa)";

            using (var conn = new SqlConnection(cs))
            using (var cmd = new SqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@tenDN", tenDN ?? "");
                cmd.Parameters.AddWithValue("@maQTV", (object)maQtv ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@phuongThuc", phuongThuc ?? "");
                cmd.Parameters.AddWithValue("@controllerName", controllerName ?? "");
                cmd.Parameters.AddWithValue("@actionName", actionName ?? "");
                cmd.Parameters.AddWithValue("@duongDan", (object)duongDan ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@moTa", (object)moTa ?? DBNull.Value);
                conn.Open();
                cmd.ExecuteNonQuery();
            }

            CleanupExpiredLogs(cs, GetLogRetentionDays());
        }

        public static void CleanupExpiredLogs()
        {
            var cs = TryGetSqlConnectionString();
            if (string.IsNullOrEmpty(cs))
                return;
            CleanupExpiredLogs(cs, GetLogRetentionDays());
        }

        private static string GetProviderConnectionString()
        {
            try
            {
                var entry = ConfigurationManager.ConnectionStrings["school_event_managementEntities"];
                if (entry == null || string.IsNullOrEmpty(entry.ConnectionString))
                    return null;

                var raw = entry.ConnectionString;
                if (raw.IndexOf("metadata=", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    var parsed = ParseProviderConnectionString(raw);
                    return string.IsNullOrEmpty(parsed) ? raw : parsed;
                }

                return raw;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>Trích provider connection string từ chuỗi Entity Framework (metadata=...).</summary>
        private static string ParseProviderConnectionString(string entityConnectionString)
        {
            if (string.IsNullOrEmpty(entityConnectionString))
                return null;

            var m = Regex.Match(
                entityConnectionString,
                @"provider\s+connection\s+string\s*=\s*""(?<p>(?:[^""]|"""")*)""",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (!m.Success)
                return null;

            return m.Groups["p"].Value.Replace("\"\"", "\"");
        }

        private static int? ParseRetentionDays(string raw)
        {
            int value;
            if (!int.TryParse(raw, out value))
                return null;
            return AllowedRetentionDays.Contains(value) ? (int?)value : null;
        }

        private static void CleanupExpiredLogs(string cs, int retentionDays)
        {
            var deleteAll = retentionDays <= 0;
            var threshold = DateTime.Now.AddDays(-retentionDays);
            const string deleteSql = @"
DELETE FROM dbo.QTVHanhDongLog WHERE (@deleteAll = 1 OR ThoiGian < @threshold);
DELETE FROM dbo.QTVAdminDangNhapLog WHERE (@deleteAll = 1 OR ThoiGian < @threshold);";

            try
            {
                using (var conn = new SqlConnection(cs))
                using (var cmd = new SqlCommand(deleteSql, conn))
                {
                    cmd.Parameters.AddWithValue("@deleteAll", deleteAll ? 1 : 0);
                    cmd.Parameters.AddWithValue("@threshold", threshold);
                    conn.Open();
                    cmd.ExecuteNonQuery();
                }
            }
            catch
            {
                // Không chặn nghiệp vụ chính nếu cleanup lỗi.
            }
        }
    }
}
