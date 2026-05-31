package com.bezborodov.micromax.ui.home

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Modifier
import androidx.compose.ui.unit.dp
import com.bezborodov.micromax.ui.components.PlainInfoRow
import com.bezborodov.micromax.ui.components.SectionCard
import com.bezborodov.micromax.ui.components.SimpleTitle

@Composable
fun SettingsScreen(
    state: HomeUiState,
    onRefresh: () -> Unit
) {
    LazyColumn(verticalArrangement = Arrangement.spacedBy(14.dp)) {
        item { SimpleTitle("Настройки") }
        item {
            SectionCard(title = "Сервер") {
                PlainInfoRow("Подключение", "http://10.0.2.2:5101")
                PlainInfoRow("Номенклатура", "${state.snapshot.products.size} позиций")
                PlainInfoRow("Ячейки хранения", "${state.snapshot.cells.size} ячеек")
                OutlinedButton(onClick = onRefresh, modifier = Modifier.fillMaxWidth()) {
                    Text("Обновить данные")
                }
            }
        }
    }
}
