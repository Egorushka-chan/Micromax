using MicroMax.Server.Data;
using MicroMax.Server.Infrastructure.Api;
using MicroMax.Server.Models;
using MicroMax.Server.Services.Auth;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MicroMax.Server.Pages;

public sealed class UsersModel(
    MicroMaxDbContext db,
    CurrentUserService currentUserService,
    UserAccountService userAccountService) : AdminPageModel(db, currentUserService)
{
    private const string TemporaryPassword = "ChangeMe123!";

    public List<UserRow> Users { get; private set; } = [];
    public List<AdminWarehouseOption> Warehouses { get; private set; } = [];
    public List<RoleOption> Roles { get; private set; } = [];
    public List<AssignmentRow> Assignments { get; private set; } = [];
    public UserRow? SelectedUser { get; private set; }

    public string DefaultPasswordHint => TemporaryPassword;

    [BindProperty(SupportsGet = true)]
    public int? SelectedUserId { get; set; }

    [BindProperty]
    public CreateUserInput CreateForm { get; set; } = new();

    [BindProperty]
    public EditUserInput EditForm { get; set; } = new();

    [BindProperty]
    public AssignWarehouseInput AssignmentForm { get; set; } = new();

    public async Task OnGetAsync(CancellationToken cancellationToken) =>
        await LoadAsync(cancellationToken);

    public async Task<IActionResult> OnPostCreateAsync(CancellationToken cancellationToken)
    {
        try
        {
            var user = await userAccountService.CreateAsync(
                new CreateUserAccountRequest(
                    CreateForm.Email,
                    TemporaryPassword,
                    CreateForm.DisplayName,
                    CanAccessWebPanel: false),
                cancellationToken);
            SetSuccessMessage($"Пользователь создан. Временный пароль: {TemporaryPassword}");
            return RedirectToPage(new { selectedUserId = user.Id });
        }
        catch (ApiValidationException ex)
        {
            SetErrorMessage(ex.Message);
            return RedirectToPage(new { selectedUserId = SelectedUserId });
        }
        catch (ApiConflictException ex)
        {
            SetErrorMessage(ex.Message);
            return RedirectToPage(new { selectedUserId = SelectedUserId });
        }
        catch (DbUpdateException ex)
        {
            SetErrorMessage(ex);
            return RedirectToPage(new { selectedUserId = SelectedUserId });
        }
    }

    public async Task<IActionResult> OnPostUpdateAsync(CancellationToken cancellationToken)
    {
        if (EditForm.Id <= 0)
        {
            SetErrorMessage("Не удалось определить пользователя.");
            return RedirectToPage(new { selectedUserId = SelectedUserId });
        }

        var email = EditForm.Email.Trim().ToLowerInvariant();
        var displayName = EditForm.DisplayName.Trim();
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(displayName))
        {
            SetErrorMessage("Укажите email и имя пользователя.");
            return RedirectToPage(new { selectedUserId = EditForm.Id });
        }

        var user = await Db.AppUsers.FirstOrDefaultAsync(x => x.Id == EditForm.Id, cancellationToken);
        if (user is null)
        {
            SetErrorMessage("Пользователь не найден.");
            return RedirectToPage(new { selectedUserId = SelectedUserId });
        }

        user.Email = email;
        user.DisplayName = displayName;

        try
        {
            await Db.SaveChangesAsync(cancellationToken);
            SetSuccessMessage("Данные пользователя обновлены.");
        }
        catch (DbUpdateException ex)
        {
            SetErrorMessage(ex);
        }

        return RedirectToPage(new { selectedUserId = EditForm.Id });
    }

    public async Task<IActionResult> OnPostToggleActiveAsync(int userId, CancellationToken cancellationToken)
    {
        var user = await Db.AppUsers.FirstOrDefaultAsync(x => x.Id == userId, cancellationToken);
        if (user is null)
        {
            SetErrorMessage("Пользователь не найден.");
            return RedirectToPage(new { selectedUserId = SelectedUserId });
        }

        if (user.Id == GetCurrentUserId() && user.IsActive)
        {
            SetErrorMessage("Нельзя деактивировать текущую учётную запись администратора.");
            return RedirectToPage(new { selectedUserId = SelectedUserId ?? userId });
        }

        user.IsActive = !user.IsActive;

        try
        {
            await Db.SaveChangesAsync(cancellationToken);
            SetSuccessMessage(user.IsActive
                ? "Пользователь снова активирован."
                : "Пользователь деактивирован.");
        }
        catch (DbUpdateException ex)
        {
            SetErrorMessage(ex);
        }

        return RedirectToPage(new { selectedUserId = SelectedUserId ?? userId });
    }

    public async Task<IActionResult> OnPostAssignWarehouseAsync(CancellationToken cancellationToken)
    {
        if (AssignmentForm.UserId <= 0 || AssignmentForm.WarehouseId <= 0 || AssignmentForm.RoleId <= 0)
        {
            SetErrorMessage("Выберите пользователя, склад и роль.");
            return RedirectToPage(new { selectedUserId = SelectedUserId });
        }

        var adminWarehouseIds = await GetAdminWarehouseIdsAsync(cancellationToken);
        if (!adminWarehouseIds.Contains(AssignmentForm.WarehouseId))
        {
            SetErrorMessage("Нельзя назначить пользователя на недоступный склад.");
            return RedirectToPage(new { selectedUserId = AssignmentForm.UserId });
        }

        if (!await Db.AppUsers.AnyAsync(x => x.Id == AssignmentForm.UserId, cancellationToken))
        {
            SetErrorMessage("Пользователь не найден.");
            return RedirectToPage(new { selectedUserId = SelectedUserId });
        }

        if (!await Db.Roles.AnyAsync(x => x.Id == AssignmentForm.RoleId, cancellationToken))
        {
            SetErrorMessage("Выбранная роль не найдена.");
            return RedirectToPage(new { selectedUserId = AssignmentForm.UserId });
        }

        if (await Db.WarehouseUsers.AnyAsync(
                x => x.UserId == AssignmentForm.UserId && x.WarehouseId == AssignmentForm.WarehouseId,
                cancellationToken))
        {
            SetErrorMessage("Назначение для выбранного склада уже существует.");
            return RedirectToPage(new { selectedUserId = AssignmentForm.UserId });
        }

        Db.WarehouseUsers.Add(new WarehouseUser
        {
            UserId = AssignmentForm.UserId,
            WarehouseId = AssignmentForm.WarehouseId,
            RoleId = AssignmentForm.RoleId,
            CreatedAt = DateTimeOffset.UtcNow
        });

        try
        {
            await Db.SaveChangesAsync(cancellationToken);
            SetSuccessMessage("Назначение пользователя на склад создано.");
        }
        catch (DbUpdateException ex)
        {
            SetErrorMessage(ex);
        }

        return RedirectToPage(new { selectedUserId = AssignmentForm.UserId });
    }

    public async Task<IActionResult> OnPostChangeRoleAsync(int userId, int warehouseId, int roleId, CancellationToken cancellationToken)
    {
        var adminWarehouseIds = await GetAdminWarehouseIdsAsync(cancellationToken);
        if (!adminWarehouseIds.Contains(warehouseId))
        {
            SetErrorMessage("Недостаточно прав для изменения роли на выбранном складе.");
            return RedirectToPage(new { selectedUserId = userId });
        }

        var membership = await Db.WarehouseUsers
            .Include(x => x.Role)
            .FirstOrDefaultAsync(x => x.UserId == userId && x.WarehouseId == warehouseId, cancellationToken);

        if (membership is null)
        {
            SetErrorMessage("Назначение пользователя не найдено.");
            return RedirectToPage(new { selectedUserId = userId });
        }

        var targetRole = await Db.Roles.FirstOrDefaultAsync(x => x.Id == roleId, cancellationToken);
        if (targetRole is null)
        {
            SetErrorMessage("Выбранная роль не найдена.");
            return RedirectToPage(new { selectedUserId = userId });
        }

        if (await WouldRemoveLastAdminAccessAsync(userId, membership.Role!.Code, targetRole.Code, cancellationToken))
        {
            SetErrorMessage("Нельзя снять последнюю роль ADMIN у текущего администратора панели.");
            return RedirectToPage(new { selectedUserId = userId });
        }

        membership.RoleId = targetRole.Id;

        try
        {
            await Db.SaveChangesAsync(cancellationToken);
            SetSuccessMessage("Роль пользователя на складе обновлена.");
        }
        catch (DbUpdateException ex)
        {
            SetErrorMessage(ex);
        }

        return RedirectToPage(new { selectedUserId = userId });
    }

    public async Task<IActionResult> OnPostRemoveAssignmentAsync(int userId, int warehouseId, CancellationToken cancellationToken)
    {
        var adminWarehouseIds = await GetAdminWarehouseIdsAsync(cancellationToken);
        if (!adminWarehouseIds.Contains(warehouseId))
        {
            SetErrorMessage("Недостаточно прав для удаления назначения на выбранном складе.");
            return RedirectToPage(new { selectedUserId = userId });
        }

        var membership = await Db.WarehouseUsers
            .Include(x => x.Role)
            .FirstOrDefaultAsync(x => x.UserId == userId && x.WarehouseId == warehouseId, cancellationToken);

        if (membership is null)
        {
            SetErrorMessage("Назначение пользователя не найдено.");
            return RedirectToPage(new { selectedUserId = userId });
        }

        if (await WouldRemoveLastAdminAccessAsync(userId, membership.Role!.Code, null, cancellationToken))
        {
            SetErrorMessage("Нельзя удалить последнее назначение ADMIN у текущего администратора панели.");
            return RedirectToPage(new { selectedUserId = userId });
        }

        Db.WarehouseUsers.Remove(membership);

        try
        {
            await Db.SaveChangesAsync(cancellationToken);
            SetSuccessMessage("Назначение пользователя удалено.");
        }
        catch (DbUpdateException ex)
        {
            SetErrorMessage(ex);
        }

        return RedirectToPage(new { selectedUserId = userId });
    }

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        var adminWarehouseIds = await GetAdminWarehouseIdsAsync(cancellationToken);

        Warehouses = await GetAdminWarehouseOptionsAsync(cancellationToken);
        Roles = await Db.Roles
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .Select(x => new RoleOption(x.Id, x.Code, x.Name))
            .ToListAsync(cancellationToken);

        Users = await Db.AppUsers
            .AsNoTracking()
            .OrderBy(x => x.DisplayName)
            .Select(x => new UserRow(
                x.Id,
                x.Email,
                x.DisplayName,
                x.IsActive,
                x.WarehouseUsers.Count(y => adminWarehouseIds.Contains(y.WarehouseId)),
                x.WarehouseUsers.Any(y => adminWarehouseIds.Contains(y.WarehouseId) && y.Role!.Code == SystemRoleCodes.Admin)))
            .ToListAsync(cancellationToken);

        if (SelectedUserId is null)
        {
            return;
        }

        SelectedUser = Users.FirstOrDefault(x => x.Id == SelectedUserId.Value);
        if (SelectedUser is null)
        {
            return;
        }

        EditForm = new EditUserInput
        {
            Id = SelectedUser.Id,
            Email = SelectedUser.Email,
            DisplayName = SelectedUser.DisplayName
        };

        AssignmentForm = new AssignWarehouseInput
        {
            UserId = SelectedUser.Id,
            WarehouseId = Warehouses.FirstOrDefault()?.Id ?? 0,
            RoleId = Roles.FirstOrDefault()?.Id ?? 0
        };

        Assignments = await Db.WarehouseUsers
            .AsNoTracking()
            .Where(x => x.UserId == SelectedUser.Id && adminWarehouseIds.Contains(x.WarehouseId))
            .OrderBy(x => x.Warehouse!.Name)
            .ThenBy(x => x.Role!.Name)
            .Select(x => new AssignmentRow(
                x.UserId,
                x.WarehouseId,
                x.Warehouse!.Name,
                x.RoleId,
                x.Role!.Code,
                x.Role.Name,
                x.CreatedAt))
            .ToListAsync(cancellationToken);
    }

    private async Task<bool> WouldRemoveLastAdminAccessAsync(
        int userId,
        string currentRoleCode,
        string? targetRoleCode,
        CancellationToken cancellationToken)
    {
        if (userId != GetCurrentUserId())
        {
            return false;
        }

        if (!string.Equals(currentRoleCode, SystemRoleCodes.Admin, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (string.Equals(targetRoleCode, SystemRoleCodes.Admin, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var adminAssignmentsCount = await Db.WarehouseUsers
            .AsNoTracking()
            .CountAsync(x => x.UserId == userId && x.Role!.Code == SystemRoleCodes.Admin, cancellationToken);

        return adminAssignmentsCount <= 1;
    }

    public class CreateUserInput
    {
        public string Email { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
    }

    public sealed class EditUserInput : CreateUserInput
    {
        public int Id { get; set; }
    }

    public sealed class AssignWarehouseInput
    {
        public int UserId { get; set; }
        public int WarehouseId { get; set; }
        public int RoleId { get; set; }
    }

    public sealed record UserRow(
        int Id,
        string Email,
        string DisplayName,
        bool IsActive,
        int ManagedAssignmentCount,
        bool HasAdminAssignment);

    public sealed record RoleOption(int Id, string Code, string Name);

    public sealed record AssignmentRow(
        int UserId,
        int WarehouseId,
        string WarehouseName,
        int RoleId,
        string RoleCode,
        string RoleName,
        DateTimeOffset CreatedAt);
}
