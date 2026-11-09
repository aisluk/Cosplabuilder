using Cosplabuilder.Helpers;
using Cosplabuilder.Models;
using Cosplabuilder.Views.Data;
using Microsoft.EntityFrameworkCore;

namespace Cosplabuilder.Services
{
    public class EmailService
    {
        private readonly ILogger<EmailService> _logger;
        private readonly IWebHostEnvironment _env;

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
        /// Saves an uploaded IFormFile to wwwroot/uploads/ (and copies a backup to Media/)
        /// using a randomized SHA-256 hashed filename, and creates the Image record in MySQL.
        /// </summary>
        public async Task<Image> SaveUploadedImageAsync(AppDBContext db, IFormFile file, string? altText, int? userId, int? referenceId = null)
        {
            string uploadsFolder = Path.Combine(_env.WebRootPath, "uploads");
            Directory.CreateDirectory(uploadsFolder);

            string mediaFolder = Path.Combine(_env.ContentRootPath, "Media");
            Directory.CreateDirectory(mediaFolder);

            string hashedFileName = ImageNameHasher.GenerateHashedFileName(file.FileName, userId);
            string fullDiskPath = Path.Combine(uploadsFolder, hashedFileName);

            await using (var stream = new FileStream(fullDiskPath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            // Also keep a copy in the project's Media/ folder
            string mediaCopyPath = Path.Combine(mediaFolder, hashedFileName);
            File.Copy(fullDiskPath, mediaCopyPath, overwrite: true);

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

        /// <summary>
        /// Saves either an uploaded file or an explicit image URL/path as an Image record in the database.
        /// </summary>
        public async Task<Image?> SaveOrResolveImageAsync(
            AppDBContext db,
            IFormFile? file,
            string? imageUrl,
            string? altText,
            int? userId,
            int? referenceId = null)
        {
            if (file != null && file.Length > 0 && ImageNameHasher.IsAllowedExtension(file.FileName))
            {
                return await SaveUploadedImageAsync(db, file, altText, userId, referenceId);
            }

            if (!string.IsNullOrWhiteSpace(imageUrl))
            {
                string? normalized = ImageNameHasher.ResolveImageUrl(imageUrl);
                if (!string.IsNullOrEmpty(normalized))
                {
                    var image = new Image
                    {
                        FilePath = normalized,
                        UploadDate = DateTime.UtcNow,
                        AltText = string.IsNullOrWhiteSpace(altText) ? "Image" : altText.Trim(),
                        UserId = userId,
                        ReferenceId = referenceId
                    };

                    db.Images.Add(image);
                    await db.SaveChangesAsync();
                    return image;
                }
            }

            return null;
        }

        public static async System.Threading.Tasks.Task EnsureSeedDataAsync(AppDBContext db, IWebHostEnvironment env)
        {
            try
            {
                Directory.CreateDirectory(Path.Combine(env.WebRootPath, "uploads"));
                Directory.CreateDirectory(Path.Combine(env.ContentRootPath, "Media"));

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
                // Ignore if database connection is not yet ready
            }
        }
    }
}
