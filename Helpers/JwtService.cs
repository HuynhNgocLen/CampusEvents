using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace school_event_management.Models
{
    public static class JwtService
    {
        private const string SecretKey = "CampusEvents_SuperSecretKey_2024!"; // ← đổi thành key mạnh hơn, lưu trong Web.config
        private const string Issuer = "CampusEvents";
        private const string UserTypeClaim = "userType";
        public const int TokenLifetimeMinutes = 30;
        public const int RenewBeforeExpiryMinutes = 5;

        /// <summary>Mã định danh cố định trong JWT cho phiên khách (không có trong bảng SinhVien).</summary>
        public const string GuestUserId = "__guest__";

        public static string GenerateToken(string studentId, string fullName, bool isGuest = false)
        {
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SecretKey));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new List<Claim>
            {
                new Claim("studentId", studentId),
                new Claim("fullName", fullName),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim(UserTypeClaim, isGuest ? "guest" : "student")
            };

            var token = new JwtSecurityToken(
                issuer: Issuer,
                audience: Issuer,
                claims: claims,
                expires: DateTime.Now.AddMinutes(TokenLifetimeMinutes),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        /// <summary>Phiên đăng nhập khách (chỉ xem trang chủ).</summary>
        public static bool IsGuest(System.Web.HttpRequestBase request)
        {
            var principal = GetPrincipalFromRequest(request);
            return principal != null
                   && string.Equals(principal.FindFirst(UserTypeClaim)?.Value, "guest", StringComparison.OrdinalIgnoreCase);
        }

        private static ClaimsPrincipal GetPrincipalFromRequest(System.Web.HttpRequestBase request)
        {
            var cookie = request.Cookies["jwt"];
            if (cookie == null) return null;
            return ValidateToken(cookie.Value);
        }

        /// <summary>
        /// Trả về ClaimsPrincipal nếu token hợp lệ, null nếu không hợp lệ.
        /// </summary>
        public static ClaimsPrincipal ValidateToken(string token)
        {
            try
            {
                var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SecretKey));
                var handler = new JwtSecurityTokenHandler();

                var parameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = key,
                    ValidateIssuer = true,
                    ValidIssuer = Issuer,
                    ValidateAudience = true,
                    ValidAudience = Issuer,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero
                };

                return handler.ValidateToken(token, parameters, out _);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Tự gia hạn token nếu còn hạn và sắp hết hạn để tránh văng phiên khi user đang thao tác.
        /// </summary>
        public static void TryRenewTokenCookie(System.Web.HttpRequestBase request, System.Web.HttpResponseBase response)
        {
            var cookie = request?.Cookies["jwt"];
            if (cookie == null || string.IsNullOrWhiteSpace(cookie.Value)) return;

            var principal = ValidateToken(cookie.Value);
            if (principal == null || !ShouldRenew(principal)) return;

            var studentId = principal.FindFirst("studentId")?.Value;
            var fullName = principal.FindFirst("fullName")?.Value ?? string.Empty;
            var isGuest = string.Equals(
                principal.FindFirst(UserTypeClaim)?.Value,
                "guest",
                StringComparison.OrdinalIgnoreCase);

            if (string.IsNullOrWhiteSpace(studentId)) return;

            var renewedToken = GenerateToken(studentId, fullName, isGuest);
            response.Cookies.Add(new System.Web.HttpCookie("jwt", renewedToken)
            {
                HttpOnly = true,
                Expires = DateTime.Now.AddMinutes(TokenLifetimeMinutes)
            });
        }

        private static bool ShouldRenew(ClaimsPrincipal principal)
        {
            var expClaim = principal?.FindFirst(JwtRegisteredClaimNames.Exp)?.Value;
            if (string.IsNullOrWhiteSpace(expClaim)) return false;

            if (!long.TryParse(expClaim, out var expUnix)) return false;

            var expUtc = DateTimeOffset.FromUnixTimeSeconds(expUnix);
            var remaining = expUtc - DateTimeOffset.UtcNow;
            return remaining > TimeSpan.Zero && remaining <= TimeSpan.FromMinutes(RenewBeforeExpiryMinutes);
        }

        /// <summary>
        /// Lấy studentId từ JWT cookie. Trả về null nếu không hợp lệ.
        /// </summary>
        public static string GetStudentId(System.Web.HttpRequestBase request)
        {
            var principal = GetPrincipalFromRequest(request);
            return principal?.FindFirst("studentId")?.Value;
        }
    }
}