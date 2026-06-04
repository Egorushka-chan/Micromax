package com.bezborodov.micromax.ui.cells

import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.Button
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import com.bezborodov.micromax.data.CellDto
import com.bezborodov.micromax.ui.components.EmptyStateText
import com.bezborodov.micromax.ui.components.PlainInfoRow
import com.bezborodov.micromax.ui.components.SectionCard
import com.bezborodov.micromax.ui.components.SimpleTitle
import com.bezborodov.micromax.ui.components.TextMuted
import com.bezborodov.micromax.ui.home.HomeUiState

@Composable
fun CellsScreen(
    state: HomeUiState,
    canExecuteOperations: Boolean,
    onOpenOperations: () -> Unit
) {
    val cells = state.snapshot.cells
    var selectedCell by remember(cells) { mutableStateOf(cells.firstOrNull()) }

    LazyColumn(verticalArrangement = Arrangement.spacedBy(14.dp)) {
        item { SimpleTitle("Ячейки") }
        item {
            SectionCard(title = "Список ячеек") {
                if (cells.isEmpty()) {
                    EmptyStateText("Нет ячеек хранения.")
                } else {
                    cells.forEach { cell ->
                        CellRow(
                            cell = cell,
                            selected = cell.id == selectedCell?.id,
                            onClick = { selectedCell = cell }
                        )
                    }
                }
            }
        }
        item {
            SectionCard(title = "Содержимое ячейки") {
                val cell = selectedCell
                if (cell == null) {
                    EmptyStateText("Выберите ячейку для просмотра содержимого.")
                } else {
                    val stocks = state.snapshot.stocks.filter { it.cellCode == cell.code && it.quantity > 0.0 }
                    PlainInfoRow(cell.code, cell.name)
                    Text(
                        text = "Позиций в ячейке: ${stocks.size}",
                        style = MaterialTheme.typography.bodyMedium,
                        color = TextMuted
                    )

                    if (stocks.isEmpty()) {
                        EmptyStateText("В выбранной ячейке нет остатков.")
                    } else {
                        stocks.forEach { stock ->
                            PlainInfoRow(
                                title = stock.productName,
                                subtitle = "${stock.quantity} ${stock.unit} · ${stock.sku}"
                            )
                        }
                        if (canExecuteOperations) {
                            Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                                Button(onClick = onOpenOperations, modifier = Modifier.weight(1f)) {
                                    Text("Расход")
                                }
                                Button(onClick = onOpenOperations, modifier = Modifier.weight(1f)) {
                                    Text("Переместить")
                                }
                            }
                        } else {
                            Text(
                                text = "Изменение остатков доступно только пользователям с правом выполнения операций.",
                                style = MaterialTheme.typography.bodyMedium,
                                color = TextMuted
                            )
                        }
                    }
                }
            }
        }
    }
}

@Composable
private fun CellRow(
    cell: CellDto,
    selected: Boolean,
    onClick: () -> Unit
) {
    Column(
        modifier = Modifier
            .fillMaxWidth()
            .clip(RoundedCornerShape(8.dp))
            .clickable(onClick = onClick)
    ) {
        Text(
            text = cell.code,
            style = MaterialTheme.typography.titleLarge,
            fontWeight = if (selected) FontWeight.Bold else FontWeight.SemiBold
        )
        Text(
            text = cell.name,
            style = MaterialTheme.typography.bodyMedium,
            color = TextMuted
        )
    }
}
