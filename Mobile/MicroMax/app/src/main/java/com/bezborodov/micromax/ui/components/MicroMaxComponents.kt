package com.bezborodov.micromax.ui.components

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
import androidx.compose.material.icons.outlined.SwapHoriz
import androidx.compose.material.icons.outlined.WarningAmber
import androidx.compose.material3.Button
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.Divider
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.NavigationBar
import androidx.compose.material3.NavigationBarItem
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.Immutable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import com.bezborodov.micromax.ui.assistant.AiCommandButton

val ScreenBg = Color(0xFFF3F3F3)
val Accent = Color(0xFF5865F2)
val AccentDark = Color(0xFF4B55DE)
val SearchBorder = Color(0xFFE5E5E5)
val TextSecondary = Color(0xFF8A8A8A)
val TextMuted = Color(0xFF747480)

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
    Cells,
    Assistant,
    Transactions,
    Settings
}

@Composable
fun LoadingState() {
    Box(
        modifier = Modifier.fillMaxSize(),
        contentAlignment = Alignment.Center
    ) {
        Column(horizontalAlignment = Alignment.CenterHorizontally) {
            CircularProgressIndicator(color = AccentDark)
            Spacer(modifier = Modifier.height(14.dp))
            Text(
                text = "Загрузка данных микросклада...",
                style = MaterialTheme.typography.bodyLarge,
                color = TextMuted
            )
        }
    }
}

@Composable
fun FirstLoadErrorState(
    message: String,
    onRefresh: () -> Unit
) {
    Box(
        modifier = Modifier.fillMaxSize(),
        contentAlignment = Alignment.Center
    ) {
        SectionCard(title = "Не удалось загрузить данные") {
            Text(
                text = message,
                style = MaterialTheme.typography.bodyMedium,
                color = TextMuted
            )
            Button(onClick = onRefresh, modifier = Modifier.fillMaxWidth()) {
                Text("Повторить")
            }
        }
    }
}

@Composable
fun HeaderCompanyBlock(companyName: String) {
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
fun DailyStatsCard(
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
fun SearchBarBlock(
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
fun SectionCard(title: String, content: @Composable ColumnScope.() -> Unit) {
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
fun ActionMenuRow(
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
fun PlainInfoRow(title: String, subtitle: String) {
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
fun EmptyStateText(text: String) {
    Text(
        text = text,
        style = MaterialTheme.typography.bodyMedium,
        color = TextMuted,
        modifier = Modifier.padding(vertical = 8.dp)
    )
}

@Composable
fun MenuLeadingIcon(icon: HomeMenuIcon) {
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
fun HomeBottomBar(
    selectedTab: BottomTab,
    onTabClick: (BottomTab) -> Unit,
    onAssistantClick: () -> Unit = { onTabClick(BottomTab.Assistant) }
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

        AiCommandButton(
            onClick = onAssistantClick,
            modifier = Modifier
                .padding(horizontal = 8.dp)
                .size(58.dp)
        )

        NavigationBarItem(
            selected = selectedTab == BottomTab.Transactions,
            onClick = { onTabClick(BottomTab.Transactions) },
            icon = { Icon(Icons.Outlined.SwapHoriz, contentDescription = "Операции") },
            label = { Text("Операции") }
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
fun SimpleTitle(title: String) {
    Text(
        text = title,
        style = MaterialTheme.typography.headlineMedium,
        color = Color.Black,
        fontWeight = FontWeight.Bold,
        modifier = Modifier.padding(vertical = 8.dp)
    )
}

@Composable
fun CompactInput(value: String, onValueChange: (String) -> Unit, label: String) {
    OutlinedTextField(
        value = value,
        onValueChange = onValueChange,
        label = { Text(label) },
        modifier = Modifier.fillMaxWidth(),
        singleLine = true
    )
}

@Composable
fun MessageBanner(text: String) {
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
