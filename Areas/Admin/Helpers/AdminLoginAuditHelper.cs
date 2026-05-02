using System;
using System.Data.SqlClient;

namespace shcool_event_management.Areas.Admin.Helpers
{
    public static class AdminLoginAuditHelper
    {
        public static void Log(string tenDN, string maQtv, int? quyen, string ip, string userAgent)
        {
            var cs = QtvHanhDongLogHelper.TryGetSqlConnectionString();
            if (string.IsNullOrEmpty(cs))
                return;
            if (!QtvHanhDongLogHelper.IsLoggingEnabled())
            {
                QtvHanhDongLogHelper.CleanupExpiredLogs();
                return;
            }

            if (string.IsNullOrEmpty(tenDN))
                return;

            const string sql = @"
INSERT INTO dbo.QTVAdminDangNhapLog (TenDN, MaQTV, Quyen, DiaChiIP, ThietBi)
VALUES (@tenDN, @maQTV, @quyen, @ip, @ua)";

            try
            {
                using (var conn = new SqlConnection(cs))
                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@tenDN", tenDN);
                    cmd.Parameters.AddWithValue("@maQTV", (object)maQtv ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@quyen", quyen.HasValue ? (object)quyen.Value : DBNull.Value);
                    cmd.Parameters.AddWithValue("@ip", (object)Truncate(ip, 45) ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@ua", (object)Truncate(userAgent, 512) ?? DBNull.Value);
                    conn.Open();
                    cmd.ExecuteNonQuery();
                }
            }
            catch
            {
                // bảng chưa tạo hoặc lỗi — không chặn đăng nhập
            }

            QtvHanhDongLogHelper.CleanupExpiredLogs();
        }

        private static string Truncate(string s, int max)
        {
            if (string.IsNullOrEmpty(s)) return s;
            return s.Length <= max ? s : s.Substring(0, max);
        }
    }
}
