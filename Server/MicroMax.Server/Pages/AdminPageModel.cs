using MicroMax.Server.Data;
using MicroMax.Server.Models;
using MicroMax.Server.Services.Auth;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace MicroMax.Server.Pages;

public abstract class AdminPageModel(
    MicroMaxDbContext db,
    CurrentUserService currentUserService) : PageModel
{
    protected MicroMaxDbContext Db { get; } = db;

    protected int GetCurrentUserId() => currentUserService.GetRequiredUserId(User);

    protected IQueryable<WarehouseUser> GetAdminMembershipsQuery(int userId) =>
        Db.WarehouseUsers
            .AsNoTracking()
            .Where(x => x.UserId == userId && x.User!.IsActive && x.Role!.Code == SystemRoleCodes.Admin);

    protected Task<List<int>> GetAdminWarehouseIdsAsync(CancellationToken cancellationToken = default) =>
        GetAdminMembershipsQuery(GetCurrentUserId())
            .Select(x => x.WarehouseId)
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync(cancellationToken);

    protected Task<List<AdminWarehouseOption>> GetAdminWarehouseOptionsAsync(CancellationToken cancellationToken = default) =>
        GetAdminMembershipsQuery(GetCurrentUserId())
            .Select(x => new { x.WarehouseId, WarehouseName = x.Warehouse!.Name })
            .Distinct()
            .OrderBy(x => x.WarehouseName)
            .Select(x => new AdminWarehouseOption(x.WarehouseId, x.WarehouseName))
            .ToListAsync(cancellationToken);

    protected void SetSuccessMessage(string message) => TempData["StatusMessage"] = message;

    protected void SetErrorMessage(string message) => TempData["ErrorMessage"] = message;

    protected void SetErrorMessage(Exception exception) =>
        TempData["ErrorMessage"] = exception is DbUpdateException dbUpdateException
            ? dbUpdateException.InnerException?.Message ?? dbUpdateException.Message
            : exception.Message;
}

public sealed record AdminWarehouseOption(int Id, string Name);
