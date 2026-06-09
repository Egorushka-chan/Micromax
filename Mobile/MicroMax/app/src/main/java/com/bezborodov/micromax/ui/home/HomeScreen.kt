package com.bezborodov.micromax.ui.home

import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.Button
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.setValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.platform.LocalLifecycleOwner
import androidx.compose.ui.unit.dp
import androidx.lifecycle.Lifecycle
import androidx.lifecycle.repeatOnLifecycle
import androidx.lifecycle.viewmodel.compose.viewModel
import com.bezborodov.micromax.data.BarcodeDraftDto
import com.bezborodov.micromax.data.CellDto
import com.bezborodov.micromax.data.MicroMaxApiClient
import com.bezborodov.micromax.data.ProductDto
import com.bezborodov.micromax.data.UnauthorizedException
import com.bezborodov.micromax.ui.assistant.AiAssistantItemsFilter
import com.bezborodov.micromax.ui.assistant.AiAssistantOperationType
import com.bezborodov.micromax.ui.assistant.AiAssistantOverlay
import com.bezborodov.micromax.ui.assistant.AiAssistantViewModel
import com.bezborodov.micromax.ui.assistant.AiAssistantViewModelFactory
import com.bezborodov.micromax.ui.auth.SessionUiState
import com.bezborodov.micromax.ui.barcodes.BarcodeBindingDialog
import com.bezborodov.micromax.ui.barcodes.BarcodeEditorDialog
import com.bezborodov.micromax.ui.cells.CellsScreen
import com.bezborodov.micromax.ui.components.BottomTab
import com.bezborodov.micromax.ui.components.FirstLoadErrorState
import com.bezborodov.micromax.ui.components.HomeBottomBar
import com.bezborodov.micromax.ui.components.LoadingState
import com.bezborodov.micromax.ui.components.MessageBanner
import com.bezborodov.micromax.ui.components.ScreenBg
import com.bezborodov.micromax.ui.items.ItemsScreen
import com.bezborodov.micromax.ui.items.ItemsStartDestination
import com.bezborodov.micromax.ui.items.ItemsStockFilter
import com.bezborodov.micromax.ui.operations.OperationType
import com.bezborodov.micromax.ui.operations.OperationsScreen
import com.bezborodov.micromax.ui.scanner.BarcodeScannerScreen
import com.bezborodov.micromax.ui.scanner.ScannedBarcode
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.delay
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext

private const val PollingIntervalMs = 15_000L

private enum class BarcodeBindingTarget {
    Product,
    Cell
}

private data class ScannerSession(
    val title: String,
    val onScanned: (ScannedBarcode) -> Unit
)

@Composable
fun HomeScreen(
    apiClient: MicroMaxApiClient,
    sessionState: SessionUiState,
    onSessionExpired: () -> Unit,
    onLogout: () -> Unit,
    onSelectActiveWarehouse: (Int) -> Unit,
    onLoadWarehouseUsers: (Boolean) -> Unit,
    onAddWarehouseUser: (String, String) -> Unit,
    onUpdateWarehouseUserRole: (Int, String) -> Unit,
    onRemoveWarehouseUser: (Int) -> Unit,
    onCreateWarehouse: (String, String?) -> Unit,
    onCreateWarehouseFromTemplate: (String, String?, String) -> Unit,
    onLoadWarehouseTemplates: (Boolean) -> Unit,
    onClearSessionMessage: () -> Unit
) {
    val userId = sessionState.currentUser?.id ?: 0
    val selectedWarehouseId = sessionState.selectedWarehouseId ?: return
    val lifecycleOwner = LocalLifecycleOwner.current
    val permissions = sessionState.permissions
    val scope = rememberCoroutineScope()
    val viewModel: WarehouseViewModel = viewModel(
        key = "warehouse-$userId-$selectedWarehouseId",
        factory = WarehouseViewModelFactory(apiClient, selectedWarehouseId)
    )
    val assistantViewModel: AiAssistantViewModel = viewModel(
        key = "assistant-$userId-$selectedWarehouseId",
        factory = AiAssistantViewModelFactory(apiClient, selectedWarehouseId)
    )

    var selectedTab by remember(userId) { mutableStateOf(BottomTab.Home) }
    var itemsStartDestination by remember(userId) { mutableStateOf(ItemsStartDestination.List) }
    var pendingOperationType by remember(userId) { mutableStateOf<OperationType?>(null) }
    var requestedProductId by remember(userId) { mutableStateOf<Int?>(null) }
    var requestedItemsFilter by remember(userId) { mutableStateOf<ItemsStockFilter?>(null) }
    var requestedCellId by remember(userId) { mutableStateOf<Int?>(null) }
    var barcodeMessage by remember(userId) { mutableStateOf<String?>(null) }
    var unresolvedBarcode by remember(userId) { mutableStateOf<ScannedBarcode?>(null) }
    var bindingTarget by remember(userId) { mutableStateOf<BarcodeBindingTarget?>(null) }
    var showManualBarcodeDialog by remember(userId) { mutableStateOf(false) }
    var scannerSession by remember(userId) { mutableStateOf<ScannerSession?>(null) }
    var isBarcodeActionInProgress by remember(userId) { mutableStateOf(false) }
    var isWarehouseMenuOpen by remember(userId, selectedWarehouseId) { mutableStateOf(false) }
    var pendingWarehouseSelectionId by remember(userId, selectedWarehouseId) { mutableStateOf<Int?>(null) }

    val state = viewModel.uiState
    val assistantState = assistantViewModel.uiState
    val warehouseName = sessionState.selectedWarehouse?.warehouseName
    val hasLoadedData = state.snapshot.products.isNotEmpty() ||
        state.snapshot.cells.isNotEmpty() ||
        state.snapshot.stocks.isNotEmpty() ||
        state.snapshot.operations.isNotEmpty()
    val manageableCells = remember(sessionState.currentUser, state.snapshot.cells) {
        state.snapshot.cells.filter { sessionState.canManageWarehouse(it.warehouseId) }
    }

    fun openScanner(title: String, onScanned: (ScannedBarcode) -> Unit) {
        scannerSession = ScannerSession(title, onScanned)
    }

    fun handleResolveSuccess(scannedBarcode: ScannedBarcode, entityType: String?, entityId: Int?, title: String?) {
        unresolvedBarcode = null
        bindingTarget = null
        showManualBarcodeDialog = false

        when (entityType) {
            "Product" -> {
                requestedProductId = entityId
                requestedItemsFilter = ItemsStockFilter.Available
                requestedCellId = null
                itemsStartDestination = ItemsStartDestination.List
                selectedTab = BottomTab.Items
                barcodeMessage = if (!title.isNullOrBlank()) {
                    "Открыта карточка товара: $title."
                } else {
                    "Штрих-код ${scannedBarcode.rawValue} найден."
                }
            }

            "Cell" -> {
                requestedCellId = entityId
                requestedProductId = null
                selectedTab = BottomTab.Cells
                barcodeMessage = if (!title.isNullOrBlank()) {
                    "Открыта карточка ячейки: $title."
                } else {
                    "Штрих-код ${scannedBarcode.rawValue} найден."
                }
            }

            else -> {
                barcodeMessage = "Штрих-код распознан, но тип связанного объекта не поддерживается."
            }
        }
    }

    fun resolveBarcode(scannedBarcode: ScannedBarcode) {
        scope.launch {
            isBarcodeActionInProgress = true
            barcodeMessage = null

            val result = runCatching {
                withContext(Dispatchers.IO) { apiClient.resolveBarcode(selectedWarehouseId, scannedBarcode.rawValue) }
            }

            isBarcodeActionInProgress = false
            result.fold(
                onSuccess = { response ->
                    if (response.found) {
                        handleResolveSuccess(scannedBarcode, response.entityType, response.entityId, response.title)
                    } else {
                        unresolvedBarcode = scannedBarcode
                    }
                },
                onFailure = { error ->
                    if (error is UnauthorizedException) {
                        onSessionExpired()
                    } else {
                        barcodeMessage = error.message ?: "Не удалось обработать штрих-код."
                    }
                }
            )
        }
    }

    fun bindBarcodeToProduct(product: ProductDto, barcode: ScannedBarcode) {
        scope.launch {
            isBarcodeActionInProgress = true
            val result = runCatching {
                withContext(Dispatchers.IO) {
                    apiClient.addProductBarcode(
                        selectedWarehouseId,
                        product.id,
                        BarcodeDraftDto(
                            value = barcode.rawValue,
                            symbology = barcode.symbology
                        )
                    )
                }
            }

            isBarcodeActionInProgress = false
            result.fold(
                onSuccess = {
                    bindingTarget = null
                    unresolvedBarcode = null
                    requestedProductId = product.id
                    requestedItemsFilter = ItemsStockFilter.Available
                    requestedCellId = null
                    itemsStartDestination = ItemsStartDestination.List
                    selectedTab = BottomTab.Items
                    barcodeMessage = "Штрих-код привязан к товару."
                },
                onFailure = { error ->
                    if (error is UnauthorizedException) {
                        onSessionExpired()
                    } else {
                        barcodeMessage = error.message ?: "Не удалось привязать штрих-код к товару."
                    }
                }
            )
        }
    }

    fun bindBarcodeToCell(cell: CellDto, barcode: ScannedBarcode) {
        scope.launch {
            isBarcodeActionInProgress = true
            val result = runCatching {
                withContext(Dispatchers.IO) {
                    apiClient.addCellBarcode(
                        cell.id,
                        BarcodeDraftDto(
                            value = barcode.rawValue,
                            symbology = barcode.symbology
                        )
                    )
                }
            }

            isBarcodeActionInProgress = false
            result.fold(
                onSuccess = {
                    bindingTarget = null
                    unresolvedBarcode = null
                    requestedCellId = cell.id
                    requestedProductId = null
                    selectedTab = BottomTab.Cells
                    barcodeMessage = "Штрих-код привязан к ячейке."
                },
                onFailure = { error ->
                    if (error is UnauthorizedException) {
                        onSessionExpired()
                    } else {
                        barcodeMessage = error.message ?: "Не удалось привязать штрих-код к ячейке."
                    }
                }
            )
        }
    }

    LaunchedEffect(lifecycleOwner, userId) {
        lifecycleOwner.lifecycle.repeatOnLifecycle(Lifecycle.State.STARTED) {
            viewModel.refreshByPolling()
            while (true) {
                delay(PollingIntervalMs)
                viewModel.refreshByPolling()
            }
        }
    }

    LaunchedEffect(state.requiresReauthentication, assistantState.requiresReauthentication) {
        if (state.requiresReauthentication || assistantState.requiresReauthentication) {
            onSessionExpired()
        }
    }

    LaunchedEffect(sessionState.selectedWarehouseId, pendingWarehouseSelectionId) {
        if (pendingWarehouseSelectionId != null && pendingWarehouseSelectionId == sessionState.selectedWarehouseId) {
            pendingWarehouseSelectionId = null
            isWarehouseMenuOpen = false
        }
    }

    LaunchedEffect(assistantState.lastResult) {
        val result = assistantState.lastResult ?: return@LaunchedEffect
        val action = result.clientAction
        when (action?.commandType) {
            "open_products" -> {
                requestedProductId = null
                requestedCellId = null
                requestedItemsFilter = action?.itemsFilter?.toItemsStockFilter()
                    ?: ItemsStockFilter.Available
                itemsStartDestination = ItemsStartDestination.List
                selectedTab = BottomTab.Items
            }

            "find_product" -> {
                requestedProductId = action?.productId
                requestedCellId = null
                requestedItemsFilter = action?.itemsFilter?.toItemsStockFilter()
                    ?: ItemsStockFilter.Available
                itemsStartDestination = ItemsStartDestination.List
                selectedTab = BottomTab.Items
            }

            "low_stock",
            "zero_stock" -> {
                requestedProductId = null
                requestedCellId = null
                requestedItemsFilter = action?.itemsFilter?.toItemsStockFilter()
                itemsStartDestination = ItemsStartDestination.List
                selectedTab = BottomTab.Items
            }

            "create_product" -> {
                requestedProductId = null
                requestedCellId = null
                requestedItemsFilter = ItemsStockFilter.Available
                itemsStartDestination = ItemsStartDestination.List
                selectedTab = BottomTab.Items
            }

            "update_min_stock" -> {
                requestedProductId = action?.productId
                requestedCellId = null
                requestedItemsFilter = ItemsStockFilter.Available
                itemsStartDestination = ItemsStartDestination.List
                selectedTab = BottomTab.Items
            }

            "move_product",
            "write_off_product",
            "create_receipt",
            "post_receipt" -> {
                if (permissions.canExecuteOperations) {
                    pendingOperationType = action?.operationType?.toOperationType()
                    selectedTab = BottomTab.Transactions
                }
            }
        }

        if (result.success && action?.commandType?.requiresSnapshotRefresh() == true) {
            viewModel.refreshByPolling()
        }
    }

    LaunchedEffect(selectedTab, sessionState.selectedWarehouseId, sessionState.canManageSelectedWarehouseUsers) {
        if (selectedTab == BottomTab.Settings && sessionState.canManageSelectedWarehouseUsers) {
            onLoadWarehouseUsers(false)
        }
    }

    Box(modifier = Modifier.fillMaxSize()) {
        Scaffold(
            containerColor = ScreenBg,
            bottomBar = {
                HomeBottomBar(
                    selectedTab = selectedTab,
                    onTabClick = {
                        selectedTab = it
                        if (it == BottomTab.Items) {
                            itemsStartDestination = ItemsStartDestination.List
                        }
                        if (it != BottomTab.Settings) {
                            onClearSessionMessage()
                        }
                    },
                    onAssistantClick = assistantViewModel::open
                )
            }
        ) { innerPadding ->
            Column(
                modifier = Modifier
                    .fillMaxSize()
                    .background(ScreenBg)
                    .padding(innerPadding)
                    .padding(horizontal = 16.dp, vertical = 12.dp)
            ) {
                if (barcodeMessage != null && hasLoadedData) {
                    MessageBanner(barcodeMessage.orEmpty())
                    Spacer(modifier = Modifier.height(10.dp))
                }

                if (state.message != null && hasLoadedData) {
                    MessageBanner(state.message)
                    Spacer(modifier = Modifier.height(10.dp))
                }

                when {
                    state.isLoading && !hasLoadedData -> LoadingState()
                    state.message != null && !hasLoadedData -> FirstLoadErrorState(
                        message = state.message,
                        onRefresh = viewModel::retryInitialLoad
                    )

                    else -> when (selectedTab) {
                        BottomTab.Home -> HomeDashboardScreen(
                            state = state,
                            warehouseName = warehouseName,
                            canCreateProducts = permissions.canCreateProducts,
                            canExecuteOperations = permissions.canExecuteOperations,
                            onOpenWarehouseMenu = { isWarehouseMenuOpen = true },
                            onOpenItems = {
                                itemsStartDestination = ItemsStartDestination.List
                                selectedTab = BottomTab.Items
                            },
                            onOpenAddItem = {
                                itemsStartDestination = ItemsStartDestination.Add
                                selectedTab = BottomTab.Items
                            },
                            onOpenCells = { selectedTab = BottomTab.Cells },
                            onOpenOperation = { type ->
                                pendingOperationType = type
                                selectedTab = BottomTab.Transactions
                            },
                            onOpenAssistant = assistantViewModel::open,
                            onOpenBarcodeScanner = {
                                openScanner("Сканирование штрих-кода") { scannedBarcode ->
                                    resolveBarcode(scannedBarcode)
                                }
                            }
                        )

                        BottomTab.Items -> ItemsScreen(
                            state = state,
                            isSubmitting = state.isOperationSubmitting,
                            startDestination = itemsStartDestination,
                            warehouseId = selectedWarehouseId,
                            canCreateProducts = permissions.canCreateProducts,
                            canExecuteOperations = permissions.canExecuteOperations,
                            apiClient = apiClient,
                            onSessionExpired = onSessionExpired,
                            requestedProductId = requestedProductId,
                            onRequestedProductConsumed = { requestedProductId = null },
                            requestedItemsFilter = requestedItemsFilter,
                            onRequestedItemsFilterConsumed = { requestedItemsFilter = null },
                            onOpenScanner = ::openScanner,
                            onCreateProduct = viewModel::createProduct,
                            onOpenOperations = { selectedTab = BottomTab.Transactions }
                        )

                        BottomTab.Cells -> CellsScreen(
                            state = state,
                            warehouseId = selectedWarehouseId,
                            apiClient = apiClient,
                            onSessionExpired = onSessionExpired,
                            canExecuteOperations = permissions.canExecuteOperations,
                            canManageCellBarcodes = sessionState::canManageWarehouse,
                            requestedCellId = requestedCellId,
                            onRequestedCellConsumed = { requestedCellId = null },
                            onOpenScanner = ::openScanner,
                            onOpenOperations = { selectedTab = BottomTab.Transactions }
                        )

                        BottomTab.Assistant -> HomeDashboardScreen(
                            state = state,
                            warehouseName = warehouseName,
                            canCreateProducts = permissions.canCreateProducts,
                            canExecuteOperations = permissions.canExecuteOperations,
                            onOpenWarehouseMenu = { isWarehouseMenuOpen = true },
                            onOpenItems = {
                                itemsStartDestination = ItemsStartDestination.List
                                selectedTab = BottomTab.Items
                            },
                            onOpenAddItem = {
                                itemsStartDestination = ItemsStartDestination.Add
                                selectedTab = BottomTab.Items
                            },
                            onOpenCells = { selectedTab = BottomTab.Cells },
                            onOpenOperation = { type ->
                                pendingOperationType = type
                                selectedTab = BottomTab.Transactions
                            },
                            onOpenAssistant = assistantViewModel::open,
                            onOpenBarcodeScanner = {
                                openScanner("Сканирование штрих-кода") { scannedBarcode ->
                                    resolveBarcode(scannedBarcode)
                                }
                            }
                        )

                        BottomTab.Transactions -> OperationsScreen(
                            state = state,
                            canExecuteOperations = permissions.canExecuteOperations,
                            requestedOperationType = pendingOperationType,
                            onRequestedOperationConsumed = { pendingOperationType = null },
                            onReceive = viewModel::receive,
                            onWriteOff = viewModel::writeOff,
                            onMove = viewModel::move,
                            onAdjust = viewModel::adjust
                        )

                        BottomTab.Settings -> SettingsScreen(
                            state = state,
                            sessionState = sessionState,
                            onRefresh = viewModel::refreshManually,
                            onLogout = onLogout,
                            onSelectActiveWarehouse = onSelectActiveWarehouse,
                            onReloadWarehouseUsers = { onLoadWarehouseUsers(true) },
                            onAddWarehouseUser = onAddWarehouseUser,
                            onUpdateWarehouseUserRole = onUpdateWarehouseUserRole,
                            onRemoveWarehouseUser = onRemoveWarehouseUser
                        )
                    }
                }
            }
        }

        AiAssistantOverlay(
            state = assistantState,
            snapshot = state.snapshot,
            onClose = assistantViewModel::close,
            onInputChange = assistantViewModel::onInputChange,
            onSubmit = assistantViewModel::submitCurrent,
            onPromptClick = assistantViewModel::usePrompt,
            onConfirm = assistantViewModel::confirmPending,
            onClarificationChoice = assistantViewModel::chooseClarification,
            onCancelPending = assistantViewModel::rejectPending
        )

        if (isWarehouseMenuOpen) {
            Box(
                modifier = Modifier
                    .fillMaxSize()
                    .background(ScreenBg)
            ) {
                WarehouseMenuScreen(
                    sessionState = sessionState,
                    onSelectWarehouse = { warehouseId ->
                        if (warehouseId == sessionState.selectedWarehouseId) {
                            pendingWarehouseSelectionId = null
                            isWarehouseMenuOpen = false
                        } else {
                            pendingWarehouseSelectionId = warehouseId
                            onSelectActiveWarehouse(warehouseId)
                        }
                    },
                    onCreateWarehouse = onCreateWarehouse,
                    onCreateWarehouseFromTemplate = onCreateWarehouseFromTemplate,
                    onLoadTemplates = onLoadWarehouseTemplates,
                    onClose = { isWarehouseMenuOpen = false }
                )
            }
        }

        if (isBarcodeActionInProgress) {
            Box(
                modifier = Modifier
                    .fillMaxSize()
                    .background(Color.Black.copy(alpha = 0.12f))
            ) {
                LoadingState()
            }
        }
    }

    if (showManualBarcodeDialog) {
        BarcodeEditorDialog(
            title = "Ввод штрих-кода",
            confirmButtonText = "Найти",
            initialValue = unresolvedBarcode?.rawValue.orEmpty(),
            initialSymbology = unresolvedBarcode?.symbology ?: "UNKNOWN",
            onDismiss = { showManualBarcodeDialog = false },
            onOpenScanner = { callback ->
                openScanner("Сканирование штрих-кода", callback)
            },
            onConfirm = { request ->
                showManualBarcodeDialog = false
                resolveBarcode(
                    ScannedBarcode(
                        rawValue = request.value,
                        symbology = request.symbology ?: "UNKNOWN"
                    )
                )
            }
        )
    }

    unresolvedBarcode?.let { currentBarcode ->
        if (bindingTarget == null && !showManualBarcodeDialog) {
        val canBindProduct = permissions.canCreateProducts
        val canBindCell = manageableCells.isNotEmpty()

        AlertDialog(
            onDismissRequest = { unresolvedBarcode = null },
            title = { Text("Штрих-код не найден") },
            text = {
                Column {
                    Text(
                        text = "Значение ${currentBarcode.rawValue} отсутствует в системе. Сканер не меняет данные автоматически: дальнейшее действие нужно подтвердить отдельно.",
                        style = MaterialTheme.typography.bodyMedium
                    )
                    Spacer(modifier = Modifier.height(14.dp))

                    if (canBindProduct) {
                        Button(
                            onClick = { bindingTarget = BarcodeBindingTarget.Product },
                            modifier = Modifier.fillMaxWidth()
                        ) {
                            Text("Привязать к товару")
                        }
                        Spacer(modifier = Modifier.height(8.dp))
                    }

                    if (canBindCell) {
                        Button(
                            onClick = { bindingTarget = BarcodeBindingTarget.Cell },
                            modifier = Modifier.fillMaxWidth()
                        ) {
                            Text("Привязать к ячейке")
                        }
                        Spacer(modifier = Modifier.height(8.dp))
                    }

                    OutlinedButton(
                        onClick = { showManualBarcodeDialog = true },
                        modifier = Modifier.fillMaxWidth()
                    ) {
                        Text("Ввести вручную")
                    }

                    Spacer(modifier = Modifier.height(8.dp))

                    OutlinedButton(
                        onClick = {
                            unresolvedBarcode = null
                            openScanner("Сканирование штрих-кода") { scannedBarcode ->
                                resolveBarcode(scannedBarcode)
                            }
                        },
                        modifier = Modifier.fillMaxWidth()
                    ) {
                        Text("Сканировать снова")
                    }
                }
            },
            confirmButton = {},
            dismissButton = {
                TextButton(onClick = { unresolvedBarcode = null }) {
                    Text("Закрыть")
                }
            }
        )
        }
    }

    if (bindingTarget == BarcodeBindingTarget.Product && unresolvedBarcode != null) {
        BarcodeBindingDialog(
            title = "Привязать штрих-код к товару",
            items = state.snapshot.products.sortedBy { it.name.lowercase() },
            itemTitle = { it.name },
            itemSubtitle = { it.sku },
            onDismiss = { bindingTarget = null },
            onConfirm = { product ->
                bindBarcodeToProduct(product, unresolvedBarcode ?: return@BarcodeBindingDialog)
            }
        )
    }

    if (bindingTarget == BarcodeBindingTarget.Cell && unresolvedBarcode != null) {
        BarcodeBindingDialog(
            title = "Привязать штрих-код к ячейке",
            items = manageableCells.sortedBy { it.code.lowercase() },
            itemTitle = { it.code },
            itemSubtitle = { "${it.warehouseName} · ${it.zoneCode} · ${it.name}" },
            onDismiss = { bindingTarget = null },
            onConfirm = { cell ->
                bindBarcodeToCell(cell, unresolvedBarcode ?: return@BarcodeBindingDialog)
            }
        )
    }

    scannerSession?.let { session ->
        BarcodeScannerScreen(
            title = session.title,
            onScanned = { scannedBarcode ->
                val callback = session.onScanned
                scannerSession = null
                callback(scannedBarcode)
            },
            onCancel = { scannerSession = null }
        )
    }
}

private fun AiAssistantOperationType.toOperationType(): OperationType {
    return when (this) {
        AiAssistantOperationType.Receive -> OperationType.Receive
        AiAssistantOperationType.WriteOff -> OperationType.WriteOff
        AiAssistantOperationType.Move -> OperationType.Move
    }
}

private fun AiAssistantItemsFilter.toItemsStockFilter(): ItemsStockFilter {
    return when (this) {
        AiAssistantItemsFilter.Available -> ItemsStockFilter.Available
        AiAssistantItemsFilter.LowStock -> ItemsStockFilter.LowStock
        AiAssistantItemsFilter.ZeroStock -> ItemsStockFilter.ZeroStock
    }
}

private fun String.requiresSnapshotRefresh(): Boolean {
    return when (this) {
        "create_product",
        "update_min_stock",
        "move_product",
        "write_off_product",
        "post_receipt" -> true

        else -> false
    }
}
