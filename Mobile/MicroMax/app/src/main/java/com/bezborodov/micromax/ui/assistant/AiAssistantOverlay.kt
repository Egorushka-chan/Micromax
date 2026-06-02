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
import com.bezborodov.micromax.domain.assistant.AiCommand
import com.bezborodov.micromax.domain.assistant.AiCommandRegistry
import com.bezborodov.micromax.domain.assistant.AiCommandResult
import com.bezborodov.micromax.domain.assistant.AiCommandRiskLevel
import com.bezborodov.micromax.ui.components.Accent
import com.bezborodov.micromax.ui.components.AccentDark
import com.bezborodov.micromax.ui.components.ScreenBg
import com.bezborodov.micromax.ui.components.SearchBorder
import com.bezborodov.micromax.ui.components.TextMuted

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
    onClose: () -> Unit,
    onInputChange: (String) -> Unit,
    onSubmit: () -> Unit,
    onPromptClick: (String) -> Unit,
    onConfirm: () -> Unit,
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
                        Text("ИИ-помощник MicroMax", style = MaterialTheme.typography.titleLarge, fontWeight = FontWeight.Bold)
                        Text("Команды выполняются по правилам MVP", style = MaterialTheme.typography.bodyMedium, color = TextMuted)
                    }
                    IconButton(onClick = onClose) {
                        Icon(Icons.Outlined.Close, contentDescription = "Закрыть")
                    }
                }

                LazyColumn(
                    modifier = Modifier
                        .fillMaxWidth()
                        .height(260.dp),
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
                    state.lastResult?.let { result ->
                        item {
                            AiCommandResultCard(result = result)
                        }
                    }
                }

                if (state.messages.size <= 1 && state.pendingCommand == null) {
                    Column(verticalArrangement = Arrangement.spacedBy(8.dp)) {
                        Text("Быстрые подсказки", style = MaterialTheme.typography.labelLarge, color = TextMuted)
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
fun AiCommandPreviewCard(command: AiCommand) {
    val definition = AiCommandRegistry.definitionFor(command.type)
    Card(
        modifier = Modifier.fillMaxWidth(),
        shape = RoundedCornerShape(14.dp),
        colors = CardDefaults.cardColors(containerColor = Color.White)
    ) {
        Column(modifier = Modifier.padding(14.dp), verticalArrangement = Arrangement.spacedBy(8.dp)) {
            Text("Предпросмотр команды", style = MaterialTheme.typography.titleMedium, fontWeight = FontWeight.Bold)
            Text(definition.title, style = MaterialTheme.typography.bodyLarge)
            Text(definition.description, style = MaterialTheme.typography.bodyMedium, color = TextMuted)
            Text("Риск: ${command.riskLevel.label()}", style = MaterialTheme.typography.bodyMedium, color = command.riskLevel.color())
        }
    }
}

@Composable
fun AiConfirmationCard(
    command: AiCommand,
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
                Text("Нужно подтверждение", style = MaterialTheme.typography.titleMedium, fontWeight = FontWeight.Bold)
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
fun AiCommandResultCard(result: AiCommandResult) {
    Card(
        modifier = Modifier.fillMaxWidth(),
        shape = RoundedCornerShape(14.dp),
        colors = CardDefaults.cardColors(containerColor = if (result.success) Color.White else Color(0xFFFFEEEE))
    ) {
        Column(modifier = Modifier.padding(14.dp), verticalArrangement = Arrangement.spacedBy(8.dp)) {
            Text(
                text = if (result.success) "Результат" else "Нужно уточнение",
                style = MaterialTheme.typography.titleMedium,
                fontWeight = FontWeight.Bold
            )
            Text(result.message, style = MaterialTheme.typography.bodyMedium)
            result.details.take(8).forEach { detail ->
                Text("• $detail", style = MaterialTheme.typography.bodyMedium, color = TextMuted)
            }
        }
    }
}

@Composable
private fun CommandParameterRows(command: AiCommand) {
    val rows = listOfNotNull(
        command.productQuery?.let { "Товар: $it" },
        command.quantity?.let { "Количество: $it" },
        command.minQuantity?.let { "Минимальный остаток: $it" },
        command.sku?.let { "SKU: $it" },
        command.name?.let { "Название: $it" }
    )
    rows.forEach { row ->
        Text(row, style = MaterialTheme.typography.bodyMedium, color = Color(0xFF333333))
    }
}

private fun AiCommandRiskLevel.label(): String = when (this) {
    AiCommandRiskLevel.None -> "нет"
    AiCommandRiskLevel.Low -> "низкий"
    AiCommandRiskLevel.Medium -> "средний"
    AiCommandRiskLevel.High -> "высокий"
    AiCommandRiskLevel.Critical -> "критический"
}

private fun AiCommandRiskLevel.color(): Color = when (this) {
    AiCommandRiskLevel.None -> TextMuted
    AiCommandRiskLevel.Low -> Color(0xFF4C8F4A)
    AiCommandRiskLevel.Medium -> Color(0xFFD07A00)
    AiCommandRiskLevel.High -> Color(0xFFD35C46)
    AiCommandRiskLevel.Critical -> Color(0xFFB00020)
}
