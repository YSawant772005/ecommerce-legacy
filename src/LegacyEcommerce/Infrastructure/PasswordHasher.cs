using System;
using System.Security.Cryptography;

namespace LegacyEcommerce.Infrastructure
{
    public class PasswordHash
    {
        public string Hash { get; set; }
        public string Salt { get; set; }
    }

    public static class PasswordHasher
    {
        private const int Iterations = 120000;

        public static PasswordHash Hash(string password)
        {
            var saltBytes = new byte[16];
            using (var rng = new RNGCryptoServiceProvider())
            {
                rng.GetBytes(saltBytes);
            }
            var salt = Convert.ToBase64String(saltBytes);
            return new PasswordHash
            {
                Salt = salt,
                Hash = Compute(password, salt)
            };
        }

        public static bool Verify(string password, string salt, string expectedHash)
        {
            if (string.IsNullOrEmpty(salt) || string.IsNullOrEmpty(expectedHash)) return false;
            string actual;
            try
            {
                actual = Compute(password, salt);
            }
            catch
            {
                return false;
            }
            return string.Equals(actual, expectedHash, StringComparison.Ordinal);
        }

        private static string Compute(string password, string salt)
        {
            using (var pbkdf2 = new Rfc2898DeriveBytes(password, Convert.FromBase64String(salt), Iterations))
            {
                return Convert.ToBase64String(pbkdf2.GetBytes(32));
            }
        }
    }
}
