using Cosplabuilder.Helpers;
using Cosplabuilder.Models;
using Cosplabuilder.Services;
using Cosplabuilder.Views.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaskModel = Cosplabuilder.Models.Task;

namespace Cosplabuilder.Controllers
{
    public class DataController : Controller
    {
        private readonly AppDBContext _db;
        private readonly EmailService _emailService;

        public DataController(AppDBContext db, EmailService emailService)
        {
            _db = db;
            _emailService = emailService;
        }

        // ====================================================================
        // 1. MVC GUI VIEWS
        // ====================================================================

        [HttpGet("/projects")]
        [HttpGet("/Data")]
        [HttpGet("/Data/Index")]
        public async Task<IActionResult> Index(
            string? search = null,
            string? status = null,
            string? sortBy = null,
            int page = 1,
            int pageSize = 6)
        {
            if (page <= 0 || pageSize <= 0 || pageSize > 50)
            {
                return RedirectToAction("Error", "Home", new { code = 400, message = "Pagination parameters page and pageSize must be positive integers." });
            }

            var vm = await CreateBaseExplorerViewModelAsync("Projects");
            vm.SearchQuery = search;
            vm.FilterStatus = status;
            vm.SortBy = sortBy;
            vm.CurrentPage = page;
            vm.PageSize = pageSize;

            int? currentUserId = UserAccess.GetUserId(User);
            bool isAdmin = UserAccess.IsAdmin(User);

            var query = _db.Projects
                .Include(p => p.CoverImage)
                .Include(p => p.Owner)
                    .ThenInclude(u => u!.AvatarImage)
                .Include(p => p.Parts)
                .AsQueryable();

            if (!isAdmin)
            {
                if (currentUserId.HasValue)
                {
                    query = query.Where(p => p.IsPublic || p.UserId == currentUserId.Value);
                }
                else
                {
                    query = query.Where(p => p.IsPublic);
                }
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                string term = search.Trim();
                query = query.Where(p =>
                    p.Name.Contains(term) ||
                    (p.Fandom != null && p.Fandom.Contains(term)) ||
                    (p.Character != null && p.Character.Contains(term)) ||
                    (p.Description != null && p.Description.Contains(term)));
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                query = query.Where(p => p.Status == status);
            }

            query = sortBy switch
            {
                "progress_desc" => query.OrderByDescending(p => p.Progress),
                "budget_desc" => query.OrderByDescending(p => p.Budget),
                "name_asc" => query.OrderBy(p => p.Name),
                _ => query.OrderByDescending(p => p.CreationDate).ThenByDescending(p => p.Id)
            };

            vm.TotalItems = await query.CountAsync();
            vm.Projects = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return View("Index", vm);
        }

        [HttpGet("/projects/{projectId:int}")]
        [HttpGet("/projects/{projectId:int}/parts")]
        public async Task<IActionResult> ProjectDetails(
            int projectId,
            string? search = null,
            string? type = null,
            int page = 1,
            int pageSize = 6)
        {
            if (projectId <= 0 || page <= 0 || pageSize <= 0)
            {
                return RedirectToAction("Error", "Home", new { code = 400, message = "Invalid project ID or pagination parameters." });
            }

            var project = await _db.Projects
                .Include(p => p.CoverImage)
                .Include(p => p.Owner)
                    .ThenInclude(u => u!.AvatarImage)
                .Include(p => p.References)
                    .ThenInclude(r => r.Images)
                .FirstOrDefaultAsync(p => p.Id == projectId);

            if (project == null)
            {
                return RedirectToAction("Error", "Home", new { code = 404, message = $"Project #{projectId} was not found." });
            }

            if (!UserAccess.CanViewProject(User, project))
            {
                if (!UserAccess.IsAuthenticated(User))
                    return RedirectToAction("Index", "Login");

                return RedirectToAction("Error", "Home", new { code = 403, message = "This project is private." });
            }

            var vm = await CreateBaseExplorerViewModelAsync("ProjectDetail");
            vm.CurrentProject = project;
            vm.References = project.References.OrderBy(r => r.Id).ToList();
            vm.SearchQuery = search;
            vm.FilterType = type;
            vm.CurrentPage = page;
            vm.PageSize = pageSize;

            var partsQuery = _db.Parts
                .Include(pt => pt.PartImage)
                .Include(pt => pt.Tasks)
                .Include(pt => pt.MaterialUses)
                    .ThenInclude(mu => mu.Material)
                        .ThenInclude(m => m!.MaterialImage)
                .Where(pt => pt.ProjectId == projectId)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                string term = search.Trim();
                partsQuery = partsQuery.Where(pt => pt.Name.Contains(term) || (pt.Description != null && pt.Description.Contains(term)));
            }

            if (!string.IsNullOrWhiteSpace(type))
            {
                partsQuery = partsQuery.Where(pt => pt.Type == type);
            }

            vm.TotalItems = await partsQuery.CountAsync();
            vm.Parts = await partsQuery
                .OrderByDescending(pt => pt.Progress)
                .ThenBy(pt => pt.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            vm.Materials = await _db.Materials.Include(m => m.MaterialImage).OrderBy(m => m.Name).ToListAsync();

            return View("Index", vm);
        }

        [HttpGet("/projects/{projectId:int}/parts/{partId:int}/tasks")]
        [HttpGet("/projects/{projectId:int}/parts/{partId:int}")]
        public async Task<IActionResult> PartTasks(
            int projectId,
            int partId,
            string? search = null,
            string? type = null,
            string? status = null,
            int page = 1,
            int pageSize = 8)
        {
            if (projectId <= 0 || partId <= 0 || page <= 0 || pageSize <= 0)
            {
                return RedirectToAction("Error", "Home", new { code = 400, message = "Invalid IDs or pagination parameters." });
            }

            var project = await _db.Projects
                .Include(p => p.CoverImage)
                .Include(p => p.Owner)
                    .ThenInclude(u => u!.AvatarImage)
                .FirstOrDefaultAsync(p => p.Id == projectId);

            if (project == null)
            {
                return RedirectToAction("Error", "Home", new { code = 404, message = $"Project #{projectId} was not found." });
            }

            if (!UserAccess.CanViewProject(User, project))
            {
                if (!UserAccess.IsAuthenticated(User))
                    return RedirectToAction("Index", "Login");

                return RedirectToAction("Error", "Home", new { code = 403, message = "This project is private." });
            }

            var part = await _db.Parts
                .Include(pt => pt.PartImage)
                .Include(pt => pt.MaterialUses)
                    .ThenInclude(mu => mu.Material)
                        .ThenInclude(m => m!.MaterialImage)
                .FirstOrDefaultAsync(pt => pt.Id == partId && pt.ProjectId == projectId);

            if (part == null)
            {
                return RedirectToAction("Error", "Home", new { code = 404, message = $"Part #{partId} does not exist inside Project #{projectId}." });
            }

            var vm = await CreateBaseExplorerViewModelAsync("PartTasks");
            vm.CurrentProject = project;
            vm.CurrentPart = part;
            vm.SearchQuery = search;
            vm.FilterType = type;
            vm.FilterStatus = status;
            vm.CurrentPage = page;
            vm.PageSize = pageSize;

            var tasksQuery = _db.Tasks
                .Where(t => t.PartId == partId)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                string term = search.Trim();
                tasksQuery = tasksQuery.Where(t => t.Name.Contains(term) || (t.Description != null && t.Description.Contains(term)));
            }

            if (!string.IsNullOrWhiteSpace(type))
            {
                tasksQuery = tasksQuery.Where(t => t.Type == type);
            }

            if (string.Equals(status, "completed", StringComparison.OrdinalIgnoreCase))
            {
                tasksQuery = tasksQuery.Where(t => t.CompletionDate != null);
            }
            else if (string.Equals(status, "pending", StringComparison.OrdinalIgnoreCase))
            {
                tasksQuery = tasksQuery.Where(t => t.CompletionDate == null);
            }

            vm.TotalItems = await tasksQuery.CountAsync();
            vm.Tasks = await tasksQuery
                .OrderBy(t => t.CompletionDate.HasValue)
                .ThenBy(t => t.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            vm.Materials = await _db.Materials.Include(m => m.MaterialImage).OrderBy(m => m.Name).ToListAsync();

            return View("Index", vm);
        }

        /// <summary>
        /// Dedicated Tools Page (/tools)
        /// </summary>
        [HttpGet("/tools")]
        [HttpGet("/workshop")]
        [HttpGet("/workshop/tools")]
        public async Task<IActionResult> Tools(string? search = null, string? type = null, int page = 1, int pageSize = 6)
        {
            if (!UserAccess.IsAuthenticated(User))
            {
                return RedirectToAction("Index", "Login");
            }

            if (page <= 0 || pageSize <= 0 || pageSize > 50)
            {
                return RedirectToAction("Error", "Home", new { code = 400, message = "Invalid pagination parameters." });
            }

            var vm = await CreateBaseExplorerViewModelAsync("Tools");
            vm.SearchQuery = search;
            vm.FilterType = type;
            vm.CurrentPage = page;
            vm.PageSize = pageSize;

            var toolsQuery = _db.Tools
                .Include(t => t.ToolImage)
                .Include(t => t.Owner)
                    .ThenInclude(u => u!.AvatarImage)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                string term = search.Trim();
                toolsQuery = toolsQuery.Where(t => t.Name.Contains(term) || (t.Description != null && t.Description.Contains(term)));
            }

            if (!string.IsNullOrWhiteSpace(type))
            {
                toolsQuery = toolsQuery.Where(t => t.ToolType == type);
            }

            vm.TotalItems = await toolsQuery.CountAsync();
            vm.Tools = await toolsQuery
                .OrderByDescending(t => t.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return View("Index", vm);
        }

        /// <summary>
        /// Dedicated Materials Page (/materials)
        /// </summary>
        [HttpGet("/materials")]
        [HttpGet("/workshop/materials")]
        public async Task<IActionResult> Materials(string? search = null, string? type = null, int page = 1, int pageSize = 6)
        {
            if (!UserAccess.IsAuthenticated(User))
            {
                return RedirectToAction("Index", "Login");
            }

            if (page <= 0 || pageSize <= 0 || pageSize > 50)
            {
                return RedirectToAction("Error", "Home", new { code = 400, message = "Invalid pagination parameters." });
            }

            var vm = await CreateBaseExplorerViewModelAsync("Materials");
            vm.SearchQuery = search;
            vm.FilterType = type;
            vm.CurrentPage = page;
            vm.PageSize = pageSize;

            var materialsQuery = _db.Materials
                .Include(m => m.MaterialImage)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                string term = search.Trim();
                materialsQuery = materialsQuery.Where(m =>
                    m.Name.Contains(term) ||
                    (m.Description != null && m.Description.Contains(term)) ||
                    (m.Source != null && m.Source.Contains(term)));
            }

            if (!string.IsNullOrWhiteSpace(type))
            {
                materialsQuery = materialsQuery.Where(m => m.MaterialType == type);
            }

            vm.TotalItems = await materialsQuery.CountAsync();
            vm.Materials = await materialsQuery
                .OrderByDescending(m => m.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return View("Index", vm);
        }

        // ====================================================================
        // 2. MVC FORM POST ACTIONS
        // ====================================================================

        [HttpPost("/projects/form")]
        public async Task<IActionResult> SaveProjectForm(
            int? id,
            string name,
            string? fandom,
            string? character,
            string? description,
            int progress = 0,
            decimal? budget = null,
            string status = "Planned",
            bool isPublic = true,
            DateTime? completionDate = null,
            IFormFile? imageFile = null,
            string? imageUrl = null,
            string? imageAlt = null)
        {
            int? userId = UserAccess.GetUserId(User);
            if (!userId.HasValue)
            {
                return RedirectToAction("Index", "Login");
            }

            if (string.IsNullOrWhiteSpace(name) || progress < 0 || progress > 100 || (budget.HasValue && budget.Value < 0))
            {
                TempData["FlashError"] = "Please provide a valid project name, progress (0..100), and non-negative budget.";
                return RedirectToAction("Index");
            }

            if (id.HasValue && id.Value > 0)
            {
                var existing = await _db.Projects.FirstOrDefaultAsync(p => p.Id == id.Value);
                if (existing == null)
                {
                    return RedirectToAction("Error", "Home", new { code = 404, message = "Project not found." });
                }
                if (!UserAccess.CanManageResource(User, existing.UserId))
                {
                    return RedirectToAction("Error", "Home", new { code = 403, message = "You do not have permission to edit another member's project." });
                }

                existing.Name = name.Trim();
                existing.Fandom = fandom?.Trim();
                existing.Character = character?.Trim();
                existing.Description = description?.Trim();
                existing.Progress = progress;
                existing.Budget = budget;
                existing.Status = status;
                existing.IsPublic = isPublic;
                existing.CompletionDate = completionDate;

                var img = await _emailService.SaveOrResolveImageAsync(_db, imageFile, imageUrl, imageAlt ?? existing.Name, userId.Value);
                if (img != null)
                {
                    existing.ImageId = img.Id;
                }

                await _db.SaveChangesAsync();
                TempData["FlashSuccess"] = $"Project '{existing.Name}' updated.";
                return RedirectToAction("ProjectDetails", new { projectId = existing.Id });
            }
            else
            {
                var proj = new Project
                {
                    Name = name.Trim(),
                    Fandom = fandom?.Trim(),
                    Character = character?.Trim(),
                    Description = description?.Trim(),
                    Progress = progress,
                    CreationDate = DateTime.UtcNow.Date,
                    CompletionDate = completionDate,
                    Budget = budget,
                    Status = status,
                    IsPublic = isPublic,
                    UserId = userId.Value
                };

                var img = await _emailService.SaveOrResolveImageAsync(_db, imageFile, imageUrl, imageAlt ?? proj.Name, userId.Value);
                if (img != null)
                {
                    proj.ImageId = img.Id;
                }

                _db.Projects.Add(proj);
                await _db.SaveChangesAsync();
                TempData["FlashSuccess"] = $"Project '{proj.Name}' created.";
                return RedirectToAction("ProjectDetails", new { projectId = proj.Id });
            }
        }

        [HttpPost("/projects/{projectId:int}/parts/form")]
        public async Task<IActionResult> SavePartForm(
            int projectId,
            int? id,
            string name,
            string? description,
            decimal price = 0m,
            decimal time = 0m,
            int progress = 0,
            string type = "Other",
            IFormFile? imageFile = null,
            string? imageUrl = null,
            string? imageAlt = null,
            int? linkedMaterialId = null)
        {
            int? userId = UserAccess.GetUserId(User);
            if (!userId.HasValue) return RedirectToAction("Index", "Login");

            var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == projectId);
            if (project == null) return RedirectToAction("Error", "Home", new { code = 404, message = "Parent project not found." });

            if (!UserAccess.CanManageResource(User, project.UserId))
            {
                return RedirectToAction("Error", "Home", new { code = 403, message = "You can only modify parts in your own projects." });
            }

            if (string.IsNullOrWhiteSpace(name) || price < 0 || time < 0 || progress < 0 || progress > 100)
            {
                TempData["FlashError"] = "Please ensure part name is provided, price/time >= 0, and progress is 0..100.";
                return RedirectToAction("ProjectDetails", new { projectId });
            }

            Part part;
            if (id.HasValue && id.Value > 0)
            {
                var existing = await _db.Parts.FirstOrDefaultAsync(pt => pt.Id == id.Value && pt.ProjectId == projectId);
                if (existing == null) return RedirectToAction("Error", "Home", new { code = 404, message = "Part not found in this project." });

                existing.Name = name.Trim();
                existing.Description = description?.Trim();
                existing.Price = price;
                existing.Time = time;
                existing.Progress = progress;
                existing.Type = type;
                part = existing;
            }
            else
            {
                part = new Part
                {
                    Name = name.Trim(),
                    Description = description?.Trim(),
                    Price = price,
                    Time = time,
                    Progress = progress,
                    Type = type,
                    ProjectId = projectId
                };
                _db.Parts.Add(part);
            }

            var img = await _emailService.SaveOrResolveImageAsync(_db, imageFile, imageUrl, imageAlt ?? part.Name, userId.Value);
            if (img != null)
            {
                part.ImageId = img.Id;
            }

            await _db.SaveChangesAsync();

            if (linkedMaterialId.HasValue && linkedMaterialId.Value > 0)
            {
                bool alreadyLinked = await _db.MaterialUses.AnyAsync(mu => mu.PartId == part.Id && mu.MaterialId == linkedMaterialId.Value);
                if (!alreadyLinked)
                {
                    _db.MaterialUses.Add(new MaterialUse { PartId = part.Id, MaterialId = linkedMaterialId.Value });
                    await _db.SaveChangesAsync();
                }
            }

            TempData["FlashSuccess"] = $"Part '{part.Name}' saved.";
            return RedirectToAction("ProjectDetails", new { projectId });
        }

        [HttpPost("/projects/{projectId:int}/parts/{partId:int}/tasks/form")]
        public async Task<IActionResult> SaveTaskForm(
            int projectId,
            int partId,
            int? id,
            string name,
            string? description,
            decimal time = 0m,
            string type = "Make",
            bool isCompleted = false)
        {
            if (!UserAccess.IsAuthenticated(User)) return RedirectToAction("Index", "Login");

            var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == projectId);
            var part = await _db.Parts.FirstOrDefaultAsync(pt => pt.Id == partId && pt.ProjectId == projectId);
            if (project == null || part == null)
            {
                return RedirectToAction("Error", "Home", new { code = 404, message = "Project or Part was not found." });
            }

            if (!UserAccess.CanManageResource(User, project.UserId))
            {
                return RedirectToAction("Error", "Home", new { code = 403, message = "You cannot modify tasks in another member's project." });
            }

            if (string.IsNullOrWhiteSpace(name) || time < 0)
            {
                TempData["FlashError"] = "Task name is required and time cannot be negative.";
                return RedirectToAction("PartTasks", new { projectId, partId });
            }

            if (id.HasValue && id.Value > 0)
            {
                var existing = await _db.Tasks.FirstOrDefaultAsync(t => t.Id == id.Value && t.PartId == partId);
                if (existing == null) return RedirectToAction("Error", "Home", new { code = 404, message = "Task not found." });

                existing.Name = name.Trim();
                existing.Description = description?.Trim();
                existing.Time = time;
                existing.Type = type;
                existing.CompletionDate = isCompleted ? (existing.CompletionDate ?? DateTime.UtcNow.Date) : null;
            }
            else
            {
                _db.Tasks.Add(new TaskModel
                {
                    Name = name.Trim(),
                    Description = description?.Trim(),
                    Time = time,
                    Type = type,
                    CompletionDate = isCompleted ? DateTime.UtcNow.Date : null,
                    PartId = partId
                });
            }

            await _db.SaveChangesAsync();
            TempData["FlashSuccess"] = $"Task '{name}' saved.";
            return RedirectToAction("PartTasks", new { projectId, partId });
        }

        [HttpPost("/projects/{projectId:int}/references/form")]
        public async Task<IActionResult> SaveReferenceForm(
            int projectId,
            string name,
            string? link,
            string type = "Other",
            IFormFile? imageFile = null,
            string? imageUrl = null,
            string? imageAlt = null)
        {
            int? userId = UserAccess.GetUserId(User);
            if (!userId.HasValue) return RedirectToAction("Index", "Login");

            var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == projectId);
            if (project == null) return RedirectToAction("Error", "Home", new { code = 404, message = "Project not found." });
            if (!UserAccess.CanManageResource(User, project.UserId))
                return RedirectToAction("Error", "Home", new { code = 403, message = "Forbidden." });

            if (string.IsNullOrWhiteSpace(name))
            {
                TempData["FlashError"] = "Reference name is required.";
                return RedirectToAction("ProjectDetails", new { projectId });
            }

            var reference = new Reference
            {
                Name = name.Trim(),
                Link = link?.Trim(),
                Type = type,
                ProjectId = projectId
            };

            _db.References.Add(reference);
            await _db.SaveChangesAsync();

            // Save optional Reference Image linked via Image.fk_Referenceid_Reference
            await _emailService.SaveOrResolveImageAsync(
                _db,
                imageFile,
                imageUrl,
                imageAlt ?? reference.Name,
                userId.Value,
                reference.Id);

            TempData["FlashSuccess"] = "Reference added to project.";
            return RedirectToAction("ProjectDetails", new { projectId });
        }

        [HttpPost("/tools/form")]
        [HttpPost("/workshop/tools/form")]
        public async Task<IActionResult> SaveToolForm(
            int? id,
            string name,
            string? description,
            decimal price = 0m,
            string toolType = "Other",
            IFormFile? imageFile = null,
            string? imageUrl = null,
            string? imageAlt = null)
        {
            int? userId = UserAccess.GetUserId(User);
            if (!userId.HasValue) return RedirectToAction("Index", "Login");

            if (string.IsNullOrWhiteSpace(name) || price < 0)
            {
                TempData["FlashError"] = "Tool name is required and price must be non-negative.";
                return RedirectToAction("Tools");
            }

            Tool tool;
            if (id.HasValue && id.Value > 0)
            {
                var existing = await _db.Tools.FirstOrDefaultAsync(t => t.Id == id.Value);
                if (existing == null) return RedirectToAction("Error", "Home", new { code = 404, message = "Tool not found." });
                if (!UserAccess.CanManageResource(User, existing.UserId))
                    return RedirectToAction("Error", "Home", new { code = 403, message = "Forbidden." });

                existing.Name = name.Trim();
                existing.Description = description?.Trim();
                existing.Price = price;
                existing.ToolType = toolType;
                tool = existing;
            }
            else
            {
                tool = new Tool
                {
                    Name = name.Trim(),
                    Description = description?.Trim(),
                    Price = price,
                    ToolType = toolType,
                    UserId = userId.Value
                };
                _db.Tools.Add(tool);
            }

            var img = await _emailService.SaveOrResolveImageAsync(_db, imageFile, imageUrl, imageAlt ?? tool.Name, userId.Value);
            if (img != null)
            {
                tool.ImageId = img.Id;
            }

            await _db.SaveChangesAsync();
            TempData["FlashSuccess"] = $"Tool '{tool.Name}' saved.";
            return RedirectToAction("Tools");
        }

        [HttpPost("/materials/form")]
        [HttpPost("/workshop/materials/form")]
        public async Task<IActionResult> SaveMaterialForm(
            int? id,
            string name,
            string? description,
            decimal price = 0m,
            decimal amount = 1m,
            string? source = null,
            DateTime? buyDate = null,
            string materialType = "Other",
            IFormFile? imageFile = null,
            string? imageUrl = null,
            string? imageAlt = null)
        {
            int? userId = UserAccess.GetUserId(User);
            if (!userId.HasValue) return RedirectToAction("Index", "Login");

            if (string.IsNullOrWhiteSpace(name) || price < 0 || amount < 0)
            {
                TempData["FlashError"] = "Material name is required and price/amount must be non-negative.";
                return RedirectToAction("Materials");
            }

            Material mat;
            if (id.HasValue && id.Value > 0)
            {
                var existing = await _db.Materials.FirstOrDefaultAsync(m => m.Id == id.Value);
                if (existing == null) return RedirectToAction("Error", "Home", new { code = 404, message = "Material not found." });

                existing.Name = name.Trim();
                existing.Description = description?.Trim();
                existing.Price = price;
                existing.Amount = amount;
                existing.Source = source?.Trim();
                existing.BuyDate = buyDate ?? existing.BuyDate;
                existing.MaterialType = materialType;
                mat = existing;
            }
            else
            {
                mat = new Material
                {
                    Name = name.Trim(),
                    Description = description?.Trim(),
                    Price = price,
                    Amount = amount,
                    Source = source?.Trim(),
                    BuyDate = buyDate ?? DateTime.UtcNow.Date,
                    MaterialType = materialType
                };
                _db.Materials.Add(mat);
            }

            var img = await _emailService.SaveOrResolveImageAsync(_db, imageFile, imageUrl, imageAlt ?? mat.Name, userId.Value);
            if (img != null)
            {
                mat.ImageId = img.Id;
            }

            await _db.SaveChangesAsync();
            TempData["FlashSuccess"] = $"Material '{mat.Name}' saved.";
            return RedirectToAction("Materials");
        }

        // ====================================================================
        // 3. REST API ENDPOINTS — PROJECTS
        // ====================================================================

        [HttpGet("/api/projects")]
        public async Task<IActionResult> ApiGetProjects(
            [FromQuery] string? search = null,
            [FromQuery] string? status = null,
            [FromQuery] string? fandom = null,
            [FromQuery] int? userId = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 6)
        {
            if (page <= 0 || pageSize <= 0 || pageSize > 100)
            {
                return StatusCode(StatusCodes.Status400BadRequest, new
                {
                    status = 400,
                    error = "Bad Request",
                    message = "Parameters 'page' and 'pageSize' must be between 1 and 100."
                });
            }

            int? currentUserId = UserAccess.GetUserId(User);
            bool isAdmin = UserAccess.IsAdmin(User);

            var query = _db.Projects
                .Include(p => p.CoverImage)
                .Include(p => p.Owner)
                .Include(p => p.Parts)
                .AsQueryable();

            if (!isAdmin)
            {
                if (currentUserId.HasValue)
                {
                    query = query.Where(p => p.IsPublic || p.UserId == currentUserId.Value);
                }
                else
                {
                    query = query.Where(p => p.IsPublic);
                }
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                string term = search.Trim();
                query = query.Where(p => p.Name.Contains(term) || (p.Fandom != null && p.Fandom.Contains(term)) || (p.Character != null && p.Character.Contains(term)));
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                query = query.Where(p => p.Status == status);
            }

            if (!string.IsNullOrWhiteSpace(fandom))
            {
                query = query.Where(p => p.Fandom != null && p.Fandom.Contains(fandom));
            }

            if (userId.HasValue && userId.Value > 0)
            {
                query = query.Where(p => p.UserId == userId.Value);
            }

            int totalItems = await query.CountAsync();
            var items = await query
                .OrderByDescending(p => p.CreationDate)
                .ThenByDescending(p => p.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(p => new
                {
                    id = p.Id,
                    name = p.Name,
                    fandom = p.Fandom,
                    character = p.Character,
                    description = p.Description,
                    progress = p.Progress,
                    creationDate = p.CreationDate.ToString("yyyy-MM-dd"),
                    completionDate = p.CompletionDate.HasValue ? p.CompletionDate.Value.ToString("yyyy-MM-dd") : null,
                    budget = p.Budget,
                    status = p.Status,
                    isPublic = p.IsPublic,
                    userId = p.UserId,
                    ownerUsername = p.Owner != null ? p.Owner.Username : null,
                    partsCount = p.Parts.Count,
                    hypermedia = new
                    {
                        imageUrl = p.CoverImage != null ? p.CoverImage.FilePath : null,
                        imageAlt = p.CoverImage != null ? p.CoverImage.AltText : null,
                        self = $"/api/projects/{p.Id}",
                        parts = $"/api/projects/{p.Id}/parts",
                        gui = $"/projects/{p.Id}"
                    }
                })
                .ToListAsync();

            var responsePayload = new
            {
                page,
                pageSize,
                totalItems,
                totalPages = Math.Max(1, (int)Math.Ceiling((double)totalItems / pageSize)),
                filters = new { search, status, fandom, userId },
                items
            };

            string etag = UserAccess.ComputeETag(responsePayload);
            Response.Headers.ETag = etag;
            if (Request.Headers.IfNoneMatch.Contains(etag))
            {
                return StatusCode(StatusCodes.Status304NotModified);
            }

            return Ok(responsePayload);
        }

        [HttpGet("/api/projects/{projectId:int}")]
        public async Task<IActionResult> ApiGetProjectById(int projectId)
        {
            if (projectId <= 0)
            {
                return BadRequest(new { status = 400, error = "Bad Request", message = "projectId must be a positive integer." });
            }

            var p = await _db.Projects
                .Include(x => x.CoverImage)
                .Include(x => x.Owner)
                .Include(x => x.Parts)
                .Include(x => x.References)
                    .ThenInclude(r => r.Images)
                .FirstOrDefaultAsync(x => x.Id == projectId);

            if (p == null)
            {
                return NotFound(new { status = 404, error = "Not Found", message = $"Project with ID {projectId} does not exist." });
            }

            if (!UserAccess.CanViewProject(User, p))
            {
                if (!UserAccess.IsAuthenticated(User))
                    return Unauthorized(new { status = 401, error = "Unauthorized", message = "Authentication required to view this private project." });

                return StatusCode(StatusCodes.Status403Forbidden, new { status = 403, error = "Forbidden", message = "This project is private." });
            }

            var payload = new
            {
                id = p.Id,
                name = p.Name,
                fandom = p.Fandom,
                character = p.Character,
                description = p.Description,
                progress = p.Progress,
                creationDate = p.CreationDate.ToString("yyyy-MM-dd"),
                completionDate = p.CompletionDate?.ToString("yyyy-MM-dd"),
                budget = p.Budget,
                status = p.Status,
                isPublic = p.IsPublic,
                userId = p.UserId,
                ownerUsername = p.Owner?.Username,
                references = p.References.Select(r => new
                {
                    r.Id,
                    r.Name,
                    r.Link,
                    r.Type,
                    images = r.Images.Select(i => new { i.Id, i.FilePath, i.AltText })
                }),
                hypermedia = new
                {
                    imageUrl = p.CoverImage?.FilePath,
                    imageAlt = p.CoverImage?.AltText,
                    self = $"/api/projects/{p.Id}",
                    parts = $"/api/projects/{p.Id}/parts",
                    gui = $"/projects/{p.Id}"
                }
            };

            string etag = UserAccess.ComputeETag(payload);
            Response.Headers.ETag = etag;
            if (Request.Headers.IfNoneMatch.Contains(etag))
            {
                return StatusCode(StatusCodes.Status304NotModified);
            }

            return Ok(payload);
        }

        [HttpPost("/api/projects")]
        public async Task<IActionResult> ApiCreateProject([FromBody] ProjectInputDto? dto)
        {
            int? currentUserId = UserAccess.GetUserId(User);
            if (!currentUserId.HasValue)
            {
                return Unauthorized(new { status = 401, error = "Unauthorized", message = "You must be logged in to create a project." });
            }

            if (dto == null)
            {
                return BadRequest(new { status = 400, error = "Bad Request", message = "Request body cannot be null." });
            }

            if (string.IsNullOrWhiteSpace(dto.Name) || dto.Name.Length > 127)
            {
                return UnprocessableEntity(new { status = 422, error = "Unprocessable Entity", message = "Project Name is required and must not exceed 127 characters." });
            }

            if (dto.Progress < 0 || dto.Progress > 100)
            {
                return UnprocessableEntity(new { status = 422, error = "Unprocessable Entity", message = "Progress must be between 0 and 100." });
            }

            if (dto.Budget.HasValue && dto.Budget.Value < 0)
            {
                return UnprocessableEntity(new { status = 422, error = "Unprocessable Entity", message = "Budget cannot be negative." });
            }

            string status = string.IsNullOrWhiteSpace(dto.Status) ? "Planned" : dto.Status.Trim();
            if (!await _db.ProjectStatuses.AnyAsync(s => s.Name == status))
            {
                return UnprocessableEntity(new { status = 422, error = "Unprocessable Entity", message = $"Status '{status}' is not in the ProjectStatus lookup table." });
            }

            DateTime creation = dto.CreationDate?.Date ?? DateTime.UtcNow.Date;
            if (dto.CompletionDate.HasValue && dto.CompletionDate.Value.Date < creation)
            {
                return UnprocessableEntity(new { status = 422, error = "Unprocessable Entity", message = "CompletionDate cannot be earlier than CreationDate." });
            }

            bool duplicate = await _db.Projects.AnyAsync(p => p.UserId == currentUserId.Value && p.Name == dto.Name.Trim());
            if (duplicate)
            {
                return Conflict(new { status = 409, error = "Conflict", message = $"You already have a project named '{dto.Name.Trim()}'." });
            }

            var project = new Project
            {
                Name = dto.Name.Trim(),
                Fandom = dto.Fandom?.Trim(),
                Character = dto.Character?.Trim(),
                Description = dto.Description?.Trim(),
                Progress = dto.Progress,
                CreationDate = creation,
                CompletionDate = dto.CompletionDate?.Date,
                Budget = dto.Budget,
                Status = status,
                IsPublic = dto.IsPublic,
                ImageId = dto.ImageId,
                UserId = currentUserId.Value
            };

            _db.Projects.Add(project);
            await _db.SaveChangesAsync();

            return Created($"/api/projects/{project.Id}", new
            {
                status = 201,
                id = project.Id,
                name = project.Name,
                statusValue = project.Status,
                isPublic = project.IsPublic,
                progress = project.Progress,
                hypermedia = new { self = $"/api/projects/{project.Id}", parts = $"/api/projects/{project.Id}/parts" }
            });
        }

        [HttpPut("/api/projects/{projectId:int}")]
        public async Task<IActionResult> ApiUpdateProject(int projectId, [FromBody] ProjectInputDto? dto)
        {
            if (!UserAccess.IsAuthenticated(User))
            {
                return Unauthorized(new { status = 401, error = "Unauthorized", message = "Authentication required." });
            }

            if (projectId <= 0 || dto == null)
            {
                return BadRequest(new { status = 400, error = "Bad Request", message = "Valid projectId and JSON body are required." });
            }

            var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == projectId);
            if (project == null)
            {
                return NotFound(new { status = 404, error = "Not Found", message = $"Project #{projectId} not found." });
            }

            if (!UserAccess.CanManageResource(User, project.UserId))
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { status = 403, error = "Forbidden", message = "You can only update your own projects." });
            }

            if (string.IsNullOrWhiteSpace(dto.Name) || dto.Progress < 0 || dto.Progress > 100 || (dto.Budget.HasValue && dto.Budget.Value < 0))
            {
                return UnprocessableEntity(new { status = 422, error = "Unprocessable Entity", message = "Validation failed: check Name, Progress (0..100), and Budget (>= 0)." });
            }

            if (!string.IsNullOrWhiteSpace(dto.Status))
            {
                if (!await _db.ProjectStatuses.AnyAsync(s => s.Name == dto.Status))
                {
                    return UnprocessableEntity(new { status = 422, error = "Unprocessable Entity", message = $"Unknown status '{dto.Status}'." });
                }
                project.Status = dto.Status;
            }

            project.Name = dto.Name.Trim();
            project.Fandom = dto.Fandom?.Trim();
            project.Character = dto.Character?.Trim();
            project.Description = dto.Description?.Trim();
            project.Progress = dto.Progress;
            project.Budget = dto.Budget;
            project.IsPublic = dto.IsPublic;
            project.CompletionDate = dto.CompletionDate?.Date;
            if (dto.ImageId.HasValue) project.ImageId = dto.ImageId;

            await _db.SaveChangesAsync();

            return Ok(new
            {
                status = 200,
                message = "Project updated successfully.",
                id = project.Id,
                name = project.Name,
                progress = project.Progress,
                isPublic = project.IsPublic,
                projectStatus = project.Status
            });
        }

        [HttpDelete("/api/projects/{projectId:int}")]
        public async Task<IActionResult> ApiDeleteProject(int projectId)
        {
            if (!UserAccess.IsAuthenticated(User))
            {
                return Unauthorized(new { status = 401, error = "Unauthorized", message = "Authentication required." });
            }

            if (projectId <= 0)
            {
                return BadRequest(new { status = 400, error = "Bad Request", message = "Invalid projectId." });
            }

            var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == projectId);
            if (project == null)
            {
                return NotFound(new { status = 404, error = "Not Found", message = $"Project #{projectId} not found." });
            }

            if (!UserAccess.CanManageResource(User, project.UserId))
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { status = 403, error = "Forbidden", message = "You can only delete your own projects." });
            }

            _db.Projects.Remove(project);
            await _db.SaveChangesAsync();
            return NoContent();
        }

        // ====================================================================
        // 4. REST API ENDPOINTS — PARTS
        // ====================================================================

        [HttpGet("/api/parts")]
        [HttpGet("/api/projects/{projectId:int}/parts")]
        public async Task<IActionResult> ApiGetParts(
            int? projectId = null,
            [FromQuery] string? search = null,
            [FromQuery] string? type = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10)
        {
            if ((projectId.HasValue && projectId.Value <= 0) || page <= 0 || pageSize <= 0 || pageSize > 100)
            {
                return BadRequest(new { status = 400, error = "Bad Request", message = "Invalid projectId or pagination parameters." });
            }

            if (projectId.HasValue)
            {
                var proj = await _db.Projects.FirstOrDefaultAsync(p => p.Id == projectId.Value);
                if (proj == null)
                {
                    return NotFound(new { status = 404, error = "Not Found", message = $"Parent Project #{projectId.Value} was not found." });
                }

                if (!UserAccess.CanViewProject(User, proj))
                {
                    if (!UserAccess.IsAuthenticated(User))
                        return Unauthorized(new { status = 401, error = "Unauthorized", message = "Authentication required to view parts of a private project." });
                    return StatusCode(StatusCodes.Status403Forbidden, new { status = 403, error = "Forbidden", message = "Parent project is private." });
                }
            }

            int? currentUserId = UserAccess.GetUserId(User);
            bool isAdmin = UserAccess.IsAdmin(User);

            var query = _db.Parts
                .Include(pt => pt.PartImage)
                .Include(pt => pt.Project)
                .Include(pt => pt.Tasks)
                .AsQueryable();

            if (!isAdmin)
            {
                if (currentUserId.HasValue)
                    query = query.Where(pt => pt.Project != null && (pt.Project.IsPublic || pt.Project.UserId == currentUserId.Value));
                else
                    query = query.Where(pt => pt.Project != null && pt.Project.IsPublic);
            }

            if (projectId.HasValue)
            {
                query = query.Where(pt => pt.ProjectId == projectId.Value);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                string term = search.Trim();
                query = query.Where(pt => pt.Name.Contains(term) || (pt.Description != null && pt.Description.Contains(term)));
            }

            if (!string.IsNullOrWhiteSpace(type))
            {
                query = query.Where(pt => pt.Type == type);
            }

            int totalItems = await query.CountAsync();
            var items = await query
                .OrderBy(pt => pt.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(pt => new
                {
                    id = pt.Id,
                    projectId = pt.ProjectId,
                    name = pt.Name,
                    description = pt.Description,
                    price = pt.Price,
                    time = pt.Time,
                    progress = pt.Progress,
                    type = pt.Type,
                    tasksCount = pt.Tasks.Count,
                    hypermedia = new
                    {
                        imageUrl = pt.PartImage != null ? pt.PartImage.FilePath : null,
                        self = $"/api/projects/{pt.ProjectId}/parts/{pt.Id}",
                        project = $"/api/projects/{pt.ProjectId}",
                        tasks = $"/api/projects/{pt.ProjectId}/parts/{pt.Id}/tasks"
                    }
                })
                .ToListAsync();

            var payload = new
            {
                projectId,
                page,
                pageSize,
                totalItems,
                totalPages = Math.Max(1, (int)Math.Ceiling((double)totalItems / pageSize)),
                items
            };

            string etag = UserAccess.ComputeETag(payload);
            Response.Headers.ETag = etag;
            if (Request.Headers.IfNoneMatch.Contains(etag))
            {
                return StatusCode(StatusCodes.Status304NotModified);
            }

            return Ok(payload);
        }

        [HttpGet("/api/parts/{partId:int}")]
        [HttpGet("/api/projects/{projectId:int}/parts/{partId:int}")]
        public async Task<IActionResult> ApiGetPartById(int partId, int? projectId = null)
        {
            if (partId <= 0 || (projectId.HasValue && projectId.Value <= 0))
            {
                return BadRequest(new { status = 400, error = "Bad Request", message = "IDs must be positive integers." });
            }

            var part = await _db.Parts
                .Include(pt => pt.PartImage)
                .Include(pt => pt.Project)
                .Include(pt => pt.Tasks)
                .Include(pt => pt.MaterialUses)
                    .ThenInclude(mu => mu.Material)
                .FirstOrDefaultAsync(pt => pt.Id == partId && (!projectId.HasValue || pt.ProjectId == projectId.Value));

            if (part == null || part.Project == null)
            {
                return NotFound(new { status = 404, error = "Not Found", message = projectId.HasValue
                    ? $"Part #{partId} was not found under Project #{projectId.Value}."
                    : $"Part #{partId} was not found." });
            }

            if (!UserAccess.CanViewProject(User, part.Project))
            {
                if (!UserAccess.IsAuthenticated(User))
                    return Unauthorized(new { status = 401, error = "Unauthorized", message = "Authentication required." });
                return StatusCode(StatusCodes.Status403Forbidden, new { status = 403, error = "Forbidden", message = "Parent project is private." });
            }

            return Ok(new
            {
                id = part.Id,
                projectId = part.ProjectId,
                name = part.Name,
                description = part.Description,
                price = part.Price,
                time = part.Time,
                progress = part.Progress,
                type = part.Type,
                materials = part.MaterialUses.Where(m => m.Material != null).Select(m => new { m.Material!.Id, m.Material.Name, m.Material.MaterialType, m.Material.Price }),
                hypermedia = new
                {
                    imageUrl = part.PartImage?.FilePath,
                    self = $"/api/projects/{part.ProjectId}/parts/{part.Id}",
                    project = $"/api/projects/{part.ProjectId}",
                    tasks = $"/api/projects/{part.ProjectId}/parts/{part.Id}/tasks"
                }
            });
        }

        [HttpPost("/api/parts")]
        [HttpPost("/api/projects/{projectId:int}/parts")]
        public async Task<IActionResult> ApiCreatePart([FromBody] PartInputDto? dto, int? projectId = null)
        {
            if (!UserAccess.IsAuthenticated(User))
            {
                return Unauthorized(new { status = 401, error = "Unauthorized", message = "Authentication required." });
            }

            if (dto == null)
            {
                return BadRequest(new { status = 400, error = "Bad Request", message = "Request body is required." });
            }

            int targetProjectId = projectId ?? dto.ProjectId;
            if (targetProjectId <= 0)
            {
                return BadRequest(new { status = 400, error = "Bad Request", message = "A positive projectId is required." });
            }

            var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == targetProjectId);
            if (project == null)
            {
                return NotFound(new { status = 404, error = "Not Found", message = $"Parent Project #{targetProjectId} does not exist." });
            }

            if (!UserAccess.CanManageResource(User, project.UserId))
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { status = 403, error = "Forbidden", message = "You cannot add parts to another member's project." });
            }

            if (string.IsNullOrWhiteSpace(dto.Name) || dto.Price < 0 || dto.Time < 0 || dto.Progress < 0 || dto.Progress > 100)
            {
                return UnprocessableEntity(new { status = 422, error = "Unprocessable Entity", message = "Invalid part fields: Name is required, Price/Time >= 0, Progress 0..100." });
            }

            string partType = string.IsNullOrWhiteSpace(dto.Type) ? "Other" : dto.Type.Trim();
            if (!await _db.PartTypes.AnyAsync(pt => pt.Name == partType))
            {
                return UnprocessableEntity(new { status = 422, error = "Unprocessable Entity", message = $"PartType '{partType}' does not exist in lookup table." });
            }

            var part = new Part
            {
                Name = dto.Name.Trim(),
                Description = dto.Description?.Trim(),
                Price = dto.Price,
                Time = dto.Time,
                Progress = dto.Progress,
                Type = partType,
                ImageId = dto.ImageId,
                ProjectId = targetProjectId
            };

            _db.Parts.Add(part);
            await _db.SaveChangesAsync();

            return Created($"/api/projects/{targetProjectId}/parts/{part.Id}", new
            {
                status = 201,
                id = part.Id,
                projectId = part.ProjectId,
                name = part.Name,
                type = part.Type,
                price = part.Price,
                progress = part.Progress,
                hypermedia = new
                {
                    self = $"/api/projects/{targetProjectId}/parts/{part.Id}",
                    tasks = $"/api/projects/{targetProjectId}/parts/{part.Id}/tasks"
                }
            });
        }

        [HttpPut("/api/parts/{partId:int}")]
        [HttpPut("/api/projects/{projectId:int}/parts/{partId:int}")]
        public async Task<IActionResult> ApiUpdatePart(int partId, [FromBody] PartInputDto? dto, int? projectId = null)
        {
            if (!UserAccess.IsAuthenticated(User))
            {
                return Unauthorized(new { status = 401, error = "Unauthorized", message = "Authentication required." });
            }

            if (partId <= 0 || dto == null)
            {
                return BadRequest(new { status = 400, error = "Bad Request", message = "Valid partId and request body are required." });
            }

            var part = await _db.Parts
                .Include(pt => pt.Project)
                .FirstOrDefaultAsync(pt => pt.Id == partId && (!projectId.HasValue || pt.ProjectId == projectId.Value));

            if (part == null || part.Project == null)
            {
                return NotFound(new { status = 404, error = "Not Found", message = $"Part #{partId} not found." });
            }

            if (!UserAccess.CanManageResource(User, part.Project.UserId))
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { status = 403, error = "Forbidden", message = "You do not own the parent project." });
            }

            if (string.IsNullOrWhiteSpace(dto.Name) || dto.Price < 0 || dto.Time < 0 || dto.Progress < 0 || dto.Progress > 100)
            {
                return UnprocessableEntity(new { status = 422, error = "Unprocessable Entity", message = "Invalid Part values." });
            }

            if (!string.IsNullOrWhiteSpace(dto.Type))
            {
                if (!await _db.PartTypes.AnyAsync(pt => pt.Name == dto.Type))
                {
                    return UnprocessableEntity(new { status = 422, error = "Unprocessable Entity", message = $"Unknown PartType '{dto.Type}'." });
                }
                part.Type = dto.Type;
            }

            part.Name = dto.Name.Trim();
            part.Description = dto.Description?.Trim();
            part.Price = dto.Price;
            part.Time = dto.Time;
            part.Progress = dto.Progress;
            if (dto.ImageId.HasValue) part.ImageId = dto.ImageId;

            await _db.SaveChangesAsync();
            return Ok(new { status = 200, message = "Part updated.", id = part.Id, name = part.Name, progress = part.Progress });
        }

        [HttpDelete("/api/parts/{partId:int}")]
        [HttpDelete("/api/projects/{projectId:int}/parts/{partId:int}")]
        public async Task<IActionResult> ApiDeletePart(int partId, int? projectId = null)
        {
            if (!UserAccess.IsAuthenticated(User))
            {
                return Unauthorized(new { status = 401, error = "Unauthorized", message = "Authentication required." });
            }

            if (partId <= 0)
            {
                return BadRequest(new { status = 400, error = "Bad Request", message = "Invalid partId." });
            }

            var part = await _db.Parts
                .Include(pt => pt.Project)
                .FirstOrDefaultAsync(pt => pt.Id == partId && (!projectId.HasValue || pt.ProjectId == projectId.Value));

            if (part == null || part.Project == null)
            {
                return NotFound(new { status = 404, error = "Not Found", message = $"Part #{partId} not found." });
            }

            if (!UserAccess.CanManageResource(User, part.Project.UserId))
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { status = 403, error = "Forbidden", message = "You cannot delete a part from another member's project." });
            }

            _db.Parts.Remove(part);
            await _db.SaveChangesAsync();
            return NoContent();
        }

        // ====================================================================
        // 5. REST API ENDPOINTS — TASKS
        // ====================================================================

        [HttpGet("/api/tasks")]
        [HttpGet("/api/projects/{projectId:int}/parts/{partId:int}/tasks")]
        public async Task<IActionResult> ApiGetTasks(
            int? projectId = null,
            int? partId = null,
            [FromQuery] string? search = null,
            [FromQuery] string? type = null,
            [FromQuery] bool? completed = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10)
        {
            if ((projectId.HasValue && projectId.Value <= 0) || (partId.HasValue && partId.Value <= 0) || page <= 0 || pageSize <= 0 || pageSize > 100)
            {
                return BadRequest(new { status = 400, error = "Bad Request", message = "Invalid IDs or pagination parameters." });
            }

            if (projectId.HasValue && partId.HasValue)
            {
                var part = await _db.Parts
                    .Include(pt => pt.Project)
                    .FirstOrDefaultAsync(pt => pt.Id == partId.Value && pt.ProjectId == projectId.Value);

                if (part == null || part.Project == null)
                {
                    return NotFound(new
                    {
                        status = 404,
                        error = "Not Found",
                        message = $"Part #{partId.Value} inside Project #{projectId.Value} was not found."
                    });
                }

                if (!UserAccess.CanViewProject(User, part.Project))
                {
                    if (!UserAccess.IsAuthenticated(User))
                        return Unauthorized(new { status = 401, error = "Unauthorized", message = "Authentication required." });
                    return StatusCode(StatusCodes.Status403Forbidden, new { status = 403, error = "Forbidden", message = "Parent project is private." });
                }
            }

            int? currentUserId = UserAccess.GetUserId(User);
            bool isAdmin = UserAccess.IsAdmin(User);

            var query = _db.Tasks
                .Include(t => t.Part)
                    .ThenInclude(pt => pt!.Project)
                .AsQueryable();

            if (!isAdmin)
            {
                if (currentUserId.HasValue)
                    query = query.Where(t => t.Part != null && t.Part.Project != null && (t.Part.Project.IsPublic || t.Part.Project.UserId == currentUserId.Value));
                else
                    query = query.Where(t => t.Part != null && t.Part.Project != null && t.Part.Project.IsPublic);
            }

            if (partId.HasValue)
            {
                query = query.Where(t => t.PartId == partId.Value);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                string term = search.Trim();
                query = query.Where(t => t.Name.Contains(term) || (t.Description != null && t.Description.Contains(term)));
            }

            if (!string.IsNullOrWhiteSpace(type))
            {
                query = query.Where(t => t.Type == type);
            }

            if (completed.HasValue)
            {
                query = completed.Value
                    ? query.Where(t => t.CompletionDate != null)
                    : query.Where(t => t.CompletionDate == null);
            }

            int totalItems = await query.CountAsync();
            var items = await query
                .OrderBy(t => t.CompletionDate.HasValue)
                .ThenBy(t => t.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(t => new
                {
                    id = t.Id,
                    partId = t.PartId,
                    projectId = t.Part != null ? t.Part.ProjectId : projectId,
                    name = t.Name,
                    description = t.Description,
                    time = t.Time,
                    type = t.Type,
                    isCompleted = t.CompletionDate.HasValue,
                    completionDate = t.CompletionDate.HasValue ? t.CompletionDate.Value.ToString("yyyy-MM-dd") : null,
                    hypermedia = new
                    {
                        self = t.Part != null
                            ? $"/api/projects/{t.Part.ProjectId}/parts/{t.PartId}/tasks/{t.Id}"
                            : $"/api/tasks/{t.Id}",
                        part = t.Part != null ? $"/api/projects/{t.Part.ProjectId}/parts/{t.PartId}" : null,
                        project = t.Part != null ? $"/api/projects/{t.Part.ProjectId}" : null
                    }
                })
                .ToListAsync();

            var payload = new
            {
                projectId,
                partId,
                page,
                pageSize,
                totalItems,
                totalPages = Math.Max(1, (int)Math.Ceiling((double)totalItems / pageSize)),
                items
            };

            string etag = UserAccess.ComputeETag(payload);
            Response.Headers.ETag = etag;
            if (Request.Headers.IfNoneMatch.Contains(etag))
            {
                return StatusCode(StatusCodes.Status304NotModified);
            }

            return Ok(payload);
        }

        [HttpGet("/api/tasks/{taskId:int}")]
        [HttpGet("/api/projects/{projectId:int}/parts/{partId:int}/tasks/{taskId:int}")]
        public async Task<IActionResult> ApiGetTaskById(int taskId, int? projectId = null, int? partId = null)
        {
            if (taskId <= 0 || (projectId.HasValue && projectId.Value <= 0) || (partId.HasValue && partId.Value <= 0))
            {
                return BadRequest(new { status = 400, error = "Bad Request", message = "All IDs must be positive integers." });
            }

            var task = await _db.Tasks
                .Include(t => t.Part)
                    .ThenInclude(pt => pt!.Project)
                .FirstOrDefaultAsync(t => t.Id == taskId);

            if (task == null || task.Part == null || task.Part.Project == null)
            {
                return NotFound(new { status = 404, error = "Not Found", message = $"Task #{taskId} was not found." });
            }

            if ((partId.HasValue && task.PartId != partId.Value) || (projectId.HasValue && task.Part.ProjectId != projectId.Value))
            {
                return NotFound(new
                {
                    status = 404,
                    error = "Not Found",
                    message = $"Task #{taskId} does not belong to Part #{partId} in Project #{projectId}."
                });
            }

            if (!UserAccess.CanViewProject(User, task.Part.Project))
            {
                if (!UserAccess.IsAuthenticated(User))
                    return Unauthorized(new { status = 401, error = "Unauthorized", message = "Authentication required." });
                return StatusCode(StatusCodes.Status403Forbidden, new { status = 403, error = "Forbidden", message = "Parent project is private." });
            }

            return Ok(new
            {
                id = task.Id,
                partId = task.PartId,
                projectId = task.Part.ProjectId,
                name = task.Name,
                description = task.Description,
                time = task.Time,
                type = task.Type,
                isCompleted = task.CompletionDate.HasValue,
                completionDate = task.CompletionDate?.ToString("yyyy-MM-dd"),
                hypermedia = new
                {
                    self = $"/api/projects/{task.Part.ProjectId}/parts/{task.PartId}/tasks/{task.Id}",
                    part = $"/api/projects/{task.Part.ProjectId}/parts/{task.PartId}",
                    project = $"/api/projects/{task.Part.ProjectId}"
                }
            });
        }

        [HttpPost("/api/tasks")]
        [HttpPost("/api/projects/{projectId:int}/parts/{partId:int}/tasks")]
        public async Task<IActionResult> ApiCreateTask([FromBody] TaskInputDto? dto, int? projectId = null, int? partId = null)
        {
            if (!UserAccess.IsAuthenticated(User))
            {
                return Unauthorized(new { status = 401, error = "Unauthorized", message = "Authentication required." });
            }

            if (dto == null)
            {
                return BadRequest(new { status = 400, error = "Bad Request", message = "Request body is required." });
            }

            int targetPartId = partId ?? dto.PartId;
            if (targetPartId <= 0)
            {
                return BadRequest(new { status = 400, error = "Bad Request", message = "A positive partId is required." });
            }

            var part = await _db.Parts
                .Include(pt => pt.Project)
                .FirstOrDefaultAsync(pt => pt.Id == targetPartId && (!projectId.HasValue || pt.ProjectId == projectId.Value));

            if (part == null || part.Project == null)
            {
                return NotFound(new { status = 404, error = "Not Found", message = $"Target Part #{targetPartId} not found." });
            }

            if (!UserAccess.CanManageResource(User, part.Project.UserId))
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { status = 403, error = "Forbidden", message = "You cannot add tasks to another member's project." });
            }

            if (string.IsNullOrWhiteSpace(dto.Name) || dto.Time < 0)
            {
                return UnprocessableEntity(new { status = 422, error = "Unprocessable Entity", message = "Task Name is required and Time must be >= 0." });
            }

            string taskType = string.IsNullOrWhiteSpace(dto.Type) ? "Make" : dto.Type.Trim();
            if (!await _db.TaskTypes.AnyAsync(tt => tt.Name == taskType))
            {
                return UnprocessableEntity(new { status = 422, error = "Unprocessable Entity", message = $"TaskType '{taskType}' is not in the lookup table." });
            }

            var task = new TaskModel
            {
                Name = dto.Name.Trim(),
                Description = dto.Description?.Trim(),
                Time = dto.Time,
                CompletionDate = dto.CompletionDate?.Date,
                Type = taskType,
                PartId = targetPartId
            };

            _db.Tasks.Add(task);
            await _db.SaveChangesAsync();

            return Created($"/api/projects/{part.ProjectId}/parts/{part.Id}/tasks/{task.Id}", new
            {
                status = 201,
                id = task.Id,
                partId = part.Id,
                projectId = part.ProjectId,
                name = task.Name,
                type = task.Type,
                time = task.Time,
                isCompleted = task.CompletionDate.HasValue
            });
        }

        [HttpPut("/api/tasks/{taskId:int}")]
        [HttpPut("/api/projects/{projectId:int}/parts/{partId:int}/tasks/{taskId:int}")]
        public async Task<IActionResult> ApiUpdateTask(int taskId, [FromBody] TaskInputDto? dto, int? projectId = null, int? partId = null)
        {
            if (!UserAccess.IsAuthenticated(User))
            {
                return Unauthorized(new { status = 401, error = "Unauthorized", message = "Authentication required." });
            }

            if (taskId <= 0 || dto == null)
            {
                return BadRequest(new { status = 400, error = "Bad Request", message = "Valid taskId and body are required." });
            }

            var task = await _db.Tasks
                .Include(t => t.Part)
                    .ThenInclude(pt => pt!.Project)
                .FirstOrDefaultAsync(t => t.Id == taskId);

            if (task == null || task.Part == null || task.Part.Project == null)
            {
                return NotFound(new { status = 404, error = "Not Found", message = $"Task #{taskId} not found." });
            }

            if ((partId.HasValue && task.PartId != partId.Value) || (projectId.HasValue && task.Part.ProjectId != projectId.Value))
            {
                return NotFound(new { status = 404, error = "Not Found", message = "Task does not match specified Project/Part." });
            }

            if (!UserAccess.CanManageResource(User, task.Part.Project.UserId))
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { status = 403, error = "Forbidden", message = "You do not own the parent project." });
            }

            if (string.IsNullOrWhiteSpace(dto.Name) || dto.Time < 0)
            {
                return UnprocessableEntity(new { status = 422, error = "Unprocessable Entity", message = "Task Name is required and Time must be >= 0." });
            }

            if (!string.IsNullOrWhiteSpace(dto.Type))
            {
                if (!await _db.TaskTypes.AnyAsync(tt => tt.Name == dto.Type))
                {
                    return UnprocessableEntity(new { status = 422, error = "Unprocessable Entity", message = $"Unknown TaskType '{dto.Type}'." });
                }
                task.Type = dto.Type;
            }

            task.Name = dto.Name.Trim();
            task.Description = dto.Description?.Trim();
            task.Time = dto.Time;
            task.CompletionDate = dto.CompletionDate?.Date;

            await _db.SaveChangesAsync();
            return Ok(new
            {
                status = 200,
                message = "Task updated.",
                id = task.Id,
                name = task.Name,
                isCompleted = task.CompletionDate.HasValue,
                completionDate = task.CompletionDate?.ToString("yyyy-MM-dd")
            });
        }

        [HttpDelete("/api/tasks/{taskId:int}")]
        [HttpDelete("/api/projects/{projectId:int}/parts/{partId:int}/tasks/{taskId:int}")]
        public async Task<IActionResult> ApiDeleteTask(int taskId, int? projectId = null, int? partId = null)
        {
            if (!UserAccess.IsAuthenticated(User))
            {
                return Unauthorized(new { status = 401, error = "Unauthorized", message = "Authentication required." });
            }

            if (taskId <= 0)
            {
                return BadRequest(new { status = 400, error = "Bad Request", message = "Invalid taskId." });
            }

            var task = await _db.Tasks
                .Include(t => t.Part)
                    .ThenInclude(pt => pt!.Project)
                .FirstOrDefaultAsync(t => t.Id == taskId);

            if (task == null || task.Part == null || task.Part.Project == null)
            {
                return NotFound(new { status = 404, error = "Not Found", message = $"Task #{taskId} not found." });
            }

            if ((partId.HasValue && task.PartId != partId.Value) || (projectId.HasValue && task.Part.ProjectId != projectId.Value))
            {
                return NotFound(new { status = 404, error = "Not Found", message = "Task does not belong to specified Project/Part." });
            }

            if (!UserAccess.CanManageResource(User, task.Part.Project.UserId))
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { status = 403, error = "Forbidden", message = "You do not own the parent project." });
            }

            _db.Tasks.Remove(task);
            await _db.SaveChangesAsync();
            return NoContent();
        }

        // ====================================================================
        // 6. COMPOSITE DASHBOARD, IMAGES, TOOLS, MATERIALS & STATUS ENDPOINTS
        // ====================================================================

        [HttpGet("/api/dashboard")]
        [HttpGet("/api/dashboard/{userId:int}")]
        public async Task<IActionResult> ApiGetCompositeDashboard(int? userId = null)
        {
            if (!UserAccess.IsAuthenticated(User))
            {
                return Unauthorized(new { status = 401, error = "Unauthorized", message = "Authentication required to access dashboard data." });
            }

            int currentUserId = UserAccess.GetUserId(User)!.Value;
            int targetId = (UserAccess.IsAdmin(User) && userId.HasValue) ? userId.Value : currentUserId;

            User? user = await _db.Users.Include(u => u.AvatarImage).FirstOrDefaultAsync(u => u.Id == targetId);
            if (user == null)
            {
                return NotFound(new { status = 404, error = "Not Found", message = "Target user profile was not found." });
            }

            var projects = await _db.Projects
                .Include(p => p.CoverImage)
                .Include(p => p.Parts)
                    .ThenInclude(pt => pt.Tasks)
                .Where(p => p.UserId == user.Id)
                .ToListAsync();

            var tools = await _db.Tools.Include(t => t.ToolImage).Where(t => t.UserId == user.Id).ToListAsync();
            var materials = await _db.Materials.Include(m => m.MaterialImage).ToListAsync();

            var allParts = projects.SelectMany(p => p.Parts).ToList();
            var allTasks = allParts.SelectMany(pt => pt.Tasks).ToList();

            return Ok(new
            {
                user = new
                {
                    id = user.Id,
                    username = user.Username,
                    role = user.RoleName,
                    language = user.Language,
                    currency = user.Currency,
                    avatarUrl = user.AvatarImage?.FilePath
                },
                metrics = new
                {
                    totalProjects = projects.Count,
                    activeProjects = projects.Count(p => p.Status == "Active"),
                    completedProjects = projects.Count(p => p.Status == "Completed"),
                    totalParts = allParts.Count,
                    totalTasks = allTasks.Count,
                    completedTasks = allTasks.Count(t => t.CompletionDate.HasValue),
                    totalPlannedBudget = projects.Sum(p => p.Budget ?? 0m),
                    totalPartsSpend = allParts.Sum(pt => pt.Price),
                    totalToolsInvestment = tools.Sum(t => t.Price),
                    totalHoursSpent = allParts.Sum(pt => pt.Time) + allTasks.Sum(t => t.Time)
                },
                projects = projects.Select(p => new
                {
                    p.Id,
                    p.Name,
                    p.Status,
                    p.IsPublic,
                    p.Progress,
                    p.Budget,
                    imageUrl = p.CoverImage?.FilePath,
                    partsCount = p.Parts.Count
                }),
                tools = tools.Select(t => new { t.Id, t.Name, t.ToolType, t.Price, imageUrl = t.ToolImage?.FilePath }),
                materialsCount = materials.Count
            });
        }

        [HttpPost("/api/images")]
        public async Task<IActionResult> ApiUploadImage(IFormFile? file, [FromForm] string? altText = null)
        {
            int? userId = UserAccess.GetUserId(User);
            if (!userId.HasValue)
            {
                return Unauthorized(new { status = 401, error = "Unauthorized", message = "Sign in to upload images." });
            }

            if (file == null || file.Length == 0)
            {
                return BadRequest(new { status = 400, error = "Bad Request", message = "No image file provided." });
            }

            if (!ImageNameHasher.IsAllowedExtension(file.FileName))
            {
                return UnprocessableEntity(new { status = 422, error = "Unprocessable Entity", message = "Unsupported file format. Allowed: jpg, jpeg, png, webp, gif, bmp, svg." });
            }

            var img = await _emailService.SaveUploadedImageAsync(_db, file, altText, userId.Value);
            return Created($"/api/images/{img.Id}", new
            {
                status = 201,
                id = img.Id,
                filePath = img.FilePath,
                altText = img.AltText,
                uploadDate = img.UploadDate
            });
        }

        [HttpGet("/api/lookups")]
        public async Task<IActionResult> ApiGetLookups()
        {
            return Ok(new
            {
                taskTypes = await _db.TaskTypes.OrderBy(x => x.Name).Select(x => x.Name).ToListAsync(),
                projectStatuses = await _db.ProjectStatuses.OrderBy(x => x.Name).Select(x => x.Name).ToListAsync(),
                toolTypes = await _db.ToolTypes.OrderBy(x => x.Name).Select(x => x.Name).ToListAsync(),
                referenceTypes = await _db.ReferenceTypes.OrderBy(x => x.Name).Select(x => x.Name).ToListAsync(),
                materialTypes = await _db.MaterialTypes.OrderBy(x => x.Name).Select(x => x.Name).ToListAsync(),
                partTypes = await _db.PartTypes.OrderBy(x => x.Name).Select(x => x.Name).ToListAsync(),
                languages = await _db.Languages.OrderBy(x => x.Code).ToListAsync(),
                currencies = await _db.Currencies.OrderBy(x => x.Code).ToListAsync()
            });
        }

        [HttpPost("/api/lookups/{category}")]
        public async Task<IActionResult> ApiAddLookup(string category, [FromBody] LookupInputDto? dto)
        {
            if (!UserAccess.IsAuthenticated(User))
                return Unauthorized(new { status = 401, error = "Unauthorized", message = "Authentication required." });

            if (!UserAccess.IsAdmin(User))
                return StatusCode(StatusCodes.Status403Forbidden, new { status = 403, error = "Forbidden", message = "Only Administrators can extend lookup tables." });

            if (dto == null || string.IsNullOrWhiteSpace(dto.Name))
                return UnprocessableEntity(new { status = 422, error = "Unprocessable Entity", message = "Lookup Name is required." });

            string cleanName = dto.Name.Trim();

            switch (category.ToLowerInvariant())
            {
                case "tasktype":
                    if (await _db.TaskTypes.AnyAsync(x => x.Name == cleanName))
                        return Conflict(new { status = 409, error = "Conflict", message = $"'{cleanName}' already exists in TaskType." });
                    _db.TaskTypes.Add(new TaskTypeLookup { Name = cleanName });
                    break;
                case "projectstatus":
                    if (await _db.ProjectStatuses.AnyAsync(x => x.Name == cleanName))
                        return Conflict(new { status = 409, error = "Conflict", message = $"'{cleanName}' already exists in ProjectStatus." });
                    _db.ProjectStatuses.Add(new ProjectStatusLookup { Name = cleanName });
                    break;
                case "tooltype":
                    if (await _db.ToolTypes.AnyAsync(x => x.Name == cleanName))
                        return Conflict(new { status = 409, error = "Conflict", message = $"'{cleanName}' already exists in ToolType." });
                    _db.ToolTypes.Add(new ToolTypeLookup { Name = cleanName });
                    break;
                case "referencetype":
                    if (await _db.ReferenceTypes.AnyAsync(x => x.Name == cleanName))
                        return Conflict(new { status = 409, error = "Conflict", message = $"'{cleanName}' already exists in ReferenceType." });
                    _db.ReferenceTypes.Add(new ReferenceTypeLookup { Name = cleanName });
                    break;
                case "materialtype":
                    if (await _db.MaterialTypes.AnyAsync(x => x.Name == cleanName))
                        return Conflict(new { status = 409, error = "Conflict", message = $"'{cleanName}' already exists in MaterialType." });
                    _db.MaterialTypes.Add(new MaterialTypeLookup { Name = cleanName });
                    break;
                case "parttype":
                    if (await _db.PartTypes.AnyAsync(x => x.Name == cleanName))
                        return Conflict(new { status = 409, error = "Conflict", message = $"'{cleanName}' already exists in PartType." });
                    _db.PartTypes.Add(new PartTypeLookup { Name = cleanName });
                    break;
                default:
                    return BadRequest(new { status = 400, error = "Bad Request", message = $"Unknown lookup category '{category}'." });
            }

            await _db.SaveChangesAsync();
            return Created($"/api/lookups/{category}", new { status = 201, category, name = cleanName });
        }

        [HttpGet("/api/tools")]
        public async Task<IActionResult> ApiGetTools()
        {
            if (!UserAccess.IsAuthenticated(User))
                return Unauthorized(new { status = 401, error = "Unauthorized" });
            var items = await _db.Tools.Include(t => t.ToolImage).ToListAsync();
            return Ok(items);
        }

        [HttpDelete("/api/tools/{toolId:int}")]
        public async Task<IActionResult> ApiDeleteTool(int toolId)
        {
            if (!UserAccess.IsAuthenticated(User)) return Unauthorized(new { status = 401, error = "Unauthorized" });
            var tool = await _db.Tools.FirstOrDefaultAsync(t => t.Id == toolId);
            if (tool == null) return NotFound(new { status = 404, error = "Not Found" });
            if (!UserAccess.CanManageResource(User, tool.UserId)) return StatusCode(403, new { status = 403, error = "Forbidden" });

            _db.Tools.Remove(tool);
            await _db.SaveChangesAsync();
            return NoContent();
        }

        [HttpGet("/api/materials")]
        public async Task<IActionResult> ApiGetMaterials()
        {
            if (!UserAccess.IsAuthenticated(User))
                return Unauthorized(new { status = 401, error = "Unauthorized" });
            var items = await _db.Materials.Include(m => m.MaterialImage).ToListAsync();
            return Ok(items);
        }

        [HttpDelete("/api/materials/{materialId:int}")]
        public async Task<IActionResult> ApiDeleteMaterial(int materialId)
        {
            if (!UserAccess.IsAuthenticated(User)) return Unauthorized(new { status = 401, error = "Unauthorized" });
            var mat = await _db.Materials.FirstOrDefaultAsync(m => m.Id == materialId);
            if (mat == null) return NotFound(new { status = 404, error = "Not Found" });

            _db.Materials.Remove(mat);
            await _db.SaveChangesAsync();
            return NoContent();
        }

        [HttpDelete("/api/projects/{projectId:int}/references/{refId:int}")]
        public async Task<IActionResult> ApiDeleteReference(int projectId, int refId)
        {
            if (!UserAccess.IsAuthenticated(User)) return Unauthorized(new { status = 401, error = "Unauthorized" });
            var r = await _db.References.Include(x => x.Project).FirstOrDefaultAsync(x => x.Id == refId && x.ProjectId == projectId);
            if (r == null || r.Project == null) return NotFound(new { status = 404, error = "Not Found" });
            if (!UserAccess.CanManageResource(User, r.Project.UserId)) return StatusCode(403, new { status = 403, error = "Forbidden" });

            _db.References.Remove(r);
            await _db.SaveChangesAsync();
            return NoContent();
        }

        [HttpGet("/api/status/{code:int}")]
        public IActionResult ApiVerifyStatusCode(int code)
        {
            return code switch
            {
                200 => Ok(new { status = 200, title = "200 OK", description = "Standard successful GET/PUT response." }),
                201 => Created("/api/projects/1", new { status = 201, title = "201 Created", description = "Resource created successfully with Location header." }),
                204 => NoContent(),
                302 => Redirect("/projects"),
                304 => StatusCode(StatusCodes.Status304NotModified),
                400 => BadRequest(new { status = 400, title = "400 Bad Request", description = "Invalid query parameter or malformed request syntax." }),
                401 => Unauthorized(new { status = 401, error = "401 Unauthorized", description = "Authentication required." }),
                403 => StatusCode(StatusCodes.Status403Forbidden, new { status = 403, title = "403 Forbidden", description = "Insufficient permissions for this resource." }),
                404 => NotFound(new { status = 404, title = "404 Not Found", description = "Requested resource does not exist." }),
                405 => StatusCode(StatusCodes.Status405MethodNotAllowed, new { status = 405, title = "405 Method Not Allowed", description = "HTTP verb is not supported on this URL." }),
                408 => StatusCode(StatusCodes.Status408RequestTimeout, new { status = 408, title = "408 Request Timeout", description = "The operation exceeded the configured timeout window." }),
                409 => Conflict(new { status = 409, title = "409 Conflict", description = "Duplicate resource name, username/email, or state conflict." }),
                422 => UnprocessableEntity(new { status = 422, title = "422 Unprocessable Entity", description = "Validation error on submitted fields." }),
                _ => BadRequest(new { status = 400, title = "400 Bad Request", description = "Unsupported status code requested." })
            };
        }

        private async Task<ExplorerViewModel> CreateBaseExplorerViewModelAsync(string activeView)
        {
            return new ExplorerViewModel
            {
                ActiveView = activeView,
                ProjectStatuses = await _db.ProjectStatuses.OrderBy(x => x.Name).Select(x => x.Name).ToListAsync(),
                PartTypes = await _db.PartTypes.OrderBy(x => x.Name).Select(x => x.Name).ToListAsync(),
                TaskTypes = await _db.TaskTypes.OrderBy(x => x.Name).Select(x => x.Name).ToListAsync(),
                ToolTypes = await _db.ToolTypes.OrderBy(x => x.Name).Select(x => x.Name).ToListAsync(),
                MaterialTypes = await _db.MaterialTypes.OrderBy(x => x.Name).Select(x => x.Name).ToListAsync(),
                ReferenceTypes = await _db.ReferenceTypes.OrderBy(x => x.Name).Select(x => x.Name).ToListAsync()
            };
        }
    }
}
