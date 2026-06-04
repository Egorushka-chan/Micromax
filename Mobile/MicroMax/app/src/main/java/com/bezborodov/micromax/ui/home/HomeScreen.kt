package com.bezborodov.micromax.ui.home

import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.material3.Scaffold
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalLifecycleOwner
import androidx.compose.ui.unit.dp
import androidx.lifecycle.Lifecycle
import androidx.lifecycle.repeatOnLifecycle
import androidx.lifecycle.viewmodel.compose.viewModel
import com.bezborodov.micromax.data.MicroMaxApiClient
import com.bezborodov.micromax.ui.assistant.AiAssistantNavigationTarget
import com.bezborodov.micromax.ui.assistant.AiAssistantOverlay
import com.bezborodov.micromax.ui.assistant.AiAssistantViewModel
import com.bezborodov.micromax.ui.assistant.AiAssistantViewModelFactory
import com.bezborodov.micromax.ui.auth.SessionUiState
import com.bezborodov.micromax.ui.cells.CellsScreen
import com.bezborodov.micromax.ui.components.BottomTab
import com.bezborodov.micromax.ui.components.FirstLoadErrorState
import com.bezborodov.micromax.ui.components.HomeBottomBar
import com.bezborodov.micromax.ui.components.LoadingState
import com.bezborodov.micromax.ui.components.MessageBanner
import com.bezborodov.micromax.ui.components.ScreenBg
import com.bezborodov.micromax.ui.items.ItemsScreen
import com.bezborodov.micromax.ui.items.ItemsStartDestination
import com.bezborodov.micromax.ui.operations.OperationType
import com.bezborodov.micromax.ui.operations.OperationsScreen
import kotlinx.coroutines.delay

private const val PollingIntervalMs = 15_000L

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
    onClearSessionMessage: () -> Unit
) {
    val userId = sessionState.currentUser?.id ?: 0
    val lifecycleOwner = LocalLifecycleOwner.current
    val permissions = sessionState.permissions
    val viewModel: WarehouseViewModel = viewModel(
        key = "warehouse-$userId",
        factory = WarehouseViewModelFactory(apiClient)
    )
    val assistantViewModel: AiAssistantViewModel = viewModel(
        key = "assistant-$userId",
        factory = AiAssistantViewModelFactory(apiClient)
    )

    var selectedTab by remember(userId) { mutableStateOf(BottomTab.Home) }
    var itemsStartDestination by remember(userId) { mutableStateOf(ItemsStartDestination.List) }
    var pendingOperationType by remember(userId) { mutableStateOf<OperationType?>(null) }

    val state = viewModel.uiState
    val assistantState = assistantViewModel.uiState
    val warehouseName = sessionState.currentUser?.warehouses?.firstOrNull()?.warehouseName
    val hasLoadedData = state.snapshot.products.isNotEmpty() ||
        state.snapshot.cells.isNotEmpty() ||
        state.snapshot.stocks.isNotEmpty() ||
        state.snapshot.operations.isNotEmpty()

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

    LaunchedEffect(assistantState.lastResult) {
        val result = assistantState.lastResult ?: return@LaunchedEffect
        when (result.navigationTarget) {
            AiAssistantNavigationTarget.Products -> {
                itemsStartDestination = ItemsStartDestination.List
                selectedTab = BottomTab.Items
            }

            AiAssistantNavigationTarget.Operations -> {
                if (permissions.canExecuteOperations) {
                    selectedTab = BottomTab.Transactions
                }
            }

            null -> Unit
        }
        if (result.success) {
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
                            onOpenAssistant = assistantViewModel::open
                        )

                        BottomTab.Items -> ItemsScreen(
                            state = state,
                            isSubmitting = state.isOperationSubmitting,
                            startDestination = itemsStartDestination,
                            canCreateProducts = permissions.canCreateProducts,
                            canExecuteOperations = permissions.canExecuteOperations,
                            onCreateProduct = viewModel::createProduct,
                            onOpenOperations = { selectedTab = BottomTab.Transactions }
                        )

                        BottomTab.Cells -> CellsScreen(
                            state = state,
                            canExecuteOperations = permissions.canExecuteOperations,
                            onOpenOperations = { selectedTab = BottomTab.Transactions }
                        )

                        BottomTab.Assistant -> HomeDashboardScreen(
                            state = state,
                            warehouseName = warehouseName,
                            canCreateProducts = permissions.canCreateProducts,
                            canExecuteOperations = permissions.canExecuteOperations,
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
                            onOpenAssistant = assistantViewModel::open
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
            onClose = assistantViewModel::close,
            onInputChange = assistantViewModel::onInputChange,
            onSubmit = assistantViewModel::submitCurrent,
            onPromptClick = assistantViewModel::usePrompt,
            onConfirm = assistantViewModel::confirmPending,
            onCancelPending = assistantViewModel::rejectPending
        )
    }
}
