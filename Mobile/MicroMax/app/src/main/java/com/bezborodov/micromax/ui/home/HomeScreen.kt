package com.bezborodov.micromax.ui.home

import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.ColumnScope
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.navigationBarsPadding
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.outlined.ArrowForwardIos
import androidx.compose.material.icons.outlined.Groups
import androidx.compose.material.icons.outlined.Home
import androidx.compose.material.icons.outlined.Inventory2
import androidx.compose.material.icons.outlined.Place
import androidx.compose.material.icons.outlined.QrCodeScanner
import androidx.compose.material.icons.outlined.Search
import androidx.compose.material.icons.outlined.Settings
import androidx.compose.material.icons.outlined.SmartToy
import androidx.compose.material.icons.outlined.SwapHoriz
import androidx.compose.material.icons.outlined.WarningAmber
import androidx.compose.material3.Button
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.Divider
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.NavigationBar
import androidx.compose.material3.NavigationBarItem
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.Immutable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.tooling.preview.Preview
import androidx.compose.ui.unit.dp
import com.bezborodov.micromax.data.MicroMaxApiClient
import com.bezborodov.micromax.ui.theme.MicroMaxTheme
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext

private val ScreenBg = Color(0xFFF3F3F3)
private val Accent = Color(0xFF5865F2)
private val AccentDark = Color(0xFF4B55DE)
private val SearchBorder = Color(0xFFE5E5E5)
private val TextSecondary = Color(0xFF8A8A8A)
private val TextMuted = Color(0xFF747480)

@Immutable
data class HomeMenuItem(
    val title: String,
    val subtitle: String? = null,
    val icon: HomeMenuIcon
)

enum class HomeMenuIcon {
    AddItem,
    LowStock,
    Inventory,
    Team,
    Receive,
    WriteOff,
    Move,
    Cell
}

enum class BottomTab {
    Home,
    Items,
    Assistant,
    Transactions,
    Settings
}

@Composable
fun HomeScreen(
    apiClient: MicroMaxApiClient = remember { MicroMaxApiClient() }
) {
    var selectedTab by remember { mutableStateOf(BottomTab.Home) }
    var state by remember { mutableStateOf(HomeUiState(isLoading = true)) }
    val scope = rememberCoroutineScope()

    fun refresh(showMessage: Boolean = false) {
        scope.launch {
            state = state.copy(isLoading = true, message = null)
            state = runCatching {
                val snapshot = withContext(Dispatchers.IO) { apiClient.loadSnapshot() }
                state.copy(
                    snapshot = snapshot,
                    isLoading = false,
                    message = if (showMessage) "Данные обновлены" else null
                )
            }.getOrElse {
                state.copy(
                    isLoading = false,
                    message = it.message ?: "Не удалось загрузить данные"
                )
            }
        }
    }

    LaunchedEffect(Unit) {
        refresh()
    }

    Scaffold(
        containerColor = ScreenBg,
        bottomBar = {
            HomeBottomBar(
                selectedTab = selectedTab,
                onTabClick = { selectedTab = it }
            )
        }
    ) { innerPadding ->
        Column(
            modifier = Modifier
                .fillMaxSize()
                .background(ScreenBg)
                .padding(innerPadding)
                .padding(horizontal = 16.dp, vertical = 12.dp)
        ) {
            if (state.message != null) {
                MessageBanner(state.message!!)
                Spacer(modifier = Modifier.height(10.dp))
            }

            when (selectedTab) {
                BottomTab.Home -> HomeTab(
                    state = state,
                    onRefresh = { refresh(showMessage = true) },
                    onOpenItems = { selectedTab = BottomTab.Items },
                    onOpenOperations = { selectedTab = BottomTab.Transactions },
                    onOpenAssistant = { selectedTab = BottomTab.Assistant }
                )

                BottomTab.Items -> ItemsTab(state)

                BottomTab.Assistant -> AssistantTab(
                    state = state,
                    apiClient = apiClient,
                    onStateChanged = { state = it },
                    onChanged = { refresh(showMessage = true) }
                )

                BottomTab.Transactions -> TransactionsTab(
                    state = state,
                    apiClient = apiClient,
                    onChanged = { refresh(showMessage = true) }
                )

                BottomTab.Settings -> SettingsTab(state, onRefresh = { refresh(showMessage = true) })
            }
        }
    }
}

@Composable
private fun HomeTab(
    state: HomeUiState,
    onRefresh: () -> Unit,
    onOpenItems: () -> Unit,
    onOpenOperations: () -> Unit,
    onOpenAssistant: () -> Unit
) {
    LazyColumn(verticalArrangement = Arrangement.spacedBy(14.dp)) {
        item {
            Text(
                text = "Главное окно",
                style = MaterialTheme.typography.labelMedium,
                color = TextSecondary
            )
        }

        item { HeaderCompanyBlock(companyName = state.companyName) }

        item {
            DailyStatsCard(
                dateText = state.dateText,
                totalCount = state.totalOperationCount,
                incomeCount = state.incomeCount,
                outcomeCount = state.outcomeCount
            )
        }

        item {
            SearchBarBlock(
                placeholder = "Поиск товара",
                onSearchClick = onOpenItems,
                onScannerClick = onOpenAssistant
            )
        }

        item {
            SectionCard(title = "Товары") {
                ActionMenuRow(
                    item = HomeMenuItem("Номенклатура", "Товары и текущие остатки", HomeMenuIcon.AddItem),
                    onClick = onOpenItems
                )
                ActionMenuRow(
                    item = HomeMenuItem("Просмотр ячеек", "Содержимое мест хранения", HomeMenuIcon.Cell),
                    onClick = onOpenItems
                )
            }
        }

        item {
            SectionCard(title = "Транзакции") {
                ActionMenuRow(
                    item = HomeMenuItem("Приход", "Принять товар в ячейку", HomeMenuIcon.Receive),
                    onClick = onOpenOperations
                )
                ActionMenuRow(
                    item = HomeMenuItem("Расход", "Списать товар из ячейки", HomeMenuIcon.WriteOff),
                    onClick = onOpenOperations
                )
                ActionMenuRow(
                    item = HomeMenuItem("Перемещение", "Перенести товар между ячейками", HomeMenuIcon.Move),
                    onClick = onOpenOperations
                )
            }
        }

        item {
            SectionCard(title = "Помощник") {
                ActionMenuRow(
                    item = HomeMenuItem("Командный помощник", "Поиск и операции через команды", HomeMenuIcon.Team),
                    onClick = onOpenAssistant
                )
                ActionMenuRow(
                    item = HomeMenuItem("Обновить данные", "Загрузить актуальные остатки с сервера", HomeMenuIcon.Inventory),
                    onClick = onRefresh
                )
            }
        }
    }
}

@Composable
private fun ItemsTab(state: HomeUiState) {
    LazyColumn(verticalArrangement = Arrangement.spacedBy(14.dp)) {
        item { SimpleTitle("Товары") }
        item {
            SearchBarBlock(
                placeholder = "Поиск товара",
                onSearchClick = {},
                onScannerClick = {}
            )
        }
        item {
            SectionCard(title = "Номенклатура") {
                state.snapshot.products.forEach { product ->
                    PlainInfoRow(
                        title = product.name,
                        subtitle = "${product.sku} · мин. остаток ${product.minQuantity} ${product.unit}"
                    )
                }
            }
        }
        item {
            SectionCard(title = "Остатки") {
                state.snapshot.stocks.forEach { stock ->
                    PlainInfoRow(
                        title = stock.productName,
                        subtitle = "${stock.zoneCode} / ${stock.cellCode}: ${stock.quantity} ${stock.unit}"
                    )
                }
            }
        }
        item {
            SectionCard(title = "Ячейки") {
                state.snapshot.cells.forEach { cell ->
                    PlainInfoRow(
                        title = cell.code,
                        subtitle = cell.name
                    )
                }
            }
        }
    }
}

@Composable
private fun TransactionsTab(
    state: HomeUiState,
    apiClient: MicroMaxApiClient,
    onChanged: () -> Unit
) {
    var productId by remember(state.snapshot.products) { mutableStateOf(state.snapshot.products.firstOrNull()?.id?.toString().orEmpty()) }
    var sourceCellId by remember(state.snapshot.cells) { mutableStateOf(state.snapshot.cells.firstOrNull()?.id?.toString().orEmpty()) }
    var targetCellId by remember(state.snapshot.cells) { mutableStateOf(state.snapshot.cells.firstOrNull()?.id?.toString().orEmpty()) }
    var quantity by remember { mutableStateOf("1") }
    var message by remember { mutableStateOf<String?>(null) }
    val scope = rememberCoroutineScope()

    fun runOperation(action: () -> Unit) {
        scope.launch {
            message = runCatching { withContext(Dispatchers.IO) { action() } }
                .fold(
                    onSuccess = { "Операция выполнена" },
                    onFailure = { it.message ?: "Ошибка операции" }
                )
            onChanged()
        }
    }

    LazyColumn(verticalArrangement = Arrangement.spacedBy(14.dp)) {
        item { SimpleTitle("Транзакции") }
        item {
            SectionCard(title = "Новая операция") {
                if (message != null) {
                    Text(message!!, color = AccentDark, style = MaterialTheme.typography.bodyMedium)
                }
                CompactInput(value = productId, onValueChange = { productId = it }, label = "ID товара")
                CompactInput(value = sourceCellId, onValueChange = { sourceCellId = it }, label = "ID исходной ячейки")
                CompactInput(value = targetCellId, onValueChange = { targetCellId = it }, label = "ID целевой ячейки")
                CompactInput(value = quantity, onValueChange = { quantity = it }, label = "Количество")
                Button(
                    onClick = { runOperation { apiClient.receive(productId.toInt(), targetCellId.toInt(), quantity.toDouble()) } },
                    modifier = Modifier.fillMaxWidth()
                ) { Text("Приход") }
                Button(
                    onClick = { runOperation { apiClient.writeOff(productId.toInt(), sourceCellId.toInt(), quantity.toDouble()) } },
                    modifier = Modifier.fillMaxWidth()
                ) { Text("Расход") }
                Button(
                    onClick = { runOperation { apiClient.move(productId.toInt(), sourceCellId.toInt(), targetCellId.toInt(), quantity.toDouble()) } },
                    modifier = Modifier.fillMaxWidth()
                ) { Text("Перемещение") }
            }
        }
        item {
            SectionCard(title = "Журнал операций") {
                state.snapshot.operations.forEach { operation ->
                    PlainInfoRow(
                        title = "${operation.type}: ${operation.productName}",
                        subtitle = "${operation.sourceCell.orEmpty()} → ${operation.targetCell.orEmpty()} · ${operation.quantity}"
                    )
                }
            }
        }
    }
}

@Composable
private fun AssistantTab(
    state: HomeUiState,
    apiClient: MicroMaxApiClient,
    onStateChanged: (HomeUiState) -> Unit,
    onChanged: () -> Unit
) {
    var text by remember { mutableStateOf("Где лежат перчатки?") }
    var localMessage by remember { mutableStateOf<String?>(null) }
    val scope = rememberCoroutineScope()

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
                        scope.launch {
                            runCatching { withContext(Dispatchers.IO) { apiClient.interpretAssistant(text) } }
                                .onSuccess {
                                    onStateChanged(state.copy(pendingCommand = it, message = it.summary))
                                    localMessage = null
                                }
                                .onFailure { localMessage = it.message ?: "Ошибка ассистента" }
                        }
                    },
                    modifier = Modifier.fillMaxWidth()
                ) { Text("Разобрать команду") }

                state.pendingCommand?.let { command ->
                    Divider(color = SearchBorder)
                    Text(command.summary, fontWeight = FontWeight.SemiBold)
                    if (command.requiresConfirmation) {
                        Button(
                            onClick = {
                                scope.launch {
                                    runCatching { withContext(Dispatchers.IO) { apiClient.confirmAssistant(command.commandId) } }
                                        .onSuccess {
                                            onStateChanged(state.copy(pendingCommand = null, message = "Команда подтверждена и выполнена"))
                                            onChanged()
                                        }
                                        .onFailure { localMessage = it.message ?: "Ошибка подтверждения" }
                                }
                            },
                            modifier = Modifier.fillMaxWidth()
                        ) { Text("Подтвердить выполнение") }
                    }
                }

                if (localMessage != null) {
                    Text(localMessage!!, color = MaterialTheme.colorScheme.error)
                }
            }
        }
    }
}

@Composable
private fun SettingsTab(state: HomeUiState, onRefresh: () -> Unit) {
    LazyColumn(verticalArrangement = Arrangement.spacedBy(14.dp)) {
        item { SimpleTitle("Настройки") }
        item {
            SectionCard(title = "Сервер") {
                PlainInfoRow("Подключение", "http://10.0.2.2:5101")
                PlainInfoRow("Номенклатура", "${state.snapshot.products.size} позиций")
                PlainInfoRow("Ячейки хранения", "${state.snapshot.cells.size} ячеек")
                OutlinedButton(onClick = onRefresh, modifier = Modifier.fillMaxWidth()) {
                    Text("Обновить данные")
                }
            }
        }
    }
}

@Composable
private fun HeaderCompanyBlock(companyName: String) {
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .padding(horizontal = 2.dp),
        verticalAlignment = Alignment.CenterVertically
    ) {
        Box(
            modifier = Modifier
                .size(46.dp)
                .clip(RoundedCornerShape(12.dp))
                .background(Color.White),
            contentAlignment = Alignment.Center
        ) {
            Icon(
                imageVector = Icons.Outlined.Inventory2,
                contentDescription = null,
                tint = AccentDark,
                modifier = Modifier.size(28.dp)
            )
        }

        Spacer(modifier = Modifier.width(12.dp))

        Text(
            text = companyName,
            style = MaterialTheme.typography.headlineSmall,
            color = Color(0xFF1B1B1B),
            modifier = Modifier.weight(1f)
        )

        Icon(
            imageVector = Icons.Outlined.ArrowForwardIos,
            contentDescription = null,
            tint = Color(0xFF6F6F6F),
            modifier = Modifier.size(16.dp)
        )
    }
}

@Composable
private fun DailyStatsCard(
    dateText: String,
    totalCount: Int,
    incomeCount: Int,
    outcomeCount: Int
) {
    Card(
        modifier = Modifier.fillMaxWidth(),
        shape = RoundedCornerShape(8.dp),
        colors = CardDefaults.cardColors(containerColor = Accent),
        elevation = CardDefaults.cardElevation(defaultElevation = 6.dp)
    ) {
        Column(
            modifier = Modifier
                .fillMaxWidth()
                .padding(horizontal = 24.dp, vertical = 18.dp)
        ) {
            Text(
                text = dateText,
                color = Color.White,
                style = MaterialTheme.typography.headlineSmall,
                fontWeight = FontWeight.Medium
            )

            Spacer(modifier = Modifier.height(16.dp))

            Row(
                modifier = Modifier.fillMaxWidth(),
                verticalAlignment = Alignment.CenterVertically
            ) {
                StatsColumn(modifier = Modifier.weight(1f), value = totalCount.toString(), label = "Итого")
                VerticalDivider()
                StatsColumn(modifier = Modifier.weight(1f), value = incomeCount.toString(), label = "Приход")
                VerticalDivider()
                StatsColumn(modifier = Modifier.weight(1f), value = outcomeCount.toString(), label = "Расход")
            }
        }
    }
}

@Composable
private fun StatsColumn(
    modifier: Modifier = Modifier,
    value: String,
    label: String
) {
    Column(
        modifier = modifier,
        horizontalAlignment = Alignment.CenterHorizontally
    ) {
        Text(
            text = value,
            color = Color.White,
            style = MaterialTheme.typography.headlineMedium,
            fontWeight = FontWeight.Bold
        )
        Spacer(modifier = Modifier.height(8.dp))
        Text(
            text = label,
            color = Color.White.copy(alpha = 0.76f),
            style = MaterialTheme.typography.titleMedium
        )
    }
}

@Composable
private fun VerticalDivider() {
    Divider(
        modifier = Modifier
            .height(70.dp)
            .width(1.dp),
        color = Color.White.copy(alpha = 0.22f)
    )
}

@Composable
private fun SearchBarBlock(
    placeholder: String,
    onSearchClick: () -> Unit,
    onScannerClick: () -> Unit
) {
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .height(56.dp)
            .clip(RoundedCornerShape(8.dp))
            .background(Color.White)
            .border(1.dp, SearchBorder, RoundedCornerShape(8.dp)),
        verticalAlignment = Alignment.CenterVertically
    ) {
        Row(
            modifier = Modifier
                .weight(1f)
                .clickable(onClick = onSearchClick)
                .padding(horizontal = 18.dp),
            verticalAlignment = Alignment.CenterVertically
        ) {
            Icon(
                imageVector = Icons.Outlined.Search,
                contentDescription = null,
                tint = Color(0xFFC3C3C3)
            )

            Spacer(modifier = Modifier.width(12.dp))

            Text(
                text = placeholder,
                style = MaterialTheme.typography.bodyLarge,
                color = Color(0xFFA0A0A0)
            )
        }

        Divider(
            modifier = Modifier
                .height(30.dp)
                .width(1.dp),
            color = SearchBorder
        )

        Box(
            modifier = Modifier
                .size(56.dp)
                .clickable(onClick = onScannerClick),
            contentAlignment = Alignment.Center
        ) {
            Icon(
                imageVector = Icons.Outlined.QrCodeScanner,
                contentDescription = "Сканировать",
                tint = AccentDark,
                modifier = Modifier.size(24.dp)
            )
        }
    }
}

@Composable
private fun SectionCard(title: String, content: @Composable ColumnScope.() -> Unit) {
    Card(
        modifier = Modifier.fillMaxWidth(),
        shape = RoundedCornerShape(8.dp),
        colors = CardDefaults.cardColors(containerColor = Color.White),
        elevation = CardDefaults.cardElevation(defaultElevation = 4.dp)
    ) {
        Column(
            modifier = Modifier.padding(horizontal = 18.dp, vertical = 18.dp),
            verticalArrangement = Arrangement.spacedBy(10.dp)
        ) {
            Text(
                text = title,
                style = MaterialTheme.typography.headlineSmall,
                color = Color.Black,
                fontWeight = FontWeight.Bold
            )
            content()
        }
    }
}

@Composable
private fun ActionMenuRow(
    item: HomeMenuItem,
    onClick: () -> Unit
) {
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .clip(RoundedCornerShape(8.dp))
            .clickable(onClick = onClick)
            .padding(vertical = 10.dp),
        verticalAlignment = Alignment.CenterVertically
    ) {
        MenuLeadingIcon(item.icon)

        Spacer(modifier = Modifier.width(14.dp))

        Column(modifier = Modifier.weight(1f)) {
            Text(
                text = item.title,
                style = MaterialTheme.typography.titleLarge,
                color = Color(0xFF1E1E1E)
            )
            if (item.subtitle != null) {
                Text(
                    text = item.subtitle,
                    style = MaterialTheme.typography.bodyMedium,
                    color = TextMuted
                )
            }
        }

        Icon(
            imageVector = Icons.Outlined.ArrowForwardIos,
            contentDescription = null,
            tint = Color(0xFF8B8B8B),
            modifier = Modifier.size(16.dp)
        )
    }
}

@Composable
private fun PlainInfoRow(title: String, subtitle: String) {
    Column(
        modifier = Modifier
            .fillMaxWidth()
            .padding(vertical = 7.dp)
    ) {
        Text(title, style = MaterialTheme.typography.titleMedium, fontWeight = FontWeight.SemiBold)
        Text(subtitle, style = MaterialTheme.typography.bodyMedium, color = TextMuted)
    }
}

@Composable
private fun MenuLeadingIcon(icon: HomeMenuIcon) {
    val tint = when (icon) {
        HomeMenuIcon.Receive -> Color(0xFF5B9CEB)
        HomeMenuIcon.WriteOff -> Color(0xFFE95564)
        HomeMenuIcon.Move -> Color(0xFFE8A83A)
        HomeMenuIcon.Cell -> Color(0xFF57B894)
        else -> AccentDark
    }

    val vector = when (icon) {
        HomeMenuIcon.AddItem -> Icons.Outlined.Inventory2
        HomeMenuIcon.LowStock -> Icons.Outlined.WarningAmber
        HomeMenuIcon.Inventory -> Icons.Outlined.SwapHoriz
        HomeMenuIcon.Team -> Icons.Outlined.Groups
        HomeMenuIcon.Receive -> Icons.Outlined.Inventory2
        HomeMenuIcon.WriteOff -> Icons.Outlined.WarningAmber
        HomeMenuIcon.Move -> Icons.Outlined.SwapHoriz
        HomeMenuIcon.Cell -> Icons.Outlined.Place
    }

    Icon(
        imageVector = vector,
        contentDescription = null,
        tint = tint,
        modifier = Modifier.size(26.dp)
    )
}

@Composable
private fun HomeBottomBar(
    selectedTab: BottomTab,
    onTabClick: (BottomTab) -> Unit
) {
    NavigationBar(
        containerColor = Color.White,
        tonalElevation = 6.dp,
        modifier = Modifier.navigationBarsPadding()
    ) {
        NavigationBarItem(
            selected = selectedTab == BottomTab.Home,
            onClick = { onTabClick(BottomTab.Home) },
            icon = { Icon(Icons.Outlined.Home, contentDescription = "Главная") },
            label = { Text("Главная") }
        )

        NavigationBarItem(
            selected = selectedTab == BottomTab.Items,
            onClick = { onTabClick(BottomTab.Items) },
            icon = { Icon(Icons.Outlined.Inventory2, contentDescription = "Товары") },
            label = { Text("Товары") }
        )

        Box(
            modifier = Modifier
                .padding(horizontal = 8.dp)
                .size(58.dp)
                .clip(CircleShape)
                .background(AccentDark)
                .clickable { onTabClick(BottomTab.Assistant) },
            contentAlignment = Alignment.Center
        ) {
            Icon(
                imageVector = Icons.Outlined.SmartToy,
                contentDescription = "Ассистент",
                tint = Color.White,
                modifier = Modifier.size(28.dp)
            )
        }

        NavigationBarItem(
            selected = selectedTab == BottomTab.Transactions,
            onClick = { onTabClick(BottomTab.Transactions) },
            icon = { Icon(Icons.Outlined.SwapHoriz, contentDescription = "Транзакции") },
            label = { Text("Транзакции") }
        )

        NavigationBarItem(
            selected = selectedTab == BottomTab.Settings,
            onClick = { onTabClick(BottomTab.Settings) },
            icon = { Icon(Icons.Outlined.Settings, contentDescription = "Настройки") },
            label = { Text("Настройки") }
        )
    }
}

@Composable
private fun SimpleTitle(title: String) {
    Text(
        text = title,
        style = MaterialTheme.typography.headlineMedium,
        color = Color.Black,
        fontWeight = FontWeight.Bold,
        modifier = Modifier.padding(vertical = 8.dp)
    )
}

@Composable
private fun CompactInput(value: String, onValueChange: (String) -> Unit, label: String) {
    OutlinedTextField(
        value = value,
        onValueChange = onValueChange,
        label = { Text(label) },
        modifier = Modifier.fillMaxWidth(),
        singleLine = true
    )
}

@Composable
private fun MessageBanner(text: String) {
    Card(
        colors = CardDefaults.cardColors(containerColor = Color.White),
        shape = RoundedCornerShape(8.dp),
        modifier = Modifier.fillMaxWidth()
    ) {
        Text(
            text = text,
            color = AccentDark,
            modifier = Modifier.padding(12.dp),
            style = MaterialTheme.typography.bodyMedium
        )
    }
}

@Preview(
    showBackground = true,
    backgroundColor = 0xFFF3F3F3,
    widthDp = 380,
    heightDp = 820
)
@Composable
private fun HomeScreenPreview() {
    MicroMaxTheme {
        HomeScreen()
    }
}
