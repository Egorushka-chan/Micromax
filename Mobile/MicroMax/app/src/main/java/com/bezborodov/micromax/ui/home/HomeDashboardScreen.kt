package com.bezborodov.micromax.ui.home

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.unit.dp
import com.bezborodov.micromax.ui.components.ActionMenuRow
import com.bezborodov.micromax.ui.components.DailyStatsCard
import com.bezborodov.micromax.ui.components.HeaderCompanyBlock
import com.bezborodov.micromax.ui.components.HomeMenuIcon
import com.bezborodov.micromax.ui.components.HomeMenuItem
import com.bezborodov.micromax.ui.components.SearchBarBlock
import com.bezborodov.micromax.ui.components.SectionCard
import com.bezborodov.micromax.ui.components.TextSecondary
import com.bezborodov.micromax.ui.operations.OperationType

@Composable
fun HomeDashboardScreen(
    state: HomeUiState,
    onOpenItems: () -> Unit,
    onOpenAddItem: () -> Unit,
    onOpenCells: () -> Unit,
    onOpenOperation: (OperationType) -> Unit,
    onOpenAssistant: () -> Unit
) {
    LazyColumn(verticalArrangement = Arrangement.spacedBy(14.dp)) {
        item {
            Text(
                text = "Главное окно",
                style = MaterialTheme.typography.labelMedium,
                color = TextSecondary
            )
        }

        item { HeaderCompanyBlock(companyName = state.companyName) }

        item {
            DailyStatsCard(
                dateText = state.dateText,
                totalCount = state.totalOperationCount,
                incomeCount = state.incomeCount,
                outcomeCount = state.outcomeCount
            )
        }

        item {
            SearchBarBlock(
                placeholder = "Поиск товара",
                onSearchClick = onOpenItems,
                onScannerClick = onOpenAssistant
            )
        }

        item {
            SectionCard(title = "Товары") {
                ActionMenuRow(
                    item = HomeMenuItem(
                        "Добавить товар",
                        "Завести новую номенклатуру",
                        HomeMenuIcon.AddItem
                    ),
                    onClick = onOpenAddItem
                )
                ActionMenuRow(
                    item = HomeMenuItem(
                        "Просмотр ячеек",
                        "Содержимое мест хранения",
                        HomeMenuIcon.Cell
                    ),
                    onClick = onOpenCells
                )
            }
        }

        item {
            SectionCard(title = "Транзакции") {
                ActionMenuRow(
                    item = HomeMenuItem(
                        "Приход",
                        "Принять товар в ячейку",
                        HomeMenuIcon.Receive
                    ),
                    onClick = { onOpenOperation(OperationType.Receive) }
                )
                ActionMenuRow(
                    item = HomeMenuItem(
                        "Расход",
                        "Списать товар из ячейки",
                        HomeMenuIcon.WriteOff
                    ),
                    onClick = { onOpenOperation(OperationType.WriteOff) }
                )
                ActionMenuRow(
                    item = HomeMenuItem(
                        "Перемещение",
                        "Перенести товар между ячейками",
                        HomeMenuIcon.Move
                    ),
                    onClick = { onOpenOperation(OperationType.Move) }
                )
                ActionMenuRow(
                    item = HomeMenuItem(
                        "Корректировка",
                        "Установить точный остаток в ячейке",
                        HomeMenuIcon.Adjust
                    ),
                    onClick = { onOpenOperation(OperationType.Adjust) }
                )
            }
        }

        item {
            SectionCard(title = "Помощник") {
                ActionMenuRow(
                    item = HomeMenuItem(
                        "Командный помощник",
                        "Поиск и операции через команды",
                        HomeMenuIcon.Team
                    ),
                    onClick = onOpenAssistant
                )
            }
        }
    }
}
