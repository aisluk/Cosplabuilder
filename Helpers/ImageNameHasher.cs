using System.Security.Cryptography;
using System.Text;

namespace Cosplabuilder.Helpers
{
    public static class ImageNameHasher
    {
        private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".webp", ".gif", ".bmp", ".svg"
        };

        public static bool IsAllowedExtension(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return false;

            string ext = Path.GetExtension(fileName);
            return !string.IsNullOrEmpty(ext) && AllowedExtensions.Contains(ext);
        }

        public static string GenerateHashedFileName(string originalFileName, int? userId = null)
        {
            string extension = Path.GetExtension(originalFileName ?? string.Empty).ToLowerInvariant();
            if (string.IsNullOrEmpty(extension) || !AllowedExtensions.Contains(extension))
            {
                extension = ".jpg";
            }

            byte[] randomNonce = RandomNumberGenerator.GetBytes(16);
            string nonceHex = Convert.ToHexString(randomNonce);
            long ticks = DateTime.UtcNow.Ticks;

            string seed = $"{originalFileName}|{userId ?? 0}|{ticks}|{nonceHex}|{Guid.NewGuid():N}";
            byte[] hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(seed));

            string hashHex = Convert.ToHexString(hashBytes, 0, 16).ToLowerInvariant();
            return $"img_{hashHex}{extension}";
        }

        /// <summary>
        /// Normalizes a database FilePath so relative paths, tilde paths, backslashes,
        /// or external URLs always resolve cleanly in HTML img src attributes.
        /// </summary>
        public static string? ResolveImageUrl(string? filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                return null;

            string trimmed = filePath.Trim().Replace('\\', '/');

            if (trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase))
            {
                return trimmed;
            }

            if (trimmed.StartsWith("~/", StringComparison.Ordinal))
            {
                trimmed = trimmed[1..];
            }

            if (!trimmed.StartsWith('/'))
            {
                trimmed = "/" + trimmed;
            }

            return trimmed;
        }
    }
}
