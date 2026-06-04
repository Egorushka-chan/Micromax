package com.bezborodov.micromax.ui.barcodes

import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.heightIn
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.Button
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.DropdownMenu
import androidx.compose.material3.DropdownMenuItem
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
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
import com.bezborodov.micromax.data.BarcodeDraftDto
import com.bezborodov.micromax.data.BarcodeDto
import com.bezborodov.micromax.ui.components.AccentDark
import com.bezborodov.micromax.ui.components.EmptyStateText
import com.bezborodov.micromax.ui.components.SectionCard
import com.bezborodov.micromax.ui.components.TextMuted
import com.bezborodov.micromax.ui.scanner.ScannedBarcode

private val SupportedSymbologies = listOf(
    "UNKNOWN",
    "CODE_128",
    "EAN_13",
    "EAN_8",
    "UPC_A",
    "QR_CODE"
)

@Composable
fun BarcodeSection(
    barcodes: List<BarcodeDto>,
    isLoading: Boolean,
    message: String?,
    canManageBarcodes: Boolean,
    onAddBarcode: () -> Unit,
    onDeactivateBarcode: (BarcodeDto) -> Unit
) {
    SectionCard(title = "Штрих-коды") {
        if (message != null) {
            Text(
                text = message,
                style = MaterialTheme.typography.bodyMedium,
                color = MaterialTheme.colorScheme.error
            )
        }

        if (canManageBarcodes) {
            OutlinedButton(
                onClick = onAddBarcode,
                modifier = Modifier.fillMaxWidth()
            ) {
                Text("Добавить штрих-код")
            }
        }

        when {
            isLoading -> {
                Text(
                    text = "Загрузка штрих-кодов...",
                    style = MaterialTheme.typography.bodyMedium,
                    color = TextMuted
                )
            }

            barcodes.isEmpty() -> {
                EmptyStateText("Для этой карточки пока нет активных штрих-кодов.")
            }

            else -> {
                barcodes.forEach { barcode ->
                    BarcodeRow(
                        barcode = barcode,
                        canManageBarcodes = canManageBarcodes,
                        onDeactivate = { onDeactivateBarcode(barcode) }
                    )
                }
            }
        }
    }
}

@Composable
fun BarcodeEditorDialog(
    title: String,
    confirmButtonText: String,
    initialValue: String = "",
    initialSymbology: String = "UNKNOWN",
    onDismiss: () -> Unit,
    onOpenScanner: ((ScannedBarcode) -> Unit) -> Unit,
    onConfirm: (BarcodeDraftDto) -> Unit
) {
    var value by rememberSaveable { mutableStateOf(initialValue) }
    var symbology by rememberSaveable { mutableStateOf(initialSymbology) }
    var localMessage by rememberSaveable { mutableStateOf<String?>(null) }

    AlertDialog(
        onDismissRequest = onDismiss,
        title = {
            Text(
                text = title,
                style = MaterialTheme.typography.headlineSmall,
                color = AccentDark
            )
        },
        text = {
            Column(verticalArrangement = Arrangement.spacedBy(12.dp)) {
                if (localMessage != null) {
                    Text(
                        text = localMessage.orEmpty(),
                        style = MaterialTheme.typography.bodyMedium,
                        color = MaterialTheme.colorScheme.error
                    )
                }

                OutlinedTextField(
                    value = value,
                    onValueChange = {
                        value = it
                        localMessage = null
                    },
                    label = { Text("Значение штрих-кода") },
                    modifier = Modifier.fillMaxWidth(),
                    singleLine = true
                )

                SymbologySelector(
                    selectedSymbology = symbology,
                    onSymbologySelected = {
                        symbology = it
                        localMessage = null
                    }
                )

                OutlinedButton(
                    onClick = {
                        onOpenScanner { scannedBarcode ->
                            value = scannedBarcode.rawValue
                            symbology = scannedBarcode.symbology
                            localMessage = null
                        }
                    },
                    modifier = Modifier.fillMaxWidth()
                ) {
                    Text("Сканировать камерой")
                }
            }
        },
        confirmButton = {
            Button(
                onClick = {
                    val normalizedValue = value.trim()
                    if (normalizedValue.isEmpty()) {
                        localMessage = "Введите значение штрих-кода или отсканируйте его камерой."
                        return@Button
                    }

                    onConfirm(
                        BarcodeDraftDto(
                            value = normalizedValue,
                            symbology = symbology
                        )
                    )
                }
            ) {
                Text(confirmButtonText)
            }
        },
        dismissButton = {
            TextButton(onClick = onDismiss) {
                Text("Отмена")
            }
        }
    )
}

@Composable
fun <T> BarcodeBindingDialog(
    title: String,
    items: List<T>,
    itemTitle: (T) -> String,
    itemSubtitle: (T) -> String,
    onDismiss: () -> Unit,
    onConfirm: (T) -> Unit
) {
    var query by rememberSaveable { mutableStateOf("") }
    var selectedIndex by rememberSaveable { mutableStateOf<Int?>(null) }

    val filteredItems = remember(items, query) {
        val normalizedQuery = query.trim()
        if (normalizedQuery.isEmpty()) {
            items
        } else {
            items.filter {
                itemTitle(it).contains(normalizedQuery, ignoreCase = true) ||
                    itemSubtitle(it).contains(normalizedQuery, ignoreCase = true)
            }
        }
    }

    AlertDialog(
        onDismissRequest = onDismiss,
        title = {
            Text(
                text = title,
                style = MaterialTheme.typography.headlineSmall,
                color = AccentDark
            )
        },
        text = {
            Column(verticalArrangement = Arrangement.spacedBy(12.dp)) {
                OutlinedTextField(
                    value = query,
                    onValueChange = { query = it },
                    label = { Text("Поиск") },
                    modifier = Modifier.fillMaxWidth(),
                    singleLine = true
                )

                if (filteredItems.isEmpty()) {
                    EmptyStateText("Подходящие объекты не найдены.")
                } else {
                    LazyColumn(
                        modifier = Modifier
                            .fillMaxWidth()
                            .heightIn(max = 280.dp)
                            .background(Color.White),
                        verticalArrangement = Arrangement.spacedBy(8.dp)
                    ) {
                        items(filteredItems) { item ->
                            val index = filteredItems.indexOf(item)
                            SelectableEntityCard(
                                title = itemTitle(item),
                                subtitle = itemSubtitle(item),
                                selected = selectedIndex == index,
                                onClick = { selectedIndex = index }
                            )
                        }
                    }
                }
            }
        },
        confirmButton = {
            Button(
                onClick = {
                    val selectedItem = selectedIndex?.let(filteredItems::getOrNull) ?: return@Button
                    onConfirm(selectedItem)
                }
            ) {
                Text("Привязать")
            }
        },
        dismissButton = {
            TextButton(onClick = onDismiss) {
                Text("Отмена")
            }
        }
    )
}

@Composable
private fun BarcodeRow(
    barcode: BarcodeDto,
    canManageBarcodes: Boolean,
    onDeactivate: () -> Unit
) {
    Card(
        modifier = Modifier.fillMaxWidth(),
        colors = CardDefaults.cardColors(containerColor = Color(0xFFF8F8F8)),
        shape = RoundedCornerShape(12.dp)
    ) {
        Column(
            modifier = Modifier.padding(horizontal = 14.dp, vertical = 12.dp),
            verticalArrangement = Arrangement.spacedBy(6.dp)
        ) {
            Row(
                modifier = Modifier.fillMaxWidth(),
                verticalAlignment = Alignment.CenterVertically
            ) {
                Text(
                    text = barcode.value,
                    style = MaterialTheme.typography.titleMedium,
                    fontWeight = FontWeight.SemiBold,
                    modifier = Modifier.weight(1f)
                )
                if (barcode.isPrimary) {
                    Text(
                        text = "Основной",
                        style = MaterialTheme.typography.labelLarge,
                        color = AccentDark
                    )
                }
            }

            Text(
                text = barcode.symbology,
                style = MaterialTheme.typography.bodyMedium,
                color = TextMuted
            )

            if (canManageBarcodes) {
                TextButton(
                    onClick = onDeactivate,
                    modifier = Modifier.align(Alignment.End)
                ) {
                    Text("Деактивировать")
                }
            }
        }
    }
}

@Composable
private fun SymbologySelector(
    selectedSymbology: String,
    onSymbologySelected: (String) -> Unit
) {
    var expanded by remember { mutableStateOf(false) }

    Box(modifier = Modifier.fillMaxWidth()) {
        OutlinedTextField(
            value = selectedSymbology,
            onValueChange = {},
            readOnly = true,
            label = { Text("Симвология") },
            modifier = Modifier.fillMaxWidth()
        )

        Box(
            modifier = Modifier
                .fillMaxSize()
                .background(Color.Transparent)
        ) {
            TextButton(
                onClick = { expanded = true },
                modifier = Modifier.fillMaxSize()
            ) {
                Spacer(modifier = Modifier.width(0.dp))
            }
        }

        DropdownMenu(
            expanded = expanded,
            onDismissRequest = { expanded = false }
        ) {
            SupportedSymbologies.forEach { symbology ->
                DropdownMenuItem(
                    text = { Text(symbology) },
                    onClick = {
                        onSymbologySelected(symbology)
                        expanded = false
                    }
                )
            }
        }
    }
}

@Composable
private fun SelectableEntityCard(
    title: String,
    subtitle: String,
    selected: Boolean,
    onClick: () -> Unit
) {
    Card(
        onClick = onClick,
        colors = CardDefaults.cardColors(
            containerColor = if (selected) Color(0xFFEFF3FF) else Color(0xFFF8F8F8)
        ),
        shape = RoundedCornerShape(12.dp)
    ) {
        Column(
            modifier = Modifier.padding(horizontal = 14.dp, vertical = 12.dp),
            verticalArrangement = Arrangement.spacedBy(4.dp)
        ) {
            Text(
                text = title,
                style = MaterialTheme.typography.titleMedium,
                fontWeight = FontWeight.SemiBold
            )
            Text(
                text = subtitle,
                style = MaterialTheme.typography.bodyMedium,
                color = TextMuted
            )
        }
    }
}
