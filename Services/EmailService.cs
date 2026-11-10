using Cosplabuilder.Helpers;
using Cosplabuilder.Models;
using Cosplabuilder.Views.Data;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Cosplabuilder.Services
{
    public class EmailService
    {
        private readonly ILogger<EmailService> _logger;
        private readonly IWebHostEnvironment _env;
        private static readonly object _ownerFileLock = new();

        public EmailService(ILogger<EmailService> logger, IWebHostEnvironment env)
        {
            _logger = logger;
            _env = env;
        }

        public void SendNotification(string toEmail, string subject, string body)
        {
            _logger.LogInformation("[Cosplabuilder Mail] To: {Email} | Subject: {Subject} | Body: {Body}", toEmail, subject, body);
        }

        /// <summary>
        /// Resolves the project root folder reliably even when launched from bin/Debug/...
        /// </summary>
        public static string GetProjectRootPath(IWebHostEnvironment env)
        {
            string root = !string.IsNullOrWhiteSpace(env.ContentRootPath)
                ? env.ContentRootPath
                : AppContext.BaseDirectory;

            var dir = new DirectoryInfo(root);
            while (dir != null)
            {
                if (File.Exists(Path.Combine(dir.FullName, "Cosplabuilder.csproj")))
                {
                    return dir.FullName;
                }
                dir = dir.Parent;
            }

            return root;
        }

        public static string GetUploadsFolderPath(IWebHostEnvironment env)
        {
            string webRoot = !string.IsNullOrWhiteSpace(env.WebRootPath)
                ? env.WebRootPath
                : Path.Combine(GetProjectRootPath(env), "wwwroot");

            string uploads = Path.Combine(webRoot, "uploads");
            Directory.CreateDirectory(uploads);
            return uploads;
        }

        public static string GetMediaFolderPath(IWebHostEnvironment env)
        {
            string media = Path.Combine(GetProjectRootPath(env), "Media");
            Directory.CreateDirectory(media);
            return media;
        }

        private string GetMaterialOwnersFilePath()
        {
            string propsDir = Path.Combine(GetProjectRootPath(_env), "Properties");
            Directory.CreateDirectory(propsDir);
            return Path.Combine(propsDir, "material_owners.json");
        }

        private Dictionary<int, int> LoadMaterialOwnerMap()
        {
            lock (_ownerFileLock)
            {
                string path = GetMaterialOwnersFilePath();
                if (File.Exists(path))
                {
                    try
                    {
                        string json = File.ReadAllText(path);
                        var dict = JsonSerializer.Deserialize<Dictionary<int, int>>(json);
                        if (dict != null)
                            return dict;
                    }
                    catch
                    {
                        // fallback to default map
                    }
                }

                var initial = new Dictionary<int, int>
                {
                    [1] = 2,
                    [2] = 2,
                    [3] = 1,
                    [4] = 1,
                    [5] = 1,
                    [6] = 1,
                    [7] = 1,
                    [8] = 1,
                    [9] = 1
                };
                SaveMaterialOwnerMap(initial);
                return initial;
            }
        }

        private void SaveMaterialOwnerMap(Dictionary<int, int> map)
        {
            lock (_ownerFileLock)
            {
                try
                {
                    string path = GetMaterialOwnersFilePath();
                    string json = JsonSerializer.Serialize(map, new JsonSerializerOptions { WriteIndented = true });
                    File.WriteAllText(path, json);
                }
                catch
                {
                    // ignore if read-only
                }
            }
        }

        public void SetMaterialOwner(int materialId, int userId)
        {
            if (materialId <= 0 || userId <= 0) return;
            var map = LoadMaterialOwnerMap();
            map[materialId] = userId;
            SaveMaterialOwnerMap(map);
        }

        /// <summary>
        /// Determines the owner UserId for every material in the database using Image.UserId,
        /// the material_owners map, or MaterialUse -> Part -> Project.UserId.
        /// </summary>
        public async Task<HashSet<int>> GetOwnedMaterialIdsAsync(AppDBContext db, int userId)
        {
            var map = LoadMaterialOwnerMap();
            var allMaterials = await db.Materials
                .AsNoTracking()
                .Include(m => m.MaterialImage)
                .ToListAsync();

            var materialUseOwners = await db.MaterialUses
                .AsNoTracking()
                .Include(mu => mu.Part)
                    .ThenInclude(pt => pt!.Project)
                .Where(mu => mu.Part != null && mu.Part.Project != null)
                .Select(mu => new { mu.MaterialId, OwnerId = mu.Part!.Project!.UserId })
                .ToListAsync();

            var ownerByUse = materialUseOwners
                .GroupBy(x => x.MaterialId)
                .ToDictionary(g => g.Key, g => g.First().OwnerId);

            var ownedIds = new HashSet<int>();
            foreach (var m in allMaterials)
            {
                int? ownerId = null;
                if (m.MaterialImage?.UserId != null)
                {
                    ownerId = m.MaterialImage.UserId.Value;
                }
                else if (map.TryGetValue(m.Id, out int mappedUser))
                {
                    ownerId = mappedUser;
                }
                else if (ownerByUse.TryGetValue(m.Id, out int useOwner))
                {
                    ownerId = useOwner;
                }

                if (ownerId == userId)
                {
                    ownedIds.Add(m.Id);
                }
            }

            return ownedIds;
        }

        /// <summary>
        /// Synchronizes files between wwwroot/uploads/ and Media/ so both directories contain all uploaded images.
        /// </summary>
        public static void SyncUploadsAndMediaFolders(IWebHostEnvironment env)
        {
            string uploadsFolder = GetUploadsFolderPath(env);
            string mediaFolder = GetMediaFolderPath(env);

            foreach (string uploadFile in Directory.GetFiles(uploadsFolder))
            {
                string fileName = Path.GetFileName(uploadFile);
                string targetMediaPath = Path.Combine(mediaFolder, fileName);
                if (!File.Exists(targetMediaPath))
                {
                    using var src = new FileStream(uploadFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    using var dst = new FileStream(targetMediaPath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite);
                    src.CopyTo(dst);
                }
            }

            foreach (string mediaFile in Directory.GetFiles(mediaFolder))
            {
                string fileName = Path.GetFileName(mediaFile);
                string targetUploadPath = Path.Combine(uploadsFolder, fileName);
                if (!File.Exists(targetUploadPath))
                {
                    using var src = new FileStream(mediaFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    using var dst = new FileStream(targetUploadPath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite);
                    src.CopyTo(dst);
                }
            }
        }

        /// <summary>
        /// Saves an uploaded IFormFile directly from memory to both wwwroot/uploads/ and Media/
        /// using a randomized SHA-256 hashed filename, and creates the Image record in MySQL.
        /// </summary>
        public async Task<Image> SaveUploadedImageAsync(AppDBContext db, IFormFile file, string? altText, int? userId, int? referenceId = null)
        {
            string uploadsFolder = GetUploadsFolderPath(_env);
            string mediaFolder = GetMediaFolderPath(_env);

            string hashedFileName = ImageNameHasher.GenerateHashedFileName(file.FileName, userId);
            string fullUploadsPath = Path.Combine(uploadsFolder, hashedFileName);
            string fullMediaPath = Path.Combine(mediaFolder, hashedFileName);

            using var memoryBuffer = new MemoryStream();
            await file.CopyToAsync(memoryBuffer);
            byte[] fileBytes = memoryBuffer.ToArray();

            // Write directly from memory buffer to wwwroot/uploads/ and Media/ (avoids file-watcher locks on File.Copy)
            await using (var uploadStream = new FileStream(fullUploadsPath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite))
            {
                await uploadStream.WriteAsync(fileBytes);
                await uploadStream.FlushAsync();
            }

            await using (var mediaStream = new FileStream(fullMediaPath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite))
            {
                await mediaStream.WriteAsync(fileBytes);
                await mediaStream.FlushAsync();
            }

            var image = new Image
            {
                FilePath = $"/uploads/{hashedFileName}",
                UploadDate = DateTime.UtcNow,
                AltText = string.IsNullOrWhiteSpace(altText) ? Path.GetFileNameWithoutExtension(file.FileName) : altText.Trim(),
                UserId = userId,
                ReferenceId = referenceId
            };

            db.Images.Add(image);
            await db.SaveChangesAsync();
            return image;
        }

        public static async System.Threading.Tasks.Task EnsureSeedDataAsync(AppDBContext db, IWebHostEnvironment env)
        {
            try
            {
                SyncUploadsAndMediaFolders(env);
            }
            catch
            {
                // ignore file sync issues on read-only mounts
            }

            try
            {
                if (!await db.TaskTypes.AnyAsync())
                {
                    foreach (var name in new[] { "Buy", "Make", "Order", "Test", "Research", "Photograph" })
                        db.TaskTypes.Add(new TaskTypeLookup { Name = name });
                }
                if (!await db.ProjectStatuses.AnyAsync())
                {
                    foreach (var name in new[] { "Planned", "Active", "Completed", "Abandoned" })
                        db.ProjectStatuses.Add(new ProjectStatusLookup { Name = name });
                }
                if (!await db.ToolTypes.AnyAsync())
                {
                    foreach (var name in new[] { "Other", "Measurement", "Marking", "Lighting", "Pressing", "Cutting", "Burning", "Styling", "Makeup" })
                        db.ToolTypes.Add(new ToolTypeLookup { Name = name });
                }
                if (!await db.ReferenceTypes.AnyAsync())
                {
                    foreach (var name in new[] { "Character", "Costume", "Prop", "Makeup", "Wig", "Accessories", "Other" })
                        db.ReferenceTypes.Add(new ReferenceTypeLookup { Name = name });
                }
                if (!await db.MaterialTypes.AnyAsync())
                {
                    foreach (var name in new[] { "Fabric", "Craft", "Hardware", "Glue", "Wood", "Paint", "Paper", "Plastic", "Resin", "Electronics", "Wire", "Other" })
                        db.MaterialTypes.Add(new MaterialTypeLookup { Name = name });
                }
                if (!await db.PartTypes.AnyAsync())
                {
                    foreach (var name in new[] { "Jewelry", "Wig", "Weapon", "Prop", "Outfit", "Accessory", "Shoes", "Gloves", "Other" })
                        db.PartTypes.Add(new PartTypeLookup { Name = name });
                }
                if (!await db.Languages.AnyAsync())
                {
                    db.Languages.Add(new LanguageLookup { Code = "English", Name = "English" });
                    db.Languages.Add(new LanguageLookup { Code = "Lithuanian", Name = "Lithuanian" });
                }
                if (!await db.Currencies.AnyAsync())
                {
                    // might add more later
                    var currencies = new (string Code, string Name)[]
                    {
                        ("EUR", "Euro"), ("USD", "US Dollar"), ("AUD", "Australian Dollar"),
                        ("GBP", "British Pound"), ("CHF", "Swiss Franc"), ("PLN", "Polish Zloty"),
                        ("JPY", "Japanese Yen"), ("CNY", "Chinese Yuan"), ("SGD", "Singapore Dollar"), ("HKD", "Hong Kong Dollar")
                    };
                    foreach (var c in currencies)
                        db.Currencies.Add(new CurrencyLookup { Code = c.Code, Name = c.Name });
                }
                await db.SaveChangesAsync();
            }
            catch
            {
                // ignore if database connection is not yet ready
            }
        }
    }
}
