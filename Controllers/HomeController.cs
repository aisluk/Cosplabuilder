using Cosplabuilder.Helpers;
using Cosplabuilder.Models;
using Cosplabuilder.Views.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

namespace Cosplabuilder.Controllers
{
    public class HomeController : Controller
    {
        private readonly AppDBContext _db;

        public HomeController(AppDBContext db)
        {
            _db = db;
        }

        [HttpGet("/")]
        [HttpGet("/Home")]
        [HttpGet("/Home/Index")]
        public async Task<IActionResult> Index()
        {
            var model = new DashboardCompositeViewModel();
            try
            {
                int? currentUserId = UserAccess.GetUserId(User);
                bool isAdmin = UserAccess.IsAdmin(User);

                var projectsQuery = _db.Projects
                    .Include(p => p.CoverImage)
                    .Include(p => p.Owner)
                        .ThenInclude(u => u!.AvatarImage)
                    .Include(p => p.Parts)
                    .AsQueryable();

                if (!isAdmin)
                {
                    if (currentUserId.HasValue)
                    {
                        projectsQuery = projectsQuery.Where(p => p.IsPublic || p.UserId == currentUserId.Value);
                    }
                    else
                    {
                        projectsQuery = projectsQuery.Where(p => p.IsPublic);
                    }
                }

                model.Projects = await projectsQuery
                    .OrderByDescending(p => p.CreationDate)
                    .Take(6)
                    .ToListAsync();

                var visibleProjectIds = model.Projects.Select(p => p.Id).ToList();
                model.Parts = await _db.Parts.Where(pt => visibleProjectIds.Contains(pt.ProjectId)).ToListAsync();
                var visiblePartIds = model.Parts.Select(pt => pt.Id).ToList();
                model.Tasks = await _db.Tasks.Where(t => visiblePartIds.Contains(t.PartId)).ToListAsync();
            }
            catch
            {
                // graceful fallback if DB is initializing
            }

            return View(model);
        }

        [HttpGet("/Privacy")]
        [HttpGet("/Home/Privacy")]
        public IActionResult Privacy()
        {
            if (!UserAccess.IsAuthenticated(User))
            {
                return RedirectToAction("Index", "Login");
            }

            if (!UserAccess.IsAdmin(User))
            {
                return RedirectToAction("Error", new { code = 403, message = "Administrator privileges are required to view the REST API specification." });
            }

            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        [HttpGet("/Home/Error")]
        public IActionResult Error(int? code = null, string? message = null)
        {
            int statusCode = code ?? (HttpContext.Response.StatusCode >= 400 ? HttpContext.Response.StatusCode : 400);
            if (statusCode == 500)
            {
                statusCode = 400;
            }

            HttpContext.Response.StatusCode = statusCode;
            return View(new ErrorViewModel
            {
                RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier,
                StatusCode = statusCode,
                Title = GetStatusTitle(statusCode),
                Message = message ?? GetDefaultStatusMessage(statusCode)
            });
        }

        private static string GetStatusTitle(int code) => code switch
        {
            400 => "400 Bad Request",
            401 => "401 Unauthorized",
            403 => "403 Forbidden",
            404 => "404 Not Found",
            405 => "405 Method Not Allowed",
            408 => "408 Request Timeout",
            409 => "409 Conflict",
            422 => "422 Unprocessable Entity",
            _ => $"{code} Request Notice"
        };

        private static string GetDefaultStatusMessage(int code) => code switch
        {
            400 => "The request parameters could not be understood by the server.",
            401 => "Please sign in to access this page.",
            403 => "You do not have permission to view or modify this resource.",
            404 => "The requested item could not be found.",
            405 => "The HTTP method used is not allowed for this URL.",
            408 => "The operation timed out before completion.",
            409 => "A conflict occurred with an existing record.",
            422 => "Please check the submitted fields and try again.",
            _ => "The request could not be completed."
        };
    }
}
