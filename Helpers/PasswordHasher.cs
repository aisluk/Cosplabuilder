using System.Security.Cryptography;
using System.Text;

namespace Cosplabuilder.Helpers
{
    /// <summary>
    /// Standalone password hashing utility using multi-stage salted PBKDF2-SHA256
    /// combined with iterative XOR-rotated SHA-256 block mixing.
    /// Kept in an isolated file so the hashing algorithm or pepper can be swapped
    /// or excluded from public repository uploads easily.
    /// </summary>
    public static class PasswordHasher
    {
        private const int SaltSize = 16; // 128-bit cryptographic salt
        private const int KeySize = 32;  // 256-bit derived subkey
        private const int DefaultIterations = 12000;
        private const int MixingRounds = 64;
        private const string VersionPrefix = "cbv1";
        private static readonly byte[] InternalPepper = Encoding.UTF8.GetBytes("Cosplanuotojas-LT-SaltPepper-2026#!");

        /// <summary>
        /// Hashes a plaintext password into a formatted string: cbv1$iterations$base64Salt$base64Hash
        /// Supports full UTF-8 including Lithuanian characters (ąčęėįšųūž).
        /// </summary>
        public static string HashPassword(string password)
        {
            if (string.IsNullOrEmpty(password))
            {
                throw new ArgumentException("Password cannot be null or empty.", nameof(password));
            }

            byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);
            byte[] hash = ComputeMultiStageHash(password, salt, DefaultIterations);

            string saltB64 = Convert.ToBase64String(salt);
            string hashB64 = Convert.ToBase64String(hash);

            return $"{VersionPrefix}${DefaultIterations}${saltB64}${hashB64}";
        }

        /// <summary>
        /// Verifies a plaintext password against a stored hash string in constant time.
        /// </summary>
        public static bool VerifyPassword(string password, string storedHash)
        {
            if (string.IsNullOrEmpty(password) || string.IsNullOrWhiteSpace(storedHash))
            {
                return false;
            }

            // Support legacy/plain fallback only if stored password is not in cbv1$ format
            if (!storedHash.StartsWith(VersionPrefix + "$", StringComparison.Ordinal))
            {
                return CryptographicOperations.FixedTimeEquals(
                    Encoding.UTF8.GetBytes(password),
                    Encoding.UTF8.GetBytes(storedHash));
            }

            string[] parts = storedHash.Split('$');
            if (parts.Length != 4)
            {
                return false;
            }

            if (!int.TryParse(parts[1], out int iterations) || iterations < 1000)
            {
                return false;
            }

            try
            {
                byte[] salt = Convert.FromBase64String(parts[2]);
                byte[] expectedHash = Convert.FromBase64String(parts[3]);

                byte[] actualHash = ComputeMultiStageHash(password, salt, iterations);
                return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
            }
            catch
            {
                return false;
            }
        }

        private static byte[] ComputeMultiStageHash(string password, byte[] salt, int iterations)
        {
            // Stage 1: Combine UTF-8 password bytes with internal pepper and salt reversal
            byte[] passwordBytes = Encoding.UTF8.GetBytes(password);
            byte[] seasonedInput = new byte[passwordBytes.Length + InternalPepper.Length];
            Buffer.BlockCopy(passwordBytes, 0, seasonedInput, 0, passwordBytes.Length);
            for (int i = 0; i < InternalPepper.Length; i++)
            {
                seasonedInput[passwordBytes.Length + i] = (byte)(InternalPepper[i] ^ salt[i % salt.Length]);
            }

            // Stage 2: PBKDF2 with HMAC-SHA256
            byte[] derivedKey = Rfc2898DeriveBytes.Pbkdf2(
                seasonedInput,
                salt,
                iterations,
                HashAlgorithmName.SHA256,
                KeySize);

            // Stage 3: Iterative XOR bit-rotation + SHA-256 mixing rounds
            byte[] state = new byte[KeySize];
            Buffer.BlockCopy(derivedKey, 0, state, 0, KeySize);

            byte[] roundBuffer = new byte[KeySize + salt.Length + 4];
            for (int round = 0; round < MixingRounds; round++)
            {
                for (int i = 0; i < KeySize; i++)
                {
                    byte rotated = (byte)((state[i] << 3) | (state[i] >> 5));
                    roundBuffer[i] = (byte)(rotated ^ salt[(i + round) % salt.Length]);
                }

                Buffer.BlockCopy(salt, 0, roundBuffer, KeySize, salt.Length);
                BitConverter.TryWriteBytes(roundBuffer.AsSpan(KeySize + salt.Length, 4), round);

                byte[] digest = SHA256.HashData(roundBuffer);
                for (int i = 0; i < KeySize; i++)
                {
                    state[i] ^= digest[i];
                }
            }

            return state;
        }
    }
}
