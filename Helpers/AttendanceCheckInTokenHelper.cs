using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Web;

namespace school_event_management.Helpers
{
    /// <summary>
    /// Chữ ký URL điểm danh (QR): HMAC theo MaEvent + cửa sổ thời gian; token đổi mỗi 1 phút để hạn chế chia sẻ link tĩnh.
    /// </summary>
    public static class AttendanceCheckInTokenHelper
    {
        /// <summary>Độ dài cửa sổ token (giây). Đổi token / QR theo chu kỳ này.</summary>
        public const int TokenWindowSeconds = 60;

        private const string PayloadPrefix = "chk:";

        public static long GetWindowStartUnixSeconds(DateTime utcNow)
        {
            if (utcNow.Kind != DateTimeKind.Utc)
                utcNow = utcNow.ToUniversalTime();

            var epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var total = (long)Math.Floor((utcNow - epoch).TotalSeconds);
            return total - total % TokenWindowSeconds;
        }

        /// <summary>Thời điểm UTC mà token hiện tại hết hiệu lực (cuối cửa sổ).</summary>
        public static DateTime GetCurrentTokenExpiresAtUtc(DateTime? utcNow = null)
        {
            var w = GetWindowStartUnixSeconds(utcNow ?? DateTime.UtcNow);
            return DateTimeOffset.FromUnixTimeSeconds(w + TokenWindowSeconds).UtcDateTime;
        }

        private static byte[] ComputeHmacBytes(int maEvent, string secret, long windowStartUnix)
        {
            var payload = PayloadPrefix
                          + maEvent.ToString(CultureInfo.InvariantCulture)
                          + ":"
                          + windowStartUnix.ToString(CultureInfo.InvariantCulture);
            var keyBytes = Encoding.UTF8.GetBytes(secret ?? string.Empty);
            using (var hmac = new HMACSHA256(keyBytes))
                return hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        }

        public static string CreateToken(int maEvent, string secret, DateTime? utcNow = null)
        {
            var window = GetWindowStartUnixSeconds(utcNow ?? DateTime.UtcNow);
            var hash = ComputeHmacBytes(maEvent, secret, window);
            return HttpServerUtility.UrlTokenEncode(hash);
        }

        /// <summary>Kiểm token: cửa sổ hiện tại hoặc cửa sổ trước (tránh lệch giây khi đổi chu kỳ).</summary>
        public static bool ValidateToken(int maEvent, string token, string secret, DateTime? utcNow = null)
        {
            if (string.IsNullOrWhiteSpace(token)) return false;

            byte[] decoded;
            try
            {
                decoded = HttpServerUtility.UrlTokenDecode(token.Trim());
            }
            catch
            {
                return false;
            }

            if (decoded == null || decoded.Length == 0) return false;

            var now = utcNow ?? DateTime.UtcNow;
            var w0 = GetWindowStartUnixSeconds(now);
            var w1 = w0 - TokenWindowSeconds;

            return ConstantTimeEquals(decoded, ComputeHmacBytes(maEvent, secret, w0))
                   || ConstantTimeEquals(decoded, ComputeHmacBytes(maEvent, secret, w1));
        }

        private static bool ConstantTimeEquals(byte[] a, byte[] b)
        {
            if (a == null || b == null || a.Length != b.Length) return false;
            var diff = 0;
            for (var i = 0; i < a.Length; i++)
                diff |= a[i] ^ b[i];
            return diff == 0;
        }
    }
}
