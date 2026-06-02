package com.bezborodov.micromax.ui.assistant

import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.setValue
import androidx.lifecycle.ViewModel
import androidx.lifecycle.ViewModelProvider
import androidx.lifecycle.viewModelScope
import com.bezborodov.micromax.data.MicroMaxApiClient
import com.bezborodov.micromax.data.WarehouseSnapshot
import com.bezborodov.micromax.domain.assistant.AiCommand
import com.bezborodov.micromax.domain.assistant.AiCommandExecutor
import com.bezborodov.micromax.domain.assistant.AiCommandParser
import com.bezborodov.micromax.domain.assistant.AiCommandResult
import com.bezborodov.micromax.domain.assistant.AiCommandType
import com.bezborodov.micromax.domain.assistant.AiCommandValidator
import com.bezborodov.micromax.domain.assistant.MockAiCommandExecutor
import com.bezborodov.micromax.domain.assistant.RuleBasedAiCommandParser
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext

class AiAssistantViewModel(
    private val parser: AiCommandParser,
    private val validator: AiCommandValidator,
    private val executor: AiCommandExecutor
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
    }

    fun close() {
        uiState = uiState.copy(isOpen = false)
    }

    fun onInputChange(value: String) {
        uiState = uiState.copy(inputText = value)
    }

    fun usePrompt(prompt: String, snapshot: WarehouseSnapshot) {
        uiState = uiState.copy(inputText = prompt)
        submit(prompt, snapshot)
    }

    fun submitCurrent(snapshot: WarehouseSnapshot) {
        submit(uiState.inputText, snapshot)
    }

    fun confirmPending(snapshot: WarehouseSnapshot) {
        val command = uiState.pendingCommand ?: return
        execute(command, snapshot, clearPending = true)
    }

    fun rejectPending() {
        uiState = uiState.copy(
            pendingCommand = null,
            lastResult = AiCommandResult(success = true, message = "Команда отменена."),
            messages = uiState.messages + AiChatMessage("Команда отменена.", false)
        )
    }

    private fun submit(text: String, snapshot: WarehouseSnapshot) {
        if (text.isBlank()) {
            val result = AiCommandResult(false, "Введите команду для помощника.")
            uiState = uiState.copy(lastResult = result)
            return
        }

        val command = parser.parse(text, snapshot)
        if (command.type == AiCommandType.CancelCommand) {
            uiState = uiState.copy(
                inputText = "",
                pendingCommand = null,
                lastResult = AiCommandResult(true, "Ожидающая команда отменена."),
                messages = uiState.messages + AiChatMessage(text, true) + AiChatMessage("Ожидающая команда отменена.", false)
            )
            return
        }

        val validationResult = validator.validate(command, snapshot)
        if (validationResult != null) {
            uiState = uiState.copy(
                inputText = "",
                pendingCommand = null,
                lastResult = validationResult,
                messages = uiState.messages + AiChatMessage(text, true) + AiChatMessage(validationResult.message, false)
            )
            return
        }

        uiState = uiState.copy(
            inputText = "",
            messages = uiState.messages + AiChatMessage(text, true)
        )

        if (command.requiresConfirmation) {
            uiState = uiState.copy(
                pendingCommand = command,
                lastResult = null,
                messages = uiState.messages + AiChatMessage("Команда требует подтверждения. Проверьте карточку ниже.", false)
            )
            return
        }

        execute(command, snapshot, clearPending = false)
    }

    private fun execute(command: AiCommand, snapshot: WarehouseSnapshot, clearPending: Boolean) {
        viewModelScope.launch {
            uiState = uiState.copy(isProcessing = true)
            val result = runCatching {
                withContext(Dispatchers.IO) { executor.execute(command, snapshot) }
            }.getOrElse {
                AiCommandResult(false, it.message ?: "Не удалось выполнить команду.")
            }

            uiState = uiState.copy(
                isProcessing = false,
                pendingCommand = if (clearPending) null else uiState.pendingCommand,
                lastResult = result,
                messages = uiState.messages + AiChatMessage(result.message, false)
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
            return AiAssistantViewModel(
                parser = RuleBasedAiCommandParser(),
                validator = AiCommandValidator(),
                executor = MockAiCommandExecutor(apiClient)
            ) as T
        }
        throw IllegalArgumentException("Unknown ViewModel class: ${modelClass.name}")
    }
}
