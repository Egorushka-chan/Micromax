using MicroMax.Server.Models;

namespace MicroMax.Server.Services.Auth;

public static class RolePermissionMap
{
    private static readonly IReadOnlyDictionary<string, WarehousePermission[]> PermissionsByRole =
        new Dictionary<string, WarehousePermission[]>(StringComparer.OrdinalIgnoreCase)
        {
            [SystemRoleCodes.Admin] =
            [
                WarehousePermission.WarehouseRead,
                WarehousePermission.WarehouseManage,
                WarehousePermission.ProductRead,
                WarehousePermission.ProductManage,
                WarehousePermission.StockRead,
                WarehousePermission.OperationsExecute,
                WarehousePermission.OperationsReadJournal,
                WarehousePermission.UsersManage
            ],
            [SystemRoleCodes.Worker] =
            [
                WarehousePermission.WarehouseRead,
                WarehousePermission.ProductRead,
                WarehousePermission.StockRead,
                WarehousePermission.OperationsExecute,
                WarehousePermission.OperationsReadJournal
            ],
            [SystemRoleCodes.Viewer] =
            [
                WarehousePermission.WarehouseRead,
                WarehousePermission.ProductRead,
                WarehousePermission.StockRead,
                WarehousePermission.OperationsReadJournal
            ]
        };

    public static bool HasPermission(string roleCode, WarehousePermission permission) =>
        PermissionsByRole.TryGetValue(roleCode, out var permissions) && permissions.Contains(permission);
}
