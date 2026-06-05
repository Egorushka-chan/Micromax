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
    val clarificationCommand: AiAssistantCommand? = null,
    val lastResult: AiAssistantResult? = null,
    val quickPrompts: List<String> = listOf(
        "Покажи сводку по складу",
        "Покажи товары с низким остатком",
        "Покажи товары с нулевым остатком",
        "Покажи доступные команды"
    ),
    val commandDefinitions: List<AiAssistantCommandDefinition> = emptyList(),
    val requiresReauthentication: Boolean = false
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
    val productId: Int?,
    val sourceCellId: Int?,
    val targetCellId: Int?,
    val quantity: Double?,
    val minQuantity: Double?,
    val summary: String,
    val requiresConfirmation: Boolean,
    val clarificationQuestion: String?,
    val clarificationTarget: String?,
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
    val isClarification: Boolean = false,
    val clientAction: AiAssistantClientAction? = null
)

@Immutable
data class AiAssistantClientAction(
    val commandType: String,
    val productId: Int? = null,
    val sourceCellId: Int? = null,
    val targetCellId: Int? = null,
    val quantity: Double? = null,
    val minQuantity: Double? = null,
    val itemsFilter: AiAssistantItemsFilter? = null,
    val operationType: AiAssistantOperationType? = null
)

enum class AiAssistantItemsFilter {
    Available,
    LowStock,
    ZeroStock
}

enum class AiAssistantOperationType {
    Receive,
    WriteOff,
    Move
}

@Immutable
data class AiAssistantCommandDefinition(
    val type: String,
    val title: String,
    val description: String,
    val riskLevel: String,
    val examples: List<String>
)
