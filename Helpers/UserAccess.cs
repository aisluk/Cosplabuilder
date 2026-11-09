using Cosplabuilder.Models;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Cosplabuilder.Helpers
{
    public static class UserRoles
    {
        public const string Guest = "Guest";
        public const string Member = "Member";
        public const string Admin = "Admin";
    }

    public static class UserAccess
    {
        public static bool IsAuthenticated(ClaimsPrincipal user)
        {
            return user?.Identity?.IsAuthenticated == true;
        }

        public static int? GetUserId(ClaimsPrincipal user)
        {
            if (!IsAuthenticated(user))
                return null;

            string? val = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (int.TryParse(val, out int id))
                return id;

            return null;
        }

        public static string GetUsername(ClaimsPrincipal user)
        {
            if (!IsAuthenticated(user))
                return "Guest";

            return user.FindFirstValue(ClaimTypes.Name) ?? "Member";
        }

        public static string GetRole(ClaimsPrincipal user)
        {
            if (!IsAuthenticated(user))
                return UserRoles.Guest;

            return user.FindFirstValue(ClaimTypes.Role) ?? UserRoles.Member;
        }

        public static bool IsAdmin(ClaimsPrincipal user)
        {
            return IsAuthenticated(user) &&
                   string.Equals(GetRole(user), UserRoles.Admin, StringComparison.OrdinalIgnoreCase);
        }

        public static bool CanManageResource(ClaimsPrincipal user, int resourceOwnerUserId)
        {
            if (!IsAuthenticated(user))
                return false;

            if (IsAdmin(user))
                return true;

            int? currentUserId = GetUserId(user);
            return currentUserId.HasValue && currentUserId.Value == resourceOwnerUserId;
        }

        public static bool CanViewProject(ClaimsPrincipal user, Project project)
        {
            if (project.IsPublic)
                return true;

            return CanManageResource(user, project.UserId);
        }

        public static string ComputeETag(object payload)
        {
            string json = JsonSerializer.Serialize(payload);
            byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(json));
            string hex = Convert.ToHexString(hash, 0, 12).ToLowerInvariant();
            return $"\"{hex}\"";
        }
    }
}
