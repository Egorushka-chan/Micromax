package com.bezborodov.micromax.ui.assistant

import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.imePadding
import androidx.compose.foundation.layout.navigationBarsPadding
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.outlined.Close
import androidx.compose.material.icons.outlined.Mic
import androidx.compose.material.icons.outlined.Send
import androidx.compose.material.icons.outlined.SmartToy
import androidx.compose.material.icons.outlined.WarningAmber
import androidx.compose.material3.Button
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.FloatingActionButton
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import com.bezborodov.micromax.data.WarehouseSnapshot
import com.bezborodov.micromax.ui.components.Accent
import com.bezborodov.micromax.ui.components.AccentDark
import com.bezborodov.micromax.ui.components.ScreenBg
import com.bezborodov.micromax.ui.components.SearchBorder
import com.bezborodov.micromax.ui.components.TextMuted
import java.util.Locale
import kotlin.math.abs
import kotlin.math.roundToLong

@Composable
fun AiCommandButton(
    onClick: () -> Unit,
    modifier: Modifier = Modifier
) {
    FloatingActionButton(
        onClick = onClick,
        modifier = modifier,
        containerColor = AccentDark,
        contentColor = Color.White,
        shape = CircleShape
    ) {
        Icon(
            imageVector = Icons.Outlined.SmartToy,
            contentDescription = "Открыть ИИ-помощника",
            modifier = Modifier.size(28.dp)
        )
    }
}

@Composable
fun AiAssistantOverlay(
    state: AiAssistantUiState,
    snapshot: WarehouseSnapshot,
    onClose: () -> Unit,
    onInputChange: (String) -> Unit,
    onSubmit: () -> Unit,
    onPromptClick: (String) -> Unit,
    onConfirm: () -> Unit,
    onClarificationChoice: (String) -> Unit,
    onCancelPending: () -> Unit
) {
    if (!state.isOpen) {
        return
    }

    Box(
        modifier = Modifier
            .fillMaxSize()
            .background(Color.Black.copy(alpha = 0.42f))
            .clickable(enabled = false) {}
    ) {
        Box(
            modifier = Modifier
                .matchParentSize()
                .background(Accent.copy(alpha = 0.16f))
        )

        Card(
            modifier = Modifier
                .align(Alignment.BottomCenter)
                .fillMaxWidth()
                .navigationBarsPadding()
                .imePadding()
                .padding(14.dp),
            shape = RoundedCornerShape(24.dp),
            colors = CardDefaults.cardColors(containerColor = ScreenBg),
            elevation = CardDefaults.cardElevation(defaultElevation = 12.dp)
        ) {
            Column(
                modifier = Modifier
                    .fillMaxWidth()
                    .padding(16.dp),
                verticalArrangement = Arrangement.spacedBy(12.dp)
            ) {
                Row(verticalAlignment = Alignment.CenterVertically) {
                    Box(
                        modifier = Modifier
                            .size(42.dp)
                            .clip(CircleShape)
                            .background(AccentDark),
                        contentAlignment = Alignment.Center
                    ) {
                        Icon(Icons.Outlined.SmartToy, contentDescription = null, tint = Color.White)
                    }
                    Spacer(modifier = Modifier.width(12.dp))
                    Column(modifier = Modifier.weight(1f)) {
                        Text(
                            text = "ИИ-помощник MicroMax",
                            style = MaterialTheme.typography.titleLarge,
                            fontWeight = FontWeight.Bold
                        )
                        Text(
                            text = "Команды выполняются по правилам MVP",
                            style = MaterialTheme.typography.bodyMedium,
                            color = TextMuted
                        )
                    }
                    IconButton(onClick = onClose) {
                        Icon(Icons.Outlined.Close, contentDescription = "Закрыть")
                    }
                }

                LazyColumn(
                    modifier = Modifier
                        .fillMaxWidth()
                        .height(300.dp),
                    verticalArrangement = Arrangement.spacedBy(8.dp)
                ) {
                    if (state.messages.isEmpty()) {
                        item {
                            AiMessageBubble(
                                text = "Введите команду или выберите подсказку ниже.",
                                fromUser = false
                            )
                        }
                    }

                    items(state.messages) { message ->
                        AiMessageBubble(text = message.text, fromUser = message.fromUser)
                    }

                    state.pendingCommand?.let { command ->
                        item {
                            AiCommandPreviewCard(command = command)
                        }
                        item {
                            AiConfirmationCard(
                                command = command,
                                isProcessing = state.isProcessing,
                                onConfirm = onConfirm,
                                onCancel = onCancelPending
                            )
                        }
                    }

                    state.clarificationCommand?.let { command ->
                        item {
                            AiCommandPreviewCard(
                                command = command,
                                title = "Уточнение команды"
                            )
                        }
                        item {
                            AiClarificationCard(
                                command = command,
                                isProcessing = state.isProcessing,
                                onChoiceClick = onClarificationChoice,
                                onCancel = onCancelPending
                            )
                        }
                    }

                    val result = state.lastResult
                    if (result != null && !(state.clarificationCommand != null && result.isClarification)) {
                        item {
                            AiCommandResultCard(
                                result = result,
                                snapshot = snapshot,
                                commandDefinitions = state.commandDefinitions
                            )
                        }
                    }
                }

                if (state.messages.size <= 1 &&
                    state.pendingCommand == null &&
                    state.clarificationCommand == null
                ) {
                    Column(verticalArrangement = Arrangement.spacedBy(8.dp)) {
                        Text(
                            text = "Быстрые подсказки",
                            style = MaterialTheme.typography.labelLarge,
                            color = TextMuted
                        )
                        state.quickPrompts.forEach { prompt ->
                            OutlinedButton(
                                onClick = { onPromptClick(prompt) },
                                modifier = Modifier.fillMaxWidth()
                            ) {
                                Text(prompt)
                            }
                        }
                    }
                }

                AiChatInput(
                    value = state.inputText,
                    isProcessing = state.isProcessing,
                    onValueChange = onInputChange,
                    onSubmit = onSubmit
                )
            }
        }
    }
}

@Composable
fun AiChatInput(
    value: String,
    isProcessing: Boolean,
    onValueChange: (String) -> Unit,
    onSubmit: () -> Unit
) {
    Row(
        modifier = Modifier.fillMaxWidth(),
        verticalAlignment = Alignment.CenterVertically,
        horizontalArrangement = Arrangement.spacedBy(8.dp)
    ) {
        OutlinedTextField(
            value = value,
            onValueChange = onValueChange,
            modifier = Modifier.weight(1f),
            minLines = 1,
            maxLines = 3,
            label = { Text("Команда") },
            placeholder = { Text("Например: найди перчатки") }
        )
        IconButton(
            onClick = {},
            enabled = false,
            modifier = Modifier
                .size(48.dp)
                .clip(CircleShape)
                .border(1.dp, SearchBorder, CircleShape)
        ) {
            Icon(Icons.Outlined.Mic, contentDescription = "Голосовой ввод", tint = TextMuted)
        }
        Button(
            onClick = onSubmit,
            enabled = !isProcessing && value.isNotBlank(),
            modifier = Modifier.height(56.dp)
        ) {
            Icon(Icons.Outlined.Send, contentDescription = "Отправить")
        }
    }
}

@Composable
fun AiMessageBubble(
    text: String,
    fromUser: Boolean
) {
    Row(
        modifier = Modifier.fillMaxWidth(),
        horizontalArrangement = if (fromUser) Arrangement.End else Arrangement.Start
    ) {
        Text(
            text = text,
            modifier = Modifier
                .fillMaxWidth(0.84f)
                .clip(RoundedCornerShape(16.dp))
                .background(if (fromUser) AccentDark else Color.White)
                .padding(horizontal = 14.dp, vertical = 10.dp),
            color = if (fromUser) Color.White else Color(0xFF222222),
            style = MaterialTheme.typography.bodyMedium
        )
    }
}

@Composable
fun AiCommandPreviewCard(
    command: AiAssistantCommand,
    title: String = "Предпросмотр команды"
) {
    Card(
        modifier = Modifier.fillMaxWidth(),
        shape = RoundedCornerShape(14.dp),
        colors = CardDefaults.cardColors(containerColor = Color.White)
    ) {
        Column(
            modifier = Modifier.padding(14.dp),
            verticalArrangement = Arrangement.spacedBy(8.dp)
        ) {
            Text(
                text = title,
                style = MaterialTheme.typography.titleMedium,
                fontWeight = FontWeight.Bold
            )
            Text(command.summary, style = MaterialTheme.typography.bodyLarge)
            Text(
                text = "Провайдер: ${command.provider}",
                style = MaterialTheme.typography.bodyMedium,
                color = TextMuted
            )
            Text(
                text = "Риск: ${command.riskLevel.label()}",
                style = MaterialTheme.typography.bodyMedium,
                color = command.riskLevel.color()
            )
            CommandParameterRows(command)
        }
    }
}

@Composable
fun AiConfirmationCard(
    command: AiAssistantCommand,
    isProcessing: Boolean,
    onConfirm: () -> Unit,
    onCancel: () -> Unit
) {
    Card(
        modifier = Modifier.fillMaxWidth(),
        shape = RoundedCornerShape(14.dp),
        colors = CardDefaults.cardColors(containerColor = Color(0xFFFFF8E8))
    ) {
        Column(modifier = Modifier.padding(14.dp), verticalArrangement = Arrangement.spacedBy(10.dp)) {
            Row(verticalAlignment = Alignment.CenterVertically) {
                Icon(Icons.Outlined.WarningAmber, contentDescription = null, tint = Color(0xFFD07A00))
                Spacer(modifier = Modifier.width(8.dp))
                Text(
                    "Нужно подтверждение",
                    style = MaterialTheme.typography.titleMedium,
                    fontWeight = FontWeight.Bold
                )
            }
            Text(
                text = "Команда может изменить данные микросклада. После подтверждения действие будет отправлено на сервер.",
                style = MaterialTheme.typography.bodyMedium,
                color = TextMuted
            )
            CommandParameterRows(command)
            Row(horizontalArrangement = Arrangement.spacedBy(10.dp)) {
                OutlinedButton(
                    onClick = onCancel,
                    enabled = !isProcessing,
                    modifier = Modifier.weight(1f)
                ) {
                    Text("Отменить")
                }
                Button(
                    onClick = onConfirm,
                    enabled = !isProcessing,
                    modifier = Modifier.weight(1f)
                ) {
                    Text(if (isProcessing) "Выполнение..." else "Подтвердить")
                }
            }
        }
    }
}

@Composable
fun AiClarificationCard(
    command: AiAssistantCommand,
    isProcessing: Boolean,
    onChoiceClick: (String) -> Unit,
    onCancel: () -> Unit
) {
    Card(
        modifier = Modifier.fillMaxWidth(),
        shape = RoundedCornerShape(14.dp),
        colors = CardDefaults.cardColors(containerColor = Color(0xFFF6F7FF))
    ) {
        Column(
            modifier = Modifier.padding(14.dp),
            verticalArrangement = Arrangement.spacedBy(10.dp)
        ) {
            Text(
                text = command.clarificationQuestion ?: "Выберите уточнение для продолжения.",
                style = MaterialTheme.typography.titleMedium,
                fontWeight = FontWeight.Bold
            )

            if (command.choices.isEmpty()) {
                Text(
                    text = "Подходящих вариантов пока нет. Попробуйте переформулировать команду.",
                    style = MaterialTheme.typography.bodyMedium,
                    color = TextMuted
                )
            } else {
                command.choices.forEach { choice ->
                    OutlinedButton(
                        onClick = { onChoiceClick(choice.id) },
                        enabled = !isProcessing,
                        modifier = Modifier.fillMaxWidth()
                    ) {
                        Text(choice.label)
                    }
                }
            }

            OutlinedButton(
                onClick = onCancel,
                enabled = !isProcessing,
                modifier = Modifier.fillMaxWidth()
            ) {
                Text("Отменить команду")
            }
        }
    }
}

@Composable
fun AiCommandResultCard(
    result: AiAssistantResult,
    snapshot: WarehouseSnapshot,
    commandDefinitions: List<AiAssistantCommandDefinition>
) {
    val details = buildResultDetails(
        result = result,
        snapshot = snapshot,
        commandDefinitions = commandDefinitions
    )

    Card(
        modifier = Modifier.fillMaxWidth(),
        shape = RoundedCornerShape(14.dp),
        colors = CardDefaults.cardColors(
            containerColor = when {
                result.isClarification -> Color(0xFFF6F7FF)
                result.success -> Color.White
                else -> Color(0xFFFFEEEE)
            }
        )
    ) {
        Column(modifier = Modifier.padding(14.dp), verticalArrangement = Arrangement.spacedBy(8.dp)) {
            Text(
                text = when {
                    result.isClarification -> "Нужно уточнение"
                    result.success -> "Результат"
                    else -> "Ошибка"
                },
                style = MaterialTheme.typography.titleMedium,
                fontWeight = FontWeight.Bold
            )
            Text(result.message, style = MaterialTheme.typography.bodyMedium)
            details.take(8).forEach { detail ->
                Text("- $detail", style = MaterialTheme.typography.bodyMedium, color = TextMuted)
            }
        }
    }
}

@Composable
private fun CommandParameterRows(command: AiAssistantCommand) {
    val rows = listOfNotNull(
        "Тип команды: ${command.commandType}",
        command.productId?.let { "Товар ID: $it" },
        command.sourceCellId?.let { "Исходная ячейка ID: $it" },
        command.targetCellId?.let { "Целевая ячейка ID: $it" },
        command.quantity?.let { "Количество: ${it.formatQuantity()}" },
        command.minQuantity?.let { "Мин. остаток: ${it.formatQuantity()}" },
        command.clarificationTarget?.let { "Нужно уточнить: ${it.clarificationLabel()}" }
    )

    rows.forEach { row ->
        Text(row, style = MaterialTheme.typography.bodyMedium, color = Color(0xFF333333))
    }
}

private fun buildResultDetails(
    result: AiAssistantResult,
    snapshot: WarehouseSnapshot,
    commandDefinitions: List<AiAssistantCommandDefinition>
): List<String> {
    return when (result.clientAction?.commandType) {
        "help" -> {
            commandDefinitions
                .sortedBy { it.title.lowercase(Locale.getDefault()) }
                .map { definition ->
                    buildString {
                        append(definition.title)
                        append(": ")
                        append(definition.description)
                    }
                }
                .ifEmpty { result.details }
        }

        "warehouse_summary" -> buildWarehouseSummary(snapshot)
        else -> result.details
    }
}

private fun buildWarehouseSummary(snapshot: WarehouseSnapshot): List<String> {
    val stockBySku = snapshot.stocks.groupBy { it.sku }.mapValues { (_, items) ->
        items.sumOf { it.quantity }
    }
    val lowStockCount = snapshot.products.count { product ->
        val quantity = stockBySku[product.sku] ?: 0.0
        quantity > 0.0 && quantity <= product.minQuantity
    }
    val zeroStockCount = snapshot.products.count { product ->
        (stockBySku[product.sku] ?: 0.0) <= 0.0
    }
    val activeCellCount = snapshot.stocks
        .filter { it.quantity > 0.0 }
        .map { it.cellCode }
        .distinct()
        .size
    val totalQuantity = snapshot.stocks.sumOf { it.quantity }

    return listOf(
        "Номенклатура: ${snapshot.products.size}",
        "Ячейки хранения: ${snapshot.cells.size}",
        "Активные ячейки с остатком: $activeCellCount",
        "Общий остаток: ${totalQuantity.formatQuantity()}",
        "Позиций с низким остатком: $lowStockCount",
        "Позиций с нулевым остатком: $zeroStockCount",
        "Операций в журнале: ${snapshot.operations.size}"
    )
}

private fun String.label(): String = when (this) {
    "None" -> "нет"
    "Low" -> "низкий"
    "Medium" -> "средний"
    "High" -> "высокий"
    "Critical" -> "критический"
    else -> ifBlank { "не указан" }
}

private fun String.color(): Color = when (this) {
    "None" -> TextMuted
    "Low" -> Color(0xFF4C8F4A)
    "Medium" -> Color(0xFFD07A00)
    "High" -> Color(0xFFD35C46)
    "Critical" -> Color(0xFFB00020)
    else -> TextMuted
}

private fun String.clarificationLabel(): String = when (this) {
    "Product" -> "товар"
    "SourceCell" -> "исходную ячейку"
    "TargetCell" -> "целевую ячейку"
    "Command" -> "тип команды"
    else -> this
}

private fun Double.formatQuantity(): String {
    val rounded = roundToLong().toDouble()
    return if (abs(this - rounded) < 0.000001) {
        rounded.toLong().toString()
    } else {
        String.format(Locale.US, "%.2f", this).trimEnd('0').trimEnd('.')
    }
}
