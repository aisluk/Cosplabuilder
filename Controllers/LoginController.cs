using Cosplabuilder.Helpers;
using Cosplabuilder.Models;
using Cosplabuilder.Views.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Cosplabuilder.Controllers
{
    public class LoginController : Controller
    {
        private readonly AppDBContext _db;

        public LoginController(AppDBContext db)
        {
            _db = db;
        }

        [HttpGet("/login")]
        [HttpGet("/Login/Index")]
        public IActionResult Index(string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        [HttpPost("/login")]
        [HttpPost("/Login/Index")]
        public async Task<IActionResult> Login(string usernameOrEmail, string password, string? returnUrl = null)
        {
            if (string.IsNullOrWhiteSpace(usernameOrEmail) || string.IsNullOrWhiteSpace(password))
            {
                HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
                ViewData["Error"] = "Please enter both your username/email and password.";
                return View("Index");
            }

            string normalized = usernameOrEmail.Trim();
            var user = await _db.Users
                .Include(u => u.AvatarImage)
                .FirstOrDefaultAsync(u => u.Username == normalized || u.Email == normalized);

            if (user == null || !PasswordHasher.VerifyPassword(password, user.PasswordHashed))
            {
                HttpContext.Response.StatusCode = StatusCodes.Status401Unauthorized;
                ViewData["Error"] = "Invalid username/email or password.";
                return View("Index");
            }

            string roleName = user.Type == 1 ? UserRoles.Admin : UserRoles.Member;

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new(ClaimTypes.Name, user.Username),
                new(ClaimTypes.Email, user.Email),
                new(ClaimTypes.Role, roleName),
                new("Currency", user.Currency),
                new("Language", user.Language)
            };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                principal,
                new AuthenticationProperties
                {
                    IsPersistent = true,
                    ExpiresUtc = DateTimeOffset.UtcNow.AddDays(7)
                });

            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return RedirectToAction("Index", "Profile");
        }

        [HttpPost("/logout")]
        [HttpPost("/Login/Logout")]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            TempData["FlashSuccess"] = "You have been signed out.";
            return RedirectToAction("Index", "Home");
        }
    }
}
