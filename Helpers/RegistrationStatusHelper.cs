using System;

namespace school_event_management.Helpers
{
    /// <summary>
    /// Chuẩn hóa trạng thái đăng ký. Đọc vẫn hỗ trợ legacy (Chờ xác nhận / Đã xác nhận); ghi DB dùng chuỗi nghiệp vụ mới.
    /// </summary>
    public static class RegistrationStatusHelper
    {
        public static string Normalize(string rawStatus)
        {
            var status = (rawStatus ?? string.Empty).Trim();
            if (status == "Đã hoàn thành" || status == "Đã hủy" || status == "Đã đăng ký")
                return status;

            if (status == "Đã xác nhận")
                return "Đã hoàn thành";
            if (status == "Chờ xác nhận")
                return "Đã đăng ký";

            return "Đã đăng ký";
        }

        public static bool MatchStatusWithLegacy(string rawStatus, string selectedStatus)
        {
            if (string.IsNullOrWhiteSpace(selectedStatus))
                return true;

            var normalized = Normalize(rawStatus);
            return string.Equals(normalized, selectedStatus, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Giá trị lưu xuống CSDL (đồng bộ với admin / sinh viên đăng ký).</summary>
        public static string ToStoredRegistrationStatus(string normalizedStatus)
        {
            var status = (normalizedStatus ?? string.Empty).Trim();
            if (status == "Đã hoàn thành" || status == "Đã hủy" || status == "Đã đăng ký")
                return status;
            return "Đã đăng ký";
        }
    }
}
