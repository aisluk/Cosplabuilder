namespace Cosplabuilder.Models
{
    public class ErrorViewModel
    {
        public string? RequestId { get; set; }
        public int StatusCode { get; set; } = 400;
        public string Title { get; set; } = "Request Error";
        public string Message { get; set; } = string.Empty;

        public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);
    }

    public class ProjectInputDto
    {
        public string Name { get; set; } = string.Empty;
        public string? Fandom { get; set; }
        public string? Character { get; set; }
        public string? Description { get; set; }
        public int Progress { get; set; } = 0;
        public DateTime? CreationDate { get; set; }
        public DateTime? CompletionDate { get; set; }
        public decimal? Budget { get; set; }
        public string Status { get; set; } = "Planned";
        public bool IsPublic { get; set; } = true;
        public int? ImageId { get; set; }
        public int? UserId { get; set; }
    }

    public class PartInputDto
    {
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public decimal Price { get; set; } = 0m;
        public decimal Time { get; set; } = 0m;
        public int Progress { get; set; } = 0;
        public string Type { get; set; } = "Other";
        public int? ImageId { get; set; }
        public int ProjectId { get; set; }
    }

    public class TaskInputDto
    {
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public decimal Time { get; set; } = 0m;
        public DateTime? CompletionDate { get; set; }
        public string Type { get; set; } = "Make";
        public int PartId { get; set; }
    }

    public class ToolInputDto
    {
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public decimal Price { get; set; } = 0m;
        public string ToolType { get; set; } = "Other";
        public int? ImageId { get; set; }
    }

    public class MaterialInputDto
    {
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public decimal Price { get; set; } = 0m;
        public decimal Amount { get; set; } = 0m;
        public string? Source { get; set; }
        public DateTime? BuyDate { get; set; }
        public string MaterialType { get; set; } = "Other";
        public int? ImageId { get; set; }
    }

    public class ReferenceInputDto
    {
        public string Name { get; set; } = string.Empty;
        public string? Link { get; set; }
        public string Type { get; set; } = "Other";
        public int ProjectId { get; set; }
    }

    public class LookupInputDto
    {
        public string? Code { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    public class DashboardCompositeViewModel
    {
        public User ProfileUser { get; set; } = new();
        public List<Project> Projects { get; set; } = new();
        public List<Part> Parts { get; set; } = new();
        public List<Task> Tasks { get; set; } = new();
        public List<Tool> Tools { get; set; } = new();
        public List<Material> Materials { get; set; } = new();
        public List<Image> UploadedImages { get; set; } = new();

        public int TotalProjects => Projects.Count;
        public int ActiveProjects => Projects.Count(p => p.Status == "Active");
        public int CompletedProjects => Projects.Count(p => p.Status == "Completed");
        public int TotalParts => Parts.Count;
        public int TotalTasks => Tasks.Count;
        public int CompletedTasks => Tasks.Count(t => t.CompletionDate.HasValue);
        public decimal TotalPlannedBudget => Projects.Sum(p => p.Budget ?? 0m);
        public decimal TotalPartsCost => Parts.Sum(p => p.Price);
        public decimal TotalToolsCost => Tools.Sum(t => t.Price);
        public decimal TotalMaterialsCost => Materials.Sum(m => m.Price);
        public decimal TotalHoursLogged => Parts.Sum(p => p.Time) + Tasks.Sum(t => t.Time);
        public int AverageProgress => Projects.Count > 0 ? (int)Math.Round(Projects.Average(p => p.Progress)) : 0;

        public List<string> Languages { get; set; } = new();
        public List<string> Currencies { get; set; } = new();
        public List<string> ProjectStatuses { get; set; } = new();
        public List<string> PartTypes { get; set; } = new();
        public List<string> TaskTypes { get; set; } = new();
        public List<string> ToolTypes { get; set; } = new();
        public List<string> MaterialTypes { get; set; } = new();
        public List<string> ReferenceTypes { get; set; } = new();
        public List<User> AllUsers { get; set; } = new();
    }

    public class ExplorerViewModel
    {
        public string ActiveView { get; set; } = "Projects";
        public Project? CurrentProject { get; set; }
        public Part? CurrentPart { get; set; }

        public List<Project> Projects { get; set; } = new();
        public List<Part> Parts { get; set; } = new();
        public List<Task> Tasks { get; set; } = new();
        public List<Tool> Tools { get; set; } = new();
        public List<Material> Materials { get; set; } = new();
        public List<Reference> References { get; set; } = new();

        public int CurrentPage { get; set; } = 1;
        public int PageSize { get; set; } = 6;
        public int TotalItems { get; set; } = 0;
        public int TotalPages => PageSize > 0 ? Math.Max(1, (int)Math.Ceiling((double)TotalItems / PageSize)) : 1;

        public string? SearchQuery { get; set; }
        public string? FilterStatus { get; set; }
        public string? FilterType { get; set; }
        public string? SortBy { get; set; }

        public List<string> ProjectStatuses { get; set; } = new();
        public List<string> PartTypes { get; set; } = new();
        public List<string> TaskTypes { get; set; } = new();
        public List<string> ToolTypes { get; set; } = new();
        public List<string> MaterialTypes { get; set; } = new();
        public List<string> ReferenceTypes { get; set; } = new();
    }
}
