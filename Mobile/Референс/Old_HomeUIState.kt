package com.bezborodov.micromax.ui.home

import androidx.compose.runtime.Immutable

@Immutable
data class HomeUiState(
    val companyName: String = "ООО “Развитие”",
    val dateText: String = "Сегодня 23 апреля",
    val incomeCount: Int = 0,
    val outcomeCount: Int = 0,
    val menuItems: List<HomeMenuItem> = listOf(
        HomeMenuItem("Добавить вещь", HomeMenuIcon.AddItem),
        HomeMenuItem("Просмотр нехватки запасов", HomeMenuIcon.LowStock),
        HomeMenuItem("Начать инвентаризацию", HomeMenuIcon.Inventory),
        HomeMenuItem("Управление командой", HomeMenuIcon.Team)
    )
)