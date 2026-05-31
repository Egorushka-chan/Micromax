package com.bezborodov.micromax.ui.items

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.material3.Button
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import com.bezborodov.micromax.data.ProductDto
import com.bezborodov.micromax.ui.components.EmptyStateText
import com.bezborodov.micromax.ui.components.PlainInfoRow
import com.bezborodov.micromax.ui.components.SectionCard
import com.bezborodov.micromax.ui.components.SimpleTitle
import com.bezborodov.micromax.ui.components.TextMuted
import com.bezborodov.micromax.ui.home.HomeUiState

@Composable
fun ItemsScreen(
    state: HomeUiState,
    onOpenOperations: () -> Unit
) {
    var query by remember { mutableStateOf("") }
    val products = state.snapshot.products.filter { product ->
        val normalized = query.trim()
        normalized.isBlank() ||
            product.name.contains(normalized, ignoreCase = true) ||
            product.sku.contains(normalized, ignoreCase = true)
    }

    LazyColumn(verticalArrangement = Arrangement.spacedBy(14.dp)) {
        item { SimpleTitle("Товары") }
        item {
            OutlinedTextField(
                value = query,
                onValueChange = { query = it },
                label = { Text("Поиск по названию или артикулу") },
                modifier = Modifier.fillMaxWidth(),
                singleLine = true
            )
        }
        item {
            SectionCard(title = "Номенклатура") {
                if (products.isEmpty()) {
                    EmptyStateText(if (query.isBlank()) "Нет товаров." else "Товары не найдены.")
                } else {
                    products.forEach { product ->
                        ProductCard(
                            product = product,
                            state = state,
                            onOpenOperations = onOpenOperations
                        )
                    }
                }
            }
        }
    }
}

@Composable
private fun ProductCard(
    product: ProductDto,
    state: HomeUiState,
    onOpenOperations: () -> Unit
) {
    val stocks = state.snapshot.stocks.filter { it.sku == product.sku && it.quantity > 0.0 }

    Column(verticalArrangement = Arrangement.spacedBy(8.dp)) {
        PlainInfoRow(
            title = product.name,
            subtitle = "${product.sku} · мин. остаток ${product.minQuantity} ${product.unit}"
        )

        if (stocks.isEmpty()) {
            Text("Нет остатков по ячейкам.", color = TextMuted)
        } else {
            Text("Остатки по ячейкам", fontWeight = FontWeight.SemiBold)
            stocks.forEach { stock ->
                Text(
                    text = "${stock.zoneCode} / ${stock.cellCode}: ${stock.quantity} ${stock.unit}",
                    color = TextMuted
                )
            }
        }

        Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
            Button(onClick = onOpenOperations, modifier = Modifier.weight(1f)) {
                Text("Приход")
            }
            Button(onClick = onOpenOperations, modifier = Modifier.weight(1f)) {
                Text("Расход")
            }
        }
        Button(onClick = onOpenOperations, modifier = Modifier.fillMaxWidth()) {
            Text("Перемещение")
        }
    }
}
