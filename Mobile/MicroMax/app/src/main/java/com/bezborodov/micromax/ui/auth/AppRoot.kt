package com.bezborodov.micromax.ui.auth

import androidx.compose.runtime.Composable
import androidx.lifecycle.viewmodel.compose.viewModel
import com.bezborodov.micromax.data.MicroMaxApiClient
import com.bezborodov.micromax.data.SessionRepository
import com.bezborodov.micromax.ui.components.LoadingState
import com.bezborodov.micromax.ui.home.HomeScreen

@Composable
fun AppRoot(
    apiClient: MicroMaxApiClient,
    sessionRepository: SessionRepository
) {
    val sessionViewModel: SessionViewModel = viewModel(
        factory = SessionViewModelFactory(sessionRepository)
    )
    val sessionState = sessionViewModel.uiState

    when {
        sessionState.isRestoringSession -> LoadingState()

        !sessionState.isAuthenticated -> AuthScreen(
            state = sessionState,
            onLogin = sessionViewModel::login,
            onRegister = sessionViewModel::register,
            onClearMessage = sessionViewModel::clearMessage
        )

        !sessionState.hasWarehouses -> NoWarehouseAccessScreen(
            state = sessionState,
            onCreateWarehouse = sessionViewModel::createFirstWarehouse,
            onLogout = sessionViewModel::logout
        )

        else -> HomeScreen(
            apiClient = apiClient,
            sessionState = sessionState,
            onSessionExpired = sessionViewModel::handleUnauthorized,
            onLogout = sessionViewModel::logout,
            onSelectActiveWarehouse = sessionViewModel::selectActiveWarehouse,
            onLoadWarehouseUsers = sessionViewModel::loadUsersForSelectedWarehouse,
            onAddWarehouseUser = sessionViewModel::addWarehouseUser,
            onUpdateWarehouseUserRole = sessionViewModel::updateWarehouseUserRole,
            onRemoveWarehouseUser = sessionViewModel::removeWarehouseUser,
            onClearSessionMessage = sessionViewModel::clearMessage
        )
    }
}
