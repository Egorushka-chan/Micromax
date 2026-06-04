using System.Security.Claims;

namespace MicroMax.Server.Services.Auth;

public sealed class CurrentUserService
{
    public int GetRequiredUserId(ClaimsPrincipal principal)
    {
        var claimValue = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(claimValue, out var userId))
        {
            throw new InvalidOperationException("Не удалось определить текущего пользователя.");
        }

        return userId;
    }
}
