package com.bezborodov.micromax.ui.home

import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.outlined.ArrowDropDown
import androidx.compose.material3.Button
import androidx.compose.material3.DropdownMenu
import androidx.compose.material3.DropdownMenuItem
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import com.bezborodov.micromax.data.CurrentUserWarehouse
import com.bezborodov.micromax.data.RoleAdmin
import com.bezborodov.micromax.data.RoleViewer
import com.bezborodov.micromax.data.RoleWorker
import com.bezborodov.micromax.data.WarehouseUser
import com.bezborodov.micromax.ui.auth.SessionUiState
import com.bezborodov.micromax.ui.components.MessageBanner
import com.bezborodov.micromax.ui.components.PlainInfoRow
import com.bezborodov.micromax.ui.components.SectionCard
import com.bezborodov.micromax.ui.components.SimpleTitle
import com.bezborodov.micromax.ui.components.TextMuted

private val SupportedRoleCodes = listOf(RoleAdmin, RoleWorker, RoleViewer)

@Composable
fun SettingsScreen(
    state: HomeUiState,
    sessionState: SessionUiState,
    onRefresh: () -> Unit,
    onLogout: () -> Unit,
    onSelectActiveWarehouse: (Int) -> Unit,
    onReloadWarehouseUsers: () -> Unit,
    onAddWarehouseUser: (String, String) -> Unit,
    onUpdateWarehouseUserRole: (Int, String) -> Unit,
    onRemoveWarehouseUser: (Int) -> Unit
) {
    val warehouses = sessionState.currentUser?.warehouses.orEmpty()
    val selectedWarehouse = sessionState.selectedWarehouse

    LazyColumn(verticalArrangement = Arrangement.spacedBy(14.dp)) {
        item { SimpleTitle("Настройки") }

        sessionState.message?.let { message ->
            item { MessageBanner(message) }
        }

        item {
            SectionCard(title = "Текущий пользователь") {
                PlainInfoRow(
                    title = sessionState.currentUser?.displayName ?: "Неизвестный пользователь",
                    subtitle = sessionState.currentUser?.email ?: "Email недоступен"
                )
                Text(
                    text = if (sessionState.currentUser?.isActive == true) {
                        "Учётная запись активна"
                    } else {
                        "Учётная запись отключена"
                    },
                    style = MaterialTheme.typography.bodyMedium,
                    color = TextMuted
                )

                if (warehouses.isEmpty()) {
                    Text(
                        text = "Пока нет доступных складов.",
                        style = MaterialTheme.typography.bodyMedium,
                        color = TextMuted
                    )
                } else {
                    warehouses.forEach { warehouse ->
                        PlainInfoRow(
                            title = warehouse.warehouseName,
                            subtitle = "Роль: ${warehouse.roleName}"
                        )
                    }
                }
            }
        }

        item {
            SectionCard(title = "Склад и сервер") {
                PlainInfoRow("Подключение", "http://10.0.2.2:5101")
                PlainInfoRow("Номенклатура", "${state.snapshot.products.size} позиций")
                PlainInfoRow("Ячейки хранения", "${state.snapshot.cells.size} ячеек")

                if (warehouses.size > 1) {
                    WarehouseSelector(
                        warehouses = warehouses,
                        selectedWarehouseId = sessionState.selectedWarehouseId,
                        onWarehouseSelected = onSelectActiveWarehouse
                    )
                } else {
                    selectedWarehouse?.let {
                        PlainInfoRow("Склад для управления", "${it.warehouseName} · ${it.roleName}")
                    }
                }

                OutlinedButton(onClick = onRefresh, modifier = Modifier.fillMaxWidth()) {
                    Text("Обновить данные")
                }
                OutlinedButton(onClick = onLogout, modifier = Modifier.fillMaxWidth()) {
                    Text("Выйти из аккаунта")
                }
            }
        }

        item {
            SectionCard(title = "Пользователи склада") {
                if (selectedWarehouse == null) {
                    Text(
                        text = "Нет выбранного склада.",
                        style = MaterialTheme.typography.bodyMedium,
                        color = TextMuted
                    )
                } else if (!sessionState.canManageSelectedWarehouseUsers) {
                    PlainInfoRow(
                        title = selectedWarehouse.warehouseName,
                        subtitle = "Текущая роль ${selectedWarehouse.roleName} не позволяет управлять участниками склада."
                    )
                } else {
                    PlainInfoRow(
                        title = selectedWarehouse.warehouseName,
                        subtitle = "Управление доступом для выбранного склада"
                    )

                    AddWarehouseUserForm(
                        isSubmitting = sessionState.isWarehouseUserSubmitting,
                        onSubmit = onAddWarehouseUser
                    )

                    OutlinedButton(
                        onClick = onReloadWarehouseUsers,
                        enabled = !sessionState.isWarehouseUsersLoading && !sessionState.isWarehouseUserSubmitting,
                        modifier = Modifier.fillMaxWidth()
                    ) {
                        Text(if (sessionState.isWarehouseUsersLoading) "Загрузка..." else "Обновить список пользователей")
                    }

                    if (sessionState.warehouseUsers.isEmpty() && !sessionState.isWarehouseUsersLoading) {
                        Text(
                            text = "У выбранного склада пока нет участников.",
                            style = MaterialTheme.typography.bodyMedium,
                            color = TextMuted
                        )
                    }

                    sessionState.warehouseUsers.forEach { warehouseUser ->
                        WarehouseUserCard(
                            user = warehouseUser,
                            currentUserId = sessionState.currentUser?.id,
                            isSubmitting = sessionState.isWarehouseUserSubmitting,
                            onUpdateRole = onUpdateWarehouseUserRole,
                            onRemoveUser = onRemoveWarehouseUser
                        )
                    }
                }
            }
        }
    }
}

@Composable
private fun WarehouseSelector(
    warehouses: List<CurrentUserWarehouse>,
    selectedWarehouseId: Int?,
    onWarehouseSelected: (Int) -> Unit
) {
    var isExpanded by remember { mutableStateOf(false) }
    val selectedWarehouse = warehouses.firstOrNull { it.warehouseId == selectedWarehouseId } ?: warehouses.firstOrNull()

    Box(modifier = Modifier.fillMaxWidth()) {
        OutlinedTextField(
            value = selectedWarehouse?.let { "${it.warehouseName} · ${it.roleName}" } ?: "",
            onValueChange = {},
            readOnly = true,
            modifier = Modifier.fillMaxWidth(),
            label = { Text("Склад для управления") },
            trailingIcon = {
                androidx.compose.material3.Icon(
                    imageVector = Icons.Outlined.ArrowDropDown,
                    contentDescription = null
                )
            }
        )

        Box(
            modifier = Modifier
                .fillMaxWidth()
                .height(56.dp)
                .clickable { isExpanded = true }
        )

        DropdownMenu(
            expanded = isExpanded,
            onDismissRequest = { isExpanded = false },
            modifier = Modifier
                .fillMaxWidth(0.94f)
                .background(Color.White)
        ) {
            warehouses.forEach { warehouse ->
                DropdownMenuItem(
                    text = { Text("${warehouse.warehouseName} · ${warehouse.roleName}") },
                    onClick = {
                        onWarehouseSelected(warehouse.warehouseId)
                        isExpanded = false
                    }
                )
            }
        }
    }
}

@Composable
private fun AddWarehouseUserForm(
    isSubmitting: Boolean,
    onSubmit: (String, String) -> Unit
) {
    var email by rememberSaveable { mutableStateOf("") }
    var roleCode by rememberSaveable { mutableStateOf(RoleWorker) }

    Column(verticalArrangement = Arrangement.spacedBy(10.dp)) {
        Text(
            text = "Добавить пользователя",
            style = MaterialTheme.typography.titleMedium,
            fontWeight = FontWeight.SemiBold
        )
        OutlinedTextField(
            value = email,
            onValueChange = { email = it },
            label = { Text("Email пользователя") },
            modifier = Modifier.fillMaxWidth(),
            singleLine = true
        )
        RoleSelector(
            label = "Роль",
            selectedRoleCode = roleCode,
            onRoleSelected = { roleCode = it }
        )
        Button(
            onClick = {
                onSubmit(email, roleCode)
            },
            enabled = !isSubmitting,
            modifier = Modifier.fillMaxWidth()
        ) {
            Text(if (isSubmitting) "Сохранение..." else "Добавить в склад")
        }
    }
}

@Composable
private fun WarehouseUserCard(
    user: WarehouseUser,
    currentUserId: Int?,
    isSubmitting: Boolean,
    onUpdateRole: (Int, String) -> Unit,
    onRemoveUser: (Int) -> Unit
) {
    var selectedRoleCode by rememberSaveable(user.userId, user.roleCode) { mutableStateOf(user.roleCode) }
    var confirmRemoval by rememberSaveable(user.userId) { mutableStateOf(false) }

    Column(
        modifier = Modifier
            .fillMaxWidth()
            .background(Color(0xFFF8F8F8), RoundedCornerShape(10.dp))
            .padding(14.dp),
        verticalArrangement = Arrangement.spacedBy(10.dp)
    ) {
        Text(
            text = if (user.userId == currentUserId) {
                "${user.displayName} (вы)"
            } else {
                user.displayName
            },
            style = MaterialTheme.typography.titleMedium,
            fontWeight = FontWeight.SemiBold
        )
        Text(user.email, style = MaterialTheme.typography.bodyMedium, color = TextMuted)
        Text(
            text = if (user.isActive) "Активен" else "Отключён",
            style = MaterialTheme.typography.bodySmall,
            color = TextMuted
        )

        RoleSelector(
            label = "Роль в складе",
            selectedRoleCode = selectedRoleCode,
            onRoleSelected = {
                selectedRoleCode = it
                confirmRemoval = false
            }
        )

        Row(horizontalArrangement = Arrangement.spacedBy(8.dp), modifier = Modifier.fillMaxWidth()) {
            OutlinedButton(
                onClick = {
                    onUpdateRole(user.userId, selectedRoleCode)
                    confirmRemoval = false
                },
                enabled = !isSubmitting && selectedRoleCode != user.roleCode,
                modifier = Modifier.weight(1f)
            ) {
                Text("Сохранить роль")
            }
            OutlinedButton(
                onClick = { confirmRemoval = !confirmRemoval },
                enabled = !isSubmitting,
                modifier = Modifier.weight(1f)
            ) {
                Text(if (confirmRemoval) "Отменить" else "Удалить")
            }
        }

        if (confirmRemoval) {
            Button(
                onClick = { onRemoveUser(user.userId) },
                enabled = !isSubmitting,
                modifier = Modifier.fillMaxWidth()
            ) {
                Text(if (isSubmitting) "Удаление..." else "Подтвердить удаление")
            }
        }
    }
}

@Composable
private fun RoleSelector(
    label: String,
    selectedRoleCode: String,
    onRoleSelected: (String) -> Unit
) {
    var expanded by remember { mutableStateOf(false) }

    Box(modifier = Modifier.fillMaxWidth()) {
        OutlinedTextField(
            value = roleLabel(selectedRoleCode),
            onValueChange = {},
            readOnly = true,
            modifier = Modifier.fillMaxWidth(),
            label = { Text(label) },
            trailingIcon = {
                androidx.compose.material3.Icon(
                    imageVector = Icons.Outlined.ArrowDropDown,
                    contentDescription = null
                )
            }
        )

        Box(
            modifier = Modifier
                .fillMaxWidth()
                .height(56.dp)
                .clickable { expanded = true }
        )

        DropdownMenu(
            expanded = expanded,
            onDismissRequest = { expanded = false },
            modifier = Modifier
                .fillMaxWidth(0.94f)
                .background(Color.White)
        ) {
            SupportedRoleCodes.forEach { roleCode ->
                DropdownMenuItem(
                    text = { Text(roleLabel(roleCode)) },
                    onClick = {
                        onRoleSelected(roleCode)
                        expanded = false
                    }
                )
            }
        }
    }
}

private fun roleLabel(roleCode: String): String {
    return when (roleCode) {
        RoleAdmin -> "ADMIN — полный доступ"
        RoleWorker -> "WORKER — операции и журнал"
        RoleViewer -> "VIEWER — только просмотр"
        else -> roleCode
    }
}
