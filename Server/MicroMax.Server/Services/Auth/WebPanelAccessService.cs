using MicroMax.Server.Data;
using MicroMax.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace MicroMax.Server.Services.Auth;

/// <summary>
/// Определяет, может ли пользователь войти в веб-панель и есть ли у него права администратора склада.
/// </summary>
public sealed class WebPanelAccessService(MicroMaxDbContext db)
{
    public bool CanAccess(AppUser user) =>
        user.IsActive &&
        (user.CanAccessWebPanel || user.WarehouseUsers.Any(x => x.Role?.Code == SystemRoleCodes.Admin));

    public Task<bool> CanAccessAsync(int userId, CancellationToken cancellationToken = default) =>
        db.AppUsers
            .Where(x => x.Id == userId && x.IsActive)
            .AnyAsync(
                x => x.CanAccessWebPanel || x.WarehouseUsers.Any(y => y.Role!.Code == SystemRoleCodes.Admin),
                cancellationToken);

    public Task<bool> HasWarehouseAdminAccessAsync(int userId, CancellationToken cancellationToken = default) =>
        db.AppUsers
            .Where(x => x.Id == userId && x.IsActive)
            .AnyAsync(x => x.WarehouseUsers.Any(y => y.Role!.Code == SystemRoleCodes.Admin), cancellationToken);
}
