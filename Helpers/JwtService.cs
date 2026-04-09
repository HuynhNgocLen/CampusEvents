using Microsoft.IdentityModel.Tokens;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace school_event_management.Models
{
    public static class JwtService
    {
        private const string SecretKey = "CampusEvents_SuperSecretKey_2024!"; // ← đổi thành key mạnh hơn, lưu trong Web.config
        private const string Issuer = "CampusEvents";

        public static string GenerateToken(string studentId, string fullName)
        {
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SecretKey));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim("studentId", studentId),
                new Claim("fullName", fullName),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            var token = new JwtSecurityToken(
                issuer: Issuer,
                audience: Issuer,
                claims: claims,
                expires: DateTime.Now.AddMinutes(30),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
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
        /// Lấy studentId từ JWT cookie. Trả về null nếu không hợp lệ.
        /// </summary>
        public static string GetStudentId(System.Web.HttpRequestBase request)
        {
            var cookie = request.Cookies["jwt"];
            if (cookie == null) return null;

            var principal = ValidateToken(cookie.Value);
            return principal?.FindFirst("studentId")?.Value;
        }
    }
}