using System.Security.Cryptography;
using System.Text;

namespace PersonalCabinetEducationProgram.Services
{
    public static class LegacyPasswordHasher
    {
        public static string Hash(string input)
        {
            var bytes = Encoding.UTF8.GetBytes(input);
            var hashBytes = SHA256.HashData(bytes);
            return Convert.ToHexString(hashBytes).ToLowerInvariant();
        }

        public static bool Verify(string input, string expectedHash)
        {
            return Hash(input) == expectedHash;
        }
    }
}
