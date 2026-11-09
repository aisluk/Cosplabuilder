using Cosplabuilder.Helpers;
using Cosplabuilder.Models;
using Cosplabuilder.Services;
using Cosplabuilder.Views.Data;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// 1. Configure MySQL Database Context (utf8mb4_lithuanian_ci)
string connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Server=localhost;Port=3306;Database=cosplabuilder;Uid=root;Pwd=;CharSet=utf8mb4;";

builder.Services.AddDbContext<AppDBContext>(options =>
{
    var serverVersion = new MySqlServerVersion(new Version(8, 0, 30));
    options.UseMySql(connectionString, serverVersion);
});

// 2. Register Application Services
builder.Services.AddScoped<EmailService>();

// 3. Configure Cookie Authentication & 3-Role Authorization (Guest, Member, Admin)
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/login";
        options.LogoutPath = "/logout";
        options.AccessDeniedPath = "/Home/Error?code=403";
        options.Cookie.Name = "Cosplabuilder.Auth";
        options.Cookie.HttpOnly = true;
        options.ExpireTimeSpan = TimeSpan.FromDays(7);
    });

builder.Services.AddAuthorization();

builder.Services.AddControllersWithViews()
    .AddJsonOptions(opts =>
    {
        opts.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    });

var app = builder.Build();

// 4. Ensure initial lookup data & hypermedia directories exist
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDBContext>();
    await EmailService.EnsureSeedDataAsync(db, app.Environment);
}

// 5. Global Exception & Status Code Shield Middleware (Strictly prevents HTTP 500)
app.Use(async (context, next) =>
{
    try
    {
        await next();

        if (context.Response.StatusCode >= 500)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
        }

        if (!context.Response.HasStarted && context.Request.Path.StartsWithSegments("/api"))
        {
            if (context.Response.StatusCode == StatusCodes.Status404NotFound)
            {
                context.Response.ContentType = "application/json; charset=utf-8";
                await context.Response.WriteAsJsonAsync(new
                {
                    status = 404,
                    error = "Not Found",
                    message = $"Endpoint or resource '{context.Request.Path}' was not found."
                });
            }
            else if (context.Response.StatusCode == StatusCodes.Status405MethodNotAllowed)
            {
                context.Response.ContentType = "application/json; charset=utf-8";
                await context.Response.WriteAsJsonAsync(new
                {
                    status = 405,
                    error = "Method Not Allowed",
                    message = $"HTTP method '{context.Request.Method}' is not supported for '{context.Request.Path}'."
                });
            }
        }
    }
    catch (Exception ex)
    {
        if (context.Response.HasStarted)
        {
            return;
        }

        int mappedStatus = ex switch
        {
            OperationCanceledException or TimeoutException => StatusCodes.Status408RequestTimeout,
            DbUpdateException => StatusCodes.Status409Conflict,
            JsonException or FormatException or ArgumentException => StatusCodes.Status400BadRequest,
            UnauthorizedAccessException => StatusCodes.Status403Forbidden,
            _ => StatusCodes.Status422UnprocessableEntity
        };

        context.Response.StatusCode = mappedStatus;

        if (context.Request.Path.StartsWithSegments("/api"))
        {
            context.Response.ContentType = "application/json; charset=utf-8";
            await context.Response.WriteAsJsonAsync(new
            {
                status = mappedStatus,
                error = mappedStatus switch
                {
                    400 => "Bad Request",
                    403 => "Forbidden",
                    408 => "Request Timeout",
                    409 => "Conflict",
                    _ => "Unprocessable Entity"
                },
                message = ex.Message
            });
        }
        else
        {
            context.Response.Redirect($"/Home/Error?code={mappedStatus}");
        }
    }
});

app.UseHttpsRedirection();
app.UseStaticFiles();

// Also serve images stored inside the project's Media/ directory at /Media/...
string mediaPath = Path.Combine(app.Environment.ContentRootPath, "Media");
Directory.CreateDirectory(mediaPath);
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(mediaPath),
    RequestPath = "/Media"
});

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();
