package com.bezborodov.micromax.ui.assistant

import androidx.compose.runtime.Immutable
import com.bezborodov.micromax.domain.assistant.AiCommand
import com.bezborodov.micromax.domain.assistant.AiCommandResult

@Immutable
data class AiAssistantUiState(
    val isOpen: Boolean = false,
    val inputText: String = "",
    val isProcessing: Boolean = false,
    val messages: List<AiChatMessage> = emptyList(),
    val pendingCommand: AiCommand? = null,
    val lastResult: AiCommandResult? = null,
    val quickPrompts: List<String> = listOf(
        "Покажи сводку по складу",
        "Покажи товары с низким остатком",
        "Покажи товары с нулевым остатком",
        "Покажи доступные команды"
    )
)

@Immutable
data class AiChatMessage(
    val text: String,
    val fromUser: Boolean
)
