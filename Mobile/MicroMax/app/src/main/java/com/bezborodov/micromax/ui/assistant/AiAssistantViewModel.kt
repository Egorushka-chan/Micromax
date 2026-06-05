package com.bezborodov.micromax.ui.assistant

import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.setValue
import androidx.lifecycle.ViewModel
import androidx.lifecycle.ViewModelProvider
import androidx.lifecycle.viewModelScope
import com.bezborodov.micromax.data.AssistantChoiceDto
import com.bezborodov.micromax.data.AssistantCommandDefinitionDto
import com.bezborodov.micromax.data.AssistantCommandDto
import com.bezborodov.micromax.data.AssistantCommandResultDto
import com.bezborodov.micromax.data.MicroMaxApiClient
import com.bezborodov.micromax.data.UnauthorizedException
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext

class AiAssistantViewModel(
    private val apiClient: MicroMaxApiClient
) : ViewModel() {
    var uiState by mutableStateOf(AiAssistantUiState())
        private set

    fun open() {
        uiState = uiState.copy(
            isOpen = true,
            messages = uiState.messages.ifEmpty {
                listOf(AiChatMessage("Я помогу найти товар, показать остатки или подготовить складскую операцию. Опасные действия выполняются только после подтверждения.", false))
            }
        )
        loadCommandsIfNeeded()
    }

    fun close() {
        uiState = uiState.copy(isOpen = false)
    }

    fun onInputChange(value: String) {
        uiState = uiState.copy(inputText = value)
    }

    fun usePrompt(prompt: String) {
        uiState = uiState.copy(inputText = prompt)
        submit(prompt)
    }

    fun submitCurrent() {
        submit(uiState.inputText)
    }

    fun confirmPending() {
        val command = uiState.pendingCommand ?: return
        viewModelScope.launch {
            uiState = uiState.copy(isProcessing = true)
            val result = runCatching {
                withContext(Dispatchers.IO) { apiClient.confirmAssistant(command.commandId).toUiResult(command) }
            }.getOrElse { error ->
                if (handleUnauthorizedDuringAction(error)) {
                    return@launch
                }

                AiAssistantResult(false, error.message ?: "Не удалось подтвердить команду.")
            }

            uiState = uiState.copy(
                isProcessing = false,
                pendingCommand = null,
                clarificationCommand = null,
                lastResult = result,
                messages = uiState.messages + AiChatMessage(result.message, false)
            )
        }
    }

    fun rejectPending() {
        cancelPendingCommand("Команда отменена.", "Нет ожидающей команды для отмены.")
    }

    fun chooseClarification(choiceId: String) {
        val command = uiState.clarificationCommand ?: return
        viewModelScope.launch {
            uiState = uiState.copy(isProcessing = true, lastResult = null)

            val response = runCatching {
                withContext(Dispatchers.IO) { apiClient.clarifyAssistant(command.commandId, choiceId).toUiCommand() }
            }.getOrElse { error ->
                if (handleUnauthorizedDuringAction(error)) {
                    return@launch
                }

                val result = AiAssistantResult(false, error.message ?: "Не удалось уточнить команду.")
                uiState = uiState.copy(
                    isProcessing = false,
                    lastResult = result,
                    messages = uiState.messages + AiChatMessage(result.message, false)
                )
                return@launch
            }

            applyAssistantResponse(response)
        }
    }

    private fun submit(text: String) {
        if (text.isBlank()) {
            uiState = uiState.copy(lastResult = AiAssistantResult(false, "Введите команду для помощника."))
            return
        }

        viewModelScope.launch {
            uiState = uiState.copy(
                inputText = "",
                isProcessing = true,
                lastResult = null,
                messages = uiState.messages + AiChatMessage(text, true)
            )

            val response = runCatching {
                withContext(Dispatchers.IO) { apiClient.interpretAssistant(text).toUiCommand() }
            }.getOrElse { error ->
                if (handleUnauthorizedDuringAction(error)) {
                    return@launch
                }

                val result = AiAssistantResult(false, error.message ?: "Не удалось обработать команду.")
                uiState = uiState.copy(
                    isProcessing = false,
                    lastResult = result,
                    messages = uiState.messages + AiChatMessage(result.message, false)
                )
                return@launch
            }

            if (response.mode.equals("Command", ignoreCase = true) &&
                response.commandType.equals("cancel", ignoreCase = true)
            ) {
                cancelPendingCommand(
                    successMessage = response.summary.ifBlank { "Команда отменена." },
                    missingMessage = "Нет ожидающей команды для отмены."
                )
                return@launch
            }

            applyAssistantResponse(response)
        }
    }

    private fun applyAssistantResponse(response: AiAssistantCommand) {
        val message = response.clarificationQuestion ?: response.summary
        uiState = when {
            response.requiresConfirmation -> uiState.copy(
                isProcessing = false,
                pendingCommand = response,
                clarificationCommand = null,
                lastResult = null,
                messages = uiState.messages + AiChatMessage("Команда требует подтверждения. Проверьте карточку ниже.", false)
            )

            response.mode.equals("Clarification", ignoreCase = true) -> uiState.copy(
                isProcessing = false,
                pendingCommand = null,
                clarificationCommand = response.takeIf { it.choices.isNotEmpty() },
                lastResult = AiAssistantResult(
                    success = false,
                    message = message,
                    isClarification = true
                ),
                messages = uiState.messages + AiChatMessage(message, false)
            )

            else -> uiState.copy(
                isProcessing = false,
                pendingCommand = null,
                clarificationCommand = null,
                lastResult = AiAssistantResult(
                    success = true,
                    message = message,
                    clientAction = response.toClientAction()
                ),
                messages = uiState.messages + AiChatMessage(message, false)
            )
        }
    }

    private fun cancelPendingCommand(successMessage: String, missingMessage: String) {
        val command = uiState.pendingCommand ?: uiState.clarificationCommand
        if (command == null) {
            val result = AiAssistantResult(success = true, message = missingMessage)
            uiState = uiState.copy(
                isProcessing = false,
                lastResult = result,
                messages = uiState.messages + AiChatMessage(result.message, false)
            )
            return
        }

        viewModelScope.launch {
            uiState = uiState.copy(isProcessing = true)
            val result = runCatching {
                withContext(Dispatchers.IO) { apiClient.confirmAssistant(command.commandId, confirmed = false) }
            }.fold(
                onSuccess = { response ->
                    AiAssistantResult(
                        success = response.success,
                        message = response.message.ifBlank { successMessage },
                        details = response.details
                    )
                },
                onFailure = { error ->
                    if (handleUnauthorizedDuringAction(error)) {
                        return@launch
                    }

                    AiAssistantResult(false, error.message ?: "Не удалось отменить команду.")
                }
            )

            uiState = uiState.copy(
                isProcessing = false,
                pendingCommand = null,
                clarificationCommand = null,
                lastResult = result,
                messages = uiState.messages + AiChatMessage(result.message, false)
            )
        }
    }

    private fun handleUnauthorizedDuringAction(error: Throwable): Boolean {
        if (error !is UnauthorizedException) {
            return false
        }

        uiState = uiState.copy(
            isProcessing = false,
            pendingCommand = null,
            clarificationCommand = null,
            requiresReauthentication = true,
            lastResult = AiAssistantResult(false, error.message ?: "Сессия истекла. Войдите снова."),
            messages = uiState.messages + AiChatMessage(error.message ?: "Сессия истекла. Войдите снова.", false)
        )
        return true
    }

    private fun loadCommandsIfNeeded() {
        if (uiState.commandDefinitions.isNotEmpty() || uiState.isLoadingCommands) {
            return
        }

        viewModelScope.launch {
            uiState = uiState.copy(isLoadingCommands = true)
            val definitions = runCatching {
                withContext(Dispatchers.IO) { apiClient.loadAssistantCommands().map { it.toUiDefinition() } }
            }.getOrElse { error ->
                if (error is UnauthorizedException) {
                    uiState = uiState.copy(
                        isLoadingCommands = false,
                        requiresReauthentication = true
                    )
                    return@launch
                }
                emptyList()
            }

            uiState = uiState.copy(
                isLoadingCommands = false,
                commandDefinitions = definitions,
                quickPrompts = definitions.mapNotNull { it.examples.firstOrNull() }.take(4).ifEmpty { uiState.quickPrompts }
            )
        }
    }
}

class AiAssistantViewModelFactory(
    private val apiClient: MicroMaxApiClient
) : ViewModelProvider.Factory {
    @Suppress("UNCHECKED_CAST")
    override fun <T : ViewModel> create(modelClass: Class<T>): T {
        if (modelClass.isAssignableFrom(AiAssistantViewModel::class.java)) {
            return AiAssistantViewModel(apiClient) as T
        }
        throw IllegalArgumentException("Unknown ViewModel class: ${modelClass.name}")
    }
}

private fun AssistantCommandDto.toUiCommand(): AiAssistantCommand {
    return AiAssistantCommand(
        commandId = commandId,
        mode = mode,
        provider = provider,
        commandType = commandType,
        riskLevel = riskLevel,
        productId = productId,
        sourceCellId = sourceCellId,
        targetCellId = targetCellId,
        quantity = quantity,
        minQuantity = minQuantity,
        summary = summary,
        requiresConfirmation = requiresConfirmation,
        clarificationQuestion = clarificationQuestion,
        clarificationTarget = clarificationTarget,
        choices = choices.map { it.toUiChoice() }
    )
}

private fun AssistantChoiceDto.toUiChoice(): AiAssistantChoice {
    return AiAssistantChoice(id = id, label = label, kind = kind)
}

private fun AssistantCommandDefinitionDto.toUiDefinition(): AiAssistantCommandDefinition {
    return AiAssistantCommandDefinition(
        type = type,
        title = title,
        description = description,
        riskLevel = riskLevel,
        examples = examples
    )
}

private fun AssistantCommandResultDto.toUiResult(command: AiAssistantCommand?): AiAssistantResult {
    return AiAssistantResult(
        success = success,
        message = message,
        details = details,
        clientAction = command?.toClientAction()
    )
}

private fun AiAssistantCommand.toClientAction(): AiAssistantClientAction? {
    return commandType.toClientAction(
        productId = productId,
        sourceCellId = sourceCellId,
        targetCellId = targetCellId,
        quantity = quantity,
        minQuantity = minQuantity
    )
}

private fun String.toClientAction(
    productId: Int?,
    sourceCellId: Int?,
    targetCellId: Int?,
    quantity: Double?,
    minQuantity: Double?
): AiAssistantClientAction? = when (this) {
    "open_products" -> AiAssistantClientAction(
        commandType = this,
        itemsFilter = AiAssistantItemsFilter.Available
    )

    "find_product" -> AiAssistantClientAction(
        commandType = this,
        productId = productId,
        itemsFilter = AiAssistantItemsFilter.Available
    )

    "low_stock" -> AiAssistantClientAction(
        commandType = this,
        itemsFilter = AiAssistantItemsFilter.LowStock
    )

    "zero_stock" -> AiAssistantClientAction(
        commandType = this,
        itemsFilter = AiAssistantItemsFilter.ZeroStock
    )

    "create_product" -> AiAssistantClientAction(
        commandType = this,
        itemsFilter = AiAssistantItemsFilter.Available
    )

    "update_min_stock" -> AiAssistantClientAction(
        commandType = this,
        productId = productId,
        minQuantity = minQuantity,
        itemsFilter = AiAssistantItemsFilter.Available
    )

    "move_product" -> AiAssistantClientAction(
        commandType = this,
        productId = productId,
        sourceCellId = sourceCellId,
        targetCellId = targetCellId,
        quantity = quantity,
        operationType = AiAssistantOperationType.Move
    )

    "write_off_product" -> AiAssistantClientAction(
        commandType = this,
        productId = productId,
        sourceCellId = sourceCellId,
        quantity = quantity,
        operationType = AiAssistantOperationType.WriteOff
    )

    "create_receipt",
    "post_receipt" -> AiAssistantClientAction(
        commandType = this,
        productId = productId,
        targetCellId = targetCellId,
        quantity = quantity,
        operationType = AiAssistantOperationType.Receive
    )

    "warehouse_summary",
    "help",
    "cancel" -> AiAssistantClientAction(
        commandType = this,
        productId = productId,
        sourceCellId = sourceCellId,
        targetCellId = targetCellId,
        quantity = quantity,
        minQuantity = minQuantity
    )

    else -> null
}
