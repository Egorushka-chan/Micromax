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
                withContext(Dispatchers.IO) { apiClient.confirmAssistant(command.commandId).toUiResult(command.commandType) }
            }.getOrElse {
                if (it is UnauthorizedException) {
                    uiState = uiState.copy(
                        isProcessing = false,
                        pendingCommand = null,
                        requiresReauthentication = true,
                        lastResult = AiAssistantResult(false, it.message ?: "Сессия истекла. Войдите снова."),
                        messages = uiState.messages + AiChatMessage(it.message ?: "Сессия истекла. Войдите снова.", false)
                    )
                    return@launch
                }

                AiAssistantResult(false, it.message ?: "Не удалось подтвердить команду.")
            }

            uiState = uiState.copy(
                isProcessing = false,
                pendingCommand = null,
                lastResult = result,
                messages = uiState.messages + AiChatMessage(result.message, false)
            )
        }
    }

    fun rejectPending() {
        uiState = uiState.copy(
            pendingCommand = null,
            lastResult = AiAssistantResult(success = true, message = "Команда отменена."),
            messages = uiState.messages + AiChatMessage("Команда отменена.", false)
        )
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
            }.getOrElse {
                if (it is UnauthorizedException) {
                    uiState = uiState.copy(
                        isProcessing = false,
                        pendingCommand = null,
                        requiresReauthentication = true,
                        lastResult = AiAssistantResult(false, it.message ?: "Сессия истекла. Войдите снова."),
                        messages = uiState.messages + AiChatMessage(it.message ?: "Сессия истекла. Войдите снова.", false)
                    )
                    return@launch
                }

                val result = AiAssistantResult(false, it.message ?: "Не удалось обработать команду.")
                uiState = uiState.copy(
                    isProcessing = false,
                    pendingCommand = null,
                    lastResult = result,
                    messages = uiState.messages + AiChatMessage(result.message, false)
                )
                return@launch
            }

            val message = response.clarificationQuestion ?: response.summary
            uiState = when {
                response.requiresConfirmation -> uiState.copy(
                    isProcessing = false,
                    pendingCommand = response,
                    messages = uiState.messages + AiChatMessage("Команда требует подтверждения. Проверьте карточку ниже.", false)
                )

                response.mode.equals("Clarification", ignoreCase = true) -> uiState.copy(
                    isProcessing = false,
                    pendingCommand = null,
                    lastResult = AiAssistantResult(false, message, response.choices.map { it.label }),
                    messages = uiState.messages + AiChatMessage(message, false)
                )

                else -> uiState.copy(
                    isProcessing = false,
                    pendingCommand = null,
                    lastResult = AiAssistantResult(true, message, navigationTarget = response.commandType.toNavigationTarget()),
                    messages = uiState.messages + AiChatMessage(message, false)
                )
            }
        }
    }

    private fun loadCommandsIfNeeded() {
        if (uiState.commandDefinitions.isNotEmpty() || uiState.isLoadingCommands) {
            return
        }

        viewModelScope.launch {
            uiState = uiState.copy(isLoadingCommands = true)
            val definitions = runCatching {
                withContext(Dispatchers.IO) { apiClient.loadAssistantCommands().map { it.toUiDefinition() } }
            }.getOrElse {
                if (it is UnauthorizedException) {
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
        summary = summary,
        requiresConfirmation = requiresConfirmation,
        clarificationQuestion = clarificationQuestion,
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

private fun AssistantCommandResultDto.toUiResult(commandType: String): AiAssistantResult {
    return AiAssistantResult(
        success = success,
        message = message,
        details = details,
        navigationTarget = commandType.toNavigationTarget()
    )
}

private fun String.toNavigationTarget(): AiAssistantNavigationTarget? = when (this) {
    "open_products",
    "find_product",
    "low_stock",
    "zero_stock",
    "create_product",
    "update_min_stock" -> AiAssistantNavigationTarget.Products

    "move_product",
    "write_off_product",
    "create_receipt",
    "post_receipt" -> AiAssistantNavigationTarget.Operations

    else -> null
}
