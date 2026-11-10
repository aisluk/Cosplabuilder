using Cosplabuilder.Helpers;
using Cosplabuilder.Models;
using Cosplabuilder.Services;
using Cosplabuilder.Views.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Cosplabuilder.Controllers
{
    public class ProfileController : Controller
    {
        private readonly AppDBContext _db;
        private readonly EmailService _emailService;

        public ProfileController(AppDBContext db, EmailService emailService)
        {
            _db = db;
            _emailService = emailService;
        }

        [HttpGet("/profile")]
        [HttpGet("/dashboard")]
        [HttpGet("/Profile/Index")]
        public async Task<IActionResult> Index(int? userId = null)
        {
            int? currentUserId = UserAccess.GetUserId(User);
            if (!currentUserId.HasValue)
            {
                return RedirectToAction("Index", "Login");
            }

            bool isAdmin = UserAccess.IsAdmin(User);
            int targetUserId = (isAdmin && userId.HasValue) ? userId.Value : currentUserId.Value;

            var profileUser = await _db.Users
                .Include(u => u.AvatarImage)
                .FirstOrDefaultAsync(u => u.Id == targetUserId);

            if (profileUser == null)
            {
                return RedirectToAction("Error", "Home", new { code = 404, message = "User profile not found." });
            }

            var projectsQuery = _db.Projects
                .Include(p => p.CoverImage)
                .Include(p => p.Owner)
                    .ThenInclude(u => u!.AvatarImage)
                .Include(p => p.Parts)
                    .ThenInclude(pt => pt.Tasks)
                .Include(p => p.References)
                    .ThenInclude(r => r.Images)
                .AsQueryable();

            if (!isAdmin)
            {
                projectsQuery = projectsQuery.Where(p => p.UserId == profileUser.Id);
            }

            var projects = await projectsQuery.OrderByDescending(p => p.CreationDate).ToListAsync();
            var projectIds = projects.Select(p => p.Id).ToList();

            var parts = await _db.Parts
                .Include(pt => pt.PartImage)
                .Include(pt => pt.Tasks)
                .Where(pt => projectIds.Contains(pt.ProjectId))
                .ToListAsync();

            var partIds = parts.Select(pt => pt.Id).ToList();
            var tasks = await _db.Tasks
                .Where(t => partIds.Contains(t.PartId))
                .OrderBy(t => t.CompletionDate.HasValue)
                .ThenBy(t => t.Id)
                .ToListAsync();

            var tools = await _db.Tools
                .Include(t => t.ToolImage)
                .Where(t => t.UserId == profileUser.Id)
                .ToListAsync();

            var ownedMaterialIds = await _emailService.GetOwnedMaterialIdsAsync(_db, profileUser.Id);
            var materials = await _db.Materials
                .Include(m => m.MaterialImage)
                .Where(m => ownedMaterialIds.Contains(m.Id))
                .ToListAsync();

            var model = new DashboardCompositeViewModel
            {
                ProfileUser = profileUser,
                Projects = projects,
                Parts = parts,
                Tasks = tasks,
                Tools = tools,
                Materials = materials,
                Languages = await _db.Languages.OrderBy(l => l.Code).Select(l => l.Code).ToListAsync(),
                Currencies = await _db.Currencies.OrderBy(c => c.Code).Select(c => c.Code).ToListAsync()
            };

            if (isAdmin)
            {
                model.AllUsers = await _db.Users.Include(u => u.AvatarImage).OrderBy(u => u.Id).ToListAsync();
            }

            return View(model);
        }

        [HttpPost("/profile/preferences")]
        public async Task<IActionResult> UpdatePreferences(
            string language,
            string currency,
            string? newPassword,
            IFormFile? avatarFile)
        {
            int? userId = UserAccess.GetUserId(User);
            if (!userId.HasValue)
            {
                return RedirectToAction("Index", "Login");
            }

            var user = await _db.Users
                .Include(u => u.AvatarImage)
                .FirstOrDefaultAsync(u => u.Id == userId.Value);
            if (user == null)
            {
                return NotFound();
            }

            if (!string.IsNullOrWhiteSpace(language) && await _db.Languages.AnyAsync(l => l.Code == language))
            {
                user.Language = language;
            }

            if (!string.IsNullOrWhiteSpace(currency) && await _db.Currencies.AnyAsync(c => c.Code == currency))
            {
                user.Currency = currency;
            }

            if (!string.IsNullOrWhiteSpace(newPassword))
            {
                if (newPassword.Length < 6)
                {
                    TempData["FlashError"] = "New password must be at least 6 characters.";
                    return RedirectToAction("Index");
                }
                user.PasswordHashed = PasswordHasher.HashPassword(newPassword);
            }

            string? activeAvatarPath = user.AvatarImage?.FilePath;
            if (avatarFile != null && avatarFile.Length > 0)
            {
                if (!ImageNameHasher.IsAllowedExtension(avatarFile.FileName))
                {
                    TempData["FlashError"] = "Unsupported image file extension.";
                    return RedirectToAction("Index");
                }

                var uploadedImg = await _emailService.SaveUploadedImageAsync(
                    _db,
                    avatarFile,
                    $"{user.Username} avatar",
                    user.Id);

                if (uploadedImg != null)
                {
                    user.ImageId = uploadedImg.Id;
                    user.AvatarImage = uploadedImg;
                    activeAvatarPath = uploadedImg.FilePath;
                }
            }

            await _db.SaveChangesAsync();

            // Refresh authentication cookie so updated avatar, currency, and language appear across all pages immediately
            string roleName = user.Type >= 1 ? UserRoles.Admin : UserRoles.Member;
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new(ClaimTypes.Name, user.Username),
                new(ClaimTypes.Email, user.Email),
                new(ClaimTypes.Role, roleName),
                new("Currency", user.Currency),
                new("Language", user.Language),
                new("AvatarUrl", ImageNameHasher.ResolveImageUrl(activeAvatarPath) ?? string.Empty)
            };
            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(identity),
                new AuthenticationProperties
                {
                    IsPersistent = true,
                    ExpiresUtc = DateTimeOffset.UtcNow.AddDays(7)
                });

            TempData["FlashSuccess"] = "Profile preferences updated.";
            return RedirectToAction("Index");
        }

        [HttpPost("/profile/users/{targetUserId:int}/promote")]
        public async Task<IActionResult> PromoteUserToAdmin(int targetUserId)
        {
            if (!UserAccess.IsAdmin(User))
            {
                return RedirectToAction("Error", "Home", new { code = 403, message = "Administrator privileges required." });
            }

            var targetUser = await _db.Users.FirstOrDefaultAsync(u => u.Id == targetUserId);
            if (targetUser == null)
            {
                return RedirectToAction("Error", "Home", new { code = 404, message = "User not found." });
            }

            if (targetUser.Type >= 1)
            {
                TempData["FlashError"] = "Administrators cannot be demoted back to regular members.";
                return RedirectToAction("Index");
            }

            targetUser.Type = 1;
            await _db.SaveChangesAsync();
            TempData["FlashSuccess"] = $"Promoted '{targetUser.Username}' to Administrator.";
            return RedirectToAction("Index");
        }
    }
}
