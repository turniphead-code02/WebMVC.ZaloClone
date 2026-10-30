using System.Security.Claims;

namespace ZaloClone.Infrastructure;

public static class ClaimsPrincipalExtensions
{
    public static Guid GetUserId(this ClaimsPrincipal? user)
    {
        if (user == null) return Guid.Empty;
        var idClaim = user.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(idClaim, out var id) ? id : Guid.Empty;
    }

    public static string GetDisplayName(this ClaimsPrincipal? user)
    {
        return user?.FindFirstValue(ClaimTypes.Name) ?? string.Empty;
    }

    public static string GetRole(this ClaimsPrincipal? user)
    {
        return user?.FindFirstValue(ClaimTypes.Role) ?? "User";
    }

    public static bool IsAdmin(this ClaimsPrincipal? user)
    {
        return user?.IsInRole("Admin") ?? false;
    }
}
