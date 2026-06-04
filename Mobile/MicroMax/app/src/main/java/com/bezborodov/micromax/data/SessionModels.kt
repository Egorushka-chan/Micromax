package com.bezborodov.micromax.data

import androidx.compose.runtime.Immutable
import java.time.OffsetDateTime

const val RoleAdmin = "ADMIN"
const val RoleWorker = "WORKER"
const val RoleViewer = "VIEWER"

private val OperationRoles = setOf(RoleAdmin, RoleWorker)

@Immutable
data class CurrentUserWarehouse(
    val warehouseId: Int,
    val warehouseName: String,
    val roleCode: String,
    val roleName: String
)

@Immutable
data class CurrentUser(
    val id: Int,
    val email: String,
    val displayName: String,
    val isActive: Boolean,
    val warehouses: List<CurrentUserWarehouse>
)

@Immutable
data class WarehouseUser(
    val userId: Int,
    val email: String,
    val displayName: String,
    val isActive: Boolean,
    val roleCode: String,
    val roleName: String,
    val createdAt: String
)

@Immutable
data class AuthSession(
    val accessToken: String,
    val accessTokenExpiresAt: OffsetDateTime,
    val refreshToken: String,
    val user: CurrentUser,
    val activeWarehouseIdForSettings: Int?
)

@Immutable
data class AuthTokens(
    val accessToken: String,
    val accessTokenExpiresAt: OffsetDateTime,
    val refreshToken: String,
    val user: CurrentUser
)

data class UserPermissions(
    val canReadWarehouseData: Boolean,
    val canCreateProducts: Boolean,
    val canExecuteOperations: Boolean
)

fun CurrentUser.permissions(): UserPermissions {
    val roles = warehouses.map(CurrentUserWarehouse::roleCode)
    return UserPermissions(
        canReadWarehouseData = roles.isNotEmpty(),
        canCreateProducts = roles.any { it == RoleAdmin },
        canExecuteOperations = roles.any { it in OperationRoles }
    )
}

fun CurrentUserWarehouse.roleSummary(): String {
    return "$warehouseName · $roleName"
}
