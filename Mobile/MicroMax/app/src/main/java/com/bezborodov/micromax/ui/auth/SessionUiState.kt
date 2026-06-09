package com.bezborodov.micromax.ui.auth

import androidx.compose.runtime.Immutable
import com.bezborodov.micromax.data.AuthSession
import com.bezborodov.micromax.data.CurrentUserWarehouse
import com.bezborodov.micromax.data.RoleAdmin
import com.bezborodov.micromax.data.UserPermissions
import com.bezborodov.micromax.data.WarehouseSetupTemplate
import com.bezborodov.micromax.data.WarehouseUser
import com.bezborodov.micromax.data.permissionsForWarehouse

@Immutable
data class SessionUiState(
    val isRestoringSession: Boolean = true,
    val isSubmitting: Boolean = false,
    val isCreatingWarehouse: Boolean = false,
    val isWarehouseUsersLoading: Boolean = false,
    val isWarehouseUserSubmitting: Boolean = false,
    val isWarehouseTemplatesLoading: Boolean = false,
    val currentSession: AuthSession? = null,
    val warehouseUsers: List<WarehouseUser> = emptyList(),
    val loadedWarehouseUsersWarehouseId: Int? = null,
    val warehouseSetupTemplates: List<WarehouseSetupTemplate> = emptyList(),
    val message: String? = null
) {
    val isAuthenticated: Boolean
        get() = currentSession != null

    val currentUser = currentSession?.user

    val hasWarehouses: Boolean
        get() = currentUser?.warehouses?.isNotEmpty() == true

    val selectedWarehouse: CurrentUserWarehouse?
        get() = currentSession?.selectedWarehouse

    val selectedWarehouseId: Int?
        get() = selectedWarehouse?.warehouseId

    val requiresWarehouseSelection: Boolean
        get() = hasWarehouses && selectedWarehouse == null

    val permissions: UserPermissions
        get() = permissionsForWarehouse(selectedWarehouse)

    val canManageSelectedWarehouseUsers: Boolean
        get() = selectedWarehouse?.roleCode == RoleAdmin

    fun canManageWarehouse(warehouseId: Int?): Boolean {
        if (warehouseId == null) {
            return false
        }

        return currentUser?.warehouses?.any {
            it.warehouseId == warehouseId && it.roleCode == RoleAdmin
        } == true
    }
}
