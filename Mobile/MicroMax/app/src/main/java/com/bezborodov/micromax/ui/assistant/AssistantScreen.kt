package com.bezborodov.micromax.ui.assistant

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.material3.Button
import androidx.compose.material3.Divider
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import com.bezborodov.micromax.ui.components.SearchBorder
import com.bezborodov.micromax.ui.components.SectionCard
import com.bezborodov.micromax.ui.components.SimpleTitle
import com.bezborodov.micromax.ui.components.TextMuted
import com.bezborodov.micromax.ui.home.HomeUiState

@Composable
fun AssistantScreen(
    state: HomeUiState,
    onInterpretCommand: (String) -> Unit,
    onConfirmCommand: (String) -> Unit
) {
    var text by remember { mutableStateOf("Где лежат перчатки?") }
    var history by remember { mutableStateOf(listOf<String>()) }

    LazyColumn(verticalArrangement = Arrangement.spacedBy(14.dp)) {
        item { SimpleTitle("Ассистент") }
        item {
            SectionCard(title = "Командный помощник") {
                Text(
                    text = "Операции изменения остатков выполняются только после подтверждения.",
                    color = TextMuted,
                    style = MaterialTheme.typography.bodyMedium
                )
                OutlinedTextField(
                    value = text,
                    onValueChange = { text = it },
                    label = { Text("Команда") },
                    minLines = 2,
                    modifier = Modifier.fillMaxWidth()
                )
                Button(
                    onClick = {
                        history = (listOf(text) + history).take(5)
                        onInterpretCommand(text)
                    },
                    enabled = !state.isAssistantLoading,
                    modifier = Modifier.fillMaxWidth()
                ) { Text(if (state.isAssistantLoading) "Обработка..." else "Разобрать команду") }

                state.pendingCommand?.let { command ->
                    Divider(color = SearchBorder)
                    Text("Результат", fontWeight = FontWeight.SemiBold)
                    Text(command.summary, color = TextMuted)
                    if (command.requiresConfirmation) {
                        Button(
                            onClick = { onConfirmCommand(command.commandId) },
                            enabled = !state.isOperationSubmitting,
                            modifier = Modifier.fillMaxWidth()
                        ) {
                            Text(if (state.isOperationSubmitting) "Выполнение..." else "Подтвердить выполнение")
                        }
                    }
                }
            }
        }
        item {
            SectionCard(title = "История запросов") {
                if (history.isEmpty()) {
                    Text("История запросов пока пуста.", color = TextMuted)
                } else {
                    history.forEach { command ->
                        Text(command, style = MaterialTheme.typography.bodyMedium)
                    }
                }
            }
        }
    }
}
