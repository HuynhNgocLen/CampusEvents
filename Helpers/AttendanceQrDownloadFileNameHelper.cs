using System.IO;
using System.Text.RegularExpressions;

namespace school_event_management.Helpers
{
    /// <summary>
    /// Quy tắc tên file ảnh QR điểm danh: Điểm Danh - (tên sự kiện)-{MaEvent}.png
    /// </summary>
    public static class AttendanceQrDownloadFileNameHelper
    {
        private const int MaxEventNameLength = 100;

        /// <summary>
        /// Tên file an toàn cho Windows (bỏ ký tự cấm, gom khoảng trắng, giới hạn độ dài tên sự kiện).
        /// </summary>
        public static string Build(int maEvent, string tenEvent)
        {
            var name = (tenEvent ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(name))
                name = "SuKien";

            foreach (var c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');

            name = Regex.Replace(name, @"\s+", " ");
            if (name.Length > MaxEventNameLength)
                name = name.Substring(0, MaxEventNameLength).Trim();

            return "Điểm Danh - (" + name + ")-" + maEvent + ".png";
        }
    }
}
