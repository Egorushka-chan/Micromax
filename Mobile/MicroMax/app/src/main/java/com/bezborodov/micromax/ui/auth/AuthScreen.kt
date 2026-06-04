package com.bezborodov.micromax.ui.auth

import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.verticalScroll
import androidx.compose.material3.Button
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.input.PasswordVisualTransformation
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import com.bezborodov.micromax.ui.components.AccentDark
import com.bezborodov.micromax.ui.components.MessageBanner
import com.bezborodov.micromax.ui.components.ScreenBg
import com.bezborodov.micromax.ui.components.TextMuted

@Composable
fun AuthScreen(
    state: SessionUiState,
    onLogin: (String, String) -> Unit,
    onRegister: (String, String, String) -> Unit,
    onClearMessage: () -> Unit
) {
    var isRegisterMode by rememberSaveable { mutableStateOf(false) }
    var displayName by rememberSaveable { mutableStateOf("") }
    var email by rememberSaveable { mutableStateOf("") }
    var password by rememberSaveable { mutableStateOf("") }

    Box(
        modifier = Modifier
            .fillMaxSize()
            .background(ScreenBg)
            .padding(20.dp)
    ) {
        Column(
            modifier = Modifier
                .align(Alignment.Center)
                .fillMaxWidth()
                .verticalScroll(rememberScrollState()),
            verticalArrangement = Arrangement.spacedBy(16.dp)
        ) {
            Column(verticalArrangement = Arrangement.spacedBy(8.dp)) {
                Text(
                    text = "MicroMax",
                    style = MaterialTheme.typography.displaySmall,
                    fontWeight = FontWeight.Bold
                )
                Text(
                    text = "Авторизация требуется для доступа к складам, операциям и журналу.",
                    style = MaterialTheme.typography.bodyLarge,
                    color = TextMuted
                )
            }

            state.message?.let {
                MessageBanner(it)
            }

            Card(
                modifier = Modifier.fillMaxWidth(),
                shape = RoundedCornerShape(18.dp),
                colors = CardDefaults.cardColors(containerColor = Color.White),
                elevation = CardDefaults.cardElevation(defaultElevation = 4.dp)
            ) {
                Column(
                    modifier = Modifier
                        .fillMaxWidth()
                        .padding(20.dp),
                    verticalArrangement = Arrangement.spacedBy(14.dp)
                ) {
                    Text(
                        text = if (isRegisterMode) "Создание учётной записи" else "Вход в систему",
                        style = MaterialTheme.typography.headlineSmall,
                        fontWeight = FontWeight.Bold
                    )

                    if (isRegisterMode) {
                        OutlinedTextField(
                            value = displayName,
                            onValueChange = { displayName = it },
                            label = { Text("Имя пользователя") },
                            modifier = Modifier.fillMaxWidth(),
                            singleLine = true
                        )
                    }

                    OutlinedTextField(
                        value = email,
                        onValueChange = { email = it },
                        label = { Text("Email") },
                        modifier = Modifier.fillMaxWidth(),
                        singleLine = true
                    )
                    OutlinedTextField(
                        value = password,
                        onValueChange = { password = it },
                        label = { Text("Пароль") },
                        modifier = Modifier.fillMaxWidth(),
                        singleLine = true,
                        visualTransformation = PasswordVisualTransformation()
                    )

                    Button(
                        onClick = {
                            if (isRegisterMode) {
                                onRegister(email, password, displayName)
                            } else {
                                onLogin(email, password)
                            }
                        },
                        enabled = !state.isSubmitting,
                        modifier = Modifier.fillMaxWidth()
                    ) {
                        Text(
                            if (state.isSubmitting) {
                                "Подключение..."
                            } else if (isRegisterMode) {
                                "Зарегистрироваться"
                            } else {
                                "Войти"
                            }
                        )
                    }

                    OutlinedButton(
                        onClick = {
                            onClearMessage()
                            isRegisterMode = !isRegisterMode
                        },
                        enabled = !state.isSubmitting,
                        modifier = Modifier.fillMaxWidth()
                    ) {
                        Text(
                            if (isRegisterMode) {
                                "У меня уже есть аккаунт"
                            } else {
                                "Создать новый аккаунт"
                            }
                        )
                    }
                }
            }
        }
    }
}

@Composable
fun NoWarehouseAccessScreen(
    state: SessionUiState,
    onCreateWarehouse: (String, String?) -> Unit,
    onLogout: () -> Unit
) {
    var warehouseName by rememberSaveable { mutableStateOf("") }
    var warehouseAddress by rememberSaveable { mutableStateOf("") }

    Box(
        modifier = Modifier
            .fillMaxSize()
            .background(ScreenBg)
            .padding(20.dp)
    ) {
        Column(
            modifier = Modifier
                .align(Alignment.Center)
                .fillMaxWidth()
                .verticalScroll(rememberScrollState()),
            verticalArrangement = Arrangement.spacedBy(16.dp)
        ) {
            Card(
                modifier = Modifier.fillMaxWidth(),
                shape = RoundedCornerShape(18.dp),
                colors = CardDefaults.cardColors(containerColor = Color.White),
                elevation = CardDefaults.cardElevation(defaultElevation = 4.dp)
            ) {
                Column(
                    modifier = Modifier
                        .fillMaxWidth()
                        .padding(20.dp),
                    verticalArrangement = Arrangement.spacedBy(12.dp)
                ) {
                    Text(
                        text = "Нет доступных складов",
                        style = MaterialTheme.typography.headlineSmall,
                        fontWeight = FontWeight.Bold
                    )
                    Text(
                        text = "Пользователь ${state.currentUser?.displayName ?: ""} уже вошёл в систему, но ещё не привязан ни к одному складу. Для начала работы создайте первый склад.",
                        style = MaterialTheme.typography.bodyLarge,
                        color = TextMuted
                    )
                    state.message?.let {
                        MessageBanner(it)
                    }
                    OutlinedTextField(
                        value = warehouseName,
                        onValueChange = { warehouseName = it },
                        label = { Text("Название склада") },
                        modifier = Modifier.fillMaxWidth(),
                        singleLine = true
                    )
                    OutlinedTextField(
                        value = warehouseAddress,
                        onValueChange = { warehouseAddress = it },
                        label = { Text("Адрес") },
                        modifier = Modifier.fillMaxWidth(),
                        singleLine = true
                    )
                    Button(
                        onClick = { onCreateWarehouse(warehouseName, warehouseAddress) },
                        enabled = !state.isCreatingWarehouse,
                        modifier = Modifier.fillMaxWidth()
                    ) {
                        Text(if (state.isCreatingWarehouse) "Создание..." else "Создать склад")
                    }
                    OutlinedButton(
                        onClick = onLogout,
                        enabled = !state.isCreatingWarehouse,
                        modifier = Modifier.fillMaxWidth()
                    ) {
                        Text("Выйти")
                    }
                }
            }

            Spacer(modifier = Modifier.height(8.dp))
            Text(
                text = "После создания склада приложение автоматически загрузит доступные разделы и данные текущего пользователя.",
                style = MaterialTheme.typography.bodyMedium,
                color = AccentDark,
                textAlign = TextAlign.Center,
                modifier = Modifier.fillMaxWidth()
            )
        }
    }
}
