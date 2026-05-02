using Isopoh.Cryptography.Argon2;
using System;
using System.Security.Cryptography;
using System.Text;

namespace school_event_management.Helpers
{
    public static class PasswordHasher
    {
        public static string HashPassword(string password)
        {
            if (string.IsNullOrWhiteSpace(password))
                throw new ArgumentException("Password is required.", nameof(password));

            var config = new Argon2Config
            {
                // Isopoh enum uses HybridAddressing for Argon2id.
                Type = Argon2Type.HybridAddressing,
                Version = Argon2Version.Nineteen,
                TimeCost = 3,
                MemoryCost = 65536,
                Lanes = 4,
                Threads = 2,
                HashLength = 32,
                Password = Encoding.UTF8.GetBytes(password),
                Salt = GenerateSalt(16)
            };

            return Argon2.Hash(config);
        }

        public static bool Verify(string password, string storedHash)
        {
            if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(storedHash))
                return false;

            if (IsArgon2Hash(storedHash))
                return Argon2.Verify(storedHash, password);

            if (LooksLikeSha256Hex(storedHash))
                return string.Equals(ComputeSha256Hex(password), storedHash, StringComparison.OrdinalIgnoreCase);

            return string.Equals(password, storedHash, StringComparison.Ordinal);
        }

        public static bool NeedsUpgrade(string storedHash)
        {
            return !IsArgon2Hash(storedHash);
        }

        private static bool IsArgon2Hash(string hash)
        {
            return hash.StartsWith("$argon2id$", StringComparison.OrdinalIgnoreCase)
                || hash.StartsWith("$argon2i$", StringComparison.OrdinalIgnoreCase)
                || hash.StartsWith("$argon2d$", StringComparison.OrdinalIgnoreCase);
        }

        private static bool LooksLikeSha256Hex(string hash)
        {
            if (hash.Length != 64) return false;
            for (int i = 0; i < hash.Length; i++)
            {
                char c = hash[i];
                bool isHex = (c >= '0' && c <= '9')
                    || (c >= 'a' && c <= 'f')
                    || (c >= 'A' && c <= 'F');
                if (!isHex) return false;
            }
            return true;
        }

        private static string ComputeSha256Hex(string value)
        {
            using (var sha = SHA256.Create())
            {
                var bytes = Encoding.UTF8.GetBytes(value);
                var hash = sha.ComputeHash(bytes);
                return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            }
        }

        private static byte[] GenerateSalt(int size)
        {
            var salt = new byte[size];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(salt);
            }
            return salt;
        }
    }
}
