using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace MicroMax.Server.Services.Auth;

/// <summary>
/// Проверяет, что пользователь может войти в веб-панель, даже если у него пока нет назначенных складов.
/// </summary>
public sealed class WebPanelAuthorizationHandler(WebPanelAccessService webPanelAccessService)
    : AuthorizationHandler<WebPanelRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        WebPanelRequirement requirement)
    {
        var userIdValue = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdValue, out var userId))
        {
            return;
        }

        if (await webPanelAccessService.CanAccessAsync(userId))
        {
            context.Succeed(requirement);
        }
    }
}
