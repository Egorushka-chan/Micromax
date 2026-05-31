package com.bezborodov.micromax.ui.home

import androidx.compose.ui.tooling.preview.Preview
import com.bezborodov.micromax.ui.theme.MicroMaxTheme
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.navigationBarsPadding
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.outlined.ArrowForwardIos
import androidx.compose.material.icons.outlined.Groups
import androidx.compose.material.icons.outlined.Home
import androidx.compose.material.icons.outlined.Inventory2
import androidx.compose.material.icons.outlined.QrCodeScanner
import androidx.compose.material.icons.outlined.Search
import androidx.compose.material.icons.outlined.Settings
import androidx.compose.material.icons.outlined.SmartToy
import androidx.compose.material.icons.outlined.SwapHoriz
import androidx.compose.material.icons.outlined.WarningAmber
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.Divider
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.NavigationBar
import androidx.compose.material3.NavigationBarItem
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.Immutable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp

private val ScreenBg = Color(0xFFF3F3F3)
private val Accent = Color(0xFF8E8CF6)
private val AccentDark = Color(0xFF6F6BEA)
private val CardShadow = Color(0x14000000)
private val SearchBorder = Color(0xFFE5E5E5)
private val TextSecondary = Color(0xFF8A8A8A)

@Immutable
data class HomeMenuItem(
    val title: String,
    val icon: HomeMenuIcon
)

enum class HomeMenuIcon {
    AddItem,
    LowStock,
    Inventory,
    Team
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
    state: HomeUiState = HomeUiState(),
    selectedTab: BottomTab = BottomTab.Home,
    onSearchClick: () -> Unit = {},
    onScannerClick: () -> Unit = {},
    onMenuClick: (HomeMenuItem) -> Unit = {},
    onTabClick: (BottomTab) -> Unit = {}
) {
    Scaffold(
        containerColor = ScreenBg,
        bottomBar = {
            HomeBottomBar(
                selectedTab = selectedTab,
                onTabClick = onTabClick
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
            Text(
                text = "Главное окно",
                style = MaterialTheme.typography.labelMedium,
                color = TextSecondary
            )

            Spacer(modifier = Modifier.height(10.dp))

            HeaderCompanyBlock(companyName = state.companyName)

            Spacer(modifier = Modifier.height(14.dp))

            DailyStatsCard(
                dateText = state.dateText,
                incomeCount = state.incomeCount,
                outcomeCount = state.outcomeCount
            )

            Spacer(modifier = Modifier.height(22.dp))

            SearchBarBlock(
                onSearchClick = onSearchClick,
                onScannerClick = onScannerClick
            )

            Spacer(modifier = Modifier.height(18.dp))

            Column(
                verticalArrangement = Arrangement.spacedBy(14.dp)
            ) {
                state.menuItems.forEach { item ->
                    ActionMenuCard(
                        item = item,
                        onClick = { onMenuClick(item) }
                    )
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
            modifier = Modifier
                .size(16.dp)
                .padding(top = 1.dp)
        )
    }
}

@Composable
private fun DailyStatsCard(
    dateText: String,
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
                StatsColumn(
                    modifier = Modifier.weight(1f),
                    value = incomeCount.toString(),
                    label = "Приход"
                )

                Divider(
                    modifier = Modifier
                        .height(70.dp)
                        .width(1.dp),
                    color = Color.White.copy(alpha = 0.8f)
                )

                StatsColumn(
                    modifier = Modifier.weight(1f),
                    value = outcomeCount.toString(),
                    label = "Расход"
                )
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
            color = Color.White,
            style = MaterialTheme.typography.headlineSmall
        )
    }
}

@Composable
private fun SearchBarBlock(
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
                text = "Поиск товара",
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
private fun ActionMenuCard(
    item: HomeMenuItem,
    onClick: () -> Unit
) {
    Card(
        modifier = Modifier
            .fillMaxWidth()
            .clickable(onClick = onClick),
        shape = RoundedCornerShape(8.dp),
        colors = CardDefaults.cardColors(containerColor = Color.White),
        elevation = CardDefaults.cardElevation(defaultElevation = 4.dp)
    ) {
        Row(
            modifier = Modifier
                .fillMaxWidth()
                .padding(horizontal = 16.dp, vertical = 18.dp),
            verticalAlignment = Alignment.CenterVertically
        ) {
            MenuLeadingIcon(item.icon)

            Spacer(modifier = Modifier.width(14.dp))

            Text(
                text = item.title,
                style = MaterialTheme.typography.bodyLarge,
                color = Color(0xFF1E1E1E),
                modifier = Modifier.weight(1f)
            )

            Icon(
                imageVector = Icons.Outlined.ArrowForwardIos,
                contentDescription = null,
                tint = Color(0xFF8B8B8B),
                modifier = Modifier.size(16.dp)
            )
        }
    }
}

@Composable
private fun MenuLeadingIcon(icon: HomeMenuIcon) {
    when (icon) {
        HomeMenuIcon.AddItem -> Icon(
            imageVector = Icons.Outlined.Inventory2,
            contentDescription = null,
            tint = AccentDark,
            modifier = Modifier.size(24.dp)
        )

        HomeMenuIcon.LowStock -> Icon(
            imageVector = Icons.Outlined.WarningAmber,
            contentDescription = null,
            tint = AccentDark,
            modifier = Modifier.size(24.dp)
        )

        HomeMenuIcon.Inventory -> Icon(
            imageVector = Icons.Outlined.SwapHoriz,
            contentDescription = null,
            tint = AccentDark,
            modifier = Modifier.size(24.dp)
        )

        HomeMenuIcon.Team -> Icon(
            imageVector = Icons.Outlined.Groups,
            contentDescription = null,
            tint = AccentDark,
            modifier = Modifier.size(24.dp)
        )
    }
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
            icon = { Icon(Icons.Outlined.Inventory2, contentDescription = "Вещи") },
            label = { Text("Вещи") }
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