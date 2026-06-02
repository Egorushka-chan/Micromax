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
import androidx.compose.ui.tooling.preview.Preview
import androidx.compose.ui.unit.dp
import androidx.lifecycle.viewmodel.compose.viewModel
import com.bezborodov.micromax.data.MicroMaxApiClient
import com.bezborodov.micromax.domain.assistant.AiNavigationTarget
import com.bezborodov.micromax.ui.assistant.AiAssistantOverlay
import com.bezborodov.micromax.ui.assistant.AiAssistantViewModel
import com.bezborodov.micromax.ui.assistant.AiAssistantViewModelFactory
import com.bezborodov.micromax.ui.cells.CellsScreen
import com.bezborodov.micromax.ui.components.BottomTab
import com.bezborodov.micromax.ui.components.FirstLoadErrorState
import com.bezborodov.micromax.ui.components.HomeBottomBar
import com.bezborodov.micromax.ui.components.LoadingState
import com.bezborodov.micromax.ui.components.MessageBanner
import com.bezborodov.micromax.ui.components.ScreenBg
import com.bezborodov.micromax.ui.items.ItemsScreen
import com.bezborodov.micromax.ui.items.ItemsStartDestination
import com.bezborodov.micromax.ui.operations.OperationsScreen
import com.bezborodov.micromax.ui.theme.MicroMaxTheme

@Composable
fun HomeScreen(
    apiClient: MicroMaxApiClient = remember { MicroMaxApiClient() },
    viewModel: WarehouseViewModel = viewModel(factory = WarehouseViewModelFactory(apiClient)),
    assistantViewModel: AiAssistantViewModel = viewModel(factory = AiAssistantViewModelFactory(apiClient))
) {
    var selectedTab by remember { mutableStateOf(BottomTab.Home) }
    var itemsStartDestination by remember { mutableStateOf(ItemsStartDestination.List) }
    val state = viewModel.uiState
    val assistantState = assistantViewModel.uiState
    val hasLoadedData = state.snapshot.products.isNotEmpty() ||
        state.snapshot.cells.isNotEmpty() ||
        state.snapshot.stocks.isNotEmpty() ||
        state.snapshot.operations.isNotEmpty()

    LaunchedEffect(assistantState.lastResult) {
        val result = assistantState.lastResult ?: return@LaunchedEffect
        when (result.navigationTarget) {
            AiNavigationTarget.Products -> {
                itemsStartDestination = ItemsStartDestination.List
                selectedTab = BottomTab.Items
            }

            AiNavigationTarget.Operations -> selectedTab = BottomTab.Transactions
            null -> Unit
        }
        if (result.success) {
            viewModel.loadData()
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
                        onRefresh = { viewModel.loadData(showMessage = true) }
                    )

                    else -> when (selectedTab) {
                        BottomTab.Home -> HomeDashboardScreen(
                            state = state,
                            onRefresh = { viewModel.loadData(showMessage = true) },
                            onOpenItems = {
                                itemsStartDestination = ItemsStartDestination.List
                                selectedTab = BottomTab.Items
                            },
                            onOpenAddItem = {
                                itemsStartDestination = ItemsStartDestination.Add
                                selectedTab = BottomTab.Items
                            },
                            onOpenCells = { selectedTab = BottomTab.Cells },
                            onOpenOperations = { selectedTab = BottomTab.Transactions },
                            onOpenAssistant = assistantViewModel::open
                        )

                        BottomTab.Items -> ItemsScreen(
                            state = state,
                            isSubmitting = state.isOperationSubmitting,
                            startDestination = itemsStartDestination,
                            onCreateProduct = viewModel::createProduct,
                            onOpenOperations = { selectedTab = BottomTab.Transactions }
                        )

                        BottomTab.Cells -> CellsScreen(
                            state = state,
                            onOpenOperations = { selectedTab = BottomTab.Transactions }
                        )

                        BottomTab.Assistant -> {
                            HomeDashboardScreen(
                                state = state,
                                onRefresh = { viewModel.loadData(showMessage = true) },
                                onOpenItems = {
                                    itemsStartDestination = ItemsStartDestination.List
                                    selectedTab = BottomTab.Items
                                },
                                onOpenAddItem = {
                                    itemsStartDestination = ItemsStartDestination.Add
                                    selectedTab = BottomTab.Items
                                },
                                onOpenCells = { selectedTab = BottomTab.Cells },
                                onOpenOperations = { selectedTab = BottomTab.Transactions },
                                onOpenAssistant = assistantViewModel::open
                            )
                        }

                        BottomTab.Transactions -> OperationsScreen(
                            state = state,
                            onReceive = viewModel::receive,
                            onWriteOff = viewModel::writeOff,
                            onMove = viewModel::move
                        )

                        BottomTab.Settings -> SettingsScreen(
                            state = state,
                            onRefresh = { viewModel.loadData(showMessage = true) }
                        )
                    }
                }
            }
        }

        AiAssistantOverlay(
            state = assistantState,
            onClose = assistantViewModel::close,
            onInputChange = assistantViewModel::onInputChange,
            onSubmit = { assistantViewModel.submitCurrent(state.snapshot) },
            onPromptClick = { assistantViewModel.usePrompt(it, state.snapshot) },
            onConfirm = { assistantViewModel.confirmPending(state.snapshot) },
            onCancelPending = assistantViewModel::rejectPending
        )
    }
}

@Preview(
    showBackground = true,
    backgroundColor = 0xFFF3F3F3,
    widthDp = 380,
    heightDp = 820
)
@Composable
private fun HomeScreenPreview() {
    MicroMaxTheme {
        HomeScreen()
    }
}
