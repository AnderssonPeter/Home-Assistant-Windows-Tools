using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Web;

namespace IISRPWA
{
    public class HashHelper
    {
        /// <summary>
        /// Compare all values even if difference is found, this is done to stop timing attacks.
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns></returns>
        private static bool SlowEquals(byte[] a, byte[] b)
        {
            var diff = (uint)a.Length ^ (uint)b.Length;
            for (int i = 0; i < a.Length && i < b.Length; i++)
            {
                diff |= (uint)(a[i] ^ b[i]);
            }
            return diff == 0;
        }

        public static byte[] CalculateHash(byte[] passwordBytes, byte[] salt, int iterations)
        {
            using (var algoritm = SHA512.Create())
            {
                var hash = algoritm.ComputeHash(Combine(passwordBytes, salt));
                for (var i = 1; i < iterations; i++)
                {
                    hash = algoritm.ComputeHash(Combine(hash, salt));
                }
                return hash;
            }
        }
        public static byte[] Combine(byte[] first, byte[] second)
        {
            byte[] ret = new byte[first.Length + second.Length];
            Buffer.BlockCopy(first, 0, ret, 0, first.Length);
            Buffer.BlockCopy(second, 0, ret, first.Length, second.Length);
            return ret;
        }

        public static bool VerifyPassword(string username, string password, byte[] passwordHash, byte[] passwordSalt, int iterations)
        {
            var passwordBytes = Encoding.UTF8.GetBytes(password);
            var hash = CalculateHash(passwordBytes, passwordSalt, iterations);
            return SlowEquals(hash, passwordHash);
        }

#if !WEB
        public static byte[] GenerateSalt(int size)
        {
            var salt = new byte[size];
            using (var randomProvider = RNGCryptoServiceProvider.Create())
            {
                randomProvider.GetBytes(salt);
            }
            return salt;
        }
#endif
    }
}