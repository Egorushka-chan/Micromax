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
import androidx.compose.material.icons.automirrored.outlined.ArrowBack
import androidx.compose.material.icons.outlined.ArrowDropDown
import androidx.compose.material3.Button
import androidx.compose.material3.DropdownMenu
import androidx.compose.material3.DropdownMenuItem
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
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
import com.bezborodov.micromax.data.WarehouseSetupTemplate
import com.bezborodov.micromax.ui.auth.SessionUiState
import com.bezborodov.micromax.ui.components.EmptyStateText
import com.bezborodov.micromax.ui.components.MessageBanner
import com.bezborodov.micromax.ui.components.PlainInfoRow
import com.bezborodov.micromax.ui.components.SectionCard
import com.bezborodov.micromax.ui.components.TextMuted

@Composable
fun WarehouseMenuScreen(
    sessionState: SessionUiState,
    onSelectWarehouse: (Int) -> Unit,
    onCreateWarehouse: (String, String?) -> Unit,
    onCreateWarehouseFromTemplate: (String, String?, String) -> Unit,
    onLoadTemplates: (Boolean) -> Unit,
    onClose: (() -> Unit)? = null,
    onLogout: (() -> Unit)? = null
) {
    val warehouses = sessionState.currentUser?.warehouses.orEmpty()
    val selectedWarehouse = sessionState.selectedWarehouse
    val setupTemplates = sessionState.warehouseSetupTemplates.filterNot { it.code == "EMPTY" }
    var warehouseName by rememberSaveable { mutableStateOf("") }
    var warehouseAddress by rememberSaveable { mutableStateOf("") }
    var selectedTemplateCode by rememberSaveable { mutableStateOf("") }

    LaunchedEffect(Unit) {
        onLoadTemplates(false)
    }

    LaunchedEffect(setupTemplates) {
        if (selectedTemplateCode.isBlank() && setupTemplates.isNotEmpty()) {
            selectedTemplateCode = setupTemplates.first().code
        }
    }

    LazyColumn(
        modifier = Modifier.padding(horizontal = 16.dp, vertical = 12.dp),
        verticalArrangement = Arrangement.spacedBy(14.dp)
    ) {
        item {
            Row(
                modifier = Modifier.fillMaxWidth(),
                verticalAlignment = Alignment.CenterVertically
            ) {
                if (onClose != null) {
                    IconButton(onClick = onClose) {
                        Icon(
                            imageVector = Icons.AutoMirrored.Outlined.ArrowBack,
                            contentDescription = "Назад",
                            tint = Color.Black
                        )
                    }
                } else {
                    Spacer(modifier = Modifier.width(48.dp))
                }

                Text(
                    text = "Склады",
                    style = MaterialTheme.typography.headlineSmall,
                    fontWeight = FontWeight.Bold,
                    modifier = Modifier.weight(1f)
                )

                Spacer(modifier = Modifier.width(48.dp))
            }
        }

        sessionState.message?.let { message ->
            item { MessageBanner(message) }
        }

        item {
            SectionCard(
                title = if (warehouses.isEmpty()) {
                    "Начало работы"
                } else if (selectedWarehouse == null) {
                    "Выбор активного склада"
                } else {
                    "Активный склад"
                }
            ) {
                when {
                    warehouses.isEmpty() -> {
                        EmptyStateText("У пользователя пока нет доступных складов. Создайте первый склад или склад по шаблону.")
                    }

                    selectedWarehouse == null -> {
                        Text(
                            text = "Выберите склад, в контексте которого приложение будет загружать товары, ячейки, операции и помощника.",
                            style = MaterialTheme.typography.bodyMedium,
                            color = TextMuted
                        )
                    }

                    else -> {
                        PlainInfoRow(
                            title = selectedWarehouse.warehouseName,
                            subtitle = "Текущая роль: ${selectedWarehouse.roleName}"
                        )
                    }
                }
            }
        }

        item {
            SectionCard(title = "Доступные склады") {
                if (warehouses.isEmpty()) {
                    EmptyStateText("Список складов пуст.")
                } else {
                    warehouses.forEach { warehouse ->
                        WarehouseRow(
                            warehouse = warehouse,
                            isSelected = warehouse.warehouseId == selectedWarehouse?.warehouseId,
                            onSelect = { onSelectWarehouse(warehouse.warehouseId) }
                        )
                    }
                }
            }
        }

        item {
            SectionCard(title = "Создать пустой склад") {
                OutlinedTextField(
                    value = warehouseName,
                    onValueChange = { warehouseName = it },
                    label = { Text("Название склада") },
                    modifier = Modifier.fillMaxWidth(),
                    singleLine = true
                )
                OutlinedTextField(
                    value = warehouseAddress,
                    onValueChange = { warehouseAddress = it },
                    label = { Text("Адрес") },
                    modifier = Modifier.fillMaxWidth(),
                    singleLine = true
                )
                Button(
                    onClick = { onCreateWarehouse(warehouseName, warehouseAddress.ifBlank { null }) },
                    enabled = !sessionState.isCreatingWarehouse,
                    modifier = Modifier.fillMaxWidth()
                ) {
                    Text(if (sessionState.isCreatingWarehouse) "Создание..." else "Создать склад")
                }
            }
        }

        item {
            SectionCard(title = "Быстрая настройка") {
                if (sessionState.isWarehouseTemplatesLoading && setupTemplates.isEmpty()) {
                    Text(
                        text = "Загрузка шаблонов...",
                        style = MaterialTheme.typography.bodyMedium,
                        color = TextMuted
                    )
                } else if (setupTemplates.isEmpty()) {
                    EmptyStateText("Шаблоны пока недоступны.")
                } else {
                    TemplateSelector(
                        templates = setupTemplates,
                        selectedTemplateCode = selectedTemplateCode,
                        onTemplateSelected = { selectedTemplateCode = it }
                    )

                    val selectedTemplate = setupTemplates.firstOrNull { it.code == selectedTemplateCode }
                    selectedTemplate?.let { template ->
                        PlainInfoRow(
                            title = template.name,
                            subtitle = "${template.zonesCount} зон · ${template.cellsCount} ячеек"
                        )
                        Text(
                            text = template.description,
                            style = MaterialTheme.typography.bodyMedium,
                            color = TextMuted
                        )
                    }

                    Button(
                        onClick = {
                            onCreateWarehouseFromTemplate(
                                warehouseName,
                                warehouseAddress.ifBlank { null },
                                selectedTemplateCode
                            )
                        },
                        enabled = !sessionState.isCreatingWarehouse && warehouseName.isNotBlank() && selectedTemplateCode.isNotBlank(),
                        modifier = Modifier.fillMaxWidth()
                    ) {
                        Text(if (sessionState.isCreatingWarehouse) "Создание..." else "Создать по шаблону")
                    }

                    OutlinedButton(
                        onClick = { onLoadTemplates(true) },
                        enabled = !sessionState.isWarehouseTemplatesLoading && !sessionState.isCreatingWarehouse,
                        modifier = Modifier.fillMaxWidth()
                    ) {
                        Text("Обновить шаблоны")
                    }
                }
            }
        }

        if (onLogout != null) {
            item {
                OutlinedButton(
                    onClick = onLogout,
                    enabled = !sessionState.isCreatingWarehouse,
                    modifier = Modifier.fillMaxWidth()
                ) {
                    Text("Выйти")
                }
            }
        }
    }
}

@Composable
private fun WarehouseRow(
    warehouse: CurrentUserWarehouse,
    isSelected: Boolean,
    onSelect: () -> Unit
) {
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .background(
                if (isSelected) Color(0xFFF1F4FF) else Color(0xFFF8F8F8),
                RoundedCornerShape(10.dp)
            )
            .clickable(onClick = onSelect)
            .padding(14.dp),
        verticalAlignment = Alignment.CenterVertically
    ) {
        Column(modifier = Modifier.weight(1f)) {
            Text(
                text = warehouse.warehouseName,
                style = MaterialTheme.typography.titleMedium,
                fontWeight = FontWeight.SemiBold
            )
            Text(
                text = "Роль: ${warehouse.roleName}",
                style = MaterialTheme.typography.bodyMedium,
                color = TextMuted
            )
        }

        Text(
            text = if (isSelected) "Активный" else "Открыть",
            style = MaterialTheme.typography.bodyMedium,
            color = if (isSelected) Color(0xFF4B55DE) else TextMuted
        )
    }
}

@Composable
private fun TemplateSelector(
    templates: List<WarehouseSetupTemplate>,
    selectedTemplateCode: String,
    onTemplateSelected: (String) -> Unit
) {
    var expanded by remember { mutableStateOf(false) }
    val selectedTemplate = templates.firstOrNull { it.code == selectedTemplateCode } ?: templates.firstOrNull()

    Box(modifier = Modifier.fillMaxWidth()) {
        OutlinedTextField(
            value = selectedTemplate?.name.orEmpty(),
            onValueChange = {},
            readOnly = true,
            modifier = Modifier.fillMaxWidth(),
            label = { Text("Шаблон") },
            trailingIcon = {
                Icon(
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
            modifier = Modifier.fillMaxWidth(0.94f)
        ) {
            templates.forEach { template ->
                DropdownMenuItem(
                    text = { Text(template.name) },
                    onClick = {
                        onTemplateSelected(template.code)
                        expanded = false
                    }
                )
            }
        }
    }
}
