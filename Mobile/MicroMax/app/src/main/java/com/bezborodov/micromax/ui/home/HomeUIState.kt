package com.bezborodov.micromax.ui.home

import androidx.compose.runtime.Immutable
import com.bezborodov.micromax.data.AssistantCommandDto
import com.bezborodov.micromax.data.WarehouseSnapshot

@Immutable
data class HomeUiState(
    val companyName: String = "ООО \"Развитие\"",
    val dateText: String = "Сегодня",
    val snapshot: WarehouseSnapshot = WarehouseSnapshot(),
    val isLoading: Boolean = false,
    val isOperationSubmitting: Boolean = false,
    val isAssistantLoading: Boolean = false,
    val message: String? = null,
    val pendingCommand: AssistantCommandDto? = null
) {
    val incomeCount: Int
        get() = snapshot.operations.count { it.type.equals("Receive", ignoreCase = true) }

    val outcomeCount: Int
        get() = snapshot.operations.count { it.type.equals("WriteOff", ignoreCase = true) }

    val totalOperationCount: Int
        get() = snapshot.operations.size
}
