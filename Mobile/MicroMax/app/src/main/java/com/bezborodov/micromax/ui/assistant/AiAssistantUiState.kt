package com.bezborodov.micromax.ui.assistant

import androidx.compose.runtime.Immutable

@Immutable
data class AiAssistantUiState(
    val isOpen: Boolean = false,
    val inputText: String = "",
    val isProcessing: Boolean = false,
    val isLoadingCommands: Boolean = false,
    val messages: List<AiChatMessage> = emptyList(),
    val pendingCommand: AiAssistantCommand? = null,
    val lastResult: AiAssistantResult? = null,
    val quickPrompts: List<String> = listOf(
        "Покажи сводку по складу",
        "Покажи товары с низким остатком",
        "Покажи товары с нулевым остатком",
        "Покажи доступные команды"
    ),
    val commandDefinitions: List<AiAssistantCommandDefinition> = emptyList()
)

@Immutable
data class AiChatMessage(
    val text: String,
    val fromUser: Boolean
)

@Immutable
data class AiAssistantCommand(
    val commandId: String,
    val mode: String,
    val provider: String,
    val commandType: String,
    val riskLevel: String,
    val summary: String,
    val requiresConfirmation: Boolean,
    val clarificationQuestion: String?,
    val choices: List<AiAssistantChoice>
)

@Immutable
data class AiAssistantChoice(
    val id: String,
    val label: String,
    val kind: String
)

@Immutable
data class AiAssistantResult(
    val success: Boolean,
    val message: String,
    val details: List<String> = emptyList(),
    val navigationTarget: AiAssistantNavigationTarget? = null
)

@Immutable
data class AiAssistantCommandDefinition(
    val type: String,
    val title: String,
    val description: String,
    val riskLevel: String,
    val examples: List<String>
)

enum class AiAssistantNavigationTarget {
    Products,
    Operations
}
