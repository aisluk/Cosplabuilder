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
    public class RegisterController : Controller
    {
        private readonly AppDBContext _db;
        private readonly EmailService _emailService;

        public RegisterController(AppDBContext db, EmailService emailService)
        {
            _db = db;
            _emailService = emailService;
        }

        [HttpGet("/register")]
        [HttpGet("/Register/Index")]
        public async Task<IActionResult> Index()
        {
            await LoadLookupsAsync();
            return View();
        }

        [HttpPost("/register")]
        [HttpPost("/Register/Index")]
        public async Task<IActionResult> Register(
            string username,
            string email,
            string password,
            string confirmPassword,
            string language = "English",
            string currency = "EUR",
            IFormFile? avatarFile = null,
            string? avatarUrl = null,
            string? avatarAlt = null)
        {
            await LoadLookupsAsync();

            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
                ViewData["Error"] = "Username, email, and password are required.";
                return View("Index");
            }

            username = username.Trim();
            email = email.Trim();

            if (username.Length < 3 || username.Length > 32)
            {
                HttpContext.Response.StatusCode = StatusCodes.Status422UnprocessableEntity;
                ViewData["Error"] = "Username must be between 3 and 32 characters.";
                return View("Index");
            }

            if (password.Length < 6)
            {
                HttpContext.Response.StatusCode = StatusCodes.Status422UnprocessableEntity;
                ViewData["Error"] = "Password must be at least 6 characters long.";
                return View("Index");
            }

            if (password != confirmPassword)
            {
                HttpContext.Response.StatusCode = StatusCodes.Status422UnprocessableEntity;
                ViewData["Error"] = "Passwords do not match.";
                return View("Index");
            }

            bool exists = await _db.Users.AnyAsync(u => u.Username == username || u.Email == email);
            if (exists)
            {
                HttpContext.Response.StatusCode = StatusCodes.Status409Conflict;
                ViewData["Error"] = "A user with that username or email already exists.";
                return View("Index");
            }

            bool validLang = await _db.Languages.AnyAsync(l => l.Code == language);
            if (!validLang) language = "English";

            bool validCurr = await _db.Currencies.AnyAsync(c => c.Code == currency);
            if (!validCurr) currency = "EUR";

            var newUser = new User
            {
                Username = username,
                Email = email,
                PasswordHashed = PasswordHasher.HashPassword(password),
                Type = 0,
                Language = language,
                Currency = currency
            };

            _db.Users.Add(newUser);
            await _db.SaveChangesAsync();

            var img = await _emailService.SaveOrResolveImageAsync(
                _db,
                avatarFile,
                avatarUrl,
                string.IsNullOrWhiteSpace(avatarAlt) ? $"{username} icon" : avatarAlt,
                newUser.Id);

            if (img != null)
            {
                newUser.ImageId = img.Id;
                await _db.SaveChangesAsync();
            }

            _emailService.SendNotification(newUser.Email, "Welcome to Cosplabuilder!", $"Hello {newUser.Username}, your account is ready.");

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, newUser.Id.ToString()),
                new(ClaimTypes.Name, newUser.Username),
                new(ClaimTypes.Email, newUser.Email),
                new(ClaimTypes.Role, UserRoles.Member),
                new("Currency", newUser.Currency),
                new("Language", newUser.Language)
            };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));

            TempData["FlashSuccess"] = $"Welcome, {newUser.Username}! Your account has been created.";
            return RedirectToAction("Index", "Profile");
        }

        private async System.Threading.Tasks.Task LoadLookupsAsync()
        {
            ViewBag.Languages = await _db.Languages.OrderBy(l => l.Code).Select(l => l.Code).ToListAsync();
            ViewBag.Currencies = await _db.Currencies.OrderBy(c => c.Code).Select(c => c.Code).ToListAsync();
        }
    }
}
