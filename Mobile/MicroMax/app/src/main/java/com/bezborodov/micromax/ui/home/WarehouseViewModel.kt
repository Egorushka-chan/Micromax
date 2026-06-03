package com.bezborodov.micromax.ui.home

import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.setValue
import androidx.lifecycle.ViewModel
import androidx.lifecycle.ViewModelProvider
import androidx.lifecycle.viewModelScope
import com.bezborodov.micromax.data.CreateProductRequest
import com.bezborodov.micromax.data.MicroMaxApiClient
import com.bezborodov.micromax.data.WarehouseSnapshot
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext

private enum class RefreshTrigger {
    Initial,
    Manual,
    Polling
}

class WarehouseViewModel(
    private val apiClient: MicroMaxApiClient = MicroMaxApiClient()
) : ViewModel() {
    // Защищаемся от параллельных загрузок снимка и от устаревших ответов.
    private var isSnapshotRefreshInProgress = false
    private var latestRequestId = 0L

    var uiState by mutableStateOf(HomeUiState(isLoading = true))
        private set

    init {
        retryInitialLoad()
    }

    fun retryInitialLoad() {
        refreshSnapshot(RefreshTrigger.Initial)
    }

    fun refreshManually() {
        refreshSnapshot(RefreshTrigger.Manual)
    }

    fun refreshByPolling() {
        refreshSnapshot(RefreshTrigger.Polling)
    }

    fun receive(productId: Int, targetCellId: Int, quantity: Double, comment: String? = null) {
        runChangingOperation(successMessage = "Приход выполнен") {
            apiClient.receive(productId, targetCellId, quantity, comment)
            apiClient.loadSnapshot()
        }
    }

    fun writeOff(productId: Int, sourceCellId: Int, quantity: Double, comment: String? = null) {
        runChangingOperation(successMessage = "Расход выполнен") {
            apiClient.writeOff(productId, sourceCellId, quantity, comment)
            apiClient.loadSnapshot()
        }
    }

    fun move(productId: Int, sourceCellId: Int, targetCellId: Int, quantity: Double, comment: String? = null) {
        runChangingOperation(successMessage = "Перемещение выполнено") {
            apiClient.move(productId, sourceCellId, targetCellId, quantity, comment)
            apiClient.loadSnapshot()
        }
    }

    fun adjust(productId: Int, targetCellId: Int, targetQuantity: Double, comment: String? = null) {
        runChangingOperation(successMessage = "Корректировка выполнена") {
            apiClient.adjust(productId, targetCellId, targetQuantity, comment)
            apiClient.loadSnapshot()
        }
    }

    fun createProduct(
        sku: String,
        name: String,
        unit: String,
        minQuantity: Double,
        initialCellId: Int?,
        initialQuantity: Double
    ) {
        if (sku.isBlank() || name.isBlank() || unit.isBlank()) {
            uiState = uiState.copy(message = "Заполните SKU, название и единицу измерения.")
            return
        }

        if (minQuantity < 0.0 || initialQuantity < 0.0) {
            uiState = uiState.copy(message = "Количество и минимальный остаток не могут быть отрицательными.")
            return
        }

        if (initialQuantity > 0.0 && initialCellId == null) {
            uiState = uiState.copy(message = "Для начального остатка нужно выбрать ячейку.")
            return
        }

        runChangingOperation(successMessage = "Товар добавлен") {
            // Если указан стартовый остаток, сразу выполняем приёмку в выбранную ячейку.
            val product = apiClient.createProduct(
                CreateProductRequest(
                    sku = sku.trim(),
                    name = name.trim(),
                    unit = unit.trim(),
                    minQuantity = minQuantity
                )
            )
            if (initialQuantity > 0.0 && initialCellId != null) {
                apiClient.receive(product.id, initialCellId, initialQuantity)
            }
            apiClient.loadSnapshot()
        }
    }

    fun interpretCommand(text: String) {
        if (text.isBlank()) {
            uiState = uiState.copy(message = "Введите команду для ассистента.")
            return
        }

        viewModelScope.launch {
            uiState = uiState.copy(isAssistantLoading = true, message = null)
            val result = runCatching {
                withContext(Dispatchers.IO) { apiClient.interpretAssistant(text) }
            }
            uiState = result.fold(
                onSuccess = {
                    uiState.copy(
                        pendingCommand = it,
                        isAssistantLoading = false,
                        message = it.summary
                    )
                },
                onFailure = {
                    uiState.copy(
                        isAssistantLoading = false,
                        message = it.message ?: "Ошибка ассистента"
                    )
                }
            )
        }
    }

    fun confirmCommand(commandId: String) {
        runChangingOperation(
            successMessage = "Команда подтверждена и выполнена",
            clearPendingCommand = true
        ) {
            apiClient.confirmAssistant(commandId)
            apiClient.loadSnapshot()
        }
    }

    private fun refreshSnapshot(trigger: RefreshTrigger) {
        if (isSnapshotRefreshInProgress || uiState.isOperationSubmitting) {
            return
        }

        isSnapshotRefreshInProgress = true
        val requestId = nextRequestId()
        viewModelScope.launch {
            uiState = uiState.copy(isLoading = true, message = null)
            val result = runCatching { loadSnapshot() }

            isSnapshotRefreshInProgress = false
            if (requestId != latestRequestId) {
                return@launch
            }

            uiState = result.fold(
                onSuccess = { snapshot ->
                    uiState.copy(
                        snapshot = snapshot,
                        isLoading = false,
                        message = if (trigger == RefreshTrigger.Manual) "Данные обновлены" else null
                    )
                },
                onFailure = { error ->
                    when (trigger) {
                        RefreshTrigger.Polling -> uiState.copy(isLoading = false, message = null)
                        else -> uiState.copy(
                            isLoading = false,
                            message = error.message ?: "Не удалось загрузить данные"
                        )
                    }
                }
            )
        }
    }

    private fun runChangingOperation(
        successMessage: String,
        clearPendingCommand: Boolean = false,
        action: () -> WarehouseSnapshot
    ) {
        if (uiState.isOperationSubmitting) {
            return
        }

        val requestId = nextRequestId()
        viewModelScope.launch {
            uiState = uiState.copy(isOperationSubmitting = true, message = null)
            val result = runCatching {
                withContext(Dispatchers.IO) { action() }
            }

            if (requestId != latestRequestId) {
                return@launch
            }

            uiState = result.fold(
                onSuccess = {
                    uiState.copy(
                        snapshot = it,
                        isOperationSubmitting = false,
                        pendingCommand = if (clearPendingCommand) null else uiState.pendingCommand,
                        message = successMessage
                    )
                },
                onFailure = {
                    uiState.copy(
                        isOperationSubmitting = false,
                        message = it.message ?: "Ошибка операции"
                    )
                }
            )
        }
    }

    private fun nextRequestId(): Long {
        latestRequestId += 1
        return latestRequestId
    }

    private suspend fun loadSnapshot(): WarehouseSnapshot {
        return withContext(Dispatchers.IO) { apiClient.loadSnapshot() }
    }
}

class WarehouseViewModelFactory(
    private val apiClient: MicroMaxApiClient
) : ViewModelProvider.Factory {
    @Suppress("UNCHECKED_CAST")
    override fun <T : ViewModel> create(modelClass: Class<T>): T {
        if (modelClass.isAssignableFrom(WarehouseViewModel::class.java)) {
            return WarehouseViewModel(apiClient) as T
        }
        throw IllegalArgumentException("Unknown ViewModel class: ${modelClass.name}")
    }
}
