package com.bezborodov.micromax.ui.items

import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.outlined.ArrowBack
import androidx.compose.material.icons.automirrored.outlined.ArrowForwardIos
import androidx.compose.material.icons.automirrored.outlined.Sort
import androidx.compose.material.icons.outlined.Add
import androidx.compose.material.icons.outlined.Inventory2
import androidx.compose.material.icons.outlined.QrCodeScanner
import androidx.compose.material.icons.outlined.Search
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.Button
import androidx.compose.material3.ButtonDefaults
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.DropdownMenu
import androidx.compose.material3.DropdownMenuItem
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import com.bezborodov.micromax.data.BarcodeDto
import com.bezborodov.micromax.data.CellDto
import com.bezborodov.micromax.data.MicroMaxApiClient
import com.bezborodov.micromax.data.ProductDto
import com.bezborodov.micromax.data.StockDto
import com.bezborodov.micromax.data.UnauthorizedException
import com.bezborodov.micromax.ui.barcodes.BarcodeEditorDialog
import com.bezborodov.micromax.ui.barcodes.BarcodeSection
import com.bezborodov.micromax.ui.components.AccentDark
import com.bezborodov.micromax.ui.components.EmptyStateText
import com.bezborodov.micromax.ui.components.PlainInfoRow
import com.bezborodov.micromax.ui.components.SectionCard
import com.bezborodov.micromax.ui.components.TextMuted
import com.bezborodov.micromax.ui.home.HomeUiState
import com.bezborodov.micromax.ui.scanner.ScannedBarcode
import java.util.Locale
import kotlin.math.abs
import kotlin.math.roundToLong
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext

enum class ItemsStartDestination {
    List,
    Add
}

enum class ItemsStockFilter(val title: String) {
    Available("В наличии"),
    All("Все товары"),
    LowStock("Низкий остаток"),
    ZeroStock("Нулевой остаток");

    fun next(): ItemsStockFilter {
        return when (this) {
            Available -> All
            All -> LowStock
            LowStock -> ZeroStock
            ZeroStock -> Available
        }
    }
}

private enum class ItemsDestination {
    List,
    Details,
    Add
}

private enum class ItemSortMode {
    Name,
    Stock
}

private data class ProductSummary(
    val product: ProductDto,
    val totalQuantity: Double,
    val locationCount: Int,
    val lowStock: Boolean,
    val zeroStock: Boolean
)

@Composable
fun ItemsScreen(
    state: HomeUiState,
    isSubmitting: Boolean,
    startDestination: ItemsStartDestination,
    canCreateProducts: Boolean,
    canExecuteOperations: Boolean,
    apiClient: MicroMaxApiClient,
    onSessionExpired: () -> Unit,
    requestedProductId: Int?,
    onRequestedProductConsumed: () -> Unit,
    requestedItemsFilter: ItemsStockFilter?,
    onRequestedItemsFilterConsumed: () -> Unit,
    onOpenScanner: (String, (ScannedBarcode) -> Unit) -> Unit,
    onCreateProduct: (String, String, String, Double, Int?, Double, String?, String?) -> Unit,
    onOpenOperations: () -> Unit
) {
    val effectiveStartDestination = if (canCreateProducts) {
        startDestination
    } else {
        ItemsStartDestination.List
    }

    var destination by rememberSaveable(effectiveStartDestination) {
        mutableStateOf(
            if (effectiveStartDestination == ItemsStartDestination.Add) {
                ItemsDestination.Add.name
            } else {
                ItemsDestination.List.name
            }
        )
    }
    var selectedProductId by rememberSaveable(effectiveStartDestination) { mutableStateOf(-1) }
    var addFormVersion by rememberSaveable { mutableStateOf(0) }
    var awaitingCreateResult by rememberSaveable { mutableStateOf(false) }

    LaunchedEffect(requestedProductId, state.snapshot.products) {
        val targetId = requestedProductId ?: return@LaunchedEffect
        if (state.snapshot.products.any { it.id == targetId }) {
            selectedProductId = targetId
            destination = ItemsDestination.Details.name
            onRequestedProductConsumed()
        }
    }

    if (awaitingCreateResult && !isSubmitting) {
        if (state.message == "Товар добавлен") {
            addFormVersion += 1
            destination = ItemsDestination.List.name
            selectedProductId = -1
        }
        awaitingCreateResult = false
    }

    val selectedProduct = state.snapshot.products.firstOrNull { it.id == selectedProductId }

    when (ItemsDestination.valueOf(destination)) {
        ItemsDestination.List -> ProductsListScreen(
            products = state.snapshot.products,
            stocks = state.snapshot.stocks,
            canCreateProducts = canCreateProducts,
            requestedItemsFilter = requestedItemsFilter,
            onRequestedItemsFilterConsumed = onRequestedItemsFilterConsumed,
            onOpenProduct = {
                selectedProductId = it.id
                destination = ItemsDestination.Details.name
            },
            onAddProduct = {
                selectedProductId = -1
                destination = ItemsDestination.Add.name
            }
        )

        ItemsDestination.Details -> {
            if (selectedProduct == null) {
                ProductsListScreen(
                    products = state.snapshot.products,
                    stocks = state.snapshot.stocks,
                    canCreateProducts = canCreateProducts,
                    requestedItemsFilter = requestedItemsFilter,
                    onRequestedItemsFilterConsumed = onRequestedItemsFilterConsumed,
                    onOpenProduct = {
                        selectedProductId = it.id
                        destination = ItemsDestination.Details.name
                    },
                    onAddProduct = {
                        selectedProductId = -1
                        destination = ItemsDestination.Add.name
                    }
                )
            } else {
                ProductDetailsScreen(
                    product = selectedProduct,
                    stocks = state.snapshot.stocks.filter { it.sku == selectedProduct.sku && it.quantity > 0.0 },
                    apiClient = apiClient,
                    onSessionExpired = onSessionExpired,
                    canManageBarcodes = canCreateProducts,
                    canExecuteOperations = canExecuteOperations,
                    onOpenScanner = onOpenScanner,
                    onBack = { destination = ItemsDestination.List.name },
                    onOpenOperations = onOpenOperations
                )
            }
        }

        ItemsDestination.Add -> {
            if (!canCreateProducts) {
                ProductsListScreen(
                    products = state.snapshot.products,
                    stocks = state.snapshot.stocks,
                    canCreateProducts = false,
                    requestedItemsFilter = requestedItemsFilter,
                    onRequestedItemsFilterConsumed = onRequestedItemsFilterConsumed,
                    onOpenProduct = {
                        selectedProductId = it.id
                        destination = ItemsDestination.Details.name
                    },
                    onAddProduct = {}
                )
            } else {
                AddProductScreen(
                    formVersion = addFormVersion,
                    cells = state.snapshot.cells,
                    isSubmitting = isSubmitting,
                    onBack = { destination = ItemsDestination.List.name },
                    onOpenScanner = onOpenScanner,
                    onSubmit = { sku, name, unit, minQuantity, cellId, quantity, barcodeValue, barcodeSymbology ->
                        awaitingCreateResult = true
                        onCreateProduct(
                            sku,
                            name,
                            unit,
                            minQuantity,
                            cellId,
                            quantity,
                            barcodeValue,
                            barcodeSymbology
                        )
                    }
                )
            }
        }
    }
}

@Composable
private fun ProductsListScreen(
    products: List<ProductDto>,
    stocks: List<StockDto>,
    canCreateProducts: Boolean,
    requestedItemsFilter: ItemsStockFilter?,
    onRequestedItemsFilterConsumed: () -> Unit,
    onOpenProduct: (ProductDto) -> Unit,
    onAddProduct: () -> Unit
) {
    var query by rememberSaveable { mutableStateOf("") }
    var stockFilter by rememberSaveable { mutableStateOf(ItemsStockFilter.Available.name) }
    var sortMode by rememberSaveable { mutableStateOf(ItemSortMode.Name.name) }

    LaunchedEffect(requestedItemsFilter) {
        val filter = requestedItemsFilter ?: return@LaunchedEffect
        stockFilter = filter.name
        onRequestedItemsFilterConsumed()
    }

    val normalizedQuery = query.trim()
    val activeFilter = ItemsStockFilter.valueOf(stockFilter)
    val summaries = remember(products, stocks, normalizedQuery, activeFilter, sortMode) {
        products.map { product ->
            val productStocks = stocks.filter { it.sku == product.sku }
            val totalQuantity = productStocks.sumOf { it.quantity }
            val zeroStock = totalQuantity <= 0.0
            val lowStock = totalQuantity > 0.0 && totalQuantity <= product.minQuantity
            ProductSummary(
                product = product,
                totalQuantity = totalQuantity,
                locationCount = productStocks.count { it.quantity > 0.0 },
                lowStock = lowStock,
                zeroStock = zeroStock
            )
        }.filter { summary ->
            val matchesQuery = normalizedQuery.isBlank() ||
                summary.product.name.contains(normalizedQuery, ignoreCase = true) ||
                summary.product.sku.contains(normalizedQuery, ignoreCase = true) ||
                stocks.any { stock ->
                    stock.sku == summary.product.sku &&
                        (
                            stock.cellCode.contains(normalizedQuery, ignoreCase = true) ||
                                stock.zoneCode.contains(normalizedQuery, ignoreCase = true)
                            )
                }
            val matchesAvailability = when (activeFilter) {
                ItemsStockFilter.Available -> summary.totalQuantity > 0.0
                ItemsStockFilter.All -> true
                ItemsStockFilter.LowStock -> summary.lowStock
                ItemsStockFilter.ZeroStock -> summary.zeroStock
            }
            matchesQuery && matchesAvailability
        }.sortedWith(
            when (ItemSortMode.valueOf(sortMode)) {
                ItemSortMode.Name -> compareBy<ProductSummary> { it.product.name.lowercase(Locale.getDefault()) }
                    .thenBy { it.product.sku.lowercase(Locale.getDefault()) }

                ItemSortMode.Stock -> compareByDescending<ProductSummary> { it.totalQuantity }
                    .thenBy { it.product.name.lowercase(Locale.getDefault()) }
            }
        )
    }

    LazyColumn(
        modifier = Modifier.fillMaxSize(),
        verticalArrangement = Arrangement.spacedBy(14.dp)
    ) {
        item {
            Row(
                modifier = Modifier.fillMaxWidth(),
                verticalAlignment = Alignment.CenterVertically
            ) {
                Spacer(modifier = Modifier.width(48.dp))
                Text(
                    text = "Список товаров",
                    style = MaterialTheme.typography.headlineSmall,
                    fontWeight = FontWeight.Bold,
                    modifier = Modifier.weight(1f),
                    textAlign = TextAlign.Center
                )
                if (canCreateProducts) {
                    IconButton(onClick = onAddProduct) {
                        Icon(
                            imageVector = Icons.Outlined.Add,
                            contentDescription = "Добавить товар",
                            tint = Color.Black
                        )
                    }
                } else {
                    Spacer(modifier = Modifier.width(48.dp))
                }
            }
        }

        item {
            OutlinedTextField(
                value = query,
                onValueChange = { query = it },
                modifier = Modifier.fillMaxWidth(),
                singleLine = true,
                label = { Text("Поиск по названию, SKU или ячейке") },
                leadingIcon = {
                    Icon(
                        imageVector = Icons.Outlined.Search,
                        contentDescription = null,
                        tint = TextMuted
                    )
                }
            )
        }

        item {
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.spacedBy(10.dp)
            ) {
                OutlinedButton(
                    onClick = {
                        sortMode = if (ItemSortMode.valueOf(sortMode) == ItemSortMode.Name) {
                            ItemSortMode.Stock.name
                        } else {
                            ItemSortMode.Name.name
                        }
                    },
                    modifier = Modifier.weight(1f),
                    colors = ButtonDefaults.outlinedButtonColors(containerColor = Color.White)
                ) {
                    Icon(
                        imageVector = Icons.AutoMirrored.Outlined.Sort,
                        contentDescription = null,
                        tint = TextMuted
                    )
                    Spacer(modifier = Modifier.width(8.dp))
                    Text(if (ItemSortMode.valueOf(sortMode) == ItemSortMode.Name) "По названию" else "По остатку")
                }
                OutlinedButton(
                    onClick = { stockFilter = ItemsStockFilter.valueOf(stockFilter).next().name },
                    modifier = Modifier.weight(1f),
                    colors = ButtonDefaults.outlinedButtonColors(containerColor = Color.White)
                ) {
                    Text(activeFilter.title)
                }
            }
        }

        if (summaries.isEmpty()) {
            item {
                SectionCard(title = "Товары") {
                    EmptyStateText(
                        if (normalizedQuery.isBlank()) {
                            "Список товаров пока пуст."
                        } else {
                            "По запросу ничего не найдено."
                        }
                    )
                }
            }
        } else {
            items(summaries, key = { it.product.id }) { summary ->
                ProductListRow(
                    summary = summary,
                    onClick = { onOpenProduct(summary.product) }
                )
            }
        }
    }
}

@Composable
private fun ProductListRow(
    summary: ProductSummary,
    onClick: () -> Unit
) {
    Card(
        modifier = Modifier
            .fillMaxWidth()
            .clickable(onClick = onClick),
        shape = RoundedCornerShape(12.dp),
        colors = CardDefaults.cardColors(containerColor = Color.White),
        elevation = CardDefaults.cardElevation(defaultElevation = 4.dp)
    ) {
        Row(
            modifier = Modifier
                .fillMaxWidth()
                .height(94.dp)
                .background(Color.White),
            verticalAlignment = Alignment.CenterVertically
        ) {
            Spacer(modifier = Modifier.width(16.dp))

            Box(
                modifier = Modifier
                    .width(58.dp)
                    .height(58.dp)
                    .background(Color(0xFFF1F4FF), RoundedCornerShape(12.dp)),
                contentAlignment = Alignment.Center
            ) {
                Icon(
                    imageVector = Icons.Outlined.Inventory2,
                    contentDescription = null,
                    tint = AccentDark
                )
            }

            Spacer(modifier = Modifier.width(14.dp))

            Column(modifier = Modifier.weight(1f)) {
                Text(
                    text = summary.product.name,
                    style = MaterialTheme.typography.titleLarge,
                    color = Color(0xFF1D1D1D),
                    fontWeight = FontWeight.SemiBold
                )
                Text(
                    text = summary.product.sku,
                    style = MaterialTheme.typography.bodyMedium,
                    color = TextMuted
                )
                Text(
                    text = if (summary.locationCount > 0) {
                        "${summary.locationCount} ячеек · ${summary.totalQuantity.formatQuantity()} ${summary.product.unit}"
                    } else {
                        "Нет остатка по ячейкам"
                    },
                    style = MaterialTheme.typography.bodyMedium,
                    color = if (summary.lowStock && summary.totalQuantity > 0.0) Color(0xFFD35C46) else TextMuted
                )
            }

            Column(horizontalAlignment = Alignment.End) {
                Text(
                    text = summary.totalQuantity.formatQuantity(),
                    style = MaterialTheme.typography.headlineSmall,
                    color = if (summary.totalQuantity > 0.0) AccentDark else TextMuted,
                    fontWeight = FontWeight.Bold
                )
                Text(
                    text = summary.product.unit,
                    style = MaterialTheme.typography.bodySmall,
                    color = TextMuted
                )
            }

            Spacer(modifier = Modifier.width(16.dp))
        }
    }
}

@Composable
private fun ProductDetailsScreen(
    product: ProductDto,
    stocks: List<StockDto>,
    apiClient: MicroMaxApiClient,
    onSessionExpired: () -> Unit,
    canManageBarcodes: Boolean,
    canExecuteOperations: Boolean,
    onOpenScanner: (String, (ScannedBarcode) -> Unit) -> Unit,
    onBack: () -> Unit,
    onOpenOperations: () -> Unit
) {
    val totalQuantity = stocks.sumOf { it.quantity }
    val scope = rememberCoroutineScope()

    var barcodes by remember(product.id) { mutableStateOf<List<BarcodeDto>>(emptyList()) }
    var barcodesLoading by remember(product.id) { mutableStateOf(true) }
    var barcodesMessage by remember(product.id) { mutableStateOf<String?>(null) }
    var showAddBarcodeDialog by remember(product.id) { mutableStateOf(false) }
    var barcodeToDeactivate by remember(product.id) { mutableStateOf<BarcodeDto?>(null) }

    suspend fun loadBarcodes() {
        barcodesLoading = true
        val result = runCatching {
            withContext(Dispatchers.IO) { apiClient.getProductBarcodes(product.id) }
        }
        result.fold(
            onSuccess = {
                barcodes = it
                barcodesLoading = false
            },
            onFailure = { error ->
                barcodesLoading = false
                if (error is UnauthorizedException) {
                    onSessionExpired()
                } else {
                    barcodesMessage = error.message ?: "Не удалось загрузить штрих-коды."
                }
            }
        )
    }

    LaunchedEffect(product.id) {
        loadBarcodes()
    }

    LazyColumn(
        modifier = Modifier.fillMaxSize(),
        verticalArrangement = Arrangement.spacedBy(14.dp)
    ) {
        item {
            ScreenHeader(
                title = "Карточка товара",
                onBack = onBack
            )
        }

        item {
            Card(
                modifier = Modifier.fillMaxWidth(),
                shape = RoundedCornerShape(12.dp),
                colors = CardDefaults.cardColors(containerColor = Color.White),
                elevation = CardDefaults.cardElevation(defaultElevation = 4.dp)
            ) {
                Row(
                    modifier = Modifier
                        .fillMaxWidth()
                        .height(124.dp),
                    verticalAlignment = Alignment.CenterVertically
                ) {
                    Spacer(modifier = Modifier.width(18.dp))

                    Box(
                        modifier = Modifier
                            .width(88.dp)
                            .height(88.dp)
                            .background(Color(0xFFF1F4FF), RoundedCornerShape(16.dp)),
                        contentAlignment = Alignment.Center
                    ) {
                        Icon(
                            imageVector = Icons.Outlined.Inventory2,
                            contentDescription = null,
                            tint = AccentDark
                        )
                    }

                    Spacer(modifier = Modifier.width(16.dp))

                    Column(modifier = Modifier.weight(1f)) {
                        Text(
                            text = product.name,
                            style = MaterialTheme.typography.headlineSmall,
                            fontWeight = FontWeight.Bold
                        )
                        Spacer(modifier = Modifier.height(6.dp))
                        Text(
                            text = product.sku,
                            style = MaterialTheme.typography.bodyLarge,
                            color = TextMuted
                        )
                    }

                    Spacer(modifier = Modifier.width(18.dp))
                }
            }
        }

        item {
            SectionCard(title = "Основные данные") {
                PlainInfoRow(title = "SKU", subtitle = product.sku)
                PlainInfoRow(title = "Единица измерения", subtitle = product.unit)
                PlainInfoRow(
                    title = "Минимальный остаток",
                    subtitle = "${product.minQuantity.formatQuantity()} ${product.unit}"
                )
                PlainInfoRow(
                    title = "Остаток на складе",
                    subtitle = "${totalQuantity.formatQuantity()} ${product.unit}"
                )
            }
        }

        item {
            BarcodeSection(
                barcodes = barcodes,
                isLoading = barcodesLoading,
                message = barcodesMessage,
                canManageBarcodes = canManageBarcodes,
                onAddBarcode = {
                    barcodesMessage = null
                    showAddBarcodeDialog = true
                },
                onDeactivateBarcode = {
                    barcodesMessage = null
                    barcodeToDeactivate = it
                }
            )
        }

        item {
            SectionCard(title = "Остатки по ячейкам") {
                if (stocks.isEmpty()) {
                    EmptyStateText("Товар пока не размещён ни в одной ячейке.")
                } else {
                    stocks.sortedWith(compareBy({ it.zoneCode }, { it.cellCode })).forEach { stock ->
                        StockLocationRow(stock = stock)
                    }
                }
            }
        }

        item {
            Text(
                text = "Изменение остатков выполняется только через складские операции.",
                style = MaterialTheme.typography.bodyMedium,
                color = TextMuted
            )
        }

        if (canExecuteOperations) {
            item {
                Button(
                    onClick = onOpenOperations,
                    modifier = Modifier
                        .fillMaxWidth()
                        .height(54.dp)
                ) {
                    Text("Открыть операции")
                }
            }
        }

        item { Spacer(modifier = Modifier.height(6.dp)) }
    }

    if (showAddBarcodeDialog) {
        BarcodeEditorDialog(
            title = "Добавить штрих-код товара",
            confirmButtonText = "Сохранить",
            onDismiss = { showAddBarcodeDialog = false },
            onOpenScanner = { callback ->
                onOpenScanner("Сканирование штрих-кода товара", callback)
            },
            onConfirm = { request ->
                scope.launch {
                    barcodesLoading = true
                    val result = runCatching {
                        withContext(Dispatchers.IO) {
                            apiClient.addProductBarcode(product.id, request)
                            apiClient.getProductBarcodes(product.id)
                        }
                    }
                    result.fold(
                        onSuccess = {
                            barcodes = it
                            barcodesLoading = false
                            barcodesMessage = "Штрих-код привязан к товару."
                            showAddBarcodeDialog = false
                        },
                        onFailure = { error ->
                            barcodesLoading = false
                            if (error is UnauthorizedException) {
                                onSessionExpired()
                            } else {
                                barcodesMessage = error.message ?: "Не удалось привязать штрих-код."
                            }
                        }
                    )
                }
            }
        )
    }

    if (barcodeToDeactivate != null) {
        BarcodeDeactivateDialog(
            value = barcodeToDeactivate?.value.orEmpty(),
            onDismiss = { barcodeToDeactivate = null },
            onConfirm = {
                val currentBarcode = barcodeToDeactivate ?: return@BarcodeDeactivateDialog
                scope.launch {
                    barcodesLoading = true
                    val result = runCatching {
                        withContext(Dispatchers.IO) {
                            apiClient.deactivateBarcode(currentBarcode.id)
                            apiClient.getProductBarcodes(product.id)
                        }
                    }
                    result.fold(
                        onSuccess = {
                            barcodes = it
                            barcodesLoading = false
                            barcodesMessage = "Штрих-код деактивирован."
                            barcodeToDeactivate = null
                        },
                        onFailure = { error ->
                            barcodesLoading = false
                            if (error is UnauthorizedException) {
                                onSessionExpired()
                            } else {
                                barcodesMessage = error.message ?: "Не удалось деактивировать штрих-код."
                            }
                        }
                    )
                }
            }
        )
    }
}

@Composable
private fun StockLocationRow(stock: StockDto) {
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .height(58.dp),
        verticalAlignment = Alignment.CenterVertically
    ) {
        Column(modifier = Modifier.weight(1f)) {
            Text(
                text = "${stock.zoneCode} / ${stock.cellCode}",
                style = MaterialTheme.typography.titleMedium,
                fontWeight = FontWeight.SemiBold
            )
            Text(
                text = stock.productName,
                style = MaterialTheme.typography.bodyMedium,
                color = TextMuted
            )
        }
        Text(
            text = "${stock.quantity.formatQuantity()} ${stock.unit}",
            style = MaterialTheme.typography.titleMedium,
            fontWeight = FontWeight.Bold,
            color = AccentDark
        )
    }
}

@Composable
private fun AddProductScreen(
    formVersion: Int,
    cells: List<CellDto>,
    isSubmitting: Boolean,
    onBack: () -> Unit,
    onOpenScanner: (String, (ScannedBarcode) -> Unit) -> Unit,
    onSubmit: (String, String, String, Double, Int?, Double, String?, String?) -> Unit
) {
    var sku by rememberSaveable(formVersion) { mutableStateOf("") }
    var name by rememberSaveable(formVersion) { mutableStateOf("") }
    var unit by rememberSaveable(formVersion) { mutableStateOf("шт") }
    var minQuantity by rememberSaveable(formVersion) { mutableStateOf("0") }
    var initialQuantity by rememberSaveable(formVersion) { mutableStateOf("0") }
    var barcodeValue by rememberSaveable(formVersion) { mutableStateOf("") }
    var barcodeSymbology by rememberSaveable(formVersion) { mutableStateOf("UNKNOWN") }
    var selectedCellId by rememberSaveable(formVersion) { mutableStateOf<Int?>(null) }
    var localMessage by rememberSaveable(formVersion) { mutableStateOf<String?>(null) }

    LazyColumn(
        modifier = Modifier.fillMaxSize(),
        verticalArrangement = Arrangement.spacedBy(14.dp)
    ) {
        item {
            ScreenHeader(
                title = "Добавить товар",
                onBack = onBack
            )
        }

        item {
            Card(
                modifier = Modifier.fillMaxWidth(),
                shape = RoundedCornerShape(12.dp),
                colors = CardDefaults.cardColors(containerColor = Color.White),
                elevation = CardDefaults.cardElevation(defaultElevation = 4.dp)
            ) {
                Column(
                    modifier = Modifier.fillMaxWidth(),
                    horizontalAlignment = Alignment.CenterHorizontally
                ) {
                    Spacer(modifier = Modifier.height(22.dp))
                    Box(
                        modifier = Modifier
                            .width(96.dp)
                            .height(96.dp)
                            .background(Color(0xFFF1F4FF), RoundedCornerShape(18.dp)),
                        contentAlignment = Alignment.Center
                    ) {
                        Icon(
                            imageVector = Icons.Outlined.Inventory2,
                            contentDescription = null,
                            tint = AccentDark
                        )
                    }
                    Spacer(modifier = Modifier.height(14.dp))
                    Text(
                        text = "Новая номенклатура",
                        style = MaterialTheme.typography.titleLarge,
                        fontWeight = FontWeight.Bold
                    )
                    Spacer(modifier = Modifier.height(6.dp))
                    Text(
                        text = "Заполните основные данные и при необходимости задайте стартовый остаток и внешний штрих-код.",
                        style = MaterialTheme.typography.bodyMedium,
                        color = TextMuted,
                        textAlign = TextAlign.Center
                    )
                    Spacer(modifier = Modifier.height(22.dp))
                }
            }
        }

        if (localMessage != null) {
            item {
                Text(
                    text = localMessage.orEmpty(),
                    style = MaterialTheme.typography.bodyMedium,
                    color = Color(0xFFD35C46)
                )
            }
        }

        item {
            SectionCard(title = "Основные данные") {
                OutlinedTextField(
                    value = sku,
                    onValueChange = {
                        sku = it
                        localMessage = null
                    },
                    label = { Text("SKU") },
                    modifier = Modifier.fillMaxWidth(),
                    singleLine = true
                )
                OutlinedTextField(
                    value = name,
                    onValueChange = {
                        name = it
                        localMessage = null
                    },
                    label = { Text("Название") },
                    modifier = Modifier.fillMaxWidth(),
                    singleLine = true
                )
            }
        }

        item {
            SectionCard(title = "Атрибуты") {
                OutlinedTextField(
                    value = unit,
                    onValueChange = {
                        unit = it
                        localMessage = null
                    },
                    label = { Text("Единица измерения") },
                    modifier = Modifier.fillMaxWidth(),
                    singleLine = true
                )
                OutlinedTextField(
                    value = minQuantity,
                    onValueChange = {
                        minQuantity = it
                        localMessage = null
                    },
                    label = { Text("Минимальный остаток") },
                    modifier = Modifier.fillMaxWidth(),
                    singleLine = true
                )
            }
        }

        item {
            SectionCard(title = "Штрих-код") {
                OutlinedTextField(
                    value = barcodeValue,
                    onValueChange = {
                        barcodeValue = it
                        if (it.isBlank()) {
                            barcodeSymbology = "UNKNOWN"
                        }
                        localMessage = null
                    },
                    label = { Text("Значение штрих-кода") },
                    modifier = Modifier.fillMaxWidth(),
                    singleLine = true
                )
                OutlinedTextField(
                    value = barcodeSymbology,
                    onValueChange = {},
                    readOnly = true,
                    label = { Text("Симвология") },
                    modifier = Modifier.fillMaxWidth(),
                    singleLine = true
                )
                OutlinedButton(
                    onClick = {
                        onOpenScanner("Сканирование штрих-кода товара") { scannedBarcode ->
                            barcodeValue = scannedBarcode.rawValue
                            barcodeSymbology = scannedBarcode.symbology
                            localMessage = null
                        }
                    },
                    modifier = Modifier.fillMaxWidth()
                ) {
                    Icon(
                        imageVector = Icons.Outlined.QrCodeScanner,
                        contentDescription = null
                    )
                    Spacer(modifier = Modifier.width(8.dp))
                    Text("Сканировать камерой")
                }
                Text(
                    text = "Поле необязательное. Значение сохраняется как строка, вместе с символами # и ведущими нулями.",
                    style = MaterialTheme.typography.bodySmall,
                    color = TextMuted
                )
            }
        }

        item {
            SectionCard(title = "Начальный остаток") {
                CellSelectorField(
                    cells = cells.sortedBy { it.code },
                    selectedCellId = selectedCellId,
                    onCellSelected = {
                        selectedCellId = it
                        localMessage = null
                    }
                )
                OutlinedTextField(
                    value = initialQuantity,
                    onValueChange = {
                        initialQuantity = it
                        localMessage = null
                    },
                    label = { Text("Количество") },
                    modifier = Modifier.fillMaxWidth(),
                    singleLine = true
                )
                Text(
                    text = "Если количество равно 0, ячейку можно не указывать.",
                    style = MaterialTheme.typography.bodySmall,
                    color = TextMuted
                )
            }
        }

        item {
            Button(
                onClick = {
                    val parsedMinQuantity = minQuantity.parseNumber()
                    val parsedInitialQuantity = initialQuantity.parseNumber()
                    val normalizedBarcodeValue = barcodeValue.trim()

                    localMessage = when {
                        sku.isBlank() -> "Укажите SKU."
                        name.isBlank() -> "Укажите название товара."
                        unit.isBlank() -> "Укажите единицу измерения."
                        parsedMinQuantity == null -> "Минимальный остаток должен быть числом."
                        parsedInitialQuantity == null -> "Количество должно быть числом."
                        parsedMinQuantity < 0.0 -> "Минимальный остаток не может быть отрицательным."
                        parsedInitialQuantity < 0.0 -> "Количество не может быть отрицательным."
                        parsedInitialQuantity > 0.0 && selectedCellId == null -> "Для начального остатка выберите ячейку."
                        else -> null
                    }

                    if (localMessage == null) {
                        onSubmit(
                            sku.trim(),
                            name.trim(),
                            unit.trim(),
                            parsedMinQuantity ?: 0.0,
                            selectedCellId,
                            parsedInitialQuantity ?: 0.0,
                            normalizedBarcodeValue.ifEmpty { null },
                            if (normalizedBarcodeValue.isEmpty()) null else barcodeSymbology
                        )
                    }
                },
                enabled = !isSubmitting,
                modifier = Modifier
                    .fillMaxWidth()
                    .height(54.dp)
            ) {
                Text(if (isSubmitting) "Сохранение..." else "Сохранить")
            }
        }

        item { Spacer(modifier = Modifier.height(6.dp)) }
    }
}

@Composable
private fun CellSelectorField(
    cells: List<CellDto>,
    selectedCellId: Int?,
    onCellSelected: (Int?) -> Unit
) {
    var expanded by remember { mutableStateOf(false) }
    val selectedCell = cells.firstOrNull { it.id == selectedCellId }

    Box(modifier = Modifier.fillMaxWidth()) {
        OutlinedTextField(
            value = selectedCell?.let { "${it.code} · ${it.name}" } ?: "Не выбрана",
            onValueChange = {},
            readOnly = true,
            modifier = Modifier.fillMaxWidth(),
            label = { Text("Ячейка размещения") },
            trailingIcon = {
                Icon(
                    imageVector = Icons.AutoMirrored.Outlined.ArrowForwardIos,
                    contentDescription = null,
                    tint = TextMuted
                )
            }
        )

        Box(
            modifier = Modifier
                .fillMaxSize()
                .clickable { expanded = true }
        )

        DropdownMenu(
            expanded = expanded,
            onDismissRequest = { expanded = false },
            modifier = Modifier.fillMaxWidth(0.92f)
        ) {
            DropdownMenuItem(
                text = { Text("Не указывать") },
                onClick = {
                    onCellSelected(null)
                    expanded = false
                }
            )
            cells.forEach { cell ->
                DropdownMenuItem(
                    text = { Text("${cell.code} · ${cell.name}") },
                    onClick = {
                        onCellSelected(cell.id)
                        expanded = false
                    }
                )
            }
        }
    }
}

@Composable
private fun ScreenHeader(
    title: String,
    onBack: () -> Unit
) {
    Row(
        modifier = Modifier.fillMaxWidth(),
        verticalAlignment = Alignment.CenterVertically
    ) {
        IconButton(onClick = onBack) {
            Icon(
                imageVector = Icons.AutoMirrored.Outlined.ArrowBack,
                contentDescription = "Назад",
                tint = Color.Black
            )
        }
        Text(
            text = title,
            style = MaterialTheme.typography.headlineSmall,
            fontWeight = FontWeight.Bold,
            modifier = Modifier.weight(1f),
            textAlign = TextAlign.Center
        )
        Spacer(modifier = Modifier.width(48.dp))
    }
}

@Composable
private fun BarcodeDeactivateDialog(
    value: String,
    onDismiss: () -> Unit,
    onConfirm: () -> Unit
) {
    AlertDialog(
        onDismissRequest = onDismiss,
        title = { Text("Деактивация штрих-кода") },
        text = {
            Text("Штрих-код \"$value\" будет отключён. Эту запись можно будет сохранить в истории, но она перестанет участвовать в поиске.")
        },
        confirmButton = {
            Button(onClick = onConfirm) {
                Text("Деактивировать")
            }
        },
        dismissButton = {
            TextButton(onClick = onDismiss) {
                Text("Отмена")
            }
        }
    )
}

private fun Double.formatQuantity(): String {
    val rounded = roundToLong().toDouble()
    return if (abs(this - rounded) < 0.000001) {
        rounded.toLong().toString()
    } else {
        String.format(Locale.US, "%.2f", this).trimEnd('0').trimEnd('.')
    }
}

private fun String.parseNumber(): Double? {
    return trim().replace(',', '.').toDoubleOrNull()
}
